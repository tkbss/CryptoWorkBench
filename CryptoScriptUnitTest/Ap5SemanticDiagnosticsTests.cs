using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Ap5SemanticDiagnosticsTests
{
    private sealed record Vector(string Mechanism, string Function, string Key, KeyAlgorithm Algorithm,
        KeyUsage Usage, string Data, string Expected, string[] Named, bool SelectsAlgorithm = false,
        string? TransportedKey = null)
    {
        public override string ToString() => Mechanism + "/" + Function + "/" + Key.Length;
    }

    private const string Tdea = "0123456789ABCDEFFEDCBA9876543210";
    private const string Ep2Key = "0123456789ABCDEF23456789ABCDEF01";
    private const string Salt = "0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345";
    private const string Prk = "077709362C2E32DF0DDC3F0DC47BBA6390B6C73BB50F9C3122EC844AD7C2B3E5";
    private const string Okm = "3CB25F25FAACD57A90434F64D0362F2A2D2D0A90CF1A5A4C5DB02D56ECC4C5BF34007208D5B887185865";
    private static string Hex(string value) => "0x(" + value + ")";

    // Frozen published/independent answers from the existing KDF, HMAC, EMV and
    // TR-31 reference fixtures. No expectation is computed with a production core.
    private static IEnumerable<Vector> Vectors()
    {
        yield return new("KDF-HKDF", "Derive", string.Concat(Enumerable.Repeat("0B", 22)),
            KeyAlgorithm.Unknown, KeyUsage.DeriveKey, "0x(F0F1F2F3F4F5F6F7F8F9)", Hex(Okm),
            ["#HASH:HASH-SHA256", "#OUTLEN:336", "#SALT:0x(000102030405060708090A0B0C)"]);
        yield return new("HKDF-EXTRACT", "Derive", string.Concat(Enumerable.Repeat("0B", 22)),
            KeyAlgorithm.Unknown, KeyUsage.DeriveKey, "0x()", Hex(Prk),
            ["#HASH:HASH-SHA256", "#SALT:0x(000102030405060708090A0B0C)"]);
        yield return new("HKDF-EXPAND", "Derive", Prk, KeyAlgorithm.Hmac, KeyUsage.DeriveKey,
            "0x(F0F1F2F3F4F5F6F7F8F9)", Hex(Okm), ["#HASH:HASH-SHA256", "#OUTLEN:336"]);
        yield return new("KDF-SP800-108-COUNTER", "Derive",
            "3EDC6B5B8F7AADBD713732B482B8F979286E1EA3B8F8F99C30C884CFE3349B83",
            KeyAlgorithm.Hmac, KeyUsage.DeriveKey, "\"context\"", "0x(B8E56EF25C9E6D6D97D2170AAA905FAF)",
            ["#PRF:HMAC-SHA256", "#OUTLEN:128", "#LABEL:\"label\""]);
        yield return new("KDF-SP800-108-COUNTER", "Derive", "2B7E151628AED2A6ABF7158809CF4F3C",
            KeyAlgorithm.Aes, KeyUsage.DeriveKey, "0x(0102030405)",
            "0x(64EC377C7A14A3D931E7BFD71840A493BC6012B38EC96C68A048053D0D35705B)",
            ["#PRF:AES-CMAC", "#OUTLEN:256", "#LABEL:\"aes-label\""]);
        yield return new("KDF-EP2-SESSION", "Derive", Ep2Key, KeyAlgorithm.Unknown, KeyUsage.DeriveKey,
            "0x()", "0x(CDA5C89A6B4F073779AB3B882C5CDFEFE756621E2FD4C4AC46C9FFCB9915C5CC)",
            ["#SALT:" + Hex(Salt), "#VARIANT:TC"]);
        yield return new("KDF-EP2-PAN-SURROGATE-TRX", "Derive", Ep2Key, KeyAlgorithm.Hmac, KeyUsage.DeriveKey,
            "0x(5413330089020011)", "0x(AEA780CDFC3CDA67C0FD0D70D509C9B4C1DD1F40F0C05D922BFD3BC8A01E2E6E)", []);
        yield return new("KDF-EP2-PAN-RECEIPT-TRX", "Derive", Ep2Key, KeyAlgorithm.Hmac, KeyUsage.DeriveKey,
            "0x(1322042006015445524D3132333412345678A000000111)", "0x(D37FF3ECC3F2EAC21EF3C968A9DE2934)", []);
        yield return new("KDF-EP2-PAN-RECEIPT-TRM", "Derive", Ep2Key, KeyAlgorithm.Unknown, KeyUsage.DeriveKey,
            "0x(5445524D313233340345ABC56876FF1900BCDEF3FCD35914)", "0x(F9B286738F4A3848C20C7CB12D3886E7)",
            ["#SALT:" + Hex(Salt)]);
        yield return new("DUKPT-AES-INITIAL-KEY", "Derive", "FEDCBA9876543210F1F1F1F1F1F1F1F1",
            KeyAlgorithm.Aes, KeyUsage.DeriveKey, "0x(1234567890123456)", "0x(1273671EA26AC29AFA4D1084127652A1)", []);
        yield return new("DUKPT-AES-WORKING-KEY", "Derive", "1273671EA26AC29AFA4D1084127652A1",
            KeyAlgorithm.Aes, KeyUsage.DeriveKey, "0x(123456789012345600000001)", "0x(AF8CB133A78F8DC2D1359F18527593FB)",
            ["#USAGE:PIN", "#KEYTYPE:AES-128"]);
        yield return new("DUKPT-TDEA-INITIAL-KEY", "Derive", Tdea, KeyAlgorithm.Tdea, KeyUsage.DeriveKey,
            "0x(FFFF9876543210E00000)", "0x(6AC292FAA1315B4D858AB3A3D7D5933A)", []);
        yield return new("DUKPT-TDEA-WORKING-KEY", "Derive", "6AC292FAA1315B4D858AB3A3D7D5933A",
            KeyAlgorithm.Tdea, KeyUsage.DeriveKey, "0x(FFFF9876543210E00001)", "0x(042666B49184CF5C68DE9628D0397B36)", ["#USAGE:PIN"]);
        yield return new("KDF-EMV-MASTER-A", "Derive", Tdea, KeyAlgorithm.Tdea, KeyUsage.DeriveKey,
            "\"99012345678901234\"", "0x(67F8292358083E5EA7AB7FDA58D53B6B)", ["#PSN:\"45\""]);
        yield return new("KDF-EMV-MASTER-B", "Derive", Tdea, KeyAlgorithm.Tdea, KeyUsage.DeriveKey,
            "\"99012345678901234\"", "0x(985EC4FD3EDF6162E31AF1C7D0543416)", ["#PSN:\"45\""]);
        yield return new("KDF-EMV-MASTER-C", "Derive", "2EF6E07ECBA86BCF3C3CFF7BBEBE6F38",
            KeyAlgorithm.Aes, KeyUsage.DeriveKey, "\"99012345678901234\"", "0x(35C006583E2B417207AE5B166C0EA6EF)", ["#PSN:\"45\""]);
        foreach (var h in SymmetricAlgoHmacTests.KnownAnswerVectors)
            yield return new(h.Mechanism, "Mac", h.Key, KeyAlgorithm.Hmac, KeyUsage.MacGenerate, h.Data, Hex(h.Expected), []);
        foreach (var v in EmvArpcFixtures.All)
        {
            var a = v.Cipher == "DES3-ECB" ? KeyAlgorithm.Tdea : KeyAlgorithm.Aes;
            yield return new("KDF-EMV-AC-SESSION", "Derive", v.Master, a, KeyUsage.DeriveKey, "0x(0001)", Hex(v.Key), [], true);
            yield return new("MAC-EMV-AC", "Mac", v.Key, a, KeyUsage.MacGenerate, Hex(EmvAcFixtures.Data), Hex(v.Arqc), [], true);
            yield return new("MAC-EMV-AC-VERIFY", "Mac", v.Key, a, KeyUsage.MacVerify, Hex(EmvAcFixtures.Data),
                "0x(01)", ["#EXPECTED:" + Hex(v.Arqc)], true);
            yield return new("MAC-EMV-ARPC-METHOD-1", "Mac", v.Key, a, KeyUsage.MacGenerate, Hex(v.Arqc),
                Hex(v.M1), ["#ARC:0x(3030)"], true);
            yield return new("MAC-EMV-ARPC-METHOD-2", "Mac", v.Key, a, KeyUsage.MacGenerate, Hex(v.Arqc),
                Hex(v.M2[0]), ["#CSU:0x(00000000)"], true);
        }
        foreach (var v in Tr31ReferenceVectors.All)
        {
            string composite = "\"" + v.Header + "\"" + Hex(v.Ciphertext) + Hex(v.Mac);
            yield return new("WRAP-DES3-TR31", "Wrap", v.Kbpk, KeyAlgorithm.Tdea, KeyUsage.WrapKey,
                Hex(v.Key), composite, ["#BLKH:\"" + v.Header + "\"", "#RND:" + Hex(v.ObfuscationPadding + v.CipherBlockPadding)],
                TransportedKey: v.Key);
            yield return new("WRAP-DES3-TR31", "Unwrap", v.Kbpk, KeyAlgorithm.Tdea, KeyUsage.UnwrapKey,
                "\"" + v.CompleteBlock + "\"", Hex(v.Key), []);
        }
        const string kbpk = "88E1AB2A2E3DD38C1FA039A536500CC8A87AB9D62DC92C01058FA79F44657DE6";
        const string key = "3F419E1CB7079442AA37474C2EFBF8B8";
        const string header = "D0144P0AE00E0000";
        const string cipher = "2C77FA3F4A553BED6E88AE5C172A4166E3D4ACA8E2AC71C158A476FAC12C13C3829DE55D3AB54C48F4C4FEF7AC75E90F";
        const string mac = "C47F1B77E7B19A73ED46E64410082557";
        yield return new("WRAP-AES-TR31", "Wrap", kbpk, KeyAlgorithm.Aes, KeyUsage.WrapKey, Hex(key),
            "\"" + header + "\"" + Hex(cipher) + Hex(mac),
            ["#BLKH:\"" + header + "\"", "#RND:0x(1A87BBFA2CFE78D383E5F4C6AA83473C1C2965473CE206BB855B01533782)"],
            TransportedKey: key);
        yield return new("WRAP-AES-TR31", "Unwrap", kbpk, KeyAlgorithm.Aes, KeyUsage.UnwrapKey,
            "\"" + header + cipher + mac + "\"", Hex(key), []);
    }

    public static IEnumerable<TestCaseData> Cases()
    {
        int index = 0;
        foreach (var vector in Vectors())
        {
            foreach (int state in vector.SelectsAlgorithm ? new[] { 0, 2, 5 } : new[] { 0, 1, 2, 3, 4 })
                yield return new TestCaseData(vector, state).SetName($"AP5_{++index}_{vector}_{state}");
        }
    }

    [SetUp]
    public void Setup() => EmvAcFixtures.Reset();

    private static KeyVariableDeclaration Key(string id, string hex, KeyAlgorithm algorithm) => new()
    {
        Id = id, Value = Hex(hex), KeyValue = Hex(hex), ValueFormat = FormatConversions.HEX,
        KeySizeInBits = new KeySize(hex.Length * 4), KeyType = KeyType.Secret(algorithm), Type = new CryptoTypeKey()
    };

    private static (ParameterVariableDeclaration P, KeyVariableDeclaration K, VariableDeclaration Data) Inputs(Vector v, int state)
    {
        var p = AlgorithmFactory.Create(v.Mechanism).GenerateParameters(v.Mechanism, v.Named);
        if (v.Mechanism.StartsWith("WRAP-", StringComparison.Ordinal))
        {
            p.Mechanism = v.Mechanism;
            foreach (string named in v.Named) p.SetParameter(named);
        }
        p.Id = "p"; p.Type = new CryptoTypeParameters();
        KeyAlgorithm algorithm = state == 4 ? KeyAlgorithm.Unknown :
            state is 1 or 3 ? (v.Algorithm == KeyAlgorithm.Aes ? KeyAlgorithm.Tdea : KeyAlgorithm.Aes) : v.Algorithm;
        var k = Key("k", v.Key, algorithm);
        if (state is 2 or 3) k.Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt);
        else if (state != 4 && state != 5) k.Usage = KeyUsagePolicy.Restricted(v.Usage);
        VariableDeclaration data = v.TransportedKey is null
            ? new StringVariableDeclaration { Id = "data", Value = v.Data, ValueFormat = FormatConversions.ParseString(v.Data), Type = new CryptoTypeVar() }
            : Key("data", v.TransportedKey, v.Algorithm);
        // Transporting key material does not require the KBPK's WrapKey usage.
        if (data is KeyVariableDeclaration transported)
            transported.Usage = KeyUsagePolicy.Restricted(KeyUsage.MacVerify);
        VariableDictionary.Instance().Add(p);
        VariableDictionary.Instance().Add(k);
        VariableDictionary.Instance().Add(data);
        return (p, k, data);
    }

    private static AlgorithmCallArguments Arguments(ParameterVariableDeclaration p, KeyVariableDeclaration k,
        VariableDeclaration data, ISemanticDiagnosticSink? sink) => new(new[]
        {
            new AlgorithmCallArgument(p.Value, p, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(k.Value, k, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(data.Value, data, ResolvedCallArgumentKind.Variable)
        }, sink);

    private static VariableDeclaration Direct(Vector v, AlgorithmCallArguments args) => v.Function switch
    {
        "Derive" => AlgorithmFactory.Create(v.Mechanism).Derive(args),
        "Mac" => AlgorithmFactory.Create(v.Mechanism).Mac(args),
        "Wrap" => AlgorithmFactory.Create(v.Mechanism).Wrap(args),
        _ => AlgorithmFactory.Create(v.Mechanism).Unwrap(args)
    };

    private static IEnumerable<SemanticDiagnosticCode> Metadata(IEnumerable<SemanticError> errors) =>
        errors.Where(d => d.Type == "KeyMetadata").Select(d => d.DiagnosticCode);

    private static SemanticDiagnosticCode[] ExpectedCodes(Vector v, int state)
    {
        var codes = new List<SemanticDiagnosticCode>();
        if (!v.SelectsAlgorithm && v.Algorithm != KeyAlgorithm.Unknown && state is 1 or 3)
            codes.Add(SemanticDiagnosticCode.KeyTypeConflict);
        if (state is 2 or 3) codes.Add(SemanticDiagnosticCode.KeyUsageConflict);
        return codes.ToArray();
    }

    [TestCaseSource(nameof(Cases))]
    public void FixedAnswersMetadataOrderingContextsIdentityAndNoMutation(object input, int state)
    {
        var v = (Vector)input;
        var (p, k, data) = Inputs(v, state);
        string keyBefore = k.Serialize(), parameterBefore = p.Value, dataBefore = data.Value;
        var sink = new SemanticDiagnosticSink();
        var args = Arguments(p, k, data, sink);
        Assert.That(Direct(v, args).Value, Is.EqualTo(v.Expected).IgnoreCase);
        Assert.That(Metadata(sink.GetSnapshot()), Is.EqualTo(ExpectedCodes(v, state)));
        Assert.That(sink.GetSnapshot().GroupBy(d => (d.DiagnosticCode, d.Identifier)).All(g => g.Count() == 1), Is.True);
        Assert.That(sink.GetSnapshot().Select(d => d.Severity), Is.All.EqualTo(SemanticDiagnosticSeverity.Warning));
        Assert.That(Direct(v, Arguments(p, k, data, null)).Value, Is.EqualTo(v.Expected).IgnoreCase);

        var invocationSink = new SemanticDiagnosticSink();
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument(p.Value, ResolvedCallArgumentKind.Variable, p),
            new ResolvedCallArgument(k.Value, ResolvedCallArgumentKind.Variable, k),
            new ResolvedCallArgument(data.Value, ResolvedCallArgumentKind.Variable, data)
        }, invocationSink);
        Assert.That(OperationFactory.CreateInvocationOperation(v.Function)(invocation).Value, Is.EqualTo(v.Expected).IgnoreCase);
        Assert.That(Metadata(invocationSink.GetSnapshot()), Is.EqualTo(ExpectedCodes(v, state)));

        var runner = new CryptoScriptRunner();
        string resultType = v.Function is "Derive" or "Unwrap" ? "KEY" : "VAR";
        var parser = ParserBuilder.StringBuild($"{resultType} output={v.Function}(p,k,data) VAR after=0x(01)");
        runner.Execute(parser.program());
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(VariableDictionary.Instance().Get("output").Value, Is.EqualTo(v.Expected).IgnoreCase);
        Assert.That(Metadata(runner.Diagnostics), Is.EqualTo(ExpectedCodes(v, state)));
        Assert.That(runner.Diagnostics.Select(d => d.Location!.ExecutionId).Distinct().Count(), Is.LessThanOrEqualTo(1));
        foreach (var d in runner.Diagnostics)
        {
            Assert.That(d.Location!.OperationId, Is.EqualTo(1));
            Assert.That(d.Location.ParentOperationId, Is.Null);
            Assert.That(d.Location.Mechanism, Is.EqualTo(v.Mechanism));
            Assert.That(d.Location.Identifier, Is.EqualTo("k"));
            Assert.That(d.Message, Does.Not.Contain(k.KeyValue));
        }

        string[] legacy = [p.Value, k.Value, data.Value];
        var algorithm = AlgorithmFactory.Create(v.Mechanism);
        if (v.Function == "Mac" && v.SelectsAlgorithm)
            Assert.Throws<ArgumentException>(() => algorithm.Mac(legacy));
        else
        {
            VariableDeclaration legacyResult = v.Function switch
            {
                "Derive" => algorithm.Derive(legacy), "Mac" => algorithm.Mac(legacy),
                "Wrap" => algorithm.Wrap(legacy), _ => algorithm.Unwrap(legacy)
            };
            Assert.That(legacyResult.Value, Is.EqualTo(v.Expected).IgnoreCase);
        }

        // An unrelated object with identical bytes must not affect the selected source.
        var other = Key("other", v.Key, KeyAlgorithm.Hmac);
        other.Usage = KeyUsagePolicy.Restricted(KeyUsage.Decrypt);
        VariableDictionary.Instance().Add(other);
        var identitySink = new SemanticDiagnosticSink();
        Assert.That(Direct(v, Arguments(p, k, data, identitySink)).Value, Is.EqualTo(v.Expected).IgnoreCase);
        Assert.That(Metadata(identitySink.GetSnapshot()), Is.EqualTo(ExpectedCodes(v, state)));
        Assert.That(identitySink.GetSnapshot().Select(d => d.Identifier), Is.All.EqualTo("k"));
        Assert.That(k.Serialize(), Is.EqualTo(keyBefore));
        Assert.That(p.Value, Is.EqualTo(parameterBefore));
        Assert.That(data.Value, Is.EqualTo(dataBefore));
    }

    [TestCase("WRAP-AES-TR31")]
    [TestCase("WRAP-DES3-TR31")]
    public void EqualKbpkAndTransportBytesUseOriginalReferencesAndRejectAmbiguousLegacy(string mechanism)
    {
        var p = new ParameterVariableDeclaration { Id = "p", Mechanism = mechanism };
        p.SetParameter("BLKH", mechanism == "WRAP-AES-TR31" ? "\"D0144D0AB00E0000\"" : "\"B0096D0TB00E0000\"");
        p.SetParameter("RND", "0x(0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E)");
        var a = mechanism == "WRAP-AES-TR31" ? KeyAlgorithm.Aes : KeyAlgorithm.Tdea;
        var kbpk = Key("kbpk", Tdea, a);
        kbpk.Usage = KeyUsagePolicy.Restricted(KeyUsage.WrapKey);
        var transported = Key("transported", Tdea, a);
        transported.Usage = KeyUsagePolicy.Restricted(KeyUsage.MacVerify);
        var decoy = Key("decoy", Tdea, KeyAlgorithm.Hmac);
        decoy.Usage = KeyUsagePolicy.Restricted(KeyUsage.Decrypt);
        foreach (var item in new[] { decoy, kbpk, transported }) VariableDictionary.Instance().Add(item);
        var sink = new SemanticDiagnosticSink();
        var algorithm = AlgorithmFactory.Create(mechanism);
        algorithm.Wrap(Arguments(p, kbpk, transported, sink));
        Assert.That(Metadata(sink.GetSnapshot()), Is.Empty);
        Assert.Throws<ArgumentException>(() => algorithm.Wrap(new[] { p.Value, kbpk.Value, transported.Value }));
        Assert.DoesNotThrow(() => algorithm.Wrap(new[] { p.Value, "kbpk", "transported" }));
    }

    [Test]
    public void NestedDerivationsRetainDistinctOperationContexts()
    {
        var v = Vectors().Single(v => v.Mechanism == "HMAC-SHA256");
        var (p, k, _) = Inputs(v, 3);
        var runner = new CryptoScriptRunner();
        runner.Execute(ParserBuilder.StringBuild("VAR output=Mac(p,k,Mac(p,k,data))").program());
        Assert.That(Metadata(runner.Diagnostics), Is.EqualTo(new[]
            { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict,
              SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict }));
        Assert.That(runner.Diagnostics.Select(d => d.Location!.OperationId), Is.EqualTo(new[] { 2, 2, 1, 1 }));
        Assert.That(runner.Diagnostics.Select(d => d.Location!.ParentOperationId), Is.EqualTo(new int?[] { 1, 1, null, null }));
    }

    public static IEnumerable<TestCaseData> FailureCases()
    {
        foreach (var v in Vectors())
            yield return new TestCaseData(v).SetName("AP5_failure_" + v);
    }

    [TestCaseSource(nameof(FailureCases))]
    public void WarningsSurviveSubsequentTechnicalFailureInDirectAndRunnerCalls(object input)
    {
        var v = (Vector)input;
        var (p, k, data) = Inputs(v, 2);
        // Leave metadata valid for algorithm selection and break an input decoded
        // after metadata diagnosis. HMAC likewise rejects malformed data bytes.
        data.Value = v.Function == "Wrap" ? "0x(00)" : "0x(GG)";
        data.ValueFormat = FormatConversions.HEX;
        if (data is KeyVariableDeclaration transported)
        {
            transported.KeyValue = data.Value;
            transported.KeySize = "128";
        }
        var sink = new SemanticDiagnosticSink();
        Assert.That(() => Direct(v, Arguments(p, k, data, sink)), Throws.Exception);
        Assert.That(Metadata(sink.GetSnapshot()), Is.EqualTo(new[] { SemanticDiagnosticCode.KeyUsageConflict }));
        var runner = new CryptoScriptRunner();
        string resultType = v.Function is "Derive" or "Unwrap" ? "KEY" : "VAR";
        Assert.Throws<SemanticErrorException>(() => runner.Execute(ParserBuilder.StringBuild(
            $"{resultType} failed={v.Function}(p,k,data) VAR after=0x(01)").program()));
        Assert.That(Metadata(runner.Diagnostics), Is.EqualTo(new[] { SemanticDiagnosticCode.KeyUsageConflict }));
        Assert.That(runner.Diagnostics[^1].Severity, Is.EqualTo(SemanticDiagnosticSeverity.Error));
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
    }

    public static IEnumerable<TestCaseData> AuthenticatedBlocks() =>
        Vectors().Where(v => v.Function == "Unwrap").Select((v, i) => new TestCaseData(v).SetName("AP5_auth_" + i + "_" + v));

    [TestCaseSource(nameof(AuthenticatedBlocks))]
    public void AuthenticationFailureRemainsBlockingAfterBothMetadataWarnings(object input)
    {
        var v = (Vector)input;
        var (p, k, data) = Inputs(v, 3);
        char last = data.Value[^2];
        data.Value = data.Value[..^2] + (last == '0' ? "1" : "0") + "\"";
        string before = data.Value;
        var sink = new SemanticDiagnosticSink();
        Assert.That(() => Direct(v, Arguments(p, k, data, sink)), Throws.Exception);
        Assert.That(Metadata(sink.GetSnapshot()), Is.EqualTo(new[]
            { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict }));
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(ParserBuilder.StringBuild(
            "KEY failed=Unwrap(p,k,data) VAR after=0x(01)").program()));
        Assert.That(Metadata(runner.Diagnostics), Is.EqualTo(new[]
            { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict }));
        Assert.That(VariableDictionary.Instance().Contains("failed"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        Assert.That(data.Value, Is.EqualTo(before));
    }

    public static IEnumerable<TestCaseData> StrictWeakKeyCores() =>
        Vectors().Where(v => v.Algorithm == KeyAlgorithm.Tdea &&
            (v.Mechanism.StartsWith("KDF-EMV-", StringComparison.Ordinal) ||
             v.Mechanism.StartsWith("MAC-EMV-", StringComparison.Ordinal) ||
             v.Mechanism == "WRAP-DES3-TR31"))
            .Select((v, i) => new TestCaseData(v).SetName("AP5_weak_core_" + i + "_" + v));

    [TestCaseSource(nameof(StrictWeakKeyCores))]
    public void AnalysisAdaptersReportQualityAndExecuteOrReachAuthentication(object input)
    {
        var v = (Vector)input;
        var (p, k, data) = Inputs(v, 0);
        k.Value = k.KeyValue = Hex(new string('0', v.Key.Length));
        string before = k.Serialize();
        var sink = new SemanticDiagnosticSink();
        if (v.Function == "Unwrap" || v.Mechanism == "MAC-EMV-AC-VERIFY")
            // The fixed expected cryptogram belongs to the original key, not the replaced zero key.
            Assert.That(() => Direct(v, Arguments(p, k, data, sink)), Throws.Exception);
        else
            Assert.That(Direct(v, Arguments(p, k, data, sink)).Value, Is.Not.Empty);
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(new[]
            { SemanticDiagnosticCode.DesParityViolation, SemanticDiagnosticCode.WeakDes3Key }));
        Assert.That(k.Serialize(), Is.EqualTo(before));
    }

    [Test]
    public void InitialTdeaDukptUsesTheExistingLocalAnalysisPrimitive()
    {
        var v = Vectors().Single(v => v.Mechanism == "DUKPT-TDEA-INITIAL-KEY");
        var (p, k, data) = Inputs(v, 0);
        // Each component is a known weak DES key, while K1 differs from K2.
        k.Value = k.KeyValue = "0x(0101010101010101FEFEFEFEFEFEFEFE)";
        string before = k.Serialize();
        var sink = new SemanticDiagnosticSink();
        Assert.That(Direct(v, Arguments(p, k, data, sink)), Is.TypeOf<KeyVariableDeclaration>());
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(new[] { SemanticDiagnosticCode.WeakDes3Key }));
        Assert.That(k.Serialize(), Is.EqualTo(before));
    }

    [Test]
    public void DegenerateInitialTdeaDukptMatchesIndependentSingleDesAnswerWithAndWithoutSink()
    {
        var v = Vectors().Single(v => v.Mechanism == "DUKPT-TDEA-INITIAL-KEY");
        var (p, k, data) = Inputs(v, 3);
        k.Value = k.KeyValue = "0x(0123456789ABCDEF0123456789ABCDEF)";
        string before = k.Serialize(), parameterBefore = p.Value, dataBefore = data.Value;
        // Independently calculated with .NET DES.EncryptEcb, not the production
        // Bouncy Castle EDE fallback: K1=K2 reduces EDE to single DES.
        // Input FFFF9876543210E0; keys 0123456789ABCDEF and C1E385A789ABCDEF
        // (second key is XOR C0C0C0C000000000).
        const string expected = "0x(7140297ECB0DD8F1D6D854E305FB4129)";
        var sink = new SemanticDiagnosticSink();
        Assert.That(Direct(v, Arguments(p, k, data, sink)).Value, Is.EqualTo(expected).IgnoreCase);
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(new[]
            { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict, SemanticDiagnosticCode.WeakDes3Key }));
        Assert.That(Direct(v, Arguments(p, k, data, null)).Value, Is.EqualTo(expected).IgnoreCase);
        var runner = new CryptoScriptRunner();
        runner.Execute(ParserBuilder.StringBuild("KEY initial=Derive(p,k,data)").program());
        Assert.That(VariableDictionary.Instance().Get("initial").Value, Is.EqualTo(expected).IgnoreCase);
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Is.EqualTo(new[]
            { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict, SemanticDiagnosticCode.WeakDes3Key }));
        Assert.That(AlgorithmFactory.Create(v.Mechanism).Derive(new[] { p.Value, k.Value, data.Value }).Value,
            Is.EqualTo(expected).IgnoreCase);
        Assert.That(k.Serialize(), Is.EqualTo(before));
        Assert.That(p.Value, Is.EqualTo(parameterBefore));
        Assert.That(data.Value, Is.EqualTo(dataBefore));
    }

    [Test]
    public void ArqcMismatchRemainsFailClosedAfterUsageWarning()
    {
        var v = Vectors().First(v => v.Mechanism == "MAC-EMV-AC-VERIFY" && v.Algorithm == KeyAlgorithm.Aes);
        var (p, k, _) = Inputs(v, 2);
        p.SetParameter("EXPECTED", "0x(53A78E1517C22F70)");
        var runner = new CryptoScriptRunner();
        var error = Assert.Throws<SemanticErrorException>(() => runner.Execute(ParserBuilder.StringBuild(
            "VAR failed=Mac(p,k,data) VAR after=0x(01)").program()));
        Assert.That(error!.SemanticError.Message, Is.EqualTo("EMV Application Cryptogram verification failed."));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Is.EqualTo(new[]
            { SemanticDiagnosticCode.KeyUsageConflict, SemanticDiagnosticCode.FunctionExecutionFailed }));
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
    }
}
