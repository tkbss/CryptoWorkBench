using System.Text.Json;
using CryptoScript;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.FileEncryption;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class FileHashTests
{
    private string directory = null!;
    private VariableDeclaration[] previous = null!;
    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CWE-F06-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsMacOS() && directory.StartsWith("/var/")) directory = "/private" + directory;
        if (OperatingSystem.IsMacOS() && directory.StartsWith("/tmp/")) directory = "/private" + directory;
        Directory.CreateDirectory(directory);
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }
    [TearDown] public void Cleanup()
    {
        VariableDictionary.Instance().Clear();
        foreach (var value in previous) VariableDictionary.Instance().Add(value);
        Directory.Delete(directory, true);
    }
    private (ParameterVariableDeclaration P, PathVariableDeclaration S) Inputs(string mechanism = "HASH-SHA256")
    {
        var p = AlgorithmFactory.Create(mechanism).GenerateParameters(mechanism); p.Id = "p";
        var s = new PathVariableDeclaration { Id = "source", Type = new CryptoTypePath(), Value = Path.Combine(directory, "input.bin") };
        VariableDictionary.Instance().Add(p); VariableDictionary.Instance().Add(s);
        return (p, s);
    }
    private static OperationInvocation Invocation(VariableDeclaration p, VariableDeclaration s, CancellationToken token = default) =>
        new(new[] { p, s }.Select(v => new ResolvedCallArgument(v.Value, ResolvedCallArgumentKind.Variable, v)), null, token);
    private static void Execute(string script) => new CryptoScriptRunner().Execute(ParserBuilder.StringBuild(script).program());
    public static IEnumerable<TestCaseData> References()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "F06", "vectors.json")));
        foreach (var row in doc.RootElement.EnumerateArray())
            yield return new TestCaseData(row.GetProperty("mechanism").GetString()!, row.GetProperty("length").GetInt32(), row.GetProperty("digest").GetString()!);
    }
    [Test] public void ReferencesCoverEveryRegisteredHashMechanism()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "F06", "vectors.json")));
        var actual = doc.RootElement.EnumerateArray().Select(r => r.GetProperty("mechanism").GetString()).Distinct().ToArray();
        Assert.That(actual, Has.Length.EqualTo(11));
        Assert.That(actual, Is.EquivalentTo(MechanismRegistry.Entries.Where(e => e.Supports(CryptoScriptFunction.Hash)).Select(e => e.CanonicalName)));
    }
    [TestCaseSource(nameof(References))]
    public void IndependentReferenceMatchesFileAndMemory(string mechanism, int length, string expected)
    {
        var (p,s) = Inputs(mechanism);
        byte[] bytes = Enumerable.Range(0,length).Select(i => (byte)i).ToArray();
        File.WriteAllBytes(s.Value, bytes);
        var data = new StringVariableDeclaration { Id = "data", Type = new CryptoTypeVar(), Value = FormatConversions.ByteArrayToHexString(bytes), ValueFormat = FormatConversions.HEX };
        VariableDictionary.Instance().Add(data);
        Execute("VAR result = Hash(p,source)\nVAR memory = Hash(p,data)");
        var actual = VariableDictionary.Instance().Get("result")!;
        Assert.Multiple(() =>
        {
            Assert.That(actual.Value, Is.EqualTo(expected));
            Assert.That(VariableDictionary.Instance().Get("memory")!.Value, Is.EqualTo(expected).IgnoreCase);
            Assert.That(actual.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(actual.Type, Is.TypeOf<CryptoTypeVar>());
            Assert.That(File.ReadAllBytes(s.Value), Is.EqualTo(bytes));
            Assert.That(Directory.GetFileSystemEntries(directory), Is.EqualTo(new[] { s.Value }));
        });
    }
    [TestCase("PATH result = Hash(p,source)")]
    [TestCase("KEY result = Hash(p,source)")]
    [TestCase("PARAM result = Hash(p,source)")]
    [TestCase("VAL result = Hash(p,source)")]
    [TestCase("VAR result = Hash(Parameters(HASH-SHA256),source)")]
    [TestCase("VAR result = Hash(p,Hash(p,source))")]
    [TestCase("VAR result = Hash(Hash(p,source),source)")]
    [TestCase("Print(Hash(p,source))")]
    [TestCase("VAR result = Hash(p,source,source)")]
    [TestCase("VAR result = Hash(source)")]
    [TestCase("VAR result = Hash(source,p)")]
    public void ContractRejectsBeforeFileAccess(string script)
    {
        Inputs();
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(ParserBuilder.StringBuild(script).program()));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.FunctionContractViolation));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
    }
    [TestCase("missing")] [TestCase("directory")] [TestCase("invalid")] [TestCase("locked")]
    public void BadSourceHasNoResultOrArtifacts(string failure)
    {
        var (_,s) = Inputs();
        if (failure == "invalid") s.Value = "bad\0path";
        if (failure == "directory") Directory.CreateDirectory(s.Value);
        if (failure == "locked" && !OperatingSystem.IsWindows()) Assert.Ignore("Windows sharing test");
        using var locked = failure == "locked" ? new FileStream(s.Value, FileMode.Create, FileAccess.ReadWrite, FileShare.None) : null;
        var before = Directory.GetFileSystemEntries(directory);
        Assert.Throws<SemanticErrorException>(() => Execute("VAR result = Hash(p,source)"));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(Directory.GetFileSystemEntries(directory), Is.EqualTo(before));
    }
    [TestCase(false)] [TestCase(true)]
    public void SymlinkFileOrAncestorRejected(bool ancestor)
    {
        var (p,s) = Inputs();
        string target = Path.Combine(directory, "target"); Directory.CreateDirectory(target);
        string file = Path.Combine(target, "input.bin"); File.WriteAllBytes(file, [0, 255, 13, 10]);
        string link = Path.Combine(directory, "link");
        try
        {
            if (ancestor) { Directory.CreateSymbolicLink(link, target); s.Value = Path.Combine(link, "input.bin"); }
            else File.CreateSymbolicLink(s.Value, file);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { Assert.Ignore("Symlink unavailable: " + ex.Message); }
        try
        {
            Assert.Throws<SemanticErrorException>(() => Execute("VAR result = Hash(p,source)"));
            Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(new byte[] {0,255,13,10}));
        }
        finally { if (ancestor) Directory.Delete(link); }
    }
    [TestCase("HMAC-SHA256")] [TestCase("AES-CMAC")] [TestCase("HASH-SHA999")]
    public void InvalidMechanismRejectedBeforeOpening(string mechanism)
    {
        var (p,s) = Inputs(); p.Mechanism = mechanism;
        Assert.Throws<ArgumentException>(() => new CryptoOperations().Hash(Invocation(p,s)));
        Assert.Throws<SemanticErrorException>(() => Execute("VAR result = Hash(p,source)"));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void FakeParameterAndAdditionalOptionsRejected()
    {
        var (p,s) = Inputs();
        var fake = new StringVariableDeclaration { Value = p.Value, Type = new CryptoTypeVar() };
        Assert.Throws<ArgumentException>(() => new CryptoOperations().Hash(Invocation(fake,s)));
        Assert.Throws<ArgumentException>(() => new CryptoOperations().Hash(new OperationInvocation([
            new(p.Value, ResolvedCallArgumentKind.OtherLiteral), new(s.Value, ResolvedCallArgumentKind.Variable, s)])));
        p.SetParameter("#IV:0x(00)"); p.RecordExplicitParameter("#IV");
        Assert.Throws<ArgumentException>(() => new CryptoOperations().Hash(Invocation(p,s)));
    }
    [Test] public void PathTextInVarAndLiteralRemainsMemoryData()
    {
        var (_,s) = Inputs(); string literal = "\"" + s.Value.Replace("\\", "\\\\") + "\"";
        Execute("VAR data = " + literal + "\nVAR result = Hash(p,data)\nVAR direct = Hash(p," + literal + ")");
        Assert.That(VariableDictionary.Instance().Get("result")!.Value, Is.EqualTo(VariableDictionary.Instance().Get("direct")!.Value));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void RunnerCancellationDoesNotRegisterResult()
    {
        var (_,s) = Inputs(); File.WriteAllBytes(s.Value, [1]);
        using var cts = new CancellationTokenSource();
        var runner = new CryptoScriptRunner((call, _) => { if (call.Name == "Hash") cts.Cancel(); });
        Assert.ThrowsAsync<OperationCanceledException>(async () => await runner.ExecuteAsync(ParserBuilder.StringBuild("VAR result = Hash(p,source)").program(), cts.Token));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(File.ReadAllBytes(s.Value), Is.EqualTo(new byte[] {1}));
    }
    [Test] public void PreCancellationPrecedesOpening()
    {
        var (p,s) = Inputs(); using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => new CryptoOperations().Hash(Invocation(p,s,cts.Token)));
    }
    [TestCase("cancel")] [TestCase("io")] [TestCase("grow")] [TestCase("shrink")] [TestCase("early-eof")]
    public void MidReadFailuresAfterMultipleChunks(string failure)
    {
        var (p,_) = Inputs(); using var cts = new CancellationTokenSource();
        using var stream = new GeneratedStream(8 * 65536, afterRead: s =>
        {
            if (s.Reads != 3) return;
            Assert.That(s.LastBuffer, Has.Some.EqualTo(0xA5), "The cleanup assertion requires nonzero input bytes.");
            if (failure == "cancel") cts.Cancel();
            if (failure == "io") throw new IOException("injected failure");
            if (failure == "grow") s.Size++;
            if (failure == "shrink") s.Size--;
            if (failure == "early-eof") s.EndEarly = true;
        });
        if (failure == "cancel") Assert.Throws<OperationCanceledException>(() => FileHashCoordinator.HashStream(p,stream,cts.Token));
        else Assert.Throws<IOException>(() => FileHashCoordinator.HashStream(p,stream,cts.Token));
        Assert.That(stream.Position, Is.EqualTo(3 * 65536));
        Assert.That(stream.LastBuffer, Is.All.Zero);
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void ShortReadsMatchNormalReads()
    {
        var (p,_) = Inputs();
        using var normal = new GeneratedStream(196625);
        using var shortReads = new GeneratedStream(196625, maximumChunk: 3);
        Assert.That(FileHashCoordinator.HashStream(p,shortReads).Value, Is.EqualTo(FileHashCoordinator.HashStream(p,normal).Value));
        Assert.That(shortReads.LastBuffer, Is.All.Zero);
        Assert.That(normal.LastBuffer, Is.All.Zero);
    }
    [TestCase(4)] [TestCase(5)]
    public void LengthChangeAtEofOrAfterFinalizationRejected(int check)
    {
        var (p,_) = Inputs();
        using var stream = new GeneratedStream(1, changeOnLengthCheck: check);
        Assert.Throws<IOException>(() => FileHashCoordinator.HashStream(p,stream));
        Assert.That(stream.LastBuffer, Is.All.Zero);
    }
    [TestCase(false)] [TestCase(true)]
    public void AllocationsStayBoundedForFileAndGeneratedStream(bool file)
    {
        var (p,s) = Inputs();
        long Measure(int size)
        {
            if (file) { using var output = File.Create(s.Value); output.SetLength(size); }
            using var source = new GeneratedStream(size);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var result = file ? FileHashCoordinator.Hash(p,s) : FileHashCoordinator.HashStream(p,source);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(result.Value, Does.StartWith("0x("));
            if (!file) { Assert.That(source.MaximumRead, Is.EqualTo(65536)); Assert.That(source.Position, Is.EqualTo(size)); }
            return allocated;
        }
        Measure(65536);
        long small = Measure(1 << 20), large = Measure(8 << 20);
        Assert.That(large, Is.LessThan(small + 262144));
        Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(file ? 1 : 0));
    }
    private sealed class GeneratedStream(long length, int maximumChunk = 65536, Action<GeneratedStream>? afterRead = null,
        int changeOnLengthCheck = 0) : Stream
    {
        public long Size { get; set; } = length;
        public int Reads { get; private set; }
        public int MaximumRead { get; private set; }
        public bool EndEarly { get; set; }
        public byte[]? LastBuffer { get; private set; }
        private int lengthChecks;
        public override long Length => ++lengthChecks == changeOnLengthCheck ? Size + 1 : Size;
        public override long Position { get; set; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override int Read(byte[] buffer, int offset, int count)
        {
            MaximumRead = Math.Max(MaximumRead,count);
            LastBuffer = buffer;
            int read = EndEarly ? 0 : (int)Math.Min(Math.Min(count,maximumChunk), Size - Position);
            buffer.AsSpan(offset,read).Fill(0xA5); Position += read; Reads++; afterRead?.Invoke(this); return read;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
