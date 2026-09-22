using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.CryptoAlgorithm.HMAC;
using CryptoScript.Variables;
using FluentAssertions;
using Newtonsoft.Json;

namespace CryptoScriptUnitTest;

public class KeyTypeMigrationTests
{
    private static readonly string[] AesMechanisms =
    {
        "AES-ECB", "AES-CBC", "AES-CTR", "AES-CMAC", "AES-GCM", "AES-GMAC", "AES-CCM"
    };

    private static readonly string[] TdeaMechanisms =
    {
        "DES3-ECB", "DES3-CBC", "DES3-CMAC", "DES3-RETAIL"
    };

    private static readonly string[] HmacMechanisms =
    {
        "HMAC-SHA1", "HMAC-SHA224", "HMAC-SHA256", "HMAC-SHA384", "HMAC-SHA512",
        "HMAC-SHA512-224", "HMAC-SHA512-256", "HMAC-SHA3-224", "HMAC-SHA3-256",
        "HMAC-SHA3-384", "HMAC-SHA3-512"
    };

    [TestCaseSource(nameof(AesMechanisms))]
    public void EveryAesGenerateKeyMechanismAddsAesSecretMetadata(string mechanism)
    {
        KeyVariableDeclaration key = new AES().GenerateKey(mechanism, "128");

        AssertMetadata(key, mechanism, KeyAlgorithm.Aes, 128);
    }

    [TestCaseSource(nameof(TdeaMechanisms))]
    public void EveryTdeaGenerateKeyMechanismAddsTdeaSecretMetadata(string mechanism)
    {
        KeyVariableDeclaration key = new DES3().GenerateKey(mechanism, "128");

        AssertMetadata(key, mechanism, KeyAlgorithm.Tdea, 128);
    }

    [TestCaseSource(nameof(HmacMechanisms))]
    public void EveryHmacGenerateKeyMechanismAddsHmacSecretMetadata(string mechanism)
    {
        KeyVariableDeclaration key = new HMAC().GenerateKey(mechanism, "136");

        AssertMetadata(key, mechanism, KeyAlgorithm.Hmac, 136);
    }

    [Test]
    public void KeySizeRejectsNegativeBitCounts()
    {
        Action action = () => _ = new KeySize(-1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(KeyAlgorithm.Aes, KeyMaterialKind.Public)]
    [TestCase(KeyAlgorithm.Tdea, KeyMaterialKind.Public)]
    [TestCase(KeyAlgorithm.Hmac, KeyMaterialKind.Private)]
    [TestCase(KeyAlgorithm.Rsa, KeyMaterialKind.Secret)]
    [TestCase(KeyAlgorithm.Ec, KeyMaterialKind.Secret)]
    public void InvalidAlgorithmAndMaterialCombinationsAreRejected(
        KeyAlgorithm algorithm, KeyMaterialKind materialKind)
    {
        Action action = () => _ = new KeyType(algorithm, materialKind);

        action.Should().Throw<ArgumentException>();
    }

    [TestCase(KeyAlgorithm.Rsa, KeyMaterialKind.Public)]
    [TestCase(KeyAlgorithm.Rsa, KeyMaterialKind.Private)]
    [TestCase(KeyAlgorithm.Ec, KeyMaterialKind.Public)]
    [TestCase(KeyAlgorithm.Ec, KeyMaterialKind.Private)]
    public void AsymmetricPublicAndPrivateTypesAreValid(
        KeyAlgorithm algorithm, KeyMaterialKind materialKind)
    {
        KeyType type = materialKind == KeyMaterialKind.Public
            ? KeyType.Public(algorithm)
            : KeyType.Private(algorithm);

        type.Algorithm.Should().Be(algorithm);
        type.MaterialKind.Should().Be(materialKind);
    }

    [TestCase("{\"Algorithm\":999,\"MaterialKind\":0}")]
    [TestCase("{\"Algorithm\":1,\"MaterialKind\":999}")]
    [TestCase("{\"Algorithm\":1,\"MaterialKind\":1}")]
    public void InvalidJsonCannotBypassKeyTypeInvariants(string json)
    {
        Action action = () => JsonConvert.DeserializeObject<KeyType>(json);

        action.Should().Throw<Exception>();
    }

    [Test]
    public void ExplicitNullKeyTypeIsRejectedDuringKeyDeserialization()
    {
        Action action = () => KeyVariableDeclaration.Deserialize("{\"KeyType\":null}");

        action.Should().Throw<Exception>();
    }

    [Test]
    public void SettingEitherSizeRepresentationSynchronizesTheOther()
    {
        var legacyFirst = new KeyVariableDeclaration { KeySize = "128" };
        var typedFirst = new KeyVariableDeclaration { KeySizeInBits = new KeySize(256) };

        legacyFirst.KeySizeInBits.Should().Be(new KeySize(128));
        typedFirst.KeySize.Should().Be("256");
    }

    [Test]
    public void ConflictingCodeSizeAssignmentsAreRejectedInEitherOrder()
    {
        Action legacyFirst = () => _ = new KeyVariableDeclaration
        {
            KeySize = "256",
            KeySizeInBits = new KeySize(128)
        };
        Action typedFirst = () => _ = new KeyVariableDeclaration
        {
            KeySizeInBits = new KeySize(128),
            KeySize = "256"
        };

        legacyFirst.Should().Throw<ArgumentException>();
        typedFirst.Should().Throw<ArgumentException>();
    }

    [Test]
    public void KeySizeSupportsTheExistingUnsignedSp800108Range()
    {
        ulong bits = (ulong)int.MaxValue + 1;

        new KeySize(bits).Bits.Should().Be(bits);
    }

    [TestCase("{\"KeySize\":\"256\",\"KeySizeInBits\":{\"Bits\":128}}")]
    [TestCase("{\"KeySizeInBits\":{\"Bits\":128},\"KeySize\":\"256\"}")]
    public void ConflictingJsonSizeRepresentationsAreRejectedInEitherOrder(string json)
    {
        Action action = () => KeyVariableDeclaration.Deserialize(json);

        action.Should().Throw<Exception>();
    }

    private static void AssertMetadata(
        KeyVariableDeclaration key, string legacyMechanism, KeyAlgorithm algorithm, int bits)
    {
        key.KeyType.Should().Be(KeyType.Secret(algorithm));
        key.KeySizeInBits.Should().Be(new KeySize(bits));
        key.KeySize.Should().Be(bits.ToString());
        key.Mechanism.Should().Be(legacyMechanism);
        FormatConversions.HexStringToByteArray(key.KeyValue).Length.Should().Be(bits / 8);
    }
}
