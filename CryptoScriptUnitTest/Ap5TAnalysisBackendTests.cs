using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.CryptoAlgorithm.EMV;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using Des3Algorithm = CryptoScript.CryptoAlgorithm.DES3.DES3;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Ap5TAnalysisBackendTests
{
    private sealed record Vector(Ap5TProfile Profile, string Mechanism, string Function,
        string Input, string Expected, string[] Named, KeyUsage Usage)
    { public override string ToString() => Profile.Name + "/" + Mechanism + "/" + Function + "/" + Named.FirstOrDefault(); }

    private VariableDeclaration[] previous = [];
    [SetUp]
    public void Setup()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }
    [TearDown]
    public void Restore()
    {
        VariableDictionary.Instance().Clear();
        foreach (var v in previous) VariableDictionary.Instance().Add(v);
    }
    private static string Hex(string s) => "0x(" + s + ")";

    private static IEnumerable<Vector> Vectors()
    {
        foreach (var p in Ap5TReferenceVectors.All)
        {
            if (p.Emv is { } e)
            {
                yield return new(p, "KDF-EMV-MASTER-A", "Derive", "\"1234567890123456\"", Hex(e[0]), ["#PSN:\"01\""], KeyUsage.DeriveKey);
                yield return new(p, "KDF-EMV-MASTER-B", "Derive", "\"1234567890123456789\"", Hex(e[1]), ["#PSN:\"01\""], KeyUsage.DeriveKey);
                yield return new(p, "KDF-EMV-AC-SESSION", "Derive", "0x(0001)", Hex(e[2]), [], KeyUsage.DeriveKey);
                yield return new(p, "MAC-EMV-AC", "Mac", Hex(Ap5TReferenceVectors.Data), Hex(e[3]), [], KeyUsage.MacGenerate);
                yield return new(p, "MAC-EMV-AC-VERIFY", "Mac", Hex(Ap5TReferenceVectors.Data), "0x(01)", ["#EXPECTED:" + Hex(e[3])], KeyUsage.MacVerify);
                yield return new(p, "MAC-EMV-ARPC-METHOD-1", "Mac", Hex(Ap5TReferenceVectors.Arqc), Hex(e[4]), ["#ARC:" + Hex(Ap5TReferenceVectors.Arc)], KeyUsage.MacGenerate);
                yield return new(p, "MAC-EMV-ARPC-METHOD-2", "Mac", Hex(Ap5TReferenceVectors.Arqc), Hex(e[5]), ["#CSU:" + Hex(Ap5TReferenceVectors.Csu), "#ADATA:" + Hex(Ap5TReferenceVectors.Adata)], KeyUsage.MacGenerate);
            }
            foreach (var b in p.Blocks)
            {
                yield return new(p, "WRAP-DES3-TR31", "Wrap", Hex(Ap5TReferenceVectors.Transported), b.Composite,
                    ["#BLKH:\"" + b.Header + "\"", "#RND:" + Hex(Ap5TReferenceVectors.Random)], KeyUsage.WrapKey);
                yield return new(p, "WRAP-DES3-TR31", "Unwrap", '"' + b.Wire + '"', Hex(Ap5TReferenceVectors.Transported),
                    ["#BLKH:\"" + b.Header + "\""], KeyUsage.UnwrapKey);
            }
        }
    }
    public static IEnumerable<TestCaseData> Cases() => Vectors().SelectMany(v => new[] { false, true }
        .Select(conflict => new TestCaseData(v, conflict).SetName("AP5T_" + v + "/conflict=" + conflict)));

    private static KeyVariableDeclaration Key(string id, string material) => new()
    {
        Id = id, Value = Hex(material), ValueFormat = FormatConversions.HEX,
        Type = new CryptoTypeKey(), KeyType = KeyType.Secret(KeyAlgorithm.Tdea), KeySizeInBits = new KeySize(material.Length * 4)
    };
    private static (ParameterVariableDeclaration P, KeyVariableDeclaration K, VariableDeclaration D) Inputs(Vector v, bool conflict)
    {
        var p = AlgorithmFactory.Create(v.Mechanism).GenerateParameters(v.Mechanism, v.Named);
        p.Id = "p";
        if (v.Mechanism == "WRAP-DES3-TR31")
        {
            p.Mechanism = v.Mechanism;
            foreach (string named in v.Named) p.SetParameter(named);
        }
        var k = Key("k", v.Profile.Key);
        if (conflict && (v.Mechanism.Contains("MASTER-") || v.Mechanism.StartsWith("WRAP-")))
            k.KeyType = KeyType.Secret(KeyAlgorithm.Hmac);
        k.Usage = KeyUsagePolicy.Restricted(conflict ? KeyUsage.Encrypt : v.Usage);
        VariableDeclaration d = v.Function == "Wrap" ? Key("data", Ap5TReferenceVectors.Transported)
            : new StringVariableDeclaration { Id = "data", Value = v.Input, ValueFormat = FormatConversions.ParseString(v.Input), Type = new CryptoTypeVar() };
        foreach (var item in new VariableDeclaration[] { p, k, d }) VariableDictionary.Instance().Add(item);
        return (p, k, d);
    }
    private static AlgorithmCallArguments Args(ParameterVariableDeclaration p, KeyVariableDeclaration k,
        VariableDeclaration data, ISemanticDiagnosticSink? sink) => new(new[]
        {
            new AlgorithmCallArgument(p.Value, p, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(k.Value, k, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(data.Value, data, ResolvedCallArgumentKind.Variable)
        }, sink);
    private static VariableDeclaration Direct(Vector v, AlgorithmCallArguments args)
    {
        var a = AlgorithmFactory.Create(v.Mechanism);
        return v.Function switch { "Derive" => a.Derive(args), "Mac" => a.Mac(args), "Wrap" => a.Wrap(args), _ => a.Unwrap(args) };
    }
    private static SemanticDiagnosticCode[] Expected(Vector v, bool conflict)
    {
        var result = new List<SemanticDiagnosticCode>();
        if (conflict && (v.Mechanism.Contains("MASTER-") || v.Mechanism.StartsWith("WRAP-"))) result.Add(SemanticDiagnosticCode.KeyTypeConflict);
        if (conflict) result.Add(SemanticDiagnosticCode.KeyUsageConflict);
        if (v.Profile.Parity) result.Add(SemanticDiagnosticCode.DesParityViolation);
        if (v.Profile.Weak) result.Add(SemanticDiagnosticCode.WeakDes3Key);
        return result.ToArray();
    }
    private static void Check(IEnumerable<SemanticError> errors, Vector v, bool conflict)
    {
        var ds = errors.ToArray();
        Assert.That(ds.Select(d => d.DiagnosticCode), Is.EqualTo(Expected(v, conflict)));
        foreach (var d in ds)
        {
            Assert.That(d.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
            Assert.That(d.Identifier, Is.EqualTo("k"));
            Assert.That(d.FunctionName, Is.EqualTo(v.Function));
            Assert.That(d.Location!.Mechanism, Is.EqualTo(v.Mechanism));
            Assert.That(d.Message, Does.Not.Contain(v.Profile.Key));
        }
    }

    [TestCaseSource(nameof(Cases))]
    public void IndependentAnswersWarningsIdentityAndAllSupportedInvocationForms(object input, bool conflict)
    {
        var v = (Vector)input;
        var (p, k, d) = Inputs(v, conflict);
        string beforeK = k.Serialize(), beforeP = p.Value, beforeD = d is KeyVariableDeclaration dk ? dk.Serialize() : d.Value;
        var sink = new SemanticDiagnosticSink();
        var direct = Direct(v, Args(p, k, d, sink));
        Assert.That(direct.Value, Is.EqualTo(v.Expected).IgnoreCase);
        if (direct is KeyVariableDeclaration outputKey)
        {
            Assert.That(outputKey.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Tdea)));
            Assert.That(outputKey.KeySizeInBits, Is.EqualTo(new KeySize(128)));
            Assert.That(outputKey.Usage, Is.EqualTo(KeyUsagePolicy.Unspecified));
            if (v.Function == "Derive") Assert.That(outputKey.DerivationMechanism, Is.EqualTo(v.Mechanism));
        }
        Check(sink.GetSnapshot(), v, conflict);
        Assert.That(Direct(v, Args(p, k, d, null)).Value, Is.EqualTo(v.Expected).IgnoreCase);
        var invocationSink = new SemanticDiagnosticSink();
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument(p.Value, ResolvedCallArgumentKind.Variable, p),
            new ResolvedCallArgument(k.Value, ResolvedCallArgumentKind.Variable, k),
            new ResolvedCallArgument(d.Value, ResolvedCallArgumentKind.Variable, d)
        }, invocationSink);
        Assert.That(OperationFactory.CreateInvocationOperation(v.Function)(invocation).Value, Is.EqualTo(v.Expected).IgnoreCase);
        Check(invocationSink.GetSnapshot(), v, conflict);
        var a = AlgorithmFactory.Create(v.Mechanism);
        string[] legacy = [p.Value, k.Value, d.Value];
        if (v.Function == "Mac") Assert.Throws<ArgumentException>(() => a.Mac(legacy));
        else
        {
            var result = v.Function == "Derive" ? (VariableDeclaration)a.Derive(legacy)
                : v.Function == "Wrap" ? a.Wrap([p.Value, "k", "data"]) : a.Unwrap([p.Value, "k", "data"]);
            Assert.That(result.Value, Is.EqualTo(v.Expected).IgnoreCase);
        }
        var decoy = Key("decoy", v.Profile.Key);
        VariableDictionary.Instance().Add(decoy);
        var identitySink = new SemanticDiagnosticSink();
        Assert.That(Direct(v, Args(p, k, d, identitySink)).Value, Is.EqualTo(v.Expected).IgnoreCase);
        Check(identitySink.GetSnapshot(), v, conflict);
        var runner = new CryptoScriptRunner();
        string resultType = v.Function is "Derive" or "Unwrap" ? "KEY" : "VAR";
        runner.Execute(ParserBuilder.StringBuild($"{resultType} output={v.Function}(p,k,data) VAR after=0x(01)").program());
        Assert.That(VariableDictionary.Instance().Get("output").Value, Is.EqualTo(v.Expected).IgnoreCase);
        Check(runner.Diagnostics, v, conflict);
        foreach (var diagnostic in runner.Diagnostics)
        {
            Assert.That(diagnostic.Location!.ExecutionId, Is.Not.Null.And.Not.EqualTo(Guid.Empty));
            Assert.That(diagnostic.Location.OperationId, Is.EqualTo(1));
            Assert.That(diagnostic.Location.ParentOperationId, Is.Null);
        }
        Assert.That(k.Serialize(), Is.EqualTo(beforeK));
        Assert.That(p.Value, Is.EqualTo(beforeP));
        Assert.That(d is KeyVariableDeclaration keyData ? keyData.Serialize() : d.Value, Is.EqualTo(beforeD));
    }

    public static IEnumerable<TestCaseData> Bindings() => Ap5TReferenceVectors.All.SelectMany(p => p.Blocks
        .Select(b => new TestCaseData(p, b).SetName("AP5T_Internal_" + p.Name + "/" + b.Header[0])));
    [TestCaseSource(nameof(Bindings))]
    public void IntermediateProtectionKeysMatchIndependentAnswers(object profile, object block)
    {
        var p = (Ap5TProfile)profile; var b = (Ap5TBlock)block;
        byte[] key = Convert.FromHexString(p.Key);
        var (kbek, kbak) = b.Header[0] == 'B' ? Tr31TdeaKeyDerivation.DeriveKeysForAnalysis(key)
            : Tr31TdeaKeyVariants.DeriveKeysForAnalysis(key);
        Assert.That(Convert.ToHexString(kbek), Is.EqualTo(b.Kbek));
        Assert.That(Convert.ToHexString(kbak), Is.EqualTo(b.Kbak));
        Assert.That(Convert.ToHexString(key), Is.EqualTo(p.Key));
    }

    public static IEnumerable<TestCaseData> Failures() => Vectors().Where(v => v.Function == "Unwrap" || v.Mechanism.EndsWith("VERIFY"))
        .Select(v => new TestCaseData(v).SetName("AP5T_FailClosed_" + v));
    [TestCaseSource(nameof(Failures))]
    public void FailedAuthenticationRetainsWarningsAndNeverReachesFollowingStatement(object input)
    {
        var v = (Vector)input;
        var (p, k, d) = Inputs(v, true);
        if (v.Function == "Unwrap") d.Value = d.Value[..^2] + (d.Value[^2] == '0' ? '1' : '0') + '"';
        else p.SetParameter("EXPECTED", "0x(0000000000000000)");
        string keyBefore = k.Serialize(), pBefore = p.Value, dataBefore = d.Value;
        var directSink = new SemanticDiagnosticSink();
        Assert.That(() => Direct(v, Args(p, k, d, directSink)), Throws.Exception);
        Check(directSink.GetSnapshot(), v, true);
        Assert.That(() => Direct(v, Args(p, k, d, null)), Throws.Exception);
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(ParserBuilder.StringBuild(
            $"{(v.Function == "Unwrap" ? "KEY" : "VAR")} failed={v.Function}(p,k,data) VAR after=0x(01)").program()));
        Check(runner.Diagnostics.Where(diagnostic => diagnostic.Severity == SemanticDiagnosticSeverity.Warning), v, true);
        Assert.That(runner.Diagnostics[^1].Message, Does.Contain(v.Function == "Unwrap" ? "authentication failed" : "verification failed"));
        Assert.That(VariableDictionary.Instance().Contains("failed"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        Assert.That(k.Serialize(), Is.EqualTo(keyBefore));
        Assert.That(p.Value, Is.EqualTo(pBefore));
        Assert.That(d.Value, Is.EqualTo(dataBefore));
    }

    [TestCaseSource(nameof(Bindings))]
    public void HeaderTamperingFailsAuthentication(object profile, object block)
    {
        var p = (Ap5TProfile)profile; var b = (Ap5TBlock)block;
        string header = b.Header[..5] + 'D' + b.Header[6..];
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() =>
        {
            if (header[0] == 'B') Tr31VersionBUnwrap.Unwrap(header, Convert.FromHexString(b.Ciphertext), Convert.FromHexString(b.Mac), Convert.FromHexString(p.Key), analysisKeys: true);
            else Tr31TdeaVariantUnwrap.Unwrap(header, Convert.FromHexString(b.Ciphertext), Convert.FromHexString(b.Mac), Convert.FromHexString(p.Key), analysisKeys: true);
        });
    }

    [Test]
    public void StrictApisContinueRejectingDegenerateKeys()
    {
        byte[] k = Convert.FromHexString(Ap5TReferenceVectors.All.Single(p => p.Name == "K1eqK2").Key);
        Assert.Throws<ArgumentException>(() => Des3Algorithm.ValidateUsableKey(k));
        Assert.That(() => DES3_ECB.EncryptNoPadding(k, new byte[8]), Throws.Exception);
        Assert.That(() => DES3_ECB.DecryptNoPadding(k, new byte[8]), Throws.Exception);
        Assert.That(() => DES3_CBC.EncryptNoPadding(k, new byte[8], new byte[8]), Throws.Exception);
        Assert.That(() => DES3_CBC.DecryptNoPadding(k, new byte[8], new byte[8]), Throws.Exception);
        Assert.That(() => DES3_CBC.ComputeMacNoPadding(k, new byte[8], 4), Throws.Exception);
        Assert.Throws<ArgumentException>(() => DES3_RETAIL.ComputeMac(k, [], "ISO-9797-M2", 8));
        Assert.Throws<ArgumentException>(() => Tr31TdeaKeyDerivation.DeriveKeys(k));
        Assert.Throws<ArgumentException>(() => Tr31TdeaKeyVariants.DeriveKeys(k));
        Assert.Throws<ArgumentException>(() => EmvMasterKeyDerivationOptionA.Derive(k, "1234", "00"));
        Assert.Throws<ArgumentException>(() => EmvMasterKeyDerivationOptionB.Derive(k, "1234", "00"));
        Assert.Throws<ArgumentException>(() => EmvAcSessionKeyDerivation.Derive(KeyAlgorithm.Tdea, k, new byte[2]));
        Assert.Throws<ArgumentException>(() => EmvApplicationCryptogram.ComputeAc(KeyAlgorithm.Tdea, k, []));
        var parameter = Des3AlgorithmInstance().GenerateParameters("DES3-CMAC");
        Assert.Throws<ArgumentException>(() => new DES3_CMAC().ModeMac(parameter, Key("strict", Convert.ToHexString(k)),
            new StringVariableDeclaration { Value = "0x()", ValueFormat = FormatConversions.HEX }));
    }
    private static Des3Algorithm Des3AlgorithmInstance() => new();

    [TestCase("PAN")] [TestCase("PSN")] [TestCase("ATC")]
    [TestCase("ARQC")] [TestCase("ARC")] [TestCase("CSU")] [TestCase("ADATA")]
    [TestCase("KEY15")] [TestCase("MASTER24")] [TestCase("SESSION24")] [TestCase("AC24")]
    public void AnalysisStillRejectsTechnicalEmvInputs(string scenario)
    {
        byte[] k = new byte[16];
        Assert.Throws<ArgumentException>(() =>
        {
            switch (scenario)
            {
                case "PAN": EmvMasterKeyDerivationOptionA.DeriveForAnalysis(k, "123A", "00"); break;
                case "PSN": EmvMasterKeyDerivationOptionB.DeriveForAnalysis(k, "1234", "0"); break;
                case "ATC": EmvAcSessionKeyDerivation.DeriveForAnalysis(KeyAlgorithm.Tdea, k, new byte[1]); break;
                case "ARQC": EmvApplicationCryptogram.ComputeArpcMethod1ForAnalysis(KeyAlgorithm.Tdea, k, new byte[7], new byte[2]); break;
                case "ARC": EmvApplicationCryptogram.ComputeArpcMethod1ForAnalysis(KeyAlgorithm.Tdea, k, new byte[8], new byte[3]); break;
                case "CSU": EmvApplicationCryptogram.ComputeArpcMethod2ForAnalysis(KeyAlgorithm.Tdea, k, new byte[8], new byte[3], []); break;
                case "ADATA": EmvApplicationCryptogram.ComputeArpcMethod2ForAnalysis(KeyAlgorithm.Tdea, k, new byte[8], new byte[4], new byte[9]); break;
                case "KEY15": EmvApplicationCryptogram.ComputeAcForAnalysis(KeyAlgorithm.Tdea, new byte[15], []); break;
                case "MASTER24": EmvMasterKeyDerivationOptionA.DeriveForAnalysis(new byte[24], "1234", "00"); break;
                case "SESSION24": EmvAcSessionKeyDerivation.DeriveForAnalysis(KeyAlgorithm.Tdea, new byte[24], new byte[2]); break;
                case "AC24": EmvApplicationCryptogram.ComputeAcForAnalysis(KeyAlgorithm.Tdea, new byte[24], []); break;
            }
        });
    }

    [Test]
    public void AnalysisPrimitivesKeepLengthsAndMacContracts()
    {
        byte[] key = new byte[16];
        Assert.Throws<ArgumentException>(() => DES3_ECB.EncryptNoPaddingForAnalysis(new byte[15], new byte[8]));
        Assert.Throws<ArgumentException>(() => DES3_ECB.EncryptNoPaddingForAnalysis(key, new byte[7]));
        Assert.Throws<ArgumentException>(() => DES3_CBC.EncryptNoPaddingForAnalysis(key, new byte[7], new byte[8]));
        Assert.Throws<ArgumentException>(() => DES3_CBC.DecryptNoPaddingForAnalysis(key, new byte[8], new byte[7]));
        Assert.Throws<ArgumentException>(() => DES3_CBC.ComputeMacNoPaddingForAnalysis(key, [], 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => DES3_CBC.ComputeMacNoPaddingForAnalysis(key, new byte[8], 9));
        Assert.Throws<ArgumentException>(() => DES3_CMAC.ComputeForAnalysis(new byte[15], []));
        Assert.Throws<ArgumentException>(() => DES3_RETAIL.ComputeMacForAnalysis(key, [], "PKCS7", 8));
        Assert.Throws<ArgumentException>(() => DES3_RETAIL.ComputeMacForAnalysis(key, [], "ISO-9797-M2", 3));
    }

    [Test]
    public void QualityWarningsKeepNestedParentContext()
    {
        var v = Vectors().Single(v => v.Profile.Name == "both" && v.Mechanism == "MAC-EMV-AC");
        Inputs(v, false);
        var hmac = AlgorithmFactory.Create("HMAC-SHA256").GenerateParameters("HMAC-SHA256"); hmac.Id = "ph";
        var hk = Key("hk", "0123456789ABCDEFFEDCBA9876543210"); hk.KeyType = KeyType.Secret(KeyAlgorithm.Hmac);
        VariableDictionary.Instance().Add(hmac); VariableDictionary.Instance().Add(hk);
        var runner = new CryptoScriptRunner();
        runner.Execute(ParserBuilder.StringBuild("VAR output=Mac(ph,hk,Mac(p,k,data))").program());
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Is.EqualTo(new[] { SemanticDiagnosticCode.DesParityViolation, SemanticDiagnosticCode.WeakDes3Key }));
        Assert.That(runner.Diagnostics.Select(d => d.Location!.OperationId), Is.All.EqualTo(2));
        Assert.That(runner.Diagnostics.Select(d => d.Location!.ParentOperationId), Is.All.EqualTo(1));
        Assert.That(runner.Diagnostics.Select(d => d.Location!.ExecutionId).Distinct().Count(), Is.EqualTo(1));
    }
}
