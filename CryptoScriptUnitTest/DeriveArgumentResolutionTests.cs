using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DeriveArgumentResolutionTests
{
    private const string HkdfKey = "0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B";
    private const string AesInitialKey = "1273671EA26AC29AFA4D1084127652A1";
    private const string AesKsn = "123456789012345600000001";

    [SetUp]
    public void SetUp() => VariableDictionary.Instance().Clear();

    [Test]
    public void StructuredResolverReturnsTheExactReferencedKey()
    {
        ParameterVariableDeclaration parameter = HkdfParameters();
        KeyVariableDeclaration key = Key("selected", HkdfKey, KeyAlgorithm.Hmac);
        var arguments = StructuredArguments(
            parameter, new AlgorithmCallArgument(key.Value, key), "\"\"");

        KeyVariableDeclaration resolved = KdfArgumentResolver.ResolveKey(arguments, 1);
        KeyVariableDeclaration result = new KDF_HKDF().Derive(arguments);

        resolved.Should().BeSameAs(key);
        FormatConversions.HexStringToByteArray(result.Value).Should().HaveCount(32);
    }

    [Test]
    public void StructuredResolverKeepsEqualValueKeysDistinct()
    {
        ParameterVariableDeclaration parameter = HkdfParameters();
        KeyVariableDeclaration first = Key("first", HkdfKey, KeyAlgorithm.Hmac);
        KeyVariableDeclaration second = Key("second", HkdfKey, KeyAlgorithm.Unknown);
        var arguments = StructuredArguments(
            parameter, new AlgorithmCallArgument(second.Value, second), "\"\"");

        KdfArgumentResolver.ResolveKey(arguments, 1).Should().BeSameAs(second);
        KdfArgumentResolver.ResolveKey(arguments, 1).Should().NotBeSameAs(first);
    }

    [TestCase("0x(00112233445566778899AABBCCDDEEFF)")]
    [TestCase("b64(ABEiM0RVZneImaq7zN3u/w==)")]
    [TestCase("\"secret key\"")]
    public void StructuredCryptoOperationsRejectsNonKeyLiterals(string literal)
    {
        Action action = () => new CryptoOperations().Derive(StructuredInvocation(literal));

        action.Should().Throw<ArgumentException>()
            .WithMessage("wrong key argument");
    }

    [Test]
    public void StructuredCryptoOperationsRejectsJsonKeyLiteral()
    {
        string json = Key("json", HkdfKey, KeyAlgorithm.Hmac).Serialize();

        Action action = () => new CryptoOperations().Derive(StructuredInvocation(json));

        action.Should().Throw<ArgumentException>()
            .WithMessage("wrong key argument");
    }

    [Test]
    public void StructuredCryptoOperationsRejectsVarAsKey()
    {
        ParameterVariableDeclaration parameter = HkdfParameters();
        var variable = new StringVariableDeclaration
        {
            Id = "data",
            Value = $"0x({HkdfKey})",
            ValueFormat = FormatConversions.HEX,
            Type = new CryptoTypeVar()
        };

        Action action = () => new CryptoOperations().Derive(new OperationInvocation(new[]
        {
            new ResolvedCallArgument(parameter.Value, ResolvedCallArgumentKind.Variable, parameter),
            new ResolvedCallArgument(variable.Value, ResolvedCallArgumentKind.Variable, variable),
            new ResolvedCallArgument("\"\"", ResolvedCallArgumentKind.Expression)
        }));

        action.Should().Throw<ArgumentException>()
            .WithMessage("wrong key argument");
    }

    [Test]
    public void StructuredCryptoOperationsRejectsParameterAsKey()
    {
        ParameterVariableDeclaration parameter = HkdfParameters();

        Action action = () => new CryptoOperations().Derive(new OperationInvocation(new[]
        {
            new ResolvedCallArgument(parameter.Value, ResolvedCallArgumentKind.Variable, parameter),
            new ResolvedCallArgument(parameter.Value, ResolvedCallArgumentKind.Variable, parameter),
            new ResolvedCallArgument("\"\"", ResolvedCallArgumentKind.Expression)
        }));

        action.Should().Throw<ArgumentException>()
            .WithMessage("wrong key argument");
    }

    [Test]
    public void LegacyDeriveResolvesTheRegisteredKeyByValueAndPreservesMetadata()
    {
        ParameterVariableDeclaration parameter = HkdfParameters();
        KeyVariableDeclaration key = Key("ikm", HkdfKey, KeyAlgorithm.Hmac);
        key.DerivationMechanism = "legacy-source";
        VariableDictionary.Instance().Add(key);

        AlgorithmCallArguments legacy = LegacyDeriveArgumentAdapter.Create(
            new[] { parameter.Value, key.Value, "\"\"" });

        KdfArgumentResolver.ResolveKey(legacy, 1).Should().BeSameAs(key);
        new KDF_HKDF().Derive(new[] { parameter.Value, key.Value, "\"\"" })
            .Should().BeOfType<KeyVariableDeclaration>();
    }

    [Test]
    public void LegacyDeriveRejectsAmbiguousKeyValues()
    {
        ParameterVariableDeclaration parameter = HkdfParameters();
        KeyVariableDeclaration first = Key("first", HkdfKey, KeyAlgorithm.Hmac);
        KeyVariableDeclaration second = Key("second", HkdfKey, KeyAlgorithm.Hmac);
        VariableDictionary.Instance().Add(first);
        VariableDictionary.Instance().Add(second);

        Action action = () => new KDF_HKDF().Derive(
            new[] { parameter.Value, first.Value, "\"\"" });

        action.Should().Throw<ArgumentException>()
            .WithMessage("Ambiguous KEY argument: multiple KEY variables have the same value.");
    }

    [Test]
    public void LegacyDeriveRejectsUnregisteredKeyValue()
    {
        ParameterVariableDeclaration parameter = HkdfParameters();

        Action action = () => new KDF_HKDF().Derive(new[]
        {
            parameter.Value,
            "0x(00112233445566778899AABBCCDDEEFF)",
            "\"\""
        });

        action.Should().Throw<ArgumentException>()
            .WithMessage("wrong key argument");
    }

    [Test]
    public void DukptAesLegacyAndStructuredPathsProduceTheSameWorkingKey()
    {
        var algorithm = new DUKPT_AES_WORKING_KEY();
        ParameterVariableDeclaration parameter = algorithm.GenerateParameters(
            "DUKPT-AES-WORKING-KEY", new[] { "#USAGE:PIN", "#KEYTYPE:AES-128" });
        KeyVariableDeclaration key = Key("initial", AesInitialKey, KeyAlgorithm.Aes);
        key.DerivationMechanism = "DUKPT-AES-INITIAL-KEY";
        VariableDictionary.Instance().Add(key);

        KeyVariableDeclaration structured = algorithm.Derive(StructuredArguments(
            parameter, new AlgorithmCallArgument(key.Value, key), $"0x({AesKsn})"));
        KeyVariableDeclaration legacy = (KeyVariableDeclaration)new CryptoOperations().Derive(
            parameter.Value,
            key.Value,
            $"0x({AesKsn})");

        legacy.Value.Should().Be(structured.Value);
        legacy.KeyType.Should().Be(structured.KeyType);
        legacy.KeySizeInBits.Should().Be(structured.KeySizeInBits);
        legacy.Usage.Should().Be(structured.Usage);
        legacy.DerivationMechanism.Should().Be(structured.DerivationMechanism);
    }

    private static OperationInvocation StructuredInvocation(string keyValue)
    {
        ParameterVariableDeclaration parameter = HkdfParameters();
        return new OperationInvocation(new[]
        {
            new ResolvedCallArgument(parameter.Value, ResolvedCallArgumentKind.Variable, parameter),
            new ResolvedCallArgument(keyValue, ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument("\"\"", ResolvedCallArgumentKind.Expression)
        });
    }

    private static AlgorithmCallArguments StructuredArguments(
        ParameterVariableDeclaration parameter,
        AlgorithmCallArgument key,
        string data) =>
        new(new[]
        {
            new AlgorithmCallArgument(parameter.Value, parameter),
            key,
            new AlgorithmCallArgument(data)
        });

    private static ParameterVariableDeclaration HkdfParameters() =>
        new KDF_HKDF().GenerateParameters(
            "KDF-HKDF", new[] { "#HASH:HASH-SHA256", "#OUTLEN:256" });

    private static KeyVariableDeclaration Key(
        string id,
        string hex,
        KeyAlgorithm algorithm)
    {
        string value = $"0x({hex})";
        return new KeyVariableDeclaration
        {
            Id = id,
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            KeySize = (hex.Length * 4).ToString(),
            KeyType = KeyType.Secret(algorithm),
            Type = new CryptoTypeKey()
        };
    }
}
