using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Antlr4.Runtime;
using CryptoScript;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.FileEncryption;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class CwmTests
{
    private string directory = null!;
    private ParameterVariableDeclaration p = null!;
    private KeyVariableDeclaration k = null!;
    private PathVariableDeclaration source = null!;
    private VariableDeclaration[] previous = null!;
    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CWM-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsMacOS() && directory.StartsWith("/var/")) directory = "/private" + directory;
        Directory.CreateDirectory(directory);
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
        Inputs("AES-CMAC");
    }
    [TearDown] public void Cleanup()
    {
        VariableDictionary.Instance().Clear();
        foreach (var v in previous) VariableDictionary.Instance().Add(v);
        Directory.Delete(directory, true);
    }
    private void Inputs(string mechanism)
    {
        VariableDictionary.Instance().Clear();
        var algorithm = AlgorithmFactory.Create(mechanism);
        p = mechanism == "AES-GMAC" ? algorithm.GenerateParameters(mechanism, ["#NONCE:0x(000102030405060708090A0B)"])
            : algorithm.GenerateParameters(mechanism);
        p.Id = "p";
        k = algorithm.GenerateKey(mechanism, "0x(0123456789ABCDEFFEDCBA9876543210)"); k.Id = "k";
        source = new PathVariableDeclaration { Id = "source", Value = Path.Combine(directory, "input.bin") };
        foreach (var v in new VariableDeclaration[] { p, k, source }) VariableDictionary.Instance().Add(v);
    }
    private OperationInvocation Invocation(PathVariableDeclaration path, CancellationToken token = default) =>
        new(new VariableDeclaration[] { p, k, path }.Select(v => new ResolvedCallArgument(v.Value, ResolvedCallArgumentKind.Variable, v)), null, token);
    private PathVariableDeclaration Pack() => (PathVariableDeclaration)CwmCoordinator.Execute(Invocation(source), false);
    private string Verify(PathVariableDeclaration container) => CwmCoordinator.Execute(Invocation(container), true).Value;
    private AlgorithmCallArguments Args() => new(new VariableDeclaration[] { p, k, source }.Select(v => new AlgorithmCallArgument(v.Value, v)));
    private static CryptoScriptParser.ProgramContext Parse(string script) => new CryptoScriptParser(
        new CommonTokenStream(new CryptoScriptLexer(new AntlrInputStream(script)))).program();
    private static void Run(string script) => new CryptoScriptRunner().Execute(Parse(script));
    private static JsonObject Read(PathVariableDeclaration path) => JsonNode.Parse(File.ReadAllText(path.Value))!.AsObject();
    private static void Write(PathVariableDeclaration path, JsonObject obj) => File.WriteAllText(path.Value, obj.ToJsonString());
    public static IEnumerable<TestCaseData> Cases() => MechanismRegistry.Entries.Where(FileMacCoordinator.Supports)
        .SelectMany(e => new[] { 0, 1, 7, 8, 9, 15, 16, 17, 65535, 65536, 65537, 1000003 }
            .Select(n => new TestCaseData(e.CanonicalName, n)));

    [TestCaseSource(nameof(Cases))]
    public void RoundtripAllMechanisms(string mechanism, int size)
    {
        Inputs(mechanism);
        byte[] bytes = Enumerable.Range(0, size).Select(i => (byte)i).ToArray(); File.WriteAllBytes(source.Value, bytes);
        var container = Pack();
        Assert.That(Verify(container), Is.EqualTo("true"));
        var fm = Read(container)["fileMac"]!;
        Assert.That(fm["size"]!.GetValue<long>(), Is.EqualTo(size));
        Assert.That(fm["mac"]!.GetValue<string>(), Is.EqualTo(Convert.ToHexString(FormatConversions.ToByteArray(
            FileMacCoordinator.Mac(Args(), source).Value, FormatConversions.HEX))));
        Assert.That(File.ReadAllBytes(source.Value), Is.EqualTo(bytes));
        File.WriteAllBytes(source.Value, bytes.Concat(new byte[] { 1 }).ToArray());
        Assert.That(Verify(container), Is.EqualTo("false"));
    }
    [Test] public void ScriptAndValContract()
    {
        File.WriteAllBytes(source.Value, [1, 2, 3]);
        Run("PATH container = MacPack(p,k,source)\nVAL valid = MacVerify(p,k,container)\nPrint(valid)");
        var valid = VariableDictionary.Instance().Get("valid")!;
        Assert.That(valid.Type, Is.TypeOf<CryptoTypeVal>()); Assert.That(valid.Value, Is.EqualTo("true"));
    }
    [TestCase("VAR result = MacPack(p,k,source)")]
    [TestCase("VAL result = MacPack(p,k,source)")]
    [TestCase("PATH result = MacVerify(p,k,source)")]
    [TestCase("VAR result = MacVerify(p,k,source)")]
    [TestCase("PATH result = MacPack(Parameters(AES-CMAC),k,source)")]
    [TestCase("PATH result = MacPack(p,k,source,source)")]
    [TestCase("PATH result = MacPack(p,k)")]
    [TestCase("PATH result = MacPack(k,p,source)")]
    [TestCase("PATH result = MacPack(p,k,\"missing\")")]
    [TestCase("Print(MacPack(p,k,source))")]
    [TestCase("Print(MacVerify(p,k,source))")]
    [TestCase("VAL result = MacVerify(p,k,MacPack(p,k,source))")]
    public void BadContractsDoNotTouchFiles(string script)
    {
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse(script)));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.FunctionContractViolation));
        Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
    }
    [TestCase("file")] [TestCase("size")] [TestCase("mechanism")] [TestCase("mac")] [TestCase("parameters")]
    public void MetadataTamperFailsBeforeOpeningReference(string field)
    {
        File.WriteAllBytes(source.Value, [1]); var container = Pack(); var doc = Read(container);
        var fm = doc["fileMac"]!.AsObject();
        switch (field)
        {
            case "size": fm[field] = 2; break;
            case "mac": fm[field] = new string('0', 32); break;
            case "parameters": fm[field] = new JsonObject { ["extra"] = "tampered" }; break;
            default: fm[field] = "missing.bin"; break;
        }
        Write(container, doc); File.Delete(source.Value);
        if (field is "mechanism" or "parameters") Assert.Throws<FormatException>(() => Verify(container));
        else Assert.That(Verify(container), Is.EqualTo("false"));
    }
    [TestCase("mechanism")] [TestCase("canonicalization")] [TestCase("keyDerivation")] [TestCase("mac")]
    public void MetadataAuthTamper(string field)
    {
        File.WriteAllBytes(source.Value, [1]); var container = Pack(); var doc = Read(container);
        doc["metadataAuth"]![field] = field == "mac" ? new string('0', 64) : "unsupported";
        Write(container, doc); File.Delete(source.Value);
        if (field == "mac") Assert.That(Verify(container), Is.EqualTo("false"));
        else Assert.Catch(() => Verify(container));
    }
    [Test] public void WrongKeyAndParamAndSameLengthContentChange()
    {
        File.WriteAllBytes(source.Value, [1]); var container = Pack(); string key = k.Value;
        k.Value = "0x(1023456789ABCDEFFEDCBA9876543210)";
        Assert.That(Verify(container), Is.EqualTo("false")); k.Value = key;
        p = AlgorithmFactory.Create("HMAC-SHA256").GenerateParameters("HMAC-SHA256");
        Assert.Catch(() => Verify(container));
        Inputs("AES-CMAC"); File.WriteAllBytes(source.Value, [2]);
        Assert.That(Verify(container), Is.EqualTo("false"));
    }
    [TestCase("../input.bin")] [TestCase("/input.bin")] [TestCase("C:\\input.bin")]
    [TestCase("sub/input.bin")] [TestCase("sub\\input.bin")] [TestCase("input.bin:stream")]
    public void TraversalRejectedEvenWithValidAuthentication(string name)
    {
        File.WriteAllBytes(source.Value, [1]); var container = Pack(); var doc = Read(container);
        doc["fileMac"]!["file"] = name; Sign(doc); Write(container, doc);
        Assert.Catch(() => Verify(container));
    }
    private void Sign(JsonObject doc)
    {
        using var json = JsonDocument.Parse(doc["fileMac"]!.ToJsonString());
        doc["metadataAuth"]!["mac"] = Convert.ToHexString(CwmCoordinator.Authenticate(Args(), json.RootElement));
    }
    [TestCase("root")] [TestCase("fileMac")] [TestCase("metadataAuth")] [TestCase("parameters")]
    public void DuplicateKeysRejected(string scope)
    {
        File.WriteAllBytes(source.Value, [1]); var container = Pack();
        string text = File.ReadAllText(container.Value);
        text = scope switch
        {
            "root" => text.Replace("\"format\":\"CWM\"", "\"format\":\"CWM\",\"format\":\"CWM\""),
            "fileMac" => text.Replace("\"size\":1", "\"size\":1,\"size\":1"),
            "metadataAuth" => text.Replace("\"canonicalization\":", "\"canonicalization\":\"RFC8785-JCS\",\"canonicalization\":"),
            _ => text.Replace("\"parameters\":{}", "\"parameters\":{\"x\":\"a\",\"x\":\"a\"}")
        };
        File.WriteAllText(container.Value, text); Assert.Catch(() => Verify(container));
    }
    [TestCase("version")] [TestCase("json")] [TestCase("tooLarge")] [TestCase("utf8")]
    [TestCase("unknown")] [TestCase("size")] [TestCase("hex")] [TestCase("surrogate")]
    public void MalformedContainers(string kind)
    {
        File.WriteAllBytes(source.Value, [1]); var container = Pack(); var doc = Read(container);
        if (kind == "version") doc["version"] = 2;
        if (kind == "unknown") doc["unknown"] = "x";
        if (kind == "size") doc["fileMac"]!["size"] = 9007199254740992L;
        if (kind == "hex") doc["fileMac"]!["mac"] = "abcd";
        Write(container, doc);
        if (kind == "json") File.WriteAllText(container.Value, "{");
        if (kind == "tooLarge") File.WriteAllBytes(container.Value, new byte[65537]);
        if (kind == "utf8") File.WriteAllBytes(container.Value, [0xff]);
        if (kind == "surrogate") File.WriteAllText(container.Value, File.ReadAllText(container.Value).Replace("input.bin", "\\ud800"));
        Assert.Catch(() => Verify(container));
    }
    [Test] public void MissingFilesAndDiagnosticContract()
    {
        Assert.Throws<SemanticErrorException>(() => Run("PATH result = MacPack(p,k,source)"));
        File.WriteAllBytes(source.Value, [1]); var container = Pack(); File.Delete(source.Value);
        Assert.Catch(() => Verify(container));
        container.Id = "container"; VariableDictionary.Instance().Add(container);
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse("VAL result = MacVerify(p,k,container)")));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.FunctionExecutionFailed));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
    }
    [Test] public void SymlinksRejected()
    {
        File.WriteAllBytes(source.Value, [1]); var container = Pack();
        string alias = Path.Combine(directory, "alias.cwm");
        try { File.CreateSymbolicLink(alias, container.Value); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { Assert.Ignore("Symlink unavailable: " + ex.Message); }
        Assert.Catch(() => Verify(new PathVariableDeclaration { Value = alias }));
        string target = Path.Combine(directory, "target"); File.Move(source.Value, target); File.CreateSymbolicLink(source.Value, target);
        Assert.Catch(() => Verify(container)); Assert.Catch(() => Pack());
    }
    [Test] public void ParallelPublicationDoesNotOverwrite()
    {
        File.WriteAllBytes(source.Value, [1]);
        var paths = Enumerable.Range(0, 12).AsParallel().Select(_ => Pack().Value).ToArray();
        Assert.That(paths.Distinct().Count(), Is.EqualTo(12));
        Assert.That(paths.Select(Path.GetFileName), Does.Contain("input.bin.cwm").And.Contain("input.bin.11.cwm"));
        foreach (string path in paths) Assert.That(Verify(new PathVariableDeclaration { Value = path }), Is.EqualTo("true"));
        Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
    }
    [TestCase("cancel")] [TestCase("io")] [TestCase("exhausted")]
    public void FailedPublicationCleansTemporary(string failure)
    {
        File.WriteAllBytes(source.Value, [1]); using var cts = new CancellationTokenSource();
        using var publication = new FilePublication(directory);
        Assert.Catch(() => CwmCoordinator.Pack(Args(), p, CwmCoordinator.Parameters(p), publication, "input.bin", cts.Token,
            () => { if (failure == "cancel") cts.Cancel(); if (failure == "io") throw new IOException("injected"); },
            maximumCandidates: failure == "exhausted" ? 0 : 10));
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { source.Value }));
    }
    [Test] public void PreCancelledCallsDoNotOpenOrPublish()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => CwmCoordinator.Execute(Invocation(source, cts.Token), false));
        Assert.Throws<OperationCanceledException>(() => CwmCoordinator.Execute(Invocation(source, cts.Token), true));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void ExistingFilesAndDirectoriesAreNotOverwritten()
    {
        File.WriteAllBytes(source.Value, [1]);
        string occupied = source.Value + ".cwm";
        File.WriteAllText(occupied, "preserve"); Directory.CreateDirectory(source.Value + ".1.cwm");
        Assert.That(Pack().Value, Is.EqualTo(source.Value + ".2.cwm"));
        Assert.That(File.ReadAllText(occupied), Is.EqualTo("preserve"));
        Assert.That(Directory.Exists(source.Value + ".1.cwm"), Is.True);
    }
    [Test] public void AuthenticatedWrongFileTagReturnsFalse()
    {
        File.WriteAllBytes(source.Value, [1]); var container = Pack(); var doc = Read(container);
        doc["fileMac"]!["mac"] = new string('0', 32); Sign(doc); Write(container, doc);
        Assert.That(Verify(container), Is.EqualTo("false"));
    }
    [Test] public void UnicodeFileNameIsPreservedWithoutNormalization()
    {
        source.Value = Path.Combine(directory, "e\u0301-€-😀.bin"); File.WriteAllBytes(source.Value, [1]);
        var container = Pack();
        Assert.That(Read(container)["fileMac"]!["file"]!.GetValue<string>(), Is.EqualTo("e\u0301-€-😀.bin"));
        Assert.That(Verify(container), Is.EqualTo("true"));
    }
    [Test] public void LoneSurrogateFilenameRejectedBeforeOpening()
    {
        using var publication = new FilePublication(directory);
        Assert.Throws<EncoderFallbackException>(() => CwmCoordinator.Pack(Args(), p, CwmCoordinator.Parameters(p),
            publication, "\ud800.bin", default));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }
    [Test] public void NonePaddingLengthChangeReturnsFalse()
    {
        Inputs("AES-CBC-MAC"); p.SetParameter("PAD", "NONE");
        File.WriteAllBytes(source.Value, new byte[16]); var container = Pack();
        File.WriteAllBytes(source.Value, [1]);
        Assert.That(Verify(container), Is.EqualTo("false"));
    }
    [Test] public void RunnerCancellationHasNoResultOrContainer()
    {
        File.WriteAllBytes(source.Value, [1]); using var cts = new CancellationTokenSource();
        var runner = new CryptoScriptRunner((call, _) => { if (call.Name == "MacPack") cts.Cancel(); });
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await runner.ExecuteAsync(Parse("PATH result = MacPack(p,k,source)"), cts.Token));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { source.Value }));
    }
    [Test] public void IndependentHkdfAndHmacVerification()
    {
        File.WriteAllBytes(source.Value, [0, 1, 2]); var container = Pack(); var doc = Read(container);
        string canonical = "{\"file\":\"input.bin\",\"mac\":\"" + doc["fileMac"]!["mac"]!.GetValue<string>() +
            "\",\"mechanism\":\"AES-CMAC\",\"parameters\":{},\"size\":3}";
        byte[] ikm = Convert.FromHexString("0123456789ABCDEFFEDCBA9876543210");
        // Independent RFC 5869 extract + one-block expand, no production HKDF/canonicalizer.
        byte[] prk = HMACSHA256.HashData(Encoding.UTF8.GetBytes("CryptoWorkBench/CWM/v1/HKDF-SHA256"), ikm);
        byte[] derived = HMACSHA256.HashData(prk, Encoding.UTF8.GetBytes("CryptoWorkBench/CWM/v1/metadataAuth/HMAC-SHA256").Concat(new byte[] { 1 }).ToArray());
        Assert.That(doc["metadataAuth"]!["mac"]!.GetValue<string>(),
            Is.EqualTo(Convert.ToHexString(HMACSHA256.HashData(derived, Encoding.UTF8.GetBytes(canonical)))));
    }
    [Test] public void JcsRfc8785UnicodeOrderingAndEscaping()
    {
        // RFC 8785 section 3.2.3 ordering example. Values shortened to keep the vector compact.
        const string input = "{\"\\u20ac\":\"Euro\",\"\\r\":\"CR\",\"\\ufb33\":\"Hebrew\",\"1\":\"One\",\"\\ud83d\\ude00\":\"Emoji\",\"\\u0080\":\"Control\",\"\\u00f6\":\"Latin\"}";
        using var doc = JsonDocument.Parse(input);
        const string expected = "{\"\\r\":\"CR\",\"1\":\"One\",\"\u0080\":\"Control\",\"ö\":\"Latin\",\"€\":\"Euro\",\"😀\":\"Emoji\",\"דּ\":\"Hebrew\"}";
        Assert.That(Encoding.UTF8.GetString(CwmJcs.Canonicalize(doc.RootElement)), Is.EqualTo(expected));
        using var escaped = JsonDocument.Parse("{\"s\":\"\\u000f\\b\\t\\n\\f\\r/\\\"\\\\€<>\\u2028\"}");
        Assert.That(Encoding.UTF8.GetString(CwmJcs.Canonicalize(escaped.RootElement)),
            Is.EqualTo("{\"s\":\"\\u000f\\b\\t\\n\\f\\r/\\\"\\\\€<>\u2028\"}"));
    }
    [Test] public void IndependentNodeReferenceContainerAndJcsVectors()
    {
        string fixtures = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "F04");
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "reference.json")));
        var root = reference.RootElement;
        Inputs("HMAC-SHA256");
        File.WriteAllBytes(source.Value, Convert.FromHexString(root.GetProperty("sourceHex").GetString()!));
        var container = new PathVariableDeclaration { Value = source.Value + ".cwm" };
        File.WriteAllText(container.Value, root.GetProperty("container").GetRawText());
        Assert.That(Verify(container), Is.EqualTo("true"));
        var generated = Read(Pack());
        Assert.That(generated["metadataAuth"]!["mac"]!.GetValue<string>(),
            Is.EqualTo(root.GetProperty("container").GetProperty("metadataAuth").GetProperty("mac").GetString()));
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "jcs-vectors.json")));
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            using var input = JsonDocument.Parse(vector.GetProperty("input").GetString()!);
            Assert.That(Encoding.UTF8.GetString(CwmJcs.Canonicalize(input.RootElement)),
                Is.EqualTo(vector.GetProperty("expected").GetString()));
        }
    }
    [TestCase("AES-CBC-MAC", "ISO-9797-M1", 8)]
    [TestCase("AES-CBC-MAC", "ISO-9797-M2", 12)]
    [TestCase("AES-CBC-MAC", "ISO-9797-M3", 16)]
    [TestCase("AES-CBC-MAC", "NONE", 16)]
    [TestCase("AES-CBC-MAC", "ANSI-X923", 9)]
    [TestCase("DES3-CBC", "ISO-9797-M3", 4)]
    [TestCase("DES3-CBC", "TLS-CBC", 6)]
    [TestCase("DES3-RETAIL", "ISO-9797-M1", 4)]
    public void EffectiveParametersRoundtripAndMismatch(string mechanism, string padding, int length)
    {
        Inputs(mechanism); p.SetParameter("PAD", padding); p.SetParameter("MACLEN", length.ToString());
        File.WriteAllBytes(source.Value, new byte[16]); var container = Pack();
        Assert.That(Verify(container), Is.EqualTo("true"));
        var stored = Read(container)["fileMac"]!["parameters"]!;
        Assert.That(stored["padding"]!.GetValue<string>(), Is.EqualTo(padding));
        Assert.That(stored["macLength"]!.GetValue<int>(), Is.EqualTo(length));
        p.SetParameter("PAD", padding == "ISO-9797-M1" ? "ISO-9797-M2" : "ISO-9797-M1");
        Assert.Catch(() => Verify(container));
    }
    [Test] public void GmacNonceMustMatchParam()
    {
        Inputs("AES-GMAC"); File.WriteAllBytes(source.Value, [1]); var container = Pack();
        Assert.That(Read(container)["fileMac"]!["parameters"]!["nonce"]!.GetValue<string>(), Is.EqualTo("000102030405060708090A0B"));
        p.SetParameter("NONCE", "0x(010102030405060708090A0B)");
        Assert.Catch(() => Verify(container));
    }
    [Test] public void LargeFileAllocationsRemainBounded()
    {
        Inputs("HMAC-SHA256");
        long Measure(long size)
        {
            using (var file = File.Create(source.Value)) file.SetLength(size);
            long start = GC.GetAllocatedBytesForCurrentThread();
            var container = Pack(); Assert.That(Verify(container), Is.EqualTo("true"));
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }
        Measure(65536); long small = Measure(1024 * 1024), large = Measure(64L * 1024 * 1024);
        Assert.That(large, Is.LessThan(small + 1024 * 1024));
    }
    [TestCase("9007199254740991", "9007199254740991")]
    [TestCase("-9007199254740991", "-9007199254740991")]
    [TestCase("-0", "0")]
    public void JcsSafeIntegerReferenceVectors(string input, string expected)
    {
        using var doc = JsonDocument.Parse(input);
        Assert.That(Encoding.UTF8.GetString(CwmJcs.Canonicalize(doc.RootElement)), Is.EqualTo(expected));
    }

    [TestCase("mechanism")] [TestCase("parametersString")] [TestCase("unknownParameter")]
    [TestCase("shortTag")] [TestCase("longTag")] [TestCase("padding")]
    [TestCase("macLength")] [TestCase("macLengthType")] [TestCase("iv")]
    [TestCase("nonce")] [TestCase("nonceMissing")] [TestCase("retailPadding")]
    public void InvalidStoredSchemaFailsBeforeAuthenticationAndSourceOpening(string defect)
    {
        foreach (bool authenticated in new[] { false, true })
        {
            Inputs(defect.StartsWith("nonce") ? "AES-GMAC" : defect == "retailPadding" ? "DES3-RETAIL" : "AES-CBC-MAC");
            File.WriteAllBytes(source.Value, new byte[16]);
            var container = Pack(); var doc = Read(container); var fm = doc["fileMac"]!;
            switch (defect)
            {
                case "mechanism": fm["mechanism"] = "NOT-A-MECHANISM"; break;
                case "parametersString": fm["parameters"] = "oops"; break;
                case "unknownParameter": fm["parameters"]!["extra"] = "x"; break;
                case "shortTag": fm["mac"] = "00"; break;
                case "longTag": fm["mac"] = new string('0', 128); break;
                case "padding": fm["parameters"]!["padding"] = "pkcs-7"; break;
                case "retailPadding": fm["parameters"]!["padding"] = "NONE"; break;
                case "macLength": fm["parameters"]!["macLength"] = 7; break;
                case "macLengthType": fm["parameters"]!["macLength"] = "16"; break;
                case "iv": fm["parameters"]!["iv"] = new string('1', 32); break;
                case "nonce": fm["parameters"]!["nonce"] = "ab"; break;
                case "nonceMissing": fm["parameters"] = new JsonObject(); break;
            }
            if (authenticated) Sign(doc);
            Write(container, doc);
            File.Delete(source.Value); // Any source opening would now fail with IOException instead.
            byte[] before = File.ReadAllBytes(container.Value);
            string[] entries = Directory.GetFileSystemEntries(directory);
            Assert.Throws<FormatException>(() => Verify(container), defect);
            Assert.That(File.ReadAllBytes(container.Value), Is.EqualTo(before));
            Assert.That(Directory.GetFileSystemEntries(directory), Is.EquivalentTo(entries));
            Assert.That(File.Exists(source.Value), Is.False);
        }
    }

    [TestCase("AES-CMAC", 16)] [TestCase("DES3-CMAC", 8)] [TestCase("AES-GMAC", 16)]
    [TestCase("AES-CBC-MAC", 16)] [TestCase("DES3-CBC", 8)] [TestCase("DES3-RETAIL", 8)]
    [TestCase("HMAC-SHA1", 20)] [TestCase("HMAC-SHA224", 28)] [TestCase("HMAC-SHA256", 32)]
    [TestCase("HMAC-SHA384", 48)] [TestCase("HMAC-SHA512", 64)]
    [TestCase("HMAC-SHA512-224", 28)] [TestCase("HMAC-SHA512-256", 32)]
    [TestCase("HMAC-SHA3-224", 28)] [TestCase("HMAC-SHA3-256", 32)]
    [TestCase("HMAC-SHA3-384", 48)] [TestCase("HMAC-SHA3-512", 64)]
    public void StoredTagLengthIsCheckedForEveryMechanism(string mechanism, int length)
    {
        Inputs(mechanism); File.WriteAllBytes(source.Value, [1]); var container = Pack();
        var doc = Read(container);
        Assert.That(doc["fileMac"]!["mac"]!.GetValue<string>().Length, Is.EqualTo(length * 2));
        doc["fileMac"]!["mac"] = new string('0', (length - 1) * 2);
        Write(container, doc); File.Delete(source.Value);
        Assert.Throws<FormatException>(() => Verify(container));
    }

    [TestCase(false)] [TestCase(true)]
    public void WellFormedDifferentMechanismIsComparedToCallerOnlyAfterAuthentication(bool authenticated)
    {
        File.WriteAllBytes(source.Value, [1]); var container = Pack(); var doc = Read(container);
        doc["fileMac"]!["mechanism"] = "HMAC-SHA256";
        doc["fileMac"]!["mac"] = new string('0', 64);
        if (authenticated) Sign(doc);
        Write(container, doc); File.Delete(source.Value);
        if (authenticated) Assert.Throws<ArgumentException>(() => Verify(container));
        else Assert.That(Verify(container), Is.EqualTo("false"));
    }

    [TestCase("\"junk\"")] [TestCase("\"true\"")] [TestCase("\"false\"")]
    [TestCase("0x(AA)")] [TestCase("b64(YQ==)")] [TestCase("123")]
    [TestCase("C:\\input.bin")] [TestCase("")]
    public void ValRejectsLiteralInitializersWithoutReplacingExistingValue(string literal)
    {
        var old = new ValVariableDeclaration(true) { Id = "valid" };
        VariableDictionary.Instance().Add(old);
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse("VAL valid = " + literal)));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.InvalidDeclaration));
        Assert.That(VariableDictionary.Instance().Get("valid"), Is.SameAs(old));
    }

    [Test] public void ValModelCannotBeForgedOrMutatedOutsideBooleanDomain()
    {
        VariableDeclaration value = new ValVariableDeclaration(true);
        foreach (string invalid in new[] { "", "True", "FALSE", "1", "\"true\"", "junk" })
        {
            Assert.Throws<ArgumentException>(() => value.Value = invalid);
            Assert.That(value.Value, Is.EqualTo("true"));
        }
        value.Value = "false"; Assert.That(value.Value, Is.EqualTo("false"));
        Assert.Throws<ArgumentException>(() => value.Type = new CryptoTypeVar());
        Assert.Throws<ArgumentException>(() => new StringVariableDeclaration { Type = new CryptoTypeVal() });
        Assert.Catch(() => new CryptoScript.Serialization.VariableSnapshotJsonSerializer().Serialize(new[] { value }));
    }

    [Test] public async Task ValAsyncResultsAndInvalidDeclarationsUseSameContract()
    {
        File.WriteAllBytes(source.Value, [1]);
        var runner = new CryptoScriptRunner();
        await runner.ExecuteAsync(Parse("PATH container = MacPack(p,k,source)\nVAL valid = MacVerify(p,k,container)"));
        Assert.That(VariableDictionary.Instance().Get("valid"), Is.TypeOf<ValVariableDeclaration>());
        File.WriteAllBytes(source.Value, [2]);
        await runner.ExecuteAsync(Parse("VAL valid = MacVerify(p,k,container)"));
        Assert.That(VariableDictionary.Instance().Get("valid").Value, Is.EqualTo("false"));
        Assert.ThrowsAsync<SemanticErrorException>(async () => await runner.ExecuteAsync(Parse("VAL valid = 123")));
        Assert.That(VariableDictionary.Instance().Get("valid").Value, Is.EqualTo("false"));
    }

    [TestCase("MacPack")] [TestCase("Macpack")] [TestCase("MacPACK")]
    [TestCase("MacVerify")] [TestCase("Macverify")] [TestCase("MacVERIFY")]
    public void CwmRuntimeDiagnosticsAreRedactedForEveryAcceptedSpelling(string function)
    {
        source.Value = Path.Combine(directory, "private-missing.cwm");
        var runner = new CryptoScriptRunner();
        string type = function.Equals("MacPack", StringComparison.OrdinalIgnoreCase) ? "PATH" : "VAL";
        Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse(type + " result = " + function + "(p,k,source)")));
        var error = runner.Diagnostics.Single(d => d.DiagnosticCode == SemanticDiagnosticCode.FunctionExecutionFailed);
        Assert.That(error.FunctionCall, Is.EqualTo(function + "(<redacted>)"));
        Assert.That(error.FunctionName, Is.EqualTo(function));
        Assert.That(error.Message, Is.EqualTo(function + " failed."));
        Assert.That(error.Location, Is.Not.Null);
        Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
        Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
    }
}
