using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.EMV;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

internal static class EmvArpcFixtures
{
    internal const string Method1 = "MAC-EMV-ARPC-METHOD-1";
    internal const string Method2 = "MAC-EMV-ARPC-METHOD-2";
    internal sealed record Vector(string Cipher, string Master, string Key, string Arqc,
        string M1, string M1Zero, string M1FFFF, string[] M2, string[] Iad, string NonzeroCsuIad);

    // Fixed independent AP6.1/AP6.3 OpenSSL characterizations, not production-core calls.
    // Nonzero-CSU values additionally computed with Python cryptography/OpenSSL on 2026-10-14:
    // message = ARQC || AABBCCDD || 01020304; AES CMAC or DES-CBC(KL)/D(KR)/E(KL), M2.
    internal static readonly Vector[] All =
    {
        new("DES3-ECB", "4F5276A285D36175DC4F516BBF80CB1A", "5B10C70AFEE94975A345C69888048FF9", "613D21599AAC13F5",
            "7FE9710A3DAD5725", "A90B02E8837D48F1", "518B08617D839761",
            new[] { "81884C5C", "4A9B02A7", "EF3EBD5C", "33EEDB59" },
            new[] { "81884C5C00000000", "4A9B02A70000000001", "EF3EBD5C0000000001020304", "33EEDB59000000000102030405060708" },
            "21F931B5AABBCCDD01020304"),
        new("AES-ECB", "2EF6E07ECBA86BCF3C3CFF7BBEBE6F38", EmvAcFixtures.AesKey, EmvAcFixtures.AesAc,
            "FC16C28C668DDC7C", "DD032CF5613B516D", "D424AAC5DAA672A9",
            new[] { "B7E6B69E", "50B632DC", "20A190D6", "164124C1" },
            new[] { "B7E6B69E00000000", "50B632DC0000000001", "20A190D60000000001020304", "164124C1000000000102030405060708" },
            "B47911E1AABBCCDD01020304"),
        new("AES-ECB", "7481B0525D05D393D6DCBADD333C6BC514087C6BF5FA10C4",
            "78429FD2061D24B1F8830B2C91D3ED95ED9D4B069A7C2707", "497A0F0552EDFE73",
            "6CCDF434997B63E2", "4A493CCB6F4A96B7", "8E3069C4BCA33A94",
            new[] { "0DC0A0B0", "5AD9357A", "E48DC100", "4C1DD78E" },
            new[] { "0DC0A0B000000000", "5AD9357A0000000001", "E48DC1000000000001020304", "4C1DD78E000000000102030405060708" },
            "5F91A93EAABBCCDD01020304"),
        new("AES-ECB", "14D63F23982740AC65B482BAF5913092D8132BAA4143A24D3CF437232711A507",
            "A99C5840A0CBFBF093DEA740FFFFF5A241BFE9472968CC6E0B9EC76BA280CE0F", "3AFC4EF4D46F8F27",
            "8DF1FFC8B7D2EB25", "11482FD5A201EBB0", "D964C738A75C3737",
            new[] { "0E95C016", "78E32620", "660705F9", "0E2CE01B" },
            new[] { "0E95C01600000000", "78E326200000000001", "660705F90000000001020304", "0E2CE01B000000000102030405060708" },
            "D1E5B01EAABBCCDD01020304")
    };
    internal static readonly string[] Proprietary = { "", "01", "01020304", "0102030405060708" };
    internal static string Required(string mechanism) => mechanism == Method1 ? "#ARC:0x(3030)" : "#CSU:0x(00000000)";
    internal static string Prefix(Vector? vector = null) =>
        EmvAcFixtures.Prefix(vector?.Cipher ?? "AES-ECB", vector?.Key ?? EmvAcFixtures.AesKey) +
        $"VAR arqc=0x({vector?.Arqc ?? EmvAcFixtures.AesAc}) ";
    internal static void AssertResult(string id, string expected, int bytes)
    {
        var result = VariableDictionary.Instance().Get(id);
        Assert.That(result, Is.TypeOf<StringVariableDeclaration>());
        Assert.That(result.Type, Is.TypeOf<CryptoTypeVar>());
        Assert.That(result.ValueFormat, Is.EqualTo(FormatConversions.HEX));
        Assert.That(result.Value, Is.EqualTo("0x(" + expected + ")").IgnoreCase);
        Assert.That(FormatConversions.ToByteArray(result.Value, result.ValueFormat), Has.Length.EqualTo(bytes));
    }
}

[NonParallelizable]
public class EmvArpcMethod1MechanismTests
{
    [SetUp]
    public void SetUp() => EmvAcFixtures.Reset();

    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (var v in EmvArpcFixtures.All)
            foreach (string arc in new[] { "3030", "0000", "FFFF" })
                foreach (bool direct in new[] { false, true })
                    foreach (bool literal in new[] { false, true })
                        yield return new TestCaseData(v, arc, direct, literal);
    }

    [TestCaseSource(nameof(Cases))]
    public void Method1MatchesFixedValuesThroughBothDeclarationForms(object vector, string arc, bool direct, bool literal)
    {
        var v = (EmvArpcFixtures.Vector)vector;
        string declaration = direct ? $"PARAM p=#MECH:{EmvArpcFixtures.Method1} #ARC:0x({arc}) " :
            $"PARAM p=Parameters({EmvArpcFixtures.Method1},#ARC:0x({arc})) ";
        EmvAcFixtures.Execute(EmvArpcFixtures.Prefix(v) + declaration +
            $"VAR arpc=Mac(p,sk,{(literal ? "0x(" + v.Arqc + ")" : "arqc")})");
        EmvArpcFixtures.AssertResult("arpc", arc == "3030" ? v.M1 : arc == "0000" ? v.M1Zero : v.M1FFFF, 8);
    }

    [TestCase("")]
    [TestCase("#ARC:0x(00)")]
    [TestCase("#ARC:0x(000000)")]
    [TestCase("#ARC:\"00\"")]
    [TestCase("#ARC:3030")]
    [TestCase("#ARC:b64(MDA=)")]
    [TestCase("#ARC:0x(3030),#ARC:0x(3030)")]
    [TestCase("#ARC:0x(3030),#ARC:0x(0000)")]
    [TestCase("#ARC:0x(3030),#PAD:NONE")]
    [TestCase("#ARC:0x(3030),#IV:0x(0000000000000000)")]
    [TestCase("#ARC:0x(3030),#MACLEN:8")]
    [TestCase("#ARC:0x(3030),#CSU:0x(00000000)")]
    [TestCase("#ARC:0x(3030),#ADATA:0x(01)")]
    [TestCase("#ARC:0x(3030),#VARIANT:\"IAD\"")]
    [TestCase("#ARC:0x(3030),#EXPECTED:0x(D3A78E1517C22F70)")]
    public void InvalidParametersAreRejectedForBothDeclarationForms(string parameters)
    {
        foreach (bool direct in new[] { false, true })
        {
            EmvAcFixtures.Reset();
            string declaration = direct ? $"PARAM p=#MECH:{EmvArpcFixtures.Method1} {parameters.Replace(',', ' ')} " :
                $"PARAM p=Parameters({EmvArpcFixtures.Method1}{(parameters.Length == 0 ? "" : "," + parameters)}) ";
            Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvArpcFixtures.Prefix() + declaration + "VAR arpc=Mac(p,sk,arqc)"));
            Assert.That(VariableDictionary.Instance().Contains("arpc"), Is.False);
        }
    }

    [TestCase("0x()")]
    [TestCase("0x(30GG)")]
    [TestCase("0x(3030)junk")]
    public void HostSuppliedInvalidArcIsRejected(string value)
    {
        var p = new MAC_EMV_ARPC_METHOD_1().GenerateParameters(EmvArpcFixtures.Method1, new[] { "#ARC:0x(3030)" });
        p.SetParameter("#ARC", value);
        Assert.Throws<ArgumentException>(() => MechanismParameterContractValidator.Validate(EmvArpcFixtures.Method1, CryptoScriptFunction.Mac, p));
    }

    [Test]
    public void WellFormedUnverifiedArqcRemainsIndependentlyComputable()
    {
        // Fixed Python cryptography/OpenSSL ECB characterization, ARQC all zero, ARC 3030.
        EmvAcFixtures.Execute(EmvArpcFixtures.Prefix() + $"PARAM p=Parameters({EmvArpcFixtures.Method1},#ARC:0x(3030)) " +
            "VAR arpc=Mac(p,sk,0x(0000000000000000))");
        EmvArpcFixtures.AssertResult("arpc", "0044303EC48D778E", 8);
    }
}
