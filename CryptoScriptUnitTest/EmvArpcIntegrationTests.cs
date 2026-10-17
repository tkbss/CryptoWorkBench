using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.EMV;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvArpcIntegrationTests
{
    private static readonly string[] Mechanisms = { EmvArpcFixtures.Method1, EmvArpcFixtures.Method2 };

    [SetUp]
    public void SetUp() => EmvAcFixtures.Reset();

    public static IEnumerable<TestCaseData> InvalidArguments()
    {
        foreach (string mechanism in Mechanisms)
            foreach (string call in new[]
            {
                "Mac(p,sk)", "Mac(p,sk,arqc,arqc)", "Mac(sk,p,arqc)",
                "Mac(p,arqc,arqc)", "Mac(p,p,arqc)",
                "Mac(p,0x(89F7B697A028A93345BE7A409665B9A4),arqc)",
                "Mac(p,b64(ife2l6AoqTNFvnpAlmW5pA==),arqc)", "Mac(p,\"key\",arqc)",
                "Mac(p,GenerateKey(AES-ECB,0x(89F7B697A028A93345BE7A409665B9A4)),arqc)",
                "Mac(p,sk,\"D3A78E1517C22F70\")", "Mac(p,sk,1)", "Mac(p,sk,b64(06eOFRfCL3A=))",
                "Mac(p,sk,p)", "Mac(p,sk,sk)", "Mac(p,sk,0x(00010203040506))", "Mac(p,sk,0x(000102030405060708))",
                "Mac(p,sk,Hash(HASH-SHA1,0x(0001)))", "Mac(sk,sk,arqc)"
            })
                yield return new TestCaseData(mechanism, call);
    }

    [TestCaseSource(nameof(InvalidArguments))]
    public void BothMechanismsRejectInvalidPositionsBeforeCreatingResults(string mechanism, string call)
    {
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvArpcFixtures.Prefix() +
            $"PARAM p=Parameters({mechanism},{EmvArpcFixtures.Required(mechanism)}) VAR result={call} VAR after=0x(AABB)"));
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void GenuineParamAndKeyIdentityRemainRequired(string mechanism)
    {
        EmvAcFixtures.Execute(EmvArpcFixtures.Prefix() + $"PARAM p=Parameters({mechanism},{EmvArpcFixtures.Required(mechanism)})");
        VariableDictionary.Instance().Add(new StringVariableDeclaration
        {
            Id = "fake", Value = VariableDictionary.Instance().Get("p").Value, ValueFormat = FormatConversions.PAR
        });
        var wrapper = AlgorithmFactory.Create(mechanism);
        var original = Structured();
        Assert.Throws<ArgumentException>(() => wrapper.Mac(original.Values));
        Assert.Throws<ArgumentException>(() => wrapper.Mac(original.WithSourceVariable(0,
            (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p"))));
        foreach (string first in new[] { "fake", "#MECH:" + mechanism,
                     $"Parameters({mechanism},{EmvArpcFixtures.Required(mechanism)})" })
            Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute($"VAR result=Mac({first},sk,arqc)"));
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
    }

    public static IEnumerable<TestCaseData> BadAlgorithms() =>
        Mechanisms.SelectMany(m => new[] { KeyType.Secret(KeyAlgorithm.Unknown), KeyType.Secret(KeyAlgorithm.Hmac),
            KeyType.Public(KeyAlgorithm.Rsa), KeyType.Private(KeyAlgorithm.Ec) }.Select(t => new TestCaseData(m, t)));

    [TestCaseSource(nameof(BadAlgorithms))]
    public void UnsupportedKeyMetadataIsRejected(string mechanism, KeyType type)
    {
        Prepare(mechanism);
        ((KeyVariableDeclaration)VariableDictionary.Instance().Get("sk")).KeyType = type;
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR result=Mac(p,sk,arqc)"));
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
    }

    public static IEnumerable<TestCaseData> BadKeys() =>
        Mechanisms.SelectMany(m => new[] { (KeyAlgorithm.Aes, 0), (KeyAlgorithm.Aes, 15), (KeyAlgorithm.Aes, 17),
            (KeyAlgorithm.Aes, 33), (KeyAlgorithm.Tdea, 24) }.Select(k => new TestCaseData(m, k.Item1, k.Item2)));

    [TestCaseSource(nameof(BadKeys))]
    public void InvalidKeyLengthsAreRejected(string mechanism, KeyAlgorithm algorithm, int length)
    {
        Prepare(mechanism);
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("sk");
        key.KeyType = KeyType.Secret(algorithm);
        key.KeyValue = "0x(" + new string('A', length * 2) + ")";
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR result=Mac(p,sk,arqc)"));
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void SizeMetadataAndEqualValueKeysCannotBeBypassed(string mechanism)
    {
        Prepare(mechanism);
        var dictionary = VariableDictionary.Instance();
        var key = (KeyVariableDeclaration)dictionary.Get("sk");
        dictionary.Add(new KeyVariableDeclaration
        {
            Id = "other", KeyValue = key.KeyValue, Value = key.Value, ValueFormat = key.ValueFormat,
            KeyType = KeyType.Secret(KeyAlgorithm.Hmac), KeySizeInBits = key.KeySizeInBits
        });
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR bad=Mac(p,other,arqc)"));
        EmvAcFixtures.Execute("VAR good=Mac(p,sk,arqc)");
        Assert.That(dictionary.Get("sk"), Is.SameAs(key));
        key.KeyValue = "0x(" + new string('A', 48) + ")";
        var error = Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR bad=Mac(p,sk,arqc)"));
        Assert.That(error!.SemanticError!.Message, Does.Contain("size metadata"));
        Assert.That(dictionary.Contains("bad"), Is.False);
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void UnknownDuplicateAndContradictoryParametersAreRejected(string mechanism)
    {
        foreach (string repeated in new[] { mechanism, "AES-CMAC" })
        {
            EmvAcFixtures.Reset();
            Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvArpcFixtures.Prefix() +
                $"PARAM p=#MECH:{mechanism} {EmvArpcFixtures.Required(mechanism)} #MECH:{repeated} VAR result=Mac(p,sk,arqc)"));
            EmvAcFixtures.Reset();
            Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvArpcFixtures.Prefix() +
                $"PARAM p=Parameters({mechanism},{EmvArpcFixtures.Required(mechanism)},#MECH:{repeated}) VAR result=Mac(p,sk,arqc)"));
        }
        EmvAcFixtures.Reset();
        Prepare(mechanism);
        var p = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        p.GetParameters().Add("#UNKNOWN", "private");
        var error = Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR result=Mac(p,sk,arqc)"));
        Assert.That(error!.SemanticError!.ErrorCode, Is.EqualTo(FunctionContractError.UnknownParameter));
        p.GetParameters().Remove("#UNKNOWN");
        p.GetParameters()["#MECH"] = "AES-CMAC";
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR result=Mac(p,sk,arqc)"));
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void CallerValuesStayUnchangedOnSuccessAndFailure(string mechanism)
    {
        Prepare(mechanism);
        var dictionary = VariableDictionary.Instance();
        var key = (KeyVariableDeclaration)dictionary.Get("sk");
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt);
        key.DerivationMechanism = "Imported";
        var p = dictionary.Get("p");
        var arqc = dictionary.Get("arqc");
        string keyBefore = key.Serialize(), pBefore = p.Value, arqcBefore = arqc.Value;
        EmvAcFixtures.Execute("VAR result=Mac(p,sk,arqc)");
        Assert.That(dictionary.Get("sk"), Is.SameAs(key));
        Assert.That(key.Serialize(), Is.EqualTo(keyBefore));
        Assert.That(dictionary.Get("p"), Is.SameAs(p));
        Assert.That(p.Value, Is.EqualTo(pBefore));
        Assert.That(dictionary.Get("arqc"), Is.SameAs(arqc));
        Assert.That(arqc.Value, Is.EqualTo(arqcBefore));
        arqc.Value = "0x(00010203040506)";
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR failed=Mac(p,sk,arqc)"));
        Assert.That(key.Serialize(), Is.EqualTo(keyBefore));
        Assert.That(p.Value, Is.EqualTo(pBefore));
        Assert.That(arqc.Value, Is.EqualTo("0x(00010203040506)"));
        Assert.That(dictionary.Contains("failed"), Is.False);
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void HostEmptyArqcAndNonHexVarsAreRejected(string mechanism)
    {
        foreach (string value in new[] { "0x()", "\"D3A78E1517C22F70\"", "b64(06eOFRfCL3A=)", "1" })
        {
            EmvAcFixtures.Reset();
            Prepare(mechanism);
            var arqc = VariableDictionary.Instance().Get("arqc");
            arqc.Value = value;
            arqc.ValueFormat = FormatConversions.ParseString(value);
            Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR result=Mac(p,sk,arqc)"));
        }
    }

    public static IEnumerable<TestCaseData> Pipelines() => EmvArpcFixtures.All.SelectMany(v =>
        new[] { (EmvArpcFixtures.Method1, false), (EmvArpcFixtures.Method2, false), (EmvArpcFixtures.Method2, true) }
            .Select(p => new TestCaseData(v, p.Item1, p.Item2)));

    private static string Hierarchy(EmvArpcFixtures.Vector v, string mechanism, bool iad) =>
        $"KEY mk=GenerateKey({v.Cipher},0x({v.Master})) PARAM s=Parameters(KDF-EMV-AC-SESSION) " +
        $"KEY sk=Derive(s,mk,0x(0001)) VAR data=0x({EmvAcFixtures.Data}) VAR received=0x({v.Arqc}) " +
        $"PARAM verify=Parameters(MAC-EMV-AC-VERIFY,#EXPECTED:received) " +
        $"PARAM p=Parameters({mechanism},{EmvArpcFixtures.Required(mechanism)}{(iad ? ",#VARIANT:\"IAD\"" : "")}) ";

    [TestCaseSource(nameof(Pipelines))]
    public void DeriveVerifyAndArpcUseTheFullRunnerHierarchy(object vector, string mechanism, bool iad)
    {
        var v = (EmvArpcFixtures.Vector)vector;
        EmvAcFixtures.Execute(Hierarchy(v, mechanism, iad) + "VAR verified=Mac(verify,sk,data) VAR arpc=Mac(p,sk,received)");
        Assert.That(VariableDictionary.Instance().Get("sk").Value, Is.EqualTo("0x(" + v.Key + ")").IgnoreCase);
        Assert.That(VariableDictionary.Instance().Get("verified").Value, Is.EqualTo("0x(01)"));
        EmvArpcFixtures.AssertResult("arpc", mechanism == EmvArpcFixtures.Method1 ? v.M1 : iad ? v.Iad[0] : v.M2[0],
            mechanism == EmvArpcFixtures.Method1 || iad ? 8 : 4);
    }

    [TestCaseSource(nameof(Pipelines))]
    public void FailedVerificationPreventsBothArpcMethodsAndPreservesAllInputs(object vector, string mechanism, bool iad)
    {
        var v = (EmvArpcFixtures.Vector)vector;
        // Setup and verification both execute via the public parser/runner; no injected seam.
        EmvAcFixtures.Execute(Hierarchy(v, mechanism, iad));
        var dictionary = VariableDictionary.Instance();
        byte[] wrong = Convert.FromHexString(v.Arqc);
        wrong[0] ^= 0x80;
        dictionary.Get("received").Value = FormatConversions.ByteArrayToHexString(wrong);
        ((ParameterVariableDeclaration)dictionary.Get("verify")).SetParameter("#EXPECTED", dictionary.Get("received").Value);
        var originals = new[] { "mk", "sk", "data", "received", "p", "verify" }.Select(id => dictionary.Get(id)).ToArray();
        string[] before = originals.Select(variable => variable is KeyVariableDeclaration key ? key.Serialize() : variable.Value).ToArray();
        var error = Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(
            "VAR verified=Mac(verify,sk,data) VAR arpc=Mac(p,sk,received) KEY after=GenerateKey(AES-ECB,128)"));
        Assert.That(error!.SemanticError!.Message, Is.EqualTo("EMV Application Cryptogram verification failed."));
        Assert.That(error.SemanticError.FunctionCall, Is.EqualTo("Mac(<redacted>)"));
        Assert.That(dictionary.Contains("verified"), Is.False);
        Assert.That(dictionary.Contains("arpc"), Is.False);
        Assert.That(dictionary.Contains("after"), Is.False);
        for (int i = 0; i < originals.Length; i++)
        {
            Assert.That(dictionary.Get(originals[i].Id), Is.SameAs(originals[i]));
            Assert.That(originals[i] is KeyVariableDeclaration key ? key.Serialize() : originals[i].Value, Is.EqualTo(before[i]));
        }
    }

    private static void Prepare(string mechanism) => EmvAcFixtures.Execute(EmvArpcFixtures.Prefix() +
        $"PARAM p=Parameters({mechanism},{EmvArpcFixtures.Required(mechanism)})");

    [TestCaseSource(nameof(Pipelines))]
    public void FullSingleScriptWithWrongArqcNeverReachesArpc(object vector, string mechanism, bool iad)
    {
        var v = (EmvArpcFixtures.Vector)vector;
        byte[] wrong = Convert.FromHexString(v.Arqc);
        wrong[^1] ^= 0x80;
        string script = Hierarchy(v, mechanism, iad).Replace("VAR received=0x(" + v.Arqc + ")",
            "VAR received=0x(" + Convert.ToHexString(wrong) + ")", StringComparison.Ordinal) +
            "VAR verified=Mac(verify,sk,data) VAR arpc=Mac(p,sk,received)";
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(script));
        Assert.That(VariableDictionary.Instance().Contains("verified"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("arpc"), Is.False);
        Assert.That(VariableDictionary.Instance().Get("sk").Value, Is.EqualTo("0x(" + v.Key + ")").IgnoreCase);
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void WeakKeyAnalysisContinuesWithoutMutatingInputsOrLeakingMaterial(string mechanism)
    {
        Prepare(mechanism);
        var dictionary = VariableDictionary.Instance();
        var key = (KeyVariableDeclaration)dictionary.Get("sk");
        key.KeyType = KeyType.Secret(KeyAlgorithm.Tdea);
        var reference = Ap5TReferenceVectors.All.Single(p => p.Name == "both");
        key.KeyValue = "0x(" + reference.Key + ")";
        var parameter = (ParameterVariableDeclaration)dictionary.Get("p");
        dictionary.Get("arqc").Value = "0x(" + Ap5TReferenceVectors.Arqc + ")";
        if (mechanism == EmvArpcFixtures.Method1)
            parameter.SetParameter("ARC", "0x(" + Ap5TReferenceVectors.Arc + ")");
        else
        {
            parameter.SetParameter("CSU", "0x(" + Ap5TReferenceVectors.Csu + ")");
            parameter.SetParameter("ADATA", "0x(" + Ap5TReferenceVectors.Adata + ")");
        }
        string before = key.Serialize(), parameterBefore = dictionary.Get("p").Value, arqcBefore = dictionary.Get("arqc").Value;
        var runner = new CryptoScriptRunner();
        runner.Execute(ParserBuilder.StringBuild("VAR result=Mac(p,sk,arqc) VAR after=0x(AABB)").program());
        Assert.That(dictionary.Get("result").Value, Is.EqualTo("0x(" + reference.Emv![mechanism == EmvArpcFixtures.Method1 ? 4 : 5] + ")").IgnoreCase);
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Is.EqualTo(new[]
            { SemanticDiagnosticCode.DesParityViolation, SemanticDiagnosticCode.WeakDes3Key }));
        Assert.That(runner.Diagnostics.All(d => !d.Message.Contains(reference.Key)), Is.True);
        Assert.That(key.Serialize(), Is.EqualTo(before));
        Assert.That(dictionary.Get("p").Value, Is.EqualTo(parameterBefore));
        Assert.That(dictionary.Get("arqc").Value, Is.EqualTo(arqcBefore));
        Assert.That(dictionary.Contains("result"), Is.True);
        Assert.That(dictionary.Contains("after"), Is.True);
    }

    [TestCase("MAC-EMV-AC")]
    [TestCase("MAC-EMV-AC-VERIFY")]
    public void IadVariantIsNotEnabledForExistingAcMechanisms(string mechanism)
    {
        string required = mechanism.EndsWith("VERIFY") ? ",#EXPECTED:0x(D3A78E1517C22F70)" : "";
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(
            $"PARAM p=Parameters({mechanism}{required},#VARIANT:\"IAD\")"));
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void NamedBinaryHexVarsResolveByValueAndWrongWrapperMechanismIsRejected(string mechanism)
    {
        EmvAcFixtures.Execute(EmvArpcFixtures.Prefix() + "VAR arc=0x(3030) VAR csu=0x(00000000) VAR proprietary=0x(01) " +
            $"PARAM p=Parameters({mechanism},{(mechanism == EmvArpcFixtures.Method1 ? "#ARC:arc" : "#CSU:csu,#ADATA:proprietary")}) " +
            "VAR result=Mac(p,sk,arqc)");
        EmvArpcFixtures.AssertResult("result", mechanism == EmvArpcFixtures.Method1 ? "FC16C28C668DDC7C" : "50B632DC",
            mechanism == EmvArpcFixtures.Method1 ? 8 : 4);
        var wrongWrapper = AlgorithmFactory.Create(mechanism == EmvArpcFixtures.Method1 ? EmvArpcFixtures.Method2 : EmvArpcFixtures.Method1);
        Assert.Throws<ArgumentException>(() => wrongWrapper.Mac(Structured()));
    }

    // Fixed independent AP6.5 review calculations, 2026-10-14:
    // Python cryptography 50.0.2 / OpenSSL 4.0.3; PAN 12345678901234567, PSN 45,
    // ATC 0001, transaction bytes 00..0F, ARC 3030, CSU 00000000, no proprietary data.
    // No expected value is generated by the tested C# core.
    public static IEnumerable<TestCaseData> IssuerHierarchies()
    {
        yield return new TestCaseData("A", "DES3-ECB", "0123456789ABCDEFFEDCBA9876543210",
            "946DC1A8C78A624519BC4645CB073737", "6E8CDF5C8E2992AB6995685FADABD7D5", "BEF8BC577819E418",
            "18FE08D284BC9A09", "07D8CEE2", "07D8CEE200000000");
        yield return new TestCaseData("B", "DES3-ECB", "0123456789ABCDEFFEDCBA9876543210",
            "F767ECC2263B2C543775CB62AB6B57D6", "9BAC5E0807B3204D8607B510A71598B7", "2927AC1EECAD0489",
            "27EC78EDC9B1E1B7", "BF9BDBA7", "BF9BDBA700000000");
        yield return new TestCaseData("C", "AES-ECB", "000102030405060708090A0B0C0D0E0F",
            "F43946906E94FD6A8F3613614932705A", "AB16FCC20C94D5B651482306B083409F", "3AE4A6B08369D6F1",
            "EE77EEB2D13449B0", "4EF4D913", "4EF4D91300000000");
        yield return new TestCaseData("C", "AES-ECB", "000102030405060708090A0B0C0D0E0F1011121314151617",
            "D24EDC0A6BDB84EAC13D3C5AF9095CB82E80C7CBCA66CF7D", "B917D7ABE22A0D761CE544063C28A3B7070C00310CA0D342",
            "32DE8438719DA4B4", "3832B05AC8AC6260", "9C4C890E", "9C4C890E00000000");
        yield return new TestCaseData("C", "AES-ECB", "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F",
            "7E4088A463D1ACEB804F6680B94391351E04D33F83609AEE01F892BAEF9A174A",
            "3C311B498957345F773E06C20DAC1FB19CED179BF9FC3BC5CDE62F1F1E77E613",
            "3947A9F44CCABD95", "0D1905A6B597DACE", "C6842859", "C684285900000000");
    }

    [TestCaseSource(nameof(IssuerHierarchies))]
    [Category("EmvIssuerHierarchy")]
    public void FullPanBasedIssuerHierarchyMatchesFixedIndependentValues(string option, string cipher, string issuer,
        string icc, string session, string ac, string method1, string method2, string iad)
    {
        EmvAcFixtures.Execute(
            $"KEY imk=GenerateKey({cipher},0x({issuer})) PARAM master=Parameters(KDF-EMV-MASTER-{option},#PSN:\"45\") " +
            "KEY icc=Derive(master,imk,\"12345678901234567\") PARAM s=Parameters(KDF-EMV-AC-SESSION) " +
            $"KEY sk=Derive(s,icc,0x(0001)) VAR data=0x({EmvAcFixtures.Data}) VAR received=0x({ac}) " +
            "PARAM a=Parameters(MAC-EMV-AC) VAR ac=Mac(a,sk,data) " +
            "PARAM v=Parameters(MAC-EMV-AC-VERIFY,#EXPECTED:received) VAR verified=Mac(v,sk,data) " +
            "PARAM m1=Parameters(MAC-EMV-ARPC-METHOD-1,#ARC:0x(3030)) VAR arpc1=Mac(m1,sk,received) " +
            "PARAM m2=Parameters(MAC-EMV-ARPC-METHOD-2,#CSU:0x(00000000)) VAR arpc2=Mac(m2,sk,received) " +
            "PARAM i=Parameters(MAC-EMV-ARPC-METHOD-2,#CSU:0x(00000000),#VARIANT:\"IAD\") VAR iad=Mac(i,sk,received)");
        var dictionary = VariableDictionary.Instance();
        foreach (var (id, expected, mechanism) in new[]
                 { ("icc", icc, "KDF-EMV-MASTER-" + option), ("sk", session, "KDF-EMV-AC-SESSION") })
        {
            var key = (KeyVariableDeclaration)dictionary.Get(id);
            Assert.That(key.Value, Is.EqualTo("0x(" + expected + ")").IgnoreCase);
            Assert.That(key.KeyValue, Is.EqualTo(key.Value));
            Assert.That(key.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(key.KeyType, Is.EqualTo(KeyType.Secret(option == "C" ? KeyAlgorithm.Aes : KeyAlgorithm.Tdea)));
            Assert.That(key.KeySizeInBits, Is.EqualTo(new KeySize(issuer.Length * 4)));
            Assert.That(key.Usage, Is.EqualTo(KeyUsagePolicy.Unspecified));
            Assert.That(key.DerivationMechanism, Is.EqualTo(mechanism));
        }
        EmvArpcFixtures.AssertResult("ac", ac, 8);
        EmvArpcFixtures.AssertResult("verified", "01", 1);
        EmvArpcFixtures.AssertResult("arpc1", method1, 8);
        EmvArpcFixtures.AssertResult("arpc2", method2, 4);
        EmvArpcFixtures.AssertResult("iad", iad, 8);
    }

    private static AlgorithmCallArguments Structured() => new(new[]
    {
        new AlgorithmCallArgument(VariableDictionary.Instance().Get("p").Value, VariableDictionary.Instance().Get("p"), ResolvedCallArgumentKind.Variable),
        new AlgorithmCallArgument(VariableDictionary.Instance().Get("sk").Value, VariableDictionary.Instance().Get("sk"), ResolvedCallArgumentKind.Variable),
        new AlgorithmCallArgument(VariableDictionary.Instance().Get("arqc").Value, VariableDictionary.Instance().Get("arqc"), ResolvedCallArgumentKind.Variable)
    });
}
