using CryptoScript.CryptoAlgorithm;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

public class AesCbcDefaultPaddingTests
{
    private const string Key = "0x(00112233445566778899AABBCCDDEEFF)";
    private const string Iv = "0x(000102030405060708090A0B0C0D0E0F)";

    [Test]
    public void RuntimeDefaultParametersUseRandomSixteenByteIvAndPkcs7WithHyphen()
    {
        var algorithm = AlgorithmFactory.Create("AES-CBC");
        ParameterVariableDeclaration first = algorithm.GenerateParameters("AES-CBC");
        ParameterVariableDeclaration second = algorithm.GenerateParameters("AES-CBC");

        Assert.Multiple(() =>
        {
            Assert.That(first.GetParameter("MECH"), Is.EqualTo("AES-CBC"));
            Assert.That(FormatConversions.ToByteArray(first.GetParameter("IV"), FormatConversions.HEX), Has.Length.EqualTo(16));
            Assert.That(first.GetParameter("IV"), Is.Not.EqualTo(second.GetParameter("IV")));
            Assert.That(first.GetParameter("PAD"), Is.EqualTo("PKCS-7"));
        });
    }

    [Test]
    public void ExplicitParameterOverloadWithOnlyIvDefaultsToProcessablePkcs7()
    {
        var algorithm = AlgorithmFactory.Create("AES-CBC");
        ParameterVariableDeclaration parameters = algorithm.GenerateParameters(
            "AES-CBC", new[] { $"#IV:{Iv}" });
        KeyVariableDeclaration key = algorithm.GenerateKey("AES-CBC", Key);
        var data = new StringVariableDeclaration { Value = "0x(00)", ValueFormat = FormatConversions.HEX };

        StringVariableDeclaration encrypted = algorithm.Encrypt(
            new[] { parameters.Value, key.Value, data.Value });

        Assert.Multiple(() =>
        {
            Assert.That(parameters.GetParameter("IV"), Is.EqualTo(Iv));
            Assert.That(parameters.GetParameter("PAD"), Is.EqualTo("PKCS-7"));
            Assert.That(FormatConversions.ToByteArray(encrypted.Value, FormatConversions.HEX), Has.Length.EqualTo(16));
        });
    }

    [Test]
    public void ExplicitValidPaddingIsPreservedByParameterOverload()
    {
        var algorithm = AlgorithmFactory.Create("AES-CBC");

        ParameterVariableDeclaration parameters = algorithm.GenerateParameters(
            "AES-CBC", new[] { $"#IV:{Iv}", "#PAD:ISO-7816" });

        Assert.That(parameters.GetParameter("PAD"), Is.EqualTo("ISO-7816"));
    }
}
