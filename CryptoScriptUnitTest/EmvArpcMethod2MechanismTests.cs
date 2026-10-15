using CryptoScript.CryptoAlgorithm.EMV;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvArpcMethod2MechanismTests
{
    [SetUp]
    public void SetUp() => EmvAcFixtures.Reset();

    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (var v in EmvArpcFixtures.All)
            for (int index = 0; index < EmvArpcFixtures.Proprietary.Length; index++)
                foreach (bool iad in new[] { false, true })
                    foreach (bool direct in new[] { false, true })
                        foreach (bool literal in new[] { false, true })
                            yield return new TestCaseData(v, index, iad, direct, literal)
                                .SetCategory(iad ? "EmvArpcIad" : "EmvArpcOnly");
    }

    [TestCaseSource(nameof(Cases))]
    public void Method2AndIadMatchAllFixedCharacterizations(object vector, int index, bool iad, bool direct, bool literal)
    {
        var v = (EmvArpcFixtures.Vector)vector;
        string named = "#CSU:0x(00000000)" +
            (index == 0 ? "" : ",#ADATA:0x(" + EmvArpcFixtures.Proprietary[index] + ")") +
            (iad ? ",#VARIANT:\"IAD\"" : "");
        string declaration = direct ? $"PARAM p=#MECH:{EmvArpcFixtures.Method2} {named.Replace(',', ' ')} " :
            $"PARAM p=Parameters({EmvArpcFixtures.Method2},{named}) ";
        EmvAcFixtures.Execute(EmvArpcFixtures.Prefix(v) + declaration +
            $"VAR result=Mac(p,sk,{(literal ? "0x(" + v.Arqc + ")" : "arqc")})");
        EmvArpcFixtures.AssertResult("result", iad ? v.Iad[index] : v.M2[index],
            iad ? 8 + EmvArpcFixtures.Proprietary[index].Length / 2 : 4);
    }

    public static IEnumerable<TestCaseData> Vectors() => EmvArpcFixtures.All.Select(v => new TestCaseData(v));

    [TestCaseSource(nameof(Vectors))]
    [Category("EmvArpcIad")]
    public void NonzeroCsuRetainsExactDataOrderWithoutTlv(object vector)
    {
        var v = (EmvArpcFixtures.Vector)vector;
        EmvAcFixtures.Execute(EmvArpcFixtures.Prefix(v) +
            $"PARAM p=Parameters({EmvArpcFixtures.Method2},#CSU:0x(AABBCCDD),#ADATA:0x(01020304),#VARIANT:\"IAD\") " +
            "VAR iad=Mac(p,sk,arqc)");
        EmvArpcFixtures.AssertResult("iad", v.NonzeroCsuIad, 12);
    }

    [TestCase("")]
    [TestCase("#CSU:0x(000000)")]
    [TestCase("#CSU:0x(0000000000)")]
    [TestCase("#CSU:\"00000000\"")]
    [TestCase("#CSU:0")]
    [TestCase("#CSU:b64(AAAAAA==)")]
    [TestCase("#CSU:0x(00000000),#ADATA:0x(010203040506070809)")]
    [TestCase("#CSU:0x(00000000),#ADATA:\"01\"")]
    [TestCase("#CSU:0x(00000000),#ADATA:b64(AQ==)")]
    [TestCase("#CSU:0x(00000000),#ADATA:1")]
    [TestCase("#CSU:0x(00000000),#VARIANT:\"ARPC\"")]
    [TestCase("#CSU:0x(00000000),#VARIANT:\"iad\"")]
    [TestCase("#CSU:0x(00000000),#VARIANT:1")]
    [TestCase("#CSU:0x(00000000),#VARIANT:0x(494144)")]
    [TestCase("#CSU:0x(00000000),#VARIANT:IAD")]
    [TestCase("#CSU:0x(00000000),#VARIANT:\"IAD\",#VARIANT:\"IAD\"")]
    [TestCase("#CSU:0x(00000000),#CSU:0x(00000000)")]
    [TestCase("#CSU:0x(00000000),#ADATA:0x(01),#ADATA:0x(02)")]
    [TestCase("#CSU:0x(00000000),#PAD:NONE")]
    [TestCase("#CSU:0x(00000000),#IV:0x(0000000000000000)")]
    [TestCase("#CSU:0x(00000000),#MACLEN:4")]
    [TestCase("#CSU:0x(00000000),#ARC:0x(3030)")]
    [TestCase("#CSU:0x(00000000),#EXPECTED:0x(D3A78E1517C22F70)")]
    public void InvalidParametersAreRejectedForBothDeclarationForms(string parameters)
    {
        foreach (bool direct in new[] { false, true })
        {
            EmvAcFixtures.Reset();
            string declaration = direct ? $"PARAM p=#MECH:{EmvArpcFixtures.Method2} {parameters.Replace(',', ' ')} " :
                $"PARAM p=Parameters({EmvArpcFixtures.Method2}{(parameters.Length == 0 ? "" : "," + parameters)}) ";
            Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvArpcFixtures.Prefix() + declaration + "VAR result=Mac(p,sk,arqc)"));
            Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
        }
    }

    [TestCase("#CSU", "0x()")]
    [TestCase("#CSU", "0x(0000GG00)")]
    [TestCase("#ADATA", "0x()")]
    [TestCase("#ADATA", "0x(0)")]
    [TestCase("#ADATA", "0x(010203040506070809)")]
    public void HostSuppliedInvalidBinaryParametersAreRejected(string name, string value)
    {
        var p = new MAC_EMV_ARPC_METHOD_2().GenerateParameters(EmvArpcFixtures.Method2, new[] { "#CSU:0x(00000000)" });
        p.SetParameter(name, value);
        Assert.Throws<ArgumentException>(() => MechanismParameterContractValidator.Validate(EmvArpcFixtures.Method2, CryptoScriptFunction.Mac, p));
    }

    [Test]
    public void WellFormedUnverifiedArqcRemainsIndependentlyComputable()
    {
        // Fixed independent OpenSSL CMAC value over 12 zero bytes, leftmost four.
        EmvAcFixtures.Execute(EmvArpcFixtures.Prefix() + $"PARAM p=Parameters({EmvArpcFixtures.Method2},#CSU:0x(00000000)) " +
            "VAR arpc=Mac(p,sk,0x(0000000000000000))");
        EmvArpcFixtures.AssertResult("arpc", "732F8E2D", 4);
    }
}
