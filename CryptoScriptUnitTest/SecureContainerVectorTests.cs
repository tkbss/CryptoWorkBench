using System.Security.Cryptography;
using System.Text;
using CryptoScript.Serialization;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScriptUnitTest;

public class SecureContainerVectorTests
{
    private static byte[] Hex(string text) => Convert.FromHexString(text);
    private static JArray Fixtures(string name) => JArray.Parse(File.ReadAllText(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "Fixtures", "SecureSave", name)));

    [Test] public void Argon2idRfc9106Section53()
    {
        var generator = new Argon2BytesGenerator();
        generator.Init(new Argon2Parameters.Builder(Argon2Parameters.Argon2id).WithVersion(19)
            .WithMemoryAsKB(32).WithIterations(3).WithParallelism(4)
            .WithSalt(Enumerable.Repeat((byte)2,16).ToArray()).WithSecret(Enumerable.Repeat((byte)3,8).ToArray())
            .WithAdditional(Enumerable.Repeat((byte)4,12).ToArray()).Build());
        var output = new byte[32]; generator.GenerateBytes(Enumerable.Repeat((byte)1,32).ToArray(), output);
        Assert.That(output, Is.EqualTo(Hex("0d640df58d78766c08c037a34a8b53c9d01ef0452d75b65eb52520e96b01e659")));
    }

    [Test] public void Pbkdf2Rfc7914Section11()
    {
        byte[] result = Rfc2898DeriveBytes.Pbkdf2("passwd"u8, "salt"u8, 1, HashAlgorithmName.SHA256, 64);
        Assert.That(result, Is.EqualTo(Hex("55ac046e56e3089fec1691c22544b605f94185216dde0465e68b9d57c20dacbc49ca9cccf179b645991664b39d77ef317c71b845b1e30bd509112041d3a19783")));
    }

    [Test] public void HkdfRfc5869AppendixA1()
    {
        byte[] result = HKDF.DeriveKey(HashAlgorithmName.SHA256, Enumerable.Repeat((byte)11,22).ToArray(), 42,
            Hex("000102030405060708090a0b0c"), Hex("f0f1f2f3f4f5f6f7f8f9"));
        Assert.That(result, Is.EqualTo(Hex("3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865")));
    }

    [TestCase(0)] [TestCase(1)]
    public void NistCavpAes256GcmAndGmac(int index)
    {
        var v = Fixtures("nist-gcm256.json")[index]; byte[] p = Hex((string)v["PT"]!);
        using var aes = new AesGcm(Hex((string)v["Key"]!),16);
        byte[] cipher = new byte[p.Length], tag = new byte[16];
        aes.Encrypt(Hex((string)v["IV"]!),p,cipher,tag,Hex((string)v["AAD"]!));
        Assert.That(cipher, Is.EqualTo(Hex((string)v["CT"]!)));
        Assert.That(tag, Is.EqualTo(Hex((string)v["Tag"]!)));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void FullContainerMatchesIndependentPythonOpenSslReferenceByteForByte(int index)
    {
        var v = Fixtures("vectors.json")[index];
        byte[] Field(string name) => Hex((string)v[name]!);
        var options = new SecureSaveOptions((bool)v["gmac"]!, (bool)v["pbkdf"]!);
        var entries = SecureManifest.Create(new VariableSnapshotJsonSerializer().Deserialize(Encoding.UTF8.GetString(Field("payloadHex"))));
        byte[] manifest = SecureManifest.Frame(entries), wire = SecureManifest.Json(entries);
        Assert.That(manifest, Is.EqualTo(Field("manifestHex")));
        Assert.That(SecureContainerCryptography.KeyInfo(options), Is.EqualTo(Field("infoHex")));
        Assert.That(SecureContainerCryptography.DeriveMaster(options, Field("passwordHex"), Field("saltHex")), Is.EqualTo(Field("masterHex")));
        Assert.That(HKDF.DeriveKey(HashAlgorithmName.SHA256, Field("masterHex"),32,new byte[32],Field("infoHex")), Is.EqualTo(Field("modeKeyHex")));
        Assert.That(SecureContainerCryptography.Header(options, Field("saltHex"),Field("nonceHex"),manifest,Field("payloadHex").Length), Is.EqualTo(Field("headerHex")));
        byte[] password = Field("passwordHex");
        byte[] result = SecureContainerCryptography.Protect(options,Field("payloadHex"),wire,manifest,password,Field("saltHex"),Field("nonceHex"));
        Assert.That(password, Is.All.Zero);
        Assert.That(result, Is.EqualTo(Field("envelopeHex")));
        string json = Encoding.UTF8.GetString(result);
        Assert.That(SecureContainerTestReader.Read(json,"päss🔐\0"),Is.EqualTo(Encoding.UTF8.GetString(Field("payloadHex"))));
        // JSON order and equivalent escaping are not authenticated bytes.
        var envelope = JObject.Parse(json);
        var reordered = new JObject(envelope.Properties().Reverse().Select(p => new JProperty(p.Name,p.Value.DeepClone())));
        Assert.That(SecureContainerTestReader.Read(reordered.ToString(),"päss🔐\0"),Is.EqualTo(Encoding.UTF8.GetString(Field("payloadHex"))));
    }

    [TestCase("format")] [TestCase("version")] [TestCase("mode")]
    [TestCase("salt")] [TestCase("nonce")] [TestCase("iterations")]
    [TestCase("manifest")] [TestCase("payloadEncoding")] [TestCase("payload")] [TestCase("tag")]
    public void IndependentReaderRejectsTampering(string field)
    {
        // PBKDF2/GCM keeps this test quick; other modes have full fixed-vector checks.
        var e = JObject.Parse(Encoding.UTF8.GetString(Hex((string)Fixtures("vectors.json")[2]["envelopeHex"]!)));
        switch (field)
        {
            case "salt": e["kdf"]![field] = Convert.ToBase64String(new byte[16]); break;
            case "nonce": e["cipher"]![field] = Convert.ToBase64String(new byte[12]); break;
            case "iterations": e["kdf"]![field] = 600001; break;
            case "manifest": e[field]![0]!["identifier"] = "other"; break;
            case "version": e[field] = 2; break;
            case "payload": var b = Convert.FromBase64String((string)e[field]!); b[0] ^= 1; e[field] = Convert.ToBase64String(b); break;
            case "tag": e[field] = Convert.ToBase64String(new byte[16]); break;
            default: e[field] = (string)e[field]! + "x"; break;
        }
        Assert.Throws<AuthenticationTagMismatchException>(() => SecureContainerTestReader.Read(e.ToString(),"päss🔐\0"));
    }
}
