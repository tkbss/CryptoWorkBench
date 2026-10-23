using System.Buffers.Binary;
using System.Security.Cryptography;
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
public class FileDecryptionTests
{
    private string directory = null!;
    private VariableDeclaration[] previous = null!;
    private const string Iv = "0x(000102030405060708090A0B0C0D0E0F)";

    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CWE-F02-" + Guid.NewGuid().ToString("N"));
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
    private static (ParameterVariableDeclaration P, KeyVariableDeclaration K) Parameters(int bits = 128, string padding = "PKCS-7")
    {
        var aes = new AES();
        var p = aes.GenerateParameters("AES-CBC", ["#IV:" + Iv, "#PAD:" + padding]);
        var k = aes.GenerateKey("AES-CBC", "0x(" + new string('0', bits / 4) + ")");
        p.Id = "p"; k.Id = "k";
        return (p, k);
    }
    private PathVariableDeclaration PathVariable(string name) =>
        new() { Id = "source", Type = new CryptoTypePath(), Value = Path.Combine(directory, name) };
    private PathVariableDeclaration Container(byte[] data, ParameterVariableDeclaration p, KeyVariableDeclaration k)
    {
        var source = PathVariable("message.bin");
        File.WriteAllBytes(source.Value, data);
        var encrypted = FileEncryptCoordinator.Encrypt(Arguments(p, k, source), source);
        encrypted.Id = "source";
        File.Delete(source.Value);
        return encrypted;
    }
    private static AlgorithmCallArguments Arguments(ParameterVariableDeclaration p, KeyVariableDeclaration k, PathVariableDeclaration path) =>
        new([new(p.Value, p), new(k.Value, k), new(path.Value, path)]);
    private static CryptoScriptParser.ProgramContext Parse(string script) =>
        new CryptoScriptParser(new CommonTokenStream(new CryptoScriptLexer(new AntlrInputStream(script)))).program();
    private static void Register(params VariableDeclaration[] variables)
    { foreach (var variable in variables) VariableDictionary.Instance().Add(variable); }
    private void OnlyContainer(PathVariableDeclaration source) =>
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { source.Value }));

    [TestCase(128, "NONE", 16)] [TestCase(192, "NONE", 32)] [TestCase(256, "NONE", 65536)]
    [TestCase(128, "PKCS-7", 0)] [TestCase(192, "PKCS-7", 1)] [TestCase(256, "PKCS-7", 15)]
    [TestCase(128, "PKCS-7", 16)] [TestCase(192, "PKCS-7", 17)] [TestCase(256, "PKCS-7", 65535)]
    [TestCase(128, "PKCS-7", 65536)] [TestCase(256, "PKCS-7", 65537)] [TestCase(192, "PKCS-7", 1000003)]
    public void RoundtripIsByteExactAndPreservesContainer(int bits, string padding, int length)
    {
        byte[] data = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        var (p, k) = Parameters(bits, padding); var source = Container(data, p, k);
        byte[] original = File.ReadAllBytes(source.Value); string parameterValue = p.Value;
        var result = FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(result.Value), Is.EqualTo(data));
            Assert.That(File.ReadAllBytes(source.Value), Is.EqualTo(original));
            Assert.That(result.Value, Is.EqualTo(Path.Combine(directory, "message.bin")));
            Assert.That(result.Type, Is.TypeOf<CryptoTypePath>());
            Assert.That(p.Value, Is.EqualTo(parameterValue));
            Assert.That(Directory.GetFiles(directory, ".cwe-*.tmp"), Is.Empty);
        });
    }

    [Test] public void IndependentF01ReferenceDecryptsToKnownNistPlaintext()
    {
        var source = PathVariable("reference.cwe");
        File.WriteAllBytes(source.Value, Convert.FromHexString(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "F01-reference.hex")).Trim()));
        var (p, _) = Parameters(padding: "NONE");
        var key = new AES().GenerateKey("AES-CBC", "0x(2B7E151628AED2A6ABF7158809CF4F3C)");
        var result = FileDecryptCoordinator.Decrypt(Arguments(p, key, source), source);
        Assert.That(File.ReadAllBytes(result.Value), Is.EqualTo(Convert.FromHexString("6BC1BEE22E409F96E93D7E117393172A")));
    }

    [TestCase("PKCS-7")] [TestCase("NONE")]
    public void StoredParametersOverrideUnspecifiedDefaultsWithoutMutation(string padding)
    {
        var (p, k) = Parameters(padding: padding); var source = Container(new byte[16], p, k);
        var automatic = new AES().GenerateParameters("AES-CBC");
        // Deliberately different, non-explicit defaults must not influence file decryption.
        automatic.SetParameter("IV", "0x(FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF)");
        automatic.SetParameter("PAD", "PKCS-7");
        string value = automatic.Value;
        var parameters = automatic.GetParameters().ToArray();
        var provenance = automatic.ExplicitParameterNames.ToArray();
        var result = FileDecryptCoordinator.Decrypt(Arguments(automatic, k, source), source);
        Assert.That(File.ReadAllBytes(result.Value), Is.EqualTo(new byte[16]));
        Assert.That(automatic.Value, Is.EqualTo(value));
        Assert.That(automatic.GetParameters(), Is.EqualTo(parameters));
        Assert.That(automatic.ExplicitParameterNames, Is.EqualTo(provenance));
    }

    [TestCase("IV", "0x(FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF)")]
    [TestCase("IV", "0x(00)")]
    [TestCase("PAD", "NONE")]
    [TestCase("PAD", "ISO-7816")]
    public void ExplicitConflictsFailWithoutOutput(string name, string value)
    {
        var (p, k) = Parameters(); var source = Container([1], p, k);
        p.SetParameter(name, value);
        Assert.Throws<ArgumentException>(() => FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source));
        OnlyContainer(source);
    }

    [Test] public void EquivalentExplicitBase64IvAndCaseInsensitivePaddingAreAccepted()
    {
        var (p, k) = Parameters(); var source = Container([1, 2], p, k);
        p.SetParameter("IV", "b64(AAECAwQFBgcICQoLDA0ODw==)"); p.SetParameter("PAD", "pkcs-7");
        var result = FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source);
        Assert.That(File.ReadAllBytes(result.Value), Is.EqualTo(new byte[] { 1, 2 }));
    }

    [Test] public void WrongMechanismFailsWithoutOutput()
    {
        var (p, k) = Parameters(); var source = Container([1], p, k); p.Mechanism = "AES-ECB";
        Assert.Throws<ArgumentException>(() => FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source));
        OnlyContainer(source);
    }

    [TestCase("VAR result = Decrypt(p,k,source)")]
    [TestCase("KEY result = Decrypt(p,k,source)")]
    [TestCase("PARAM result = Decrypt(p,k,source)")]
    [TestCase("Print(Decrypt(p,k,source))")]
    [TestCase("VAR result = Encrypt(p,k,Decrypt(p,k,source))")]
    [TestCase("PATH result = Decrypt(Parameters(AES-CBC),k,source)")]
    [TestCase("Print(Encrypt(p,k,source),Decrypt(p,k,source))")]
    public void InvalidContextsAreRejectedBeforeSideEffects(string script)
    {
        var (p, k) = Parameters(); var source = Container([1], p, k); Register(p, k, source);
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse(script)));
        Assert.That(runner.Diagnostics.Any(d => d.DiagnosticCode == SemanticDiagnosticCode.FunctionContractViolation), Is.True);
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        OnlyContainer(source);
    }

    [Test] public void RunnerReturnsPathAndPropagatesKeyMetadataWarnings()
    {
        var (p, k) = Parameters(); var source = Container([0, 255, 1], p, k);
        k.KeyType = KeyType.Secret(KeyAlgorithm.Hmac); k.Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt);
        Register(p, k, source); var runner = new CryptoScriptRunner();
        runner.Execute(Parse("PATH result = Decrypt(p,k,source)\nPrint(result)"));
        var result = VariableDictionary.Instance().Get("result");
        Assert.That(result, Is.TypeOf<PathVariableDeclaration>());
        Assert.That(File.ReadAllBytes(result.Value), Is.EqualTo(new byte[] { 0, 255, 1 }));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.KeyTypeConflict));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.KeyUsageConflict));
        Assert.That(runner.Diagnostics.Select(d => d.Severity), Is.All.EqualTo(SemanticDiagnosticSeverity.Warning));
    }

    [Test] public void VarAndLiteralPathStringsDoNotSelectFileDispatch()
    {
        var (p, k) = Parameters(); var source = Container([1], p, k);
        Register(p, k, new StringVariableDeclaration { Id = "source", Type = new CryptoTypeVar(), Value = source.Value });
        Assert.Throws<SemanticErrorException>(() => new CryptoScriptRunner().Execute(Parse("VAR result = Decrypt(p,k,source)")));
        string escaped = source.Value.Replace("\\", "\\\\");
        Assert.Throws<SemanticErrorException>(() => new CryptoScriptRunner().Execute(Parse($"VAR result = Decrypt(p,k,\"{escaped}\")")));
        OnlyContainer(source);
    }

    [Test] public void VarDecryptRemainsByteExact()
    {
        var (p, k) = Parameters(padding: "NONE"); Register(p, k);
        new CryptoScriptRunner().Execute(Parse("VAR result = Decrypt(p,k,0x(66E94BD4EF8A2C3B884CFA59CA342B2E))"));
        var result = VariableDictionary.Instance().Get("result");
        Assert.That(result, Is.TypeOf<StringVariableDeclaration>());
        Assert.That(FormatConversions.ToByteArray(result.Value, result.ValueFormat), Is.EqualTo(new byte[16].Select((_, i) => (byte)i).ToArray()));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(15)] [TestCase(17)] [TestCase(31)]
    public void InvalidCiphertextLengthsFailWithoutRegisteringPath(int length)
    {
        var (p, k) = Parameters(); var source = Container([1], p, k);
        byte[] frame = EncryptedFileCodec.Encode(new("AES-CBC", "PKCS-7", "AAECAwQFBgcICQoLDA0ODw=="));
        File.WriteAllBytes(source.Value, frame.Concat(new byte[length]).ToArray()); Register(p, k, source);
        Assert.Throws<SemanticErrorException>(() => new CryptoScriptRunner().Execute(Parse("PATH result = Decrypt(p,k,source)")));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null); OnlyContainer(source);
    }

    [TestCase("magic")] [TestCase("major")] [TestCase("minor")] [TestCase("oversize")]
    [TestCase("zero-header")] [TestCase("prefix-truncated")] [TestCase("header-truncated")]
    [TestCase("utf8")] [TestCase("json")] [TestCase("duplicate")] [TestCase("unknown")]
    [TestCase("mechanism")] [TestCase("padding")] [TestCase("iv")] [TestCase("encoding")]
    [TestCase("authentication")] [TestCase("tag")] [TestCase("null")] [TestCase("type")]
    public void DamagedContainersFailWithoutOutput(string damage)
    {
        var (p, k) = Parameters(); var source = Container([1], p, k);
        byte[] bytes = File.ReadAllBytes(source.Value);
        int headerLength = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(12));
        string json = Encoding.UTF8.GetString(bytes, 16, headerLength);
        switch (damage)
        {
            case "magic": bytes[0] ^= 1; break;
            case "major": bytes[9] = 2; break;
            case "minor": bytes[11] = 1; break;
            case "oversize": BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12), 4097); break;
            case "zero-header": BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12), 0); break;
            case "prefix-truncated": bytes = bytes[..15]; break;
            case "header-truncated": bytes = bytes[..(15 + headerLength)]; break;
            case "utf8": bytes[18] = 0xff; break;
            default:
                json = damage switch
                {
                    "json" => "{",
                    "duplicate" => "{\"padding\":\"PKCS-7\"," + json[1..],
                    "unknown" => "{\"extra\":1," + json[1..],
                    "mechanism" => json.Replace("AES-CBC", "AES-GCM"),
                    "padding" => json.Replace("PKCS-7", "ISO-7816"),
                    "iv" => json.Replace("AAECAwQFBgcICQoLDA0ODw==", "AA=="),
                    "encoding" => json.Replace("binary", "hex"),
                    "authentication" => json.Replace("none", "gcm"),
                    "tag" => json.Replace("\"tagLength\":0", "\"tagLength\":16"),
                    "null" => json.Replace("\"AES-CBC\"", "null"),
                    "type" => json.Replace("\"tagLength\":0", "\"tagLength\":\"0\""),
                    _ => throw new ArgumentException(damage)
                };
                byte[] metadata = Encoding.UTF8.GetBytes(json);
                bytes = bytes[..16].Concat(metadata).Concat(new byte[16]).ToArray();
                BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12), (uint)metadata.Length);
                break;
        }
        File.WriteAllBytes(source.Value, bytes); Register(p, k, source);
        Assert.Throws<SemanticErrorException>(() => new CryptoScriptRunner().Execute(Parse("PATH result = Decrypt(p,k,source)")));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null); OnlyContainer(source);
        Assert.That(File.ReadAllBytes(source.Value), Is.EqualTo(bytes));
    }

    [Test] public void InvalidPaddingNeverPublishesPartialPlaintext()
    {
        var (p, k) = Parameters(); var source = Container(new byte[65536], p, k);
        byte[] bytes = File.ReadAllBytes(source.Value);
        bytes[^17] ^= 16; // Force the final PKCS-7 byte from 16 to 0 via its predecessor block.
        File.WriteAllBytes(source.Value, bytes); Register(p, k, source);
        Assert.Throws<SemanticErrorException>(() => new CryptoScriptRunner().Execute(Parse("PATH result = Decrypt(p,k,source)")));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null); OnlyContainer(source);
    }

    [Test] public void SelectedWrongKeyFailsPaddingWithoutOutput()
    {
        var (p, k) = Parameters(); var source = Container(new byte[16], p, k);
        var wrong = new AES().GenerateKey("AES-CBC", "0x(FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF)");
        Assert.Throws<CryptographicException>(() => FileDecryptCoordinator.Decrypt(Arguments(p, wrong, source), source));
        OnlyContainer(source);
    }

    [Test] public void NoneCannotDetectWrongKey()
    {
        var (p, k) = Parameters(padding: "NONE"); var source = Container(new byte[16], p, k);
        var wrong = new AES().GenerateKey("AES-CBC", "0x(FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF)");
        var result = FileDecryptCoordinator.Decrypt(Arguments(p, wrong, source), source);
        Assert.That(File.ReadAllBytes(result.Value), Has.Length.EqualTo(16).And.Not.EqualTo(new byte[16]));
    }

    [TestCase("message.bin.cwe", "message.bin")]
    [TestCase("message.bin.1.cwe", "message.bin.1")]
    [TestCase("renamed.container", "renamed.container.decrypted")]
    public void NamesUseFirstGapAndPreserveExistingOutputs(string name, string expected)
    {
        var (p, k) = Parameters(); var source = Container([1, 2], p, k);
        string moved = Path.Combine(directory, name); if (source.Value != moved) File.Move(source.Value, moved); source.Value = moved;
        foreach (string suffix in new[] { "", ".1", ".3" }) File.WriteAllBytes(Path.Combine(directory, expected + suffix), [77]);
        var result = FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source);
        Assert.That(result.Value, Is.EqualTo(Path.Combine(directory, expected + ".2")));
        Assert.That(File.ReadAllBytes(result.Value), Is.EqualTo(new byte[] { 1, 2 }));
        foreach (string suffix in new[] { "", ".1", ".3" }) Assert.That(File.ReadAllBytes(Path.Combine(directory, expected + suffix)), Is.EqualTo(new byte[] { 77 }));
    }

    [Test] public async Task ConcurrentPublicationsNeverOverwrite()
    {
        var (p, k) = Parameters(); var source = Container([1, 2, 3], p, k);
        using var barrier = new Barrier(4);
        var outputs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source,
                beforePublish: () => { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException(); }).Value)));
        Assert.That(outputs.Distinct().Count(), Is.EqualTo(4));
        foreach (string output in outputs) Assert.That(File.ReadAllBytes(output), Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(Directory.GetFiles(directory, ".cwe-*.tmp"), Is.Empty);
    }

    [Test] public void PublicationCollisionRetriesAndDirectoriesAreNotReplaced()
    {
        var (p, k) = Parameters(); var source = Container([1], p, k);
        Directory.CreateDirectory(Path.Combine(directory, "message.bin"));
        var result = FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source,
            beforePublish: () => File.WriteAllBytes(Path.Combine(directory, "message.bin.1"), [77]));
        Assert.That(result.Value, Is.EqualTo(Path.Combine(directory, "message.bin.2")));
        Assert.That(File.ReadAllBytes(Path.Combine(directory, "message.bin.1")), Is.EqualTo(new byte[] { 77 }));
    }

    [TestCase("cancel")] [TestCase("io")] [TestCase("names")]
    public void FailureBeforePublicationCleansPlaintext(string failure)
    {
        var (p, k) = Parameters(); var source = Container(new byte[65537], p, k);
        using var cts = new CancellationTokenSource();
        Action callback = failure switch { "cancel" => cts.Cancel, "io" => () => throw new IOException("Injected I/O failure"), _ => () => { } };
        if (failure == "cancel")
            Assert.Throws<OperationCanceledException>(() => FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source, cts.Token, callback));
        else
            Assert.Throws<IOException>(() => FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source, beforePublish: callback, maximumCandidates: failure == "names" ? 0 : 1000000));
        OnlyContainer(source);
    }

    [Test] public void RunnerCancellationDoesNotRegisterPath()
    {
        var (p, k) = Parameters(); var source = Container([1], p, k); Register(p, k, source);
        using var cts = new CancellationTokenSource();
        var runner = new CryptoScriptRunner((call, _) => { if (call.Name == "Decrypt") cts.Cancel(); });
        Assert.ThrowsAsync<OperationCanceledException>(async () => await runner.ExecuteAsync(Parse("PATH result = Decrypt(p,k,source)"), cts.Token));
        OnlyContainer(source); Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
    }

    [TestCase("missing")] [TestCase("directory")] [TestCase("locked")]
    public void SourceFailuresDoNotRegisterPath(string failure)
    {
        if (failure == "locked" && !OperatingSystem.IsWindows()) Assert.Ignore("Windows mandatory sharing contract.");
        var (p, k) = Parameters(); var source = Container([1], p, k); Register(p, k, source);
        if (failure != "locked") File.Delete(source.Value);
        if (failure == "directory") Directory.CreateDirectory(source.Value);
        using var locked = failure == "locked" ? new FileStream(source.Value, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        Assert.Throws<SemanticErrorException>(() => new CryptoScriptRunner().Execute(Parse("PATH result = Decrypt(p,k,source)")));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(Directory.GetFiles(directory, ".cwe-*.tmp"), Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public void SourceAndAncestorSymlinksAreRejected(bool ancestor)
    {
        var (p, k) = Parameters(); var source = Container([1], p, k);
        string link = Path.Combine(directory, ancestor ? "linked" : "linked.cwe");
        try
        {
            if (ancestor) Directory.CreateSymbolicLink(link, directory);
            else File.CreateSymbolicLink(link, source.Value);
        }
        catch (Exception e) when (e is UnauthorizedAccessException || OperatingSystem.IsWindows() && e is IOException && (e.HResult & 0xffff) == 1314)
        { Assert.Ignore("Symbolic link privilege unavailable."); }
        var linked = PathVariable(ancestor ? Path.Combine("linked", "message.bin.cwe") : "linked.cwe");
        try { Assert.Throws<IOException>(() => FileDecryptCoordinator.Decrypt(Arguments(p, k, linked), linked)); }
        finally { if (ancestor) Directory.Delete(link); else File.Delete(link); }
        OnlyContainer(source);
    }

    [Test] public void PrivateTemporaryAndFinalPermissions()
    {
        var (p, k) = Parameters(); var source = Container([1], p, k);
        void CheckPrivate(string file)
        {
            if (OperatingSystem.IsWindows())
            {
                var acl = System.IO.FileSystemAclExtensions.GetAccessControl(new FileInfo(file));
                Assert.That(acl.AreAccessRulesProtected, Is.True);
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                foreach (System.Security.AccessControl.FileSystemAccessRule rule in
                    acl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier)))
                    Assert.That(rule.IdentityReference.Equals(identity.User), Is.True);
            }
            else Assert.That(File.GetUnixFileMode(file) & (UnixFileMode)0x1ff, Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
        }
        var result = FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source,
            beforePublish: () => CheckPrivate(Directory.GetFiles(directory, ".cwe-*.tmp").Single()));
        CheckPrivate(result.Value);
    }

    [Test] public void LargeFileRoundtripUsesBoundedMemory()
    {
        var (p, k) = Parameters(); var clear = PathVariable("large.bin");
        byte[] buffer = Enumerable.Range(0, 65536).Select(i => (byte)i).ToArray();
        using (var output = File.Create(clear.Value)) for (int i = 0; i < 1024; i++) output.Write(buffer);
        byte[] expected;
        using (var input = File.OpenRead(clear.Value)) expected = SHA256.HashData(input);
        var source = FileEncryptCoordinator.Encrypt(Arguments(p, k, clear), clear); File.Delete(clear.Value);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var result = FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(allocated, Is.LessThan(2 * 1024 * 1024));
        using var restored = File.OpenRead(result.Value);
        Assert.That(restored.Length, Is.EqualTo(64L * 1024 * 1024));
        Assert.That(SHA256.HashData(restored), Is.EqualTo(expected));
        TestContext.Out.WriteLine($"64 MiB decrypt managed allocations: {allocated} bytes.");
    }

    [Test] public void StreamHandlesShortReadsAndCancellationAndErasesWrittenPlaintextBuffer()
    {
        var (p, k) = Parameters(padding: "NONE");
        using var aes = AES_CBC.CreateCipher(k, AES_CBC.ValidateAndDecodeIv(Iv));
        using var cts = new CancellationTokenSource();
        using var input = new ObservedStream(new byte[131072], cts.Cancel);
        using var output = new CapturingStream();
        Assert.Throws<OperationCanceledException>(() => AES_CBC.DecryptStream(input, output, aes, p, cts.Token));
        Assert.That(input.Position, Is.EqualTo(7));
        using var shortInput = new ObservedStream(new byte[131072]);
        AES_CBC.DecryptStream(shortInput, output, aes, p, default);
        Assert.That(output.Bytes, Is.EqualTo(131072));
        Assert.That(shortInput.MaximumRead, Is.LessThanOrEqualTo(65536));
        Assert.That(output.LastBuffer, Is.All.Zero);
    }

    [Test] public void InvalidKeyLengthFailsBeforeCreatingPlaintext()
    {
        var (p, k) = Parameters(); var source = Container([1], p, k);
        k.Value = "0x(00)";
        Assert.Throws<CryptographicException>(() => FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source));
        OnlyContainer(source);
    }

    [Test] public void TemporaryReplacementCannotPublishForeignPlaintext()
    {
        var (p, k) = Parameters(); var source = Container([1, 2], p, k);
        void ReplaceTemporary()
        {
            string temporary = Directory.GetFiles(directory, ".cwe-*.tmp").Single();
            if (OperatingSystem.IsWindows()) Assert.Throws<IOException>(() => File.Delete(temporary));
            else { File.Delete(temporary); File.WriteAllBytes(temporary, [77]); }
        }
        if (OperatingSystem.IsWindows())
        {
            var result = FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source, beforePublish: ReplaceTemporary);
            Assert.That(File.ReadAllBytes(result.Value), Is.EqualTo(new byte[] { 1, 2 }));
        }
        else
        {
            Assert.Throws<IOException>(() => FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source, beforePublish: ReplaceTemporary));
            OnlyContainer(source);
        }
    }

    [Test] public void DirectoryMoveReturnsCurrentPathOrIsDeniedByWindowsLease()
    {
        var (p, k) = Parameters(); var source = Container([1], p, k);
        string moved = directory + "-moved";
        var result = FileDecryptCoordinator.Decrypt(Arguments(p, k, source), source, beforePublish: () =>
        {
            if (OperatingSystem.IsWindows()) Assert.Throws<IOException>(() => Directory.Move(directory, moved));
            else { Directory.Move(directory, moved); directory = moved; }
        });
        Assert.That(Path.GetDirectoryName(result.Value), Is.EqualTo(directory));
        Assert.That(File.ReadAllBytes(result.Value), Is.EqualTo(new byte[] { 1 }));
    }

    [TestCase("eof")] [TestCase("grow")] [TestCase("shrink")]
    public void StreamLengthChangesAndEarlyEofFail(string change)
    {
        var (p, k) = Parameters(padding: "NONE");
        using var aes = AES_CBC.CreateCipher(k, AES_CBC.ValidateAndDecodeIv(Iv));
        using var input = new ChangingLengthStream(change);
        Assert.Throws<IOException>(() => AES_CBC.DecryptStream(input, Stream.Null, aes, p, default));
    }

    [TestCase(false)] [TestCase(true)]
    public void StreamFailureAfterWritingPlaintextClearsBuffer(bool cancel)
    {
        var (p, k) = Parameters(padding: "NONE");
        using var aes = AES_CBC.CreateCipher(k, AES_CBC.ValidateAndDecodeIv(Iv));
        using var cts = new CancellationTokenSource();
        using var input = new MemoryStream(new byte[131072]);
        using var output = new CapturingStream(() =>
        {
            if (cancel) cts.Cancel(); else throw new IOException("Injected write failure");
        });
        if (cancel) Assert.Throws<OperationCanceledException>(() => AES_CBC.DecryptStream(input, output, aes, p, cts.Token));
        else Assert.Throws<IOException>(() => AES_CBC.DecryptStream(input, output, aes, p, default));
        Assert.That(input.Position, Is.EqualTo(65536));
        Assert.That(output.Bytes, Is.EqualTo(65536));
        Assert.That(output.LastBuffer, Is.All.Zero);
    }

    private sealed class ChangingLengthStream(string change) : MemoryStream(new byte[48])
    {
        public override long Length => Position == 0 || change == "eof" ? 32 : change == "grow" ? 48 : 16;
        public override int Read(byte[] buffer, int offset, int count) =>
            base.Read(buffer, offset, (int)Math.Min(count, Math.Max(0, (change == "eof" ? 31 : Length) - Position)));
        public override int ReadByte() => Position >= Length ? -1 : base.ReadByte();
    }

    private sealed class CapturingStream(Action? onWrite = null) : MemoryStream
    {
        public byte[] LastBuffer { get; private set; } = [];
        public long Bytes { get; private set; }
        public override void Write(byte[] buffer, int offset, int count) { LastBuffer = buffer; Bytes += count; onWrite?.Invoke(); }
        public override void Write(ReadOnlySpan<byte> buffer) { Bytes += buffer.Length; }
    }
    private sealed class ObservedStream(byte[] bytes, Action? onRead = null) : MemoryStream(bytes)
    {
        public int MaximumRead { get; private set; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            MaximumRead = Math.Max(MaximumRead, count);
            int read = base.Read(buffer, offset, Math.Min(count, 7)); onRead?.Invoke(); return read;
        }
    }
}
