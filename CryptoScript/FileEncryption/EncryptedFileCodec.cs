using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace CryptoScript.FileEncryption;

internal sealed record EncryptedFileHeader(string Mechanism, string Padding, string Iv,
    string PayloadEncoding = "binary", string Authentication = "none", int TagLength = 0);

/// <summary>Version 1.0 framing only. No encryption, key material or source paths.</summary>
internal static class EncryptedFileCodec
{
    internal const int MaximumHeaderLength = 4096;
    private static readonly byte[] Magic = "CWBENC\0\0"u8.ToArray();
    private static readonly string[] Fields = ["format", "mechanism", "padding", "iv",
        "payloadEncoding", "authentication", "tagLength"];

    internal static byte[] Encode(EncryptedFileHeader header)
    {
        Validate(header);
        using var metadata = new MemoryStream();
        using (var json = new Utf8JsonWriter(metadata, new JsonWriterOptions
               { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteString("format", "CryptoWorkBenchEncryptedFile");
            json.WriteString("mechanism", header.Mechanism);
            json.WriteString("padding", header.Padding);
            json.WriteString("iv", header.Iv);
            json.WriteString("payloadEncoding", header.PayloadEncoding);
            json.WriteString("authentication", header.Authentication);
            json.WriteNumber("tagLength", header.TagLength);
            json.WriteEndObject();
        }
        if (metadata.Length > MaximumHeaderLength) throw new InvalidDataException("CWE header too large.");
        byte[] result = new byte[16 + (int)metadata.Length];
        Magic.CopyTo(result, 0);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(12), (uint)metadata.Length);
        metadata.ToArray().CopyTo(result, 16);
        return result;
    }

    // Preserve exact framing bytes for future AAD; do not reserialize a parsed header for AAD.
    internal static (EncryptedFileHeader Header, byte[] Frame) Read(Stream input)
    {
        byte[] prefix = new byte[16];
        input.ReadExactly(prefix);
        if (!prefix.AsSpan(0, 8).SequenceEqual(Magic) ||
            BinaryPrimitives.ReadUInt16BigEndian(prefix.AsSpan(8)) != 1 ||
            BinaryPrimitives.ReadUInt16BigEndian(prefix.AsSpan(10)) != 0)
            throw new InvalidDataException("Unsupported CWE magic or version.");
        uint length = BinaryPrimitives.ReadUInt32BigEndian(prefix.AsSpan(12));
        if (length == 0 || length > MaximumHeaderLength) throw new InvalidDataException("Invalid CWE header length.");
        byte[] frame = new byte[16 + (int)length];
        prefix.CopyTo(frame, 0);
        input.ReadExactly(frame.AsSpan(16));
        try
        {
            string text = new UTF8Encoding(false, true).GetString(frame, 16, (int)length);
            using var json = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 4 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid CWE metadata.");
            var names = root.EnumerateObject().Select(p => p.Name).ToArray();
            if (names.Length != Fields.Length || names.Distinct(StringComparer.Ordinal).Count() != Fields.Length ||
                names.Except(Fields, StringComparer.Ordinal).Any() ||
                root.GetProperty("format").GetString() != "CryptoWorkBenchEncryptedFile")
                throw new InvalidDataException("Invalid CWE metadata fields.");
            var header = new EncryptedFileHeader(root.GetProperty("mechanism").GetString()!,
                root.GetProperty("padding").GetString()!, root.GetProperty("iv").GetString()!,
                root.GetProperty("payloadEncoding").GetString()!, root.GetProperty("authentication").GetString()!,
                root.GetProperty("tagLength").GetInt32());
            Validate(header);
            return (header, frame);
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or
            InvalidOperationException or FormatException or OverflowException or ArgumentException)
        { throw new InvalidDataException("Invalid CWE metadata.", error); }
    }

    private static void Validate(EncryptedFileHeader header)
    {
        if (header.Mechanism != "AES-CBC" || header.Padding is not ("PKCS-7" or "NONE") ||
            header.PayloadEncoding != "binary" || header.Authentication != "none" || header.TagLength != 0)
            throw new InvalidDataException("Unsupported CWE metadata.");
        byte[] iv;
        try { iv = Convert.FromBase64String(header.Iv); }
        catch (Exception e) when (e is FormatException or ArgumentNullException)
        { throw new InvalidDataException("Invalid CWE IV.", e); }
        if (iv.Length != 16 || Convert.ToBase64String(iv) != header.Iv)
            throw new InvalidDataException("CWE IV must be canonical padded Base64 of 16 bytes.");
    }
}
