using System.Security.Cryptography;
using System.Text;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

public class As01CompatibilityTests
{
    // Captured with the unchanged serializer at c3586fe5b07116fb314fddc46148e78d679e808b.
    // All material and the password are public test data.
    private static JObject Fixture() => JObject.Parse(File.ReadAllText(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "Fixtures", "AS01", "symmetric-baseline.json")));

    private static string PlatformLines(string value) => value.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);

    [Test]
    public void PreChangeSymmetricKeysKeepExactLegacyAndSnapshotSerialization()
    {
        var fixture = Fixture();
        var serializer = new VariableSnapshotJsonSerializer();
        string snapshot = PlatformLines((string)fixture["snapshot"]!);
        var keys = serializer.Deserialize(snapshot).Cast<KeyVariableDeclaration>().ToArray();
        Assert.That(keys.Select(k => k.Serialize()), Is.EqualTo(fixture["legacy"]!.Values<string>()));
        var legacyKeys = fixture["legacy"]!.Values<string>().Select(j => KeyVariableDeclaration.Deserialize(j!)).ToArray();
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.That(legacyKeys[i].KeyType, Is.EqualTo(keys[i].KeyType));
            Assert.That(legacyKeys[i].KeySizeInBits, Is.EqualTo(keys[i].KeySizeInBits));
            Assert.That(legacyKeys[i].KeySize, Is.EqualTo(keys[i].KeySize));
            Assert.That(legacyKeys[i].Value, Is.EqualTo(keys[i].Value));
            Assert.That(legacyKeys[i].Usage, Is.EqualTo(keys[i].Usage));
            Assert.That(legacyKeys[i].DerivationMechanism, Is.EqualTo(keys[i].DerivationMechanism));
            Assert.That((legacyKeys[i].Kcv, legacyKeys[i].Kcvc, legacyKeys[i].Kcvk),
                Is.EqualTo((keys[i].Kcv, keys[i].Kcvc, keys[i].Kcvk)));
        }
        Assert.That(serializer.Serialize(keys), Is.EqualTo(snapshot));
        Assert.That(serializer.Serialize(serializer.Deserialize(snapshot)), Is.EqualTo(snapshot));
        var manifest = SecureManifest.Create(keys);
        Assert.That(SecureManifest.Json(manifest), Is.EqualTo(Convert.FromBase64String((string)fixture["manifestJson"]!)));
        Assert.That(SecureManifest.Frame(manifest), Is.EqualTo(Convert.FromBase64String((string)fixture["manifestFrame"]!)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PreChangeSecureContainersStillAuthenticateAndSerializeIdentically(bool gmac)
    {
        var fixture = Fixture();
        byte[] expected = Convert.FromBase64String((string)fixture["envelopes"]![gmac ? 1 : 0]!);
        using var envelope = SecureEnvelope.Parse(expected);
        byte[] plaintext = SecureContainerCryptography.Unprotect(envelope,
            Encoding.UTF8.GetBytes("AS-01 fixture password"), default);
        try
        {
            Assert.That(Encoding.UTF8.GetString(plaintext), Is.EqualTo((string)fixture["snapshot"]!));
            var keys = new VariableSnapshotJsonSerializer().Deserialize(Encoding.UTF8.GetString(plaintext));
            var manifest = SecureManifest.Create(keys);
            Assert.That(manifest, Is.EqualTo(envelope.Manifest));
            Assert.That(SecureContainerCryptography.Protect(envelope.Options, plaintext,
                SecureManifest.Json(manifest), SecureManifest.Frame(manifest),
                Encoding.UTF8.GetBytes("AS-01 fixture password"), envelope.Salt, envelope.Nonce), Is.EqualTo(expected));
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    [Test]
    public void PersistedSymmetricEnumValuesRemainStable()
    {
        Assert.That(Enum.GetValues<KeyAlgorithm>(), Is.EqualTo(new[]
            { KeyAlgorithm.Unknown, KeyAlgorithm.Aes, KeyAlgorithm.Tdea, KeyAlgorithm.Hmac }));
        Assert.That(Enum.GetValues<KeyAlgorithm>().Select(a => (int)a), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        Assert.That(Enum.GetValues<KeyMaterialKind>(), Is.EqualTo(new[] { KeyMaterialKind.Secret }));
        Assert.That((int)KeyMaterialKind.Secret, Is.Zero);
    }

    [Test]
    public void RetiredNumericKeyTypesCannotBeConstructedOrLoadedFromLegacyJson()
    {
        foreach (int algorithm in new[] { 4, 5 })
        foreach (int kind in new[] { 0, 1, 2 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyType((KeyAlgorithm)algorithm, (KeyMaterialKind)kind));
            AssertLegacyRejection($"{{\"KeyType\":{{\"Algorithm\":{algorithm},\"MaterialKind\":{kind}}}}}");
        }
    }

    [TestCase("Rsa")]
    [TestCase("Ec")]
    public void RetiredNamedKeyTypesCannotBeLoadedFromLegacyJson(string algorithm)
    {
        foreach (string kind in new[] { "Secret", "Public", "Private" })
            AssertLegacyRejection($"{{\"KeyType\":{{\"Algorithm\":\"{algorithm}\",\"MaterialKind\":\"{kind}\"}}}}");
    }

    [TestCase("Public", 1)]
    [TestCase("Private", 2)]
    public void RetiredMaterialKindsAreRejectedForEverySymmetricAlgorithm(string name, int value)
    {
        foreach (var algorithm in Enum.GetValues<KeyAlgorithm>())
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyType(algorithm, (KeyMaterialKind)value));
            AssertLegacyRejection($"{{\"KeyType\":{{\"Algorithm\":{(int)algorithm},\"MaterialKind\":{value}}}}}");
            AssertLegacyRejection($"{{\"KeyType\":{{\"Algorithm\":\"{algorithm}\",\"MaterialKind\":\"{name}\"}}}}");
            AssertSnapshotRejection(algorithm.ToString(), name);
        }
    }

    [TestCase("Rsa")]
    [TestCase("Ec")]
    [TestCase("4")]
    [TestCase("5")]
    public void RetiredSnapshotAlgorithmsAreRejectedWithValidMaterial(string algorithm)
    {
        foreach (string kind in new[] { "Secret", "Public", "Private" })
            AssertSnapshotRejection(algorithm, kind);
    }

    private static void AssertLegacyRejection(string json)
    {
        var error = Assert.Catch(() => KeyVariableDeclaration.Deserialize(json));
        Assert.That(error, Is.InstanceOf<ArgumentException>().Or.InstanceOf<JsonSerializationException>());
    }

    private static void AssertSnapshotRejection(string algorithm, string kind)
    {
        var json = JObject.Parse((string)Fixture()["snapshot"]!);
        var entry = json["variables"]![0]!;
        entry["metadata"]!["keyType"]!["algorithm"] = algorithm;
        entry["metadata"]!["keyType"]!["materialKind"] = kind;
        // Keep the valid baseline material: rejection must depend on metadata alone.
        var error = Assert.Throws<JsonSerializationException>(() => new VariableSnapshotJsonSerializer().Deserialize(json.ToString()));
        Assert.That(error!.Message, Is.EqualTo("Invalid CryptoScript variable snapshot."));
        Assert.That(error.InnerException, Is.Null);
        var typeError = Assert.Throws<JsonSerializationException>(() => KeySnapshotCodec.ReadType(new(algorithm, kind)));
        Assert.That(typeError!.Message, Does.StartWith("Unsupported key"));
    }
}
