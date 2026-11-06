using System.Buffers.Binary;
using System.Text;
using Antlr4.Runtime;
using CryptoScript;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.ErrorListner;
using CryptoScript.FileEncryption;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class FileEncryptionTests
{
    private string directory = null!;
    private VariableDeclaration[] previous = null!;
    private const string Iv = "0x(000102030405060708090A0B0C0D0E0F)";

    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CWE-test-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsMacOS() && directory.StartsWith("/var/")) directory = "/private" + directory;
        Directory.CreateDirectory(directory);
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }
    [TearDown] public void Cleanup()
    {
        VariableDictionary.Instance().Clear();
        foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
        Directory.Delete(directory, true);
    }
    private PathVariableDeclaration Source(byte[] bytes)
    {
        var path = new PathVariableDeclaration { Id = "source", Type = new CryptoTypePath(), Value = Path.Combine(directory, "message.bin") };
        File.WriteAllBytes(path.Value, bytes);
        return path;
    }
    private static (ParameterVariableDeclaration P, KeyVariableDeclaration K) Parameters(int bits = 128, string padding = "PKCS-7", bool automatic = false)
    {
        var aes = new AES();
        var p = aes.GenerateParameters("AES-CBC", automatic ? ["#PAD:" + padding] : ["#IV:" + Iv, "#PAD:" + padding]);
        var k = aes.GenerateKey("AES-CBC", "0x(" + new string('0', bits / 4) + ")");
        p.Id = "p"; k.Id = "k";
        return (p, k);
    }
    private static AlgorithmCallArguments Arguments(ParameterVariableDeclaration p, KeyVariableDeclaration k, PathVariableDeclaration path) =>
        new([new(p.Value, p), new(k.Value, k), new(path.Value, path)]);
    private static (EncryptedFileHeader Header, byte[] Ciphertext) Read(string path)
    {
        using var input = File.OpenRead(path);
        var (header, _) = EncryptedFileCodec.Read(input);
        using var bytes = new MemoryStream(); input.CopyTo(bytes);
        return (header, bytes.ToArray());
    }
    private static CryptoScriptParser.ProgramContext Parse(string script) =>
        new CryptoScriptParser(new CommonTokenStream(new CryptoScriptLexer(new AntlrInputStream(script)))).program();
    private void Register(params VariableDeclaration[] variables)
    { foreach (var variable in variables) VariableDictionary.Instance().Add(variable); }
    private void NoOutput() => Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName), Is.EqualTo(new[] { "message.bin" }));

    [TestCase(128, "NONE", 16)] [TestCase(192, "NONE", 256)] [TestCase(256, "NONE", 65536)]
    [TestCase(128, "PKCS-7", 0)] [TestCase(192, "PKCS-7", 17)] [TestCase(256, "PKCS-7", 256)]
    [TestCase(128, "PKCS-7", 1000003)]
    public void MatchesVarAndPreservesSource(int bits, string padding, int length)
    {
        byte[] data = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        var source = Source(data); var (p, k) = Parameters(bits, padding);
        var result = FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source);
        var actual = Read(result.Value);
        var expected = new AES_CBC().ModeEncryption(p, k, new StringVariableDeclaration
            { Value = FormatConversions.ByteArrayToHexString(data), ValueFormat = FormatConversions.HEX });
        Assert.Multiple(() =>
        {
            Assert.That(actual.Ciphertext, Is.EqualTo(FormatConversions.ToByteArray(expected.Value, expected.ValueFormat)));
            Assert.That(File.ReadAllBytes(source.Value), Is.EqualTo(data));
            Assert.That(actual.Header, Is.EqualTo(new EncryptedFileHeader("AES-CBC", padding, "AAECAwQFBgcICQoLDA0ODw==")));
            Assert.That(result.Type, Is.TypeOf<CryptoTypePath>());
            Assert.That(result.Value, Is.EqualTo(source.Value + ".cwe"));
        });
    }

    [Test] public void RunnerReturnsPathAndPrintsIt()
    {
        var source = Source([0, 255, 1]); var (p, k) = Parameters(); Register(source, p, k);
        new CryptoScriptRunner().Execute(Parse("PATH encrypted = Encrypt(p,k,source)\nPrint(encrypted)"));
        Assert.That(VariableDictionary.Instance().Get("encrypted"), Is.TypeOf<PathVariableDeclaration>());
        Assert.That(File.Exists(VariableDictionary.Instance().Get("encrypted").Value), Is.True);
    }

    [TestCase("VAR encrypted = Encrypt(p,k,source)")]
    [TestCase("KEY encrypted = Encrypt(p,k,source)")]
    [TestCase("Print(Encrypt(p,k,source))")]
    [TestCase("VAR encrypted = Encrypt(p,k,Encrypt(p,k,source))")]
    [TestCase("PATH encrypted = Encrypt(Parameters(AES-CBC),k,source)")]
    public void RejectsInvalidContextsBeforeAnyFileOperation(string script)
    {
        var source = Source([1]); var (p, k) = Parameters(); Register(source, p, k);
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse(script)));
        Assert.That(runner.Diagnostics.Any(d => d.DiagnosticCode == SemanticDiagnosticCode.FunctionContractViolation), Is.True);
        Assert.That(VariableDictionary.Instance().Get("encrypted"), Is.Null); NoOutput();
    }

    [Test] public void PathLikeVarDoesNotAccessFile()
    {
        var source = Source([1]); var (p, k) = Parameters();
        Register(new StringVariableDeclaration { Id = "source", Type = new CryptoTypeVar(), Value = source.Value }, p, k);
        try { new CryptoScriptRunner().Execute(Parse("VAR encrypted = Encrypt(p,k,source)")); }
        catch (SemanticErrorException) { }
        NoOutput();
    }

    [Test] public void AutomaticIvIsFreshAndParameterUnchanged()
    {
        var source = Source([1, 2]); var (p, k) = Parameters(automatic: true); string original = p.Value;
        var first = Read(FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source).Value);
        var second = Read(FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source).Value);
        Assert.That(first.Header.Iv, Is.Not.EqualTo(second.Header.Iv));
        Assert.That(p.Value, Is.EqualTo(original));
    }

    [Test] public void NumberingReusesFirstGapAndNeverChangesExistingFiles()
    {
        var source = Source([1]); var (p, k) = Parameters();
        foreach (string suffix in new[] { ".cwe", ".1.cwe", ".3.cwe" }) File.WriteAllBytes(source.Value + suffix, [7, 8]);
        var result = FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source);
        Assert.That(result.Value, Is.EqualTo(source.Value + ".2.cwe"));
        foreach (string suffix in new[] { ".cwe", ".1.cwe", ".3.cwe" }) Assert.That(File.ReadAllBytes(source.Value + suffix), Is.EqualTo(new byte[] { 7, 8 }));
    }

    [Test] public void PublicationCollisionIsRetried()
    {
        var source = Source([1]); var (p, k) = Parameters();
        var result = FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source,
            beforePublish: () => File.WriteAllBytes(source.Value + ".cwe", [77]));
        Assert.That(result.Value, Is.EqualTo(source.Value + ".1.cwe"));
        Assert.That(File.ReadAllBytes(source.Value + ".cwe"), Is.EqualTo(new byte[] { 77 }));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(17)] public void NoneRejectsInvalidLengthAndCleansTemporary(int length)
    {
        var source = Source(new byte[length]); var (p, k) = Parameters(padding: "NONE");
        Assert.Throws<ArgumentException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source)); NoOutput();
    }
    [Test] public void UnsupportedPaddingAndMechanismFailWithoutOutput()
    {
        var source = Source([1]); var (p, k) = Parameters();
        p.SetParameter("PAD", "INVALID");
        Assert.Throws<ArgumentException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source));
        p.Mechanism = "AES-GCM";
        Assert.Throws<ArgumentException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source)); NoOutput();
    }
    [Test] public void CancelBeforePublicationRemovesTemporary()
    {
        var source = Source(new byte[100000]); var (p, k) = Parameters(); using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source,
            cts.Token, cts.Cancel)); NoOutput();
    }
    [Test] public void PublicationFailureRemovesTemporary()
    {
        var source = Source([1]); var (p, k) = Parameters();
        Assert.Throws<IOException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source,
            beforePublish: () => throw new IOException("Injected failure"))); NoOutput();
    }
    [Test] public void ExhaustedNamesRemoveTemporary()
    {
        var source = Source([1]); var (p, k) = Parameters();
        Assert.Throws<IOException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source, maximumCandidates: 0)); NoOutput();
    }
    [Test] public void MissingSourceDoesNotRegisterResult()
    {
        var source = Source([1]); var (p, k) = Parameters(); Register(source, p, k); File.Delete(source.Value);
        Assert.Throws<SemanticErrorException>(() => new CryptoScriptRunner().Execute(Parse("PATH encrypted = Encrypt(p,k,source)")));
        Assert.That(VariableDictionary.Instance().Get("encrypted"), Is.Null);
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void SourceSymlinkIsRejected()
    {
        var source = Source([1]); string link = Path.Combine(directory, "link.bin");
        try { File.CreateSymbolicLink(link, source.Value); }
        catch (Exception e) when (e is UnauthorizedAccessException || OperatingSystem.IsWindows() && e is IOException && (e.HResult & 0xffff) == 1314) { Assert.Ignore("Symbolic link privilege unavailable."); }
        var (p, k) = Parameters(); source.Value = link;
        Assert.Throws<IOException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source));
        Assert.That(Directory.GetFiles(directory, "*.cwe"), Is.Empty);
    }

    [Test] public void CodecHasExactFramingAndRoundTrips()
    {
        var header = new EncryptedFileHeader("AES-CBC", "NONE", "AAECAwQFBgcICQoLDA0ODw==");
        byte[] encoded = EncryptedFileCodec.Encode(header);
        Assert.That(encoded[..12], Is.EqualTo(new byte[] { 67, 87, 66, 69, 78, 67, 0, 0, 0, 1, 0, 0 }));
        Assert.That(BinaryPrimitives.ReadUInt32BigEndian(encoded.AsSpan(12)), Is.EqualTo(encoded.Length - 16));
        var result = EncryptedFileCodec.Read(new MemoryStream(encoded));
        Assert.That(result.Header, Is.EqualTo(header)); Assert.That(result.Frame, Is.EqualTo(encoded));
    }
    [TestCase(0)] [TestCase(8)] [TestCase(10)] [TestCase(12)]
    public void CodecRejectsInvalidFraming(int offset)
    {
        byte[] encoded = EncryptedFileCodec.Encode(new("AES-CBC", "NONE", "AAECAwQFBgcICQoLDA0ODw=="));
        encoded[offset] = 255;
        Assert.Throws<InvalidDataException>(() => EncryptedFileCodec.Read(new MemoryStream(encoded)));
    }
    [TestCase("\"unknown\":1,")]
    [TestCase("\"format\":\"CryptoWorkBenchEncryptedFile\",")]
    public void CodecRejectsUnknownAndDuplicateFields(string extra)
    {
        byte[] encoded = EncryptedFileCodec.Encode(new("AES-CBC", "NONE", "AAECAwQFBgcICQoLDA0ODw=="));
        byte[] json = Encoding.UTF8.GetBytes("{" + extra + Encoding.UTF8.GetString(encoded[16..])[1..]);
        byte[] frame = encoded[..16].Concat(json).ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(12), (uint)json.Length);
        Assert.Throws<InvalidDataException>(() => EncryptedFileCodec.Read(new MemoryStream(frame)));
    }
    [Test] public async Task AsyncRunnerCancellationReachesOperation()
    {
        var source = Source([1]); var (p, k) = Parameters(); Register(source, p, k);
        using var cts = new CancellationTokenSource();
        var runner = new CryptoScriptRunner((call, _) => { if (call.Name == "Encrypt") cts.Cancel(); });
        try { await runner.ExecuteAsync(Parse("PATH encrypted = Encrypt(p,k,source)"), cts.Token); Assert.Fail("Expected cancellation"); }
        catch (OperationCanceledException) { }
        NoOutput(); Assert.That(VariableDictionary.Instance().Get("encrypted"), Is.Null);
    }

    [Test] public async Task ConcurrentPublicationsHaveDistinctNames()
    {
        var source = Source([1, 2, 3]); var (p, k) = Parameters();
        var args = Arguments(p, k, source);
        var outputs = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            FileEncryptCoordinator.Encrypt(args, source).Value)));
        Assert.That(outputs.Distinct().Count(), Is.EqualTo(8));
        Assert.That(Directory.GetFiles(directory, ".cwe-*.tmp"), Is.Empty);
        foreach (string output in outputs) Assert.That(Read(output).Ciphertext.Length, Is.EqualTo(16));
    }

    [Test] public void StreamingMemoryDoesNotScaleWithInput()
    {
        var (p, k) = Parameters();
        using var aes = AES_CBC.CreateCipher(k, AES_CBC.ValidateAndDecodeIv(Iv));
        using var input = new GeneratedStream(64L * 1024 * 1024);
        long before = GC.GetAllocatedBytesForCurrentThread();
        AES_CBC.EncryptStream(input, Stream.Null, aes, p, default);
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.LessThan(2 * 1024 * 1024));
        Assert.That(input.MaximumRead, Is.LessThanOrEqualTo(65536));
    }

    [Test] public void TemporaryReplacementCannotPublishForeignBytes()
    {
        var source = Source([1]); var (p, k) = Parameters();
        void ReplaceTemporary()
        {
            string temp = Directory.GetFiles(directory, ".cwe-*.tmp").Single();
            if (OperatingSystem.IsWindows())
                Assert.Throws<IOException>(() => File.Delete(temp));
            else { File.Delete(temp); File.WriteAllBytes(temp, [77]); }
        }
        if (OperatingSystem.IsWindows())
        {
            var output = FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source, beforePublish: ReplaceTemporary);
            Assert.That(Read(output.Value).Ciphertext.Length, Is.EqualTo(16));
        }
        else
        {
            // Linux cannot relink an unlinked inode without privilege; Darwin detects identity mismatch.
            Assert.Throws<IOException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source, beforePublish: ReplaceTemporary));
            Assert.That(Directory.GetFiles(directory, "*.cwe"), Is.Empty);
        }
    }

    [Test] public void DirectoryMoveDoesNotReturnStalePath()
    {
        var source = Source([1]); var (p, k) = Parameters();
        string moved = directory + "-moved";
        var output = FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source, beforePublish: () =>
        {
            if (OperatingSystem.IsWindows()) Assert.Throws<IOException>(() => Directory.Move(directory, moved));
            else { Directory.Move(directory, moved); directory = moved; }
        });
        Assert.That(File.Exists(output.Value), Is.True);
        Assert.That(Path.GetDirectoryName(output.Value), Is.EqualTo(directory));
    }

    [Test] public void StreamingObservesCancellationBetweenChunks()
    {
        var (p, k) = Parameters(); using var cts = new CancellationTokenSource();
        using var aes = AES_CBC.CreateCipher(k, AES_CBC.ValidateAndDecodeIv(Iv));
        using var input = new GeneratedStream(1024 * 1024, cts.Cancel);
        Assert.Throws<OperationCanceledException>(() => AES_CBC.EncryptStream(input, Stream.Null, aes, p, cts.Token));
        Assert.That(input.Position, Is.EqualTo(65536));
    }

    [Test] public void NistVectorProducesExactContainer()
    {
        var source = Source(Convert.FromHexString("6BC1BEE22E409F96E93D7E117393172A"));
        var (p, _) = Parameters(padding: "NONE");
        var k = new AES().GenerateKey("AES-CBC", "0x(2B7E151628AED2A6ABF7158809CF4F3C)");
        var output = FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source);
        Assert.That(Read(output.Value).Ciphertext, Is.EqualTo(Convert.FromHexString("7649ABAC8119B246CEE98E9B12E9197D")));
        byte[] frame = EncryptedFileCodec.Encode(new("AES-CBC", "NONE", "AAECAwQFBgcICQoLDA0ODw=="));
        Assert.That(File.ReadAllBytes(output.Value), Is.EqualTo(frame.Concat(Convert.FromHexString("7649ABAC8119B246CEE98E9B12E9197D"))));
        Assert.That(File.ReadAllBytes(output.Value), Is.EqualTo(Convert.FromHexString(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "F01-reference.hex")).Trim())));
        string metadata = Encoding.UTF8.GetString(frame[16..]);
        Assert.That(metadata, Does.Not.Contain(source.Value).And.Not.Contain("2B7E1516"));
    }

    [Test] public void CodecRejectsMalformedUtf8AndTruncatedData()
    {
        byte[] frame = EncryptedFileCodec.Encode(new("AES-CBC", "NONE", "AAECAwQFBgcICQoLDA0ODw=="));
        Assert.Throws<EndOfStreamException>(() => EncryptedFileCodec.Read(new MemoryStream(frame[..^1])));
        frame[18] = 0xff;
        Assert.Throws<InvalidDataException>(() => EncryptedFileCodec.Read(new MemoryStream(frame)));
    }

    [Test] public void ExistingKeyMetadataWarningsReachRunner()
    {
        var source = Source([1]); var (p, k) = Parameters();
        k.KeyType = KeyType.Secret(KeyAlgorithm.Hmac);
        k.Usage = KeyUsagePolicy.Restricted(KeyUsage.MacGenerate);
        Register(source, p, k);
        var runner = new CryptoScriptRunner();
        runner.Execute(Parse("PATH encrypted = Encrypt(p,k,source)"));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.KeyTypeConflict));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.KeyUsageConflict));
        Assert.That(runner.Diagnostics.Select(d => d.Severity), Is.All.EqualTo(SemanticDiagnosticSeverity.Warning));
    }

    [Test] public void LockedSourceFailsWithoutOutput()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows mandatory sharing contract.");
        var source = Source([1]); var (p, k) = Parameters();
        using (var locked = new FileStream(source.Value, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source));
        NoOutput();
    }

    [Test] public void DirectorySourceFailsWithoutOutput()
    {
        var source = Source([1]); var (p, k) = Parameters();
        source.Value = Path.Combine(directory, "subdirectory"); Directory.CreateDirectory(source.Value);
        Assert.Throws<IOException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source));
        NoOutput();
    }

    [Test] public void InvalidKeyAndIvFailBeforeTemporaryCreation()
    {
        var source = Source([1]); var (p, k) = Parameters();
        p.SetParameter("IV", "0x(00)");
        Assert.Throws<ArgumentException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source));
        p.SetParameter("IV", Iv); k.Value = "0x(00)";
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() => FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source));
        NoOutput();
    }

    private sealed class GeneratedStream(long length, Action? onRead = null) : Stream
    {
        public int MaximumRead { get; private set; }
        public override long Length => length;
        public override long Position { get; set; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override int Read(byte[] buffer, int offset, int count)
        {
            MaximumRead = Math.Max(MaximumRead, count);
            int read = (int)Math.Min(count, length - Position);
            Array.Clear(buffer, offset, read); Position += read; onRead?.Invoke(); return read;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
