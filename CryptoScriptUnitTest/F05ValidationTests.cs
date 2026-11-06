using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.FileEncryption;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

public class F05ValidationTests
{
    private static readonly KeyVariableDeclaration Key = F05ReferenceTests.Key(new byte[16]);
    private static EncryptedFileHeader Header(string m, string pad = "NONE") => m switch
    {
        "AES-CBC" => new(m, pad, Convert.ToBase64String(new byte[16])),
        "AES-CTR" => new(m, pad, "", Nonce: Convert.ToBase64String(new byte[12]), Counter: "AAAAAA==", CounterEncoding: "uint32-be"),
        _ => new(m, pad, "", Authentication: m[4..], TagLength: 16, Nonce: Convert.ToBase64String(new byte[12]))
    };

    [TestCase("AES-GCM")] [TestCase("AES-CCM")]
    public void ValidButUnauthenticatedHeaderRewriteFails(string m)
    {
        var h = Header(m); byte[] frame = EncryptedFileCodec.Encode(h);
        using var payload = new MemoryStream();
        FileCipherStream.Process(new MemoryStream([1, 2, 3]), payload, Key, h, frame, true, default);
        byte[] changed = frame.Concat(new byte[] { (byte)' ' }).ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(changed.AsSpan(12), (uint)(changed.Length - 16));
        var (parsed, raw) = EncryptedFileCodec.Read(new MemoryStream(changed));
        Assert.Catch(() => FileCipherStream.Process(new MemoryStream(payload.ToArray()), Stream.Null, Key, parsed, raw, false, default));
    }

    [TestCase("null")] [TestCase("wrong-type")] [TestCase("extra-counter")]
    [TestCase("extra-nonce")] [TestCase("extra-adata")] [TestCase("extra-length")]
    [TestCase("missing-nonce")] [TestCase("counter-endian")] [TestCase("tag-size")]
    [TestCase("length-sign")] [TestCase("length-zero")] [TestCase("length-overflow")]
    [TestCase("base64")] [TestCase("duplicate")] [TestCase("unknown")]
    public void StrictMechanismMetadata(string damage)
    {
        var h = Header("AES-CBC", "PKCS-7");
        h = damage switch
        {
            "extra-counter" => h with { Counter = "AAAAAA==" },
            "extra-nonce" => h with { Nonce = "AA==" },
            "extra-adata" => h with { AssociatedData = "" },
            "extra-length" => h with { PlaintextLength = "0" },
            "missing-nonce" => Header("AES-GCM") with { Nonce = null },
            "counter-endian" => Header("AES-CTR") with { CounterEncoding = "uint32-le" },
            "tag-size" => Header("AES-GCM") with { TagLength = 12 },
            "length-sign" => h with { Padding = "ISO-9797-M1", PlaintextLength = "+1" },
            "length-zero" => h with { Padding = "ISO-9797-M1", PlaintextLength = "01" },
            "length-overflow" => h with { Padding = "ISO-9797-M1", PlaintextLength = "9223372036854775808" },
            "base64" => h with { Iv = h.Iv + "\n" },
            _ => h
        };
        if (damage is not ("null" or "wrong-type" or "duplicate" or "unknown"))
        { Assert.Throws<InvalidDataException>(() => EncryptedFileCodec.Encode(h)); return; }
        byte[] frame = EncryptedFileCodec.Encode(h);
        string json = Encoding.UTF8.GetString(frame, 16, frame.Length - 16);
        json = damage switch
        {
            "null" => json.Replace("\"iv\":\"" + h.Iv + "\"", "\"iv\":null"),
            "wrong-type" => json.Replace("\"tagLength\":0", "\"tagLength\":\"0\""),
            "duplicate" => json.Replace("{", "{\"iv\":\"\","),
            _ => json.Replace("{", "{\"unknown\":0,")
        };
        byte[] encoded = Encoding.UTF8.GetBytes(json), changed = new byte[16 + encoded.Length];
        frame.AsSpan(0, 16).CopyTo(changed); encoded.CopyTo(changed, 16);
        BinaryPrimitives.WriteUInt32BigEndian(changed.AsSpan(12), (uint)encoded.Length);
        Assert.Catch(() => EncryptedFileCodec.Read(new MemoryStream(changed)));
    }

    [TestCase("PKCS-7")] [TestCase("ANSI-X923")] [TestCase("ISO-10126")]
    [TestCase("ISO-7816")] [TestCase("ISO-9797-M2")] [TestCase("TLS-CBC")]
    [TestCase("ISO-9797-M1")] [TestCase("ISO-9797-M3")]
    public void MalformedPaddingIsRejected(string pad)
    {
        var h = Header("AES-CBC", pad);
        byte[] malformed = new byte[32];
        if (pad == "ISO-9797-M1") { h = h with { PlaintextLength = "17" }; malformed[^1] = 1; }
        else if (pad == "ISO-9797-M3") malformed[0] = 1; // unsupported high bit-length bytes
        else if (pad == "TLS-CBC") { malformed[^1] = 3; malformed[^2] = 2; }
        using var aes = Aes.Create(); aes.Key = new byte[16];
        byte[] cipher = aes.EncryptCbc(malformed, new byte[16], PaddingMode.None);
        Assert.Catch(() => FileCipherStream.Process(new MemoryStream(cipher), Stream.Null, Key, h,
            EncryptedFileCodec.Encode(h), false, default));
    }

    [Test] public void ExtendedTlsPaddingKeepsItsExistingVarContract()
    {
        byte[] padded = new byte[272]; padded.AsSpan(16).Fill(255);
        using var aes = Aes.Create(); aes.Key = new byte[16];
        byte[] cipher = aes.EncryptCbc(padded, new byte[16], PaddingMode.None);
        var h = Header("AES-CBC", "TLS-CBC"); using var output = new MemoryStream();
        FileCipherStream.Process(new MemoryStream(cipher), output, Key, h, EncryptedFileCodec.Encode(h), false, default);
        Assert.That(output.ToArray(), Is.EqualTo(new byte[16]));
    }

    [Test] public void M1RejectsSourceLengthChangeAfterHeaderCreation()
    {
        var h = Header("AES-CBC", "ISO-9797-M1") with { PlaintextLength = "16" };
        using var source = new MemoryStream(new byte[17]);
        Assert.Throws<IOException>(() => FileCipherStream.Process(source, Stream.Null, Key, h,
            EncryptedFileCodec.Encode(h), true, default));
        Assert.That(source.Position, Is.Zero);
    }

    [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)] [TestCase(13)]
    public void CcmAllNonceLengthsMatchIndependentProvider(int size)
    {
        if (!AesCcm.IsSupported) Assert.Ignore("No system CCM provider; Python vectors still cover default nonce.");
        byte[] nonce = Enumerable.Range(0, size).Select(i => (byte)i).ToArray();
        var h = Header("AES-CCM") with { Nonce = Convert.ToBase64String(nonce) };
        byte[] frame = EncryptedFileCodec.Encode(h), data = new byte[65535], cipher = new byte[65535], tag = new byte[16];
        using var reference = new AesCcm(new byte[16]); reference.Encrypt(nonce, data, cipher, tag, frame);
        using var output = new MemoryStream();
        FileCipherStream.Process(new MemoryStream(data), output, Key, h, frame, true, default);
        Assert.That(output.ToArray(), Is.EqualTo(cipher.Concat(tag).ToArray()));
    }

    [Test] public void CtrFinalCounterBlockIsAllowed()
    {
        var h = Header("AES-CTR") with { Counter = "/////w==" };
        using var output = new MemoryStream();
        FileCipherStream.Process(new MemoryStream(new byte[16]), output, Key, h, EncryptedFileCodec.Encode(h), true, default);
        using var aes = Aes.Create(); aes.Key = new byte[16];
        byte[] counter = new byte[16]; counter.AsSpan(12).Fill(255);
        Assert.That(output.ToArray(), Is.EqualTo(aes.EncryptEcb(counter, PaddingMode.None)));
    }

    [TestCase("bits")] [TestCase("length")] [TestCase("zeros")]
    public void M3RejectsMalformedLengthAndData(string damage)
    {
        byte[] padded = new byte[32]; padded[15] = damage == "bits" ? (byte)7 : damage == "length" ? (byte)136 : (byte)8;
        if (damage == "zeros") padded[^1] = 1;
        using var aes = Aes.Create(); aes.Key = new byte[16];
        byte[] cipher = aes.EncryptCbc(padded, new byte[16], PaddingMode.None);
        var h = Header("AES-CBC", "ISO-9797-M3");
        Assert.Catch(() => FileCipherStream.Process(new MemoryStream(cipher), Stream.Null, Key, h, EncryptedFileCodec.Encode(h), false, default));
    }

    [TestCase(1)] [TestCase(8)] [TestCase(16)] [TestCase(31)]
    public void GcmNonStandardNonceMatchesVarLibrary(int size)
    {
        byte[] nonce = new byte[size], data = new byte[47];
        var h = Header("AES-GCM") with { Nonce = Convert.ToBase64String(nonce) };
        byte[] frame = EncryptedFileCodec.Encode(h);
        var p = new ParameterVariableDeclaration { Mechanism = "AES-GCM" };
        p.SetParameter("NONCE", FormatConversions.ByteArrayToHexString(nonce));
        p.SetParameter("ADATA", FormatConversions.ByteArrayToHexString(frame));
        var expected = new AES_GCM().ModeEncryption(p, Key, new StringVariableDeclaration
            { Value = FormatConversions.ByteArrayToHexString(data), ValueFormat = FormatConversions.HEX });
        using var output = new MemoryStream();
        FileCipherStream.Process(new MemoryStream(data), output, Key, h, frame, true, default);
        Assert.That(output.ToArray(), Is.EqualTo(FormatConversions.ToByteArray(expected.Value, expected.ValueFormat)));
    }

    [TestCase("AES-CBC")] [TestCase("AES-CTR")] [TestCase("AES-GCM")] [TestCase("AES-CCM")]
    public void CancellationAndIoFailureEraseControlledOutput(string m)
    {
        var h = Header(m, m == "AES-CBC" ? "PKCS-7" : "NONE");
        byte[] frame = EncryptedFileCodec.Encode(h);
        using var cts = new CancellationTokenSource();
        using var output = new FailingOutput(cts);
        Assert.Throws<OperationCanceledException>(() => FileCipherStream.Process(new MemoryStream(new byte[200000]), output, Key, h, frame, true, cts.Token));
        Assert.That(output.Borrowed, Is.Not.Null);
        Assert.That(output.Borrowed!.All(b => b == 0), Is.True);
        using var failed = new FailingOutput(null);
        Assert.Throws<IOException>(() => FileCipherStream.Process(new MemoryStream(new byte[200000]), failed, Key, h, frame, true, default));
        Assert.That(failed.Borrowed!.All(b => b == 0), Is.True);
    }

    private sealed class FailingOutput(CancellationTokenSource? cts) : MemoryStream
    {
        internal byte[]? Borrowed;
        public override void Write(byte[] buffer, int offset, int count)
        {
            Borrowed = buffer;
            if (cts is null) throw new IOException("Injected write failure.");
            cts.Cancel();
        }
    }
}
