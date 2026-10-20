using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvAlgorithmSelectionContractTests
{
    [SetUp]
    public void Reset() => EmvAcFixtures.Reset();

    public static IEnumerable<TestCaseData> Hierarchies() =>
        EmvArpcFixtures.All.Select(v => new TestCaseData(v).SetName("GenerateKey_EMV_hierarchy_" + v.Cipher + "_" + v.Master.Length));

    private static CryptoScriptRunner Run(string script) => EmvAcFixtures.Execute(script);

    [TestCaseSource(nameof(Hierarchies))]
    public void GenerateKeySelectsTheUnchangedAesOrTdeaHierarchyAndUsageOnlyWarns(object value)
    {
        var v = (EmvArpcFixtures.Vector)value;
        Run($"KEY master=GenerateKey({v.Cipher},0x({v.Master})) PARAM derive=Parameters(KDF-EMV-AC-SESSION)");
        var master = (KeyVariableDeclaration)VariableDictionary.Instance().Get("master");
        var expectedAlgorithm = v.Cipher == "DES3-ECB" ? KeyAlgorithm.Tdea : KeyAlgorithm.Aes;
        Assert.That(master.KeyType, Is.EqualTo(KeyType.Secret(expectedAlgorithm)));
        master.Usage = KeyUsagePolicy.Restricted(KeyUsage.MacVerify);
        string before = master.Serialize();
        var derived = Run("KEY session=Derive(derive,master,0x(0001))");
        Assert.That(derived.Diagnostics.Select(d => d.DiagnosticCode).First(), Is.EqualTo(SemanticDiagnosticCode.KeyUsageConflict));
        var session = (KeyVariableDeclaration)VariableDictionary.Instance().Get("session");
        Assert.That(session.Value, Is.EqualTo("0x(" + v.Key + ")").IgnoreCase);
        Assert.That(session.KeyType.Algorithm, Is.EqualTo(expectedAlgorithm));
        Assert.That(master.Serialize(), Is.EqualTo(before));
        session.Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt);
        string sessionBefore = session.Serialize();
        var result = Run(
            "PARAM ac=Parameters(MAC-EMV-AC) " +
            $"PARAM verify=Parameters(MAC-EMV-AC-VERIFY,#EXPECTED:0x({v.Arqc})) " +
            "PARAM m1=Parameters(MAC-EMV-ARPC-METHOD-1,#ARC:0x(3030)) " +
            "PARAM m2=Parameters(MAC-EMV-ARPC-METHOD-2,#CSU:0x(00000000)) " +
            $"VAR computed=Mac(ac,session,0x({EmvAcFixtures.Data})) " +
            $"VAR verified=Mac(verify,session,0x({EmvAcFixtures.Data})) " +
            $"VAR arpc1=Mac(m1,session,0x({v.Arqc})) VAR arpc2=Mac(m2,session,0x({v.Arqc}))");
        foreach (var expected in new[] { ("computed", v.Arqc), ("verified", "01"), ("arpc1", v.M1), ("arpc2", v.M2[0]) })
            Assert.That(VariableDictionary.Instance().Get(expected.Item1).Value,
                Is.EqualTo("0x(" + expected.Item2 + ")").IgnoreCase);
        Assert.That(result.Diagnostics.Count(d => d.DiagnosticCode == SemanticDiagnosticCode.KeyUsageConflict), Is.EqualTo(4));
        Assert.That(result.Diagnostics.Any(d => d.DiagnosticCode == SemanticDiagnosticCode.KeyTypeConflict), Is.False);
        Assert.That(session.Serialize(), Is.EqualTo(sessionBefore));
    }

    [TestCase(KeyAlgorithm.Aes, "89F7B697A028A93345BE7A409665B9A4")]
    [TestCase(KeyAlgorithm.Tdea, "07547F1BAF714A4F608E9FE0D19938A5")]
    public void SameSixteenBytesSelectDifferentProceduresOnlyByDeclaredAlgorithm(KeyAlgorithm algorithm, string expected)
    {
        // AES: existing independent EMV reference. TDEA: independently computed
        // with .NET TripleDES ECB on 0001F00000000000 || 00010F0000000000.
        const string bytes = "0x(2EF6E07ECBA86BCF3C3CFF7BBEBE6F38)";
        var key = new KeyVariableDeclaration
        {
            Id = "master", Value = bytes, ValueFormat = FormatConversions.HEX,
            KeySizeInBits = new KeySize(128), KeyType = KeyType.Secret(algorithm), Type = new CryptoTypeKey()
        };
        var p = new KDF_EMV_AC_SESSION().GenerateParameters("KDF-EMV-AC-SESSION");
        var sink = new SemanticDiagnosticSink();
        var args = new AlgorithmCallArguments(new[]
        {
            new AlgorithmCallArgument(p.Value, p, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(bytes, key, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument("0x(0001)", kind: ResolvedCallArgumentKind.HexLiteral)
        }, sink);
        string before = key.Serialize();
        Assert.That(new KDF_EMV_AC_SESSION().Derive(args).Value, Is.EqualTo("0x(" + expected + ")").IgnoreCase);
        Assert.That(sink.GetSnapshot().Any(d => d.DiagnosticCode == SemanticDiagnosticCode.KeyTypeConflict), Is.False);
        Assert.That(key.Serialize(), Is.EqualTo(before));
    }

    public static IEnumerable<TestCaseData> UnknownCases()
    {
        foreach (string mechanism in new[] { "KDF-EMV-AC-SESSION", "MAC-EMV-AC", "MAC-EMV-AC-VERIFY",
                     "MAC-EMV-ARPC-METHOD-1", "MAC-EMV-ARPC-METHOD-2" })
            foreach (int length in new[] { 16, 24, 32 })
                yield return new TestCaseData(mechanism, length);
    }

    [TestCaseSource(nameof(UnknownCases))]
    public void UnknownFromJsonOrLegacyNeverSelectsAnAlgorithmByLength(string mechanism, int length)
    {
        foreach (bool legacy in new[] { false, true })
        {
            Reset();
            var key = KeyVariableDeclaration.Deserialize(
                "{\"Id\":\"unknown\",\"Value\":\"0x(" + new string('A', length * 2) + ")\"," +
                "\"ValueFormat\":\"HEX_STRING\"" +
                (legacy ? "" : ",\"KeyType\":{\"Algorithm\":0,\"MaterialKind\":0}") + "}");
            key.Type = new CryptoTypeKey();
            Assert.That(key.KeyType.Algorithm, Is.EqualTo(KeyAlgorithm.Unknown));
            VariableDictionary.Instance().Add(key);
            string named = mechanism switch
            {
                "MAC-EMV-AC-VERIFY" => ",#EXPECTED:0x(D3A78E1517C22F70)",
                "MAC-EMV-ARPC-METHOD-1" => ",#ARC:0x(3030)",
                "MAC-EMV-ARPC-METHOD-2" => ",#CSU:0x(00000000)",
                _ => ""
            };
            Run($"PARAM p=Parameters({mechanism}{named})");
            string call = mechanism.StartsWith("KDF-", StringComparison.Ordinal)
                ? "KEY failed=Derive(p,unknown,0x(0001))" : "VAR failed=Mac(p,unknown,0x(0001020304050607))";
            Assert.Throws<SemanticErrorException>(() => Run(call + " VAR after=0x(01)"));
            Assert.That(VariableDictionary.Instance().Contains("failed"), Is.False);
            Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        }
    }
}
