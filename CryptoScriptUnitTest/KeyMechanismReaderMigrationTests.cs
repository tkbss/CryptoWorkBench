using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;
using CryptoScript.ErrorListner;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KeyMechanismReaderMigrationTests
{
    private const string KeyMaterial = "0x(0123456789ABCDEFFEDCBA9876543210)";

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
    public void Des3WarnsForKnownNonTdeaSecretKeys(KeyAlgorithm algorithm)
    {
        var sink = new SemanticDiagnosticSink();
        Encrypt(new KeyVariableDeclaration { KeyType = KeyType.Secret(algorithm), Value = KeyMaterial,
            ValueFormat = FormatConversions.HEX }, sink);
        Assert.That(sink.GetSnapshot().Single().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyTypeConflict));
    }

    [Test]
    public void Des3WarnsForRsaAndEcMetadataWithTechnicallyValidBytes()
    {
        Action rsa = () => Encrypt(KeyType.Public(KeyAlgorithm.Rsa));
        Action ec = () => Encrypt(KeyType.Private(KeyAlgorithm.Ec));

        rsa.Should().NotThrow();
        ec.Should().NotThrow();
    }

    [TestCase("DES3-CBC", false)]
    [TestCase("AES-CBC", true)]
    [TestCase("HMAC-SHA256", true)]
    public void Des3PreservesLegacyJsonCompatibility(string legacyMechanism, bool warns)
    {
        string json = $"{{\"Mechanism\":\"{legacyMechanism}\",\"Value\":\"{KeyMaterial}\",\"KeyValue\":\"{KeyMaterial}\",\"ValueFormat\":\"HEX_STRING\"}}";
        KeyVariableDeclaration key = KeyVariableDeclaration.Deserialize(json);

        var sink = new SemanticDiagnosticSink();
        Encrypt(key, sink);
        Assert.That(sink.GetSnapshot().Count, Is.EqualTo(warns ? 1 : 0));
        if (warns) Assert.That(sink.GetSnapshot()[0].DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyTypeConflict));
    }

    private static void Encrypt(KeyType type) =>
        Encrypt(new KeyVariableDeclaration
        {
            KeyType = type,
            Value = KeyMaterial,
            ValueFormat = FormatConversions.HEX
        });

    private static void Encrypt(KeyVariableDeclaration key, ISemanticDiagnosticSink? sink = null)
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

        _ = new DES3().Encrypt(new CryptoScript.CryptoAlgorithm.AlgorithmCallArguments(new[]
        {
            new CryptoScript.CryptoAlgorithm.AlgorithmCallArgument(parameter.Value, parameter, ResolvedCallArgumentKind.Variable),
            new CryptoScript.CryptoAlgorithm.AlgorithmCallArgument(key.Value, key, ResolvedCallArgumentKind.Variable),
            new CryptoScript.CryptoAlgorithm.AlgorithmCallArgument(data.Value, kind: ResolvedCallArgumentKind.HexLiteral)
        }, sink));
    }
}
