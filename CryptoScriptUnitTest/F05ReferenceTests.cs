using System.Security.Cryptography;
using System.Text.Json;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.FileEncryption;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class F05ReferenceTests
{
    public sealed record Vector(string Mechanism, string Padding, string Key, string Plaintext,
        string Padded, string Frame, string Payload);
    public static IEnumerable<TestCaseData> Vectors()
    {
        var vectors = JsonSerializer.Deserialize<Vector[]>(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Fixtures", "F05", "vectors.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return vectors.Select((v, i) => new TestCaseData(v).SetName($"F05_{i}_{v.Mechanism}_{v.Key.Length * 4}_{v.Padding}_{v.Plaintext.Length / 2}"));
    }

    [TestCaseSource(nameof(Vectors))]
    public void IndependentExactContainerAndVar(Vector v)
    {
        byte[] frame = Convert.FromHexString(v.Frame), plaintext = Convert.FromHexString(v.Plaintext), payload = Convert.FromHexString(v.Payload);
        var (header, raw) = EncryptedFileCodec.Read(new MemoryStream(frame));
        Assert.That(raw, Is.EqualTo(frame));
        Assert.That(EncryptedFileCodec.Encode(header), Is.EqualTo(frame));
        var key = Key(Convert.FromHexString(v.Key));
        using var encrypted = new MemoryStream();
        FileCipherStream.Process(new ShortReadStream(plaintext), encrypted, key, header, frame, true, default);
        // Random filler is intentionally not deterministic; a single length octet is.
        bool deterministic = v.Padding != "ISO-10126" || v.Padded.Length / 2 - plaintext.Length == 1;
        if (deterministic) Assert.That(encrypted.ToArray(), Is.EqualTo(payload));
        using var clear = new MemoryStream();
        FileCipherStream.Process(new ShortReadStream(payload), clear, key, header, frame, false, default);
        Assert.That(clear.ToArray(), Is.EqualTo(plaintext));
        clear.SetLength(0);
        FileCipherStream.Process(new ShortReadStream(encrypted.ToArray()), clear, key, header, frame, false, default);
        Assert.That(clear.ToArray(), Is.EqualTo(plaintext));

        if (!deterministic) return;
        var p = new ParameterVariableDeclaration { Mechanism = v.Mechanism };
        p.SetParameter("PAD", v.Padding);
        if (header.Iv.Length != 0) p.SetParameter("IV", "b64(" + header.Iv + ")");
        if (header.Nonce is not null) p.SetParameter("NONCE", "b64(" + header.Nonce + ")");
        if (header.Counter is not null) p.SetParameter("COUNTER", "b64(" + header.Counter + ")");
        // VAR gets precisely the CWE AAD to independently exercise its packet implementation.
        if (header.TagLength != 0) p.SetParameter("ADATA", FormatConversions.ByteArrayToHexString(frame));
        EncryptionMode mode = v.Mechanism switch
        {
            "AES-CBC" => new AES_CBC(), "AES-ECB" => new AES_ECB(), "AES-CTR" => new AES_CTR(),
            "AES-GCM" => new AES_GCM(), "AES-CCM" => new AES_CCM(),
            "DES3-CBC" => new DES3_CBC(), _ => new DES3_ECB()
        };
        var data = new StringVariableDeclaration { Value = FormatConversions.ByteArrayToHexString(plaintext), ValueFormat = FormatConversions.HEX };
        var variableResult = mode.ModeEncryption(p, key, data);
        Assert.That(FormatConversions.ToByteArray(variableResult.Value, variableResult.ValueFormat), Is.EqualTo(payload));
    }

    internal static KeyVariableDeclaration Key(byte[] bytes) => new()
        { Value = FormatConversions.ByteArrayToHexString(bytes), ValueFormat = FormatConversions.HEX };

    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 7));
    }
}
