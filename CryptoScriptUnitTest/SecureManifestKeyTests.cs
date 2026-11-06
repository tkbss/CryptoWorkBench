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
}
