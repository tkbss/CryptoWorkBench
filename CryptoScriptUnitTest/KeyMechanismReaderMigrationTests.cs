using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KeyMechanismReaderMigrationTests
{
    private const string KeyValue = "0x(0123456789ABCDEFFEDCBA9876543210)";

    [SetUp]
    public void SetUp() => VariableDictionary.Instance().Clear();

    [TestCase(KeyAlgorithm.Tdea)]
    [TestCase(KeyAlgorithm.Unknown)]
    public void Des3AcceptsTdeaAndUnknownKeys(KeyAlgorithm algorithm)
    {
        KeyType type = KeyType.Secret(algorithm);

        Action action = () => Encrypt(type);

        action.Should().NotThrow();
    }

    [TestCase(KeyAlgorithm.Aes)]
    [TestCase(KeyAlgorithm.Hmac)]
    public void Des3RejectsKnownNonTdeaSecretKeys(KeyAlgorithm algorithm)
    {
        Action action = () => Encrypt(KeyType.Secret(algorithm));

        action.Should().Throw<ArgumentException>().WithMessage("*requires a DES3 key*");
    }

    [Test]
    public void Des3RejectsRsaAndEcKeys()
    {
        Action rsa = () => Encrypt(KeyType.Public(KeyAlgorithm.Rsa));
        Action ec = () => Encrypt(KeyType.Private(KeyAlgorithm.Ec));

        rsa.Should().Throw<ArgumentException>().WithMessage("*requires a DES3 key*");
        ec.Should().Throw<ArgumentException>().WithMessage("*requires a DES3 key*");
    }

    [TestCase("DES3-CBC", false)]
    [TestCase("AES-CBC", true)]
    [TestCase("HMAC-SHA256", true)]
    public void Des3PreservesLegacyJsonCompatibility(string legacyMechanism, bool rejected)
    {
        string json = $"{{\"Mechanism\":\"{legacyMechanism}\",\"Value\":\"{KeyValue}\",\"KeyValue\":\"{KeyValue}\",\"ValueFormat\":\"HEX_STRING\"}}";
        KeyVariableDeclaration key = KeyVariableDeclaration.Deserialize(json);

        Action action = () => Encrypt(key);

        if (rejected)
            action.Should().Throw<ArgumentException>().WithMessage("*requires a DES3 key*");
        else
            action.Should().NotThrow();
    }

    private static void Encrypt(KeyType type) =>
        Encrypt(new KeyVariableDeclaration
        {
            KeyType = type,
            Value = KeyValue,
            KeyValue = KeyValue,
            ValueFormat = FormatConversions.HEX
        });

    private static void Encrypt(KeyVariableDeclaration key)
    {
        key.Id = "k";
        var parameter = new ParameterVariableDeclaration { Id = "p", Mechanism = "DES3-ECB" };
        parameter.SetParameter("PAD", "NONE");
        var data = new StringVariableDeclaration
        {
            Id = "d", Value = "0x(4E6F772069732074)", ValueFormat = FormatConversions.HEX
        };
        VariableDictionary.Instance().Add(key);
        VariableDictionary.Instance().Add(parameter);
        VariableDictionary.Instance().Add(data);

        _ = new DES3().Encrypt(["p", "k", "d"]);
    }
}
