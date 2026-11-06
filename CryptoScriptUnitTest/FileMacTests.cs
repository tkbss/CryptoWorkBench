using Antlr4.Runtime;
using CryptoScript;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.FileEncryption;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class FileMacTests
{
    private string directory = null!;
    private VariableDeclaration[] previous = null!;
    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CWE-F03-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsMacOS() && directory.StartsWith("/var/")) directory = "/private" + directory;
        Directory.CreateDirectory(directory);
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }
    [TearDown] public void Cleanup()
    {
        VariableDictionary.Instance().Clear();
        foreach (var v in previous) VariableDictionary.Instance().Add(v);
        Directory.Delete(directory, true);
    }
    private (ParameterVariableDeclaration P, KeyVariableDeclaration K, PathVariableDeclaration S) Inputs(string mechanism = "AES-CMAC")
    {
        var algorithm = AlgorithmFactory.Create(mechanism);
        var p = mechanism == "AES-GMAC"
            ? algorithm.GenerateParameters(mechanism, ["#NONCE:0x(000102030405060708090A0B)"])
            : algorithm.GenerateParameters(mechanism);
        p.Id = "p";
        var k = algorithm.GenerateKey(mechanism, "0x(0123456789ABCDEFFEDCBA9876543210)"); k.Id = "k";
        var s = new PathVariableDeclaration { Id = "source", Type = new CryptoTypePath(), Value = Path.Combine(directory, "input.bin") };
        foreach (var v in new VariableDeclaration[] { p, k, s }) VariableDictionary.Instance().Add(v);
        return (p, k, s);
    }
    private static AlgorithmCallArguments Args(ParameterVariableDeclaration p, KeyVariableDeclaration k, VariableDeclaration s) =>
        new([new(p.Value, p), new(k.Value, k), new(s.Value, s)]);
    private static OperationInvocation Invocation(ParameterVariableDeclaration p, KeyVariableDeclaration k, VariableDeclaration s, CancellationToken token = default) =>
        new(new[] { p, k, s }.Select(v => new ResolvedCallArgument(v.Value, ResolvedCallArgumentKind.Variable, v)), null, token);
    private static CryptoScriptParser.ProgramContext Parse(string script) =>
        new CryptoScriptParser(new CommonTokenStream(new CryptoScriptLexer(new AntlrInputStream(script)))).program();
    public static IEnumerable<TestCaseData> Cases() => MechanismRegistry.Entries.Where(FileMacCoordinator.Supports)
        .SelectMany(e => new[] { 0, 1, 7, 8, 9, 15, 16, 17, 63, 64, 65, 71, 72, 73,
            103, 104, 105, 127, 128, 129, 135, 136, 137, 143, 144, 145, 65535, 65536, 65537, 1000003 }
        .Select(n => new TestCaseData(e.CanonicalName, n)));
    [TestCaseSource(nameof(Cases))]
    public void MatchesVarAndPreservesInputs(string mechanism, int length)
    {
        var (p, k, s) = Inputs(mechanism);
        byte[] bytes = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        File.WriteAllBytes(s.Value, bytes);
        string pv = p.Value, kv = k.Value;
        var data = new StringVariableDeclaration { Value = FormatConversions.ByteArrayToHexString(bytes), ValueFormat = FormatConversions.HEX };
        var expected = AlgorithmFactory.Create(mechanism).Mac(Args(p, k, data));
        var actual = new CryptoOperations().Mac(Invocation(p, k, s));
        Assert.Multiple(() =>
        {
            Assert.That(actual.Value, Is.EqualTo(expected.Value));
            Assert.That(actual.ValueFormat, Is.EqualTo(expected.ValueFormat));
            Assert.That(actual.Type, Is.TypeOf<CryptoTypeVar>());
            Assert.That(p.Value, Is.EqualTo(pv)); Assert.That(k.Value, Is.EqualTo(kv));
            Assert.That(File.ReadAllBytes(s.Value), Is.EqualTo(bytes));
            Assert.That(Directory.GetFileSystemEntries(directory), Is.EqualTo(new[] { s.Value }));
        });
    }
    [TestCase("PATH result = Mac(p,k,source)")]
    [TestCase("KEY result = Mac(p,k,source)")]
    [TestCase("PARAM result = Mac(p,k,source)")]
    [TestCase("VAR result = Mac(p,k,Mac(p,k,source))")]
    [TestCase("VAR result = Mac(Parameters(AES-CMAC),k,source)")]
    [TestCase("Print(Mac(p,k,source))")]
    public void ContractRejectsBeforeReading(string script)
    {
        Inputs(); var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse(script)));
        Assert.That(runner.Diagnostics.Any(d => d.DiagnosticCode == SemanticDiagnosticCode.FunctionContractViolation), Is.True);
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
    }
    [Test] public void DirectScriptAndMetadataDiagnostics()
    {
        var (p, k, s) = Inputs(); File.WriteAllBytes(s.Value, [0, 1, 0]);
        k.KeyType = KeyType.Secret(KeyAlgorithm.Hmac); k.Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt);
        var runner = new CryptoScriptRunner(); runner.Execute(Parse("VAR result = Mac(p,k,source)"));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.TypeOf<StringVariableDeclaration>());
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.KeyTypeConflict));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.KeyUsageConflict));
    }
    [TestCase("missing")] [TestCase("directory")] [TestCase("invalid")] [TestCase("locked")]
    public void BadSourceHasNoResultOrOutput(string failure)
    {
        var (p, k, s) = Inputs();
        if (failure == "invalid") s.Value = "bad\0path";
        if (failure == "directory") Directory.CreateDirectory(s.Value);
        if (failure == "locked" && !OperatingSystem.IsWindows()) Assert.Ignore("Windows sharing test.");
        using var locked = failure == "locked" ? new FileStream(s.Value, FileMode.Create, FileAccess.ReadWrite, FileShare.None) : null;
        var before = Directory.GetFileSystemEntries(directory);
        Assert.Throws<SemanticErrorException>(() => new CryptoScriptRunner().Execute(Parse("VAR result = Mac(p,k,source)")));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(Directory.GetFileSystemEntries(directory), Is.EqualTo(before));
    }
    [Test] public void SymlinkRejected()
    {
        var (p, k, s) = Inputs(); string target = Path.Combine(directory, "target"); File.WriteAllBytes(target, [1]);
        try { File.CreateSymbolicLink(s.Value, target); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { Assert.Ignore("Symlink creation unavailable: " + ex.Message); }
        Assert.Catch(() => FileMacCoordinator.Mac(Args(p,k,s),s));
        Assert.That(File.ReadAllBytes(target), Is.EqualTo(new byte[] { 1 }));
        Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(2));
    }
    [TestCase("AES-GMAC")] [TestCase("AES-CBC-MAC")] [TestCase("DES3-CBC")] [TestCase("DES3-RETAIL")]
    public void F032MechanismsNowSupportFiles(string mechanism)
    {
        var (p,k,s) = Inputs(mechanism); File.WriteAllBytes(s.Value, [0, 1, 0]);
        Assert.That(FileMacCoordinator.Mac(Args(p,k,s),s).Type, Is.TypeOf<CryptoTypeVar>());
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { s.Value }));
    }
    [TestCase("EP2-MESSAGE-MAC")] [TestCase("MAC-EMV-AC")]
    public void OutOfScopeMechanismsRejected(string mechanism)
    {
        var (p,k,s) = Inputs(); p.Mechanism = mechanism;
        Assert.Throws<ArgumentException>(() => FileMacCoordinator.Mac(Args(p,k,s),s));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [TestCase("AES-CMAC", "0x(00)")] [TestCase("DES3-CMAC", "0x(00)")]
    [TestCase("DES3-CMAC", "0x(00000000000000000000000000000000)")]
    public void InvalidKeysRejected(string mechanism, string key)
    {
        var (p,k,s) = Inputs(mechanism); k.Value = key;
        Assert.Catch(() => FileMacCoordinator.Mac(Args(p,k,s),s));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void PathTextInVarOrLiteralIsData()
    {
        var (p,k,s) = Inputs("HMAC-SHA256");
        string literal = "\"" + s.Value.Replace("\\", "\\\\") + "\"";
        new CryptoScriptRunner().Execute(Parse("VAR data = " + literal + "\nVAR result = Mac(p,k,data)\nVAR direct = Mac(p,k," + literal + ")"));
        Assert.That(VariableDictionary.Instance().Get("result")!.Value, Is.EqualTo(VariableDictionary.Instance().Get("direct")!.Value));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void PreCancelledInvocationHasNoResult()
    {
        var (p,k,s) = Inputs(); using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => new CryptoOperations().Mac(Invocation(p,k,s,cts.Token)));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [TestCase(true)] [TestCase(false)]
    public void ParameterTextCannotReplaceParamReference(bool variable)
    {
        var (p,k,s) = Inputs();
        var fake = new StringVariableDeclaration { Value = p.Value, Type = new CryptoTypeVar() };
        var invocation = new OperationInvocation([
            new(p.Value, variable ? ResolvedCallArgumentKind.Variable : ResolvedCallArgumentKind.OtherLiteral, variable ? fake : null),
            new(k.Value, ResolvedCallArgumentKind.Variable, k), new(s.Value, ResolvedCallArgumentKind.Variable, s)]);
        Assert.Throws<ArgumentException>(() => new CryptoOperations().Mac(invocation));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void VarCannotReplaceKeyReference()
    {
        var (p,k,s) = Inputs();
        var fake = new StringVariableDeclaration { Value = k.Value, ValueFormat = k.ValueFormat, Type = new CryptoTypeVar() };
        var invocation = new OperationInvocation([
            new(p.Value, ResolvedCallArgumentKind.Variable, p), new(fake.Value, ResolvedCallArgumentKind.Variable, fake),
            new(s.Value, ResolvedCallArgumentKind.Variable, s)]);
        Assert.Throws<ArgumentException>(() => new CryptoOperations().Mac(invocation));
    }
    [TestCase(false)] [TestCase(true)]
    public void MidReadCancellationOrFailureHasNoResult(bool failure)
    {
        var (p,k,s) = Inputs(); using var cts = new CancellationTokenSource();
        using var stream = new GeneratedStream(1000000, () => { if (failure) throw new IOException("injected read failure"); cts.Cancel(); });
        if (failure) Assert.Throws<IOException>(() => FileMacCoordinator.MacStream(Args(p,k,s), stream, cts.Token));
        else Assert.Throws<OperationCanceledException>(() => FileMacCoordinator.MacStream(Args(p,k,s), stream, cts.Token));
        Assert.That(stream.Position, Is.EqualTo(65536));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void LargeStreamAllocationsStayBounded()
    {
        var (p,k,s) = Inputs("HMAC-SHA256");
        long Measure(long size)
        {
            using var stream = new GeneratedStream(size);
            long before = GC.GetAllocatedBytesForCurrentThread();
            FileMacCoordinator.MacStream(Args(p,k,s), stream);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(stream.MaximumRead, Is.EqualTo(65536));
            Assert.That(stream.Position, Is.EqualTo(size)); return allocated;
        }
        Measure(65536);
        long small = Measure(1024 * 1024), large = Measure(64L * 1024 * 1024);
        Assert.That(large, Is.LessThan(small + 1024 * 1024));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void LargeFileAllocationsStayBounded()
    {
        var (p,k,s) = Inputs("HMAC-SHA256");
        long Measure(long size)
        {
            using (var file = File.Create(s.Value)) file.SetLength(size);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var actual = FileMacCoordinator.Mac(Args(p,k,s), s);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            using var generated = new GeneratedStream(size);
            Assert.That(actual.Value, Is.EqualTo(FileMacCoordinator.MacStream(Args(p,k,s), generated).Value));
            Assert.That(new FileInfo(s.Value).Length, Is.EqualTo(size));
            return allocated;
        }
        Measure(65536);
        long small = Measure(1024 * 1024), large = Measure(64L * 1024 * 1024);
        Assert.That(large, Is.LessThan(small + 1024 * 1024));
        Assert.That(Directory.GetFileSystemEntries(directory), Is.EqualTo(new[] { s.Value }));
    }
    [TestCase("AES-CMAC")] [TestCase("DES3-CMAC")] [TestCase("HMAC-SHA3-256")]
    public void ShortReadsAreNotEndOfFile(string mechanism)
    {
        var (p,k,s) = Inputs(mechanism);
        using var normal = new GeneratedStream(100003);
        using var shortReads = new GeneratedStream(100003, maximumChunk: 3);
        Assert.That(FileMacCoordinator.MacStream(Args(p,k,s), shortReads).Value,
            Is.EqualTo(FileMacCoordinator.MacStream(Args(p,k,s), normal).Value));
    }
    [Test] public void RunnerCancellationDoesNotRegisterResult()
    {
        var (p,k,s) = Inputs(); File.WriteAllBytes(s.Value, [1]);
        using var cts = new CancellationTokenSource();
        var runner = new CryptoScriptRunner((call, _) => { if (call.Name == "Mac") cts.Cancel(); });
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await runner.ExecuteAsync(Parse("VAR result = Mac(p,k,source)"), cts.Token));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(Directory.GetFileSystemEntries(directory), Is.EqualTo(new[] { s.Value }));
    }
    private sealed class GeneratedStream(long length, Action? onRead = null, int maximumChunk = 65536) : Stream
    {
        public int MaximumRead { get; private set; }
        public override long Length => length;
        public override long Position { get; set; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override int Read(byte[] buffer, int offset, int count)
        {
            MaximumRead = Math.Max(MaximumRead, count);
            int read = (int)Math.Min(Math.Min(count, maximumChunk), length - Position);
            Array.Clear(buffer, offset, read); Position += read; onRead?.Invoke(); return read;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
