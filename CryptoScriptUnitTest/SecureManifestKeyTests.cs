using System.Security.Cryptography;
using CryptoScript;
using CryptoScript.Model;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

public class SecureManifestKeyTests
{
    private static SecureKeyManifest Project(KeyVariableDeclaration key)
    {
        var serializer = new VariableSnapshotJsonSerializer();
        return SecureManifest.Create(serializer.Deserialize(serializer.Serialize(new[] { key }))).Single().Key!;
    }

    [TestCase(KeyAlgorithm.Aes,16,32)]
    [TestCase(KeyAlgorithm.Aes,24,32)]
    [TestCase(KeyAlgorithm.Aes,32,32)]
    [TestCase(KeyAlgorithm.Tdea,16,16)]
    [TestCase(KeyAlgorithm.Tdea,24,16)]
    [TestCase(KeyAlgorithm.Hmac,32,0)]
    [TestCase(KeyAlgorithm.Unknown,19,0)]
    public void SymmetricKeySizeAndFullKcvFollowExistingSemantics(KeyAlgorithm algorithm,int length,int kcvHexLength)
    {
        var key = new KeyVariableDeclaration { Id="key",ValueFormat=FormatConversions.HEX,
            Value=Convert.ToHexString(Enumerable.Range(1,length).Select(i=>(byte)i).ToArray()),
            KeyType=KeyType.Secret(algorithm) };
        var manifest = Project(key);
        Assert.That(manifest.Algorithm,Is.EqualTo(algorithm.ToString()));
        Assert.That(manifest.BitLength,Is.EqualTo((ulong)length*8));
        Assert.That(manifest.Kcv,Is.EqualTo(key.Kcv));
        if (kcvHexLength == 0) Assert.That(manifest.Kcv,Is.Null);
        else Assert.That(manifest.Kcv,Does.Match("^0x\\([0-9a-f]{"+kcvHexLength+"}\\)$"));
        Assert.That(key.KeySizeInBits.IsKnown,Is.False);
    }

    [TestCase(KeyAlgorithm.Rsa,KeyMaterialKind.Public)]
    [TestCase(KeyAlgorithm.Rsa,KeyMaterialKind.Private)]
    [TestCase(KeyAlgorithm.Ec,KeyMaterialKind.Public)]
    [TestCase(KeyAlgorithm.Ec,KeyMaterialKind.Private)]
    public void AsymmetricKeyUsesMathematicalBitLengthAndNullKcv(KeyAlgorithm algorithm,KeyMaterialKind kind)
    {
        using AsymmetricAlgorithm generator = algorithm == KeyAlgorithm.Rsa ? RSA.Create(2048) : ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] bytes = kind == KeyMaterialKind.Private ? generator.ExportPkcs8PrivateKey() : generator.ExportSubjectPublicKeyInfo();
        try
        {
            var key = new KeyVariableDeclaration { Id="key",Type=new CryptoTypeKey(),ValueFormat=FormatConversions.B64,
                Value=Convert.ToBase64String(bytes),KeyType=new(algorithm,kind),KeySizeInBits=new KeySize(generator.KeySize) };
            var manifest = Project(key);
            Assert.That(manifest.BitLength,Is.EqualTo((ulong)generator.KeySize));
            Assert.That(manifest.Algorithm,Is.EqualTo(algorithm.ToString()));
            Assert.That(manifest.Kcv,Is.Null);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
