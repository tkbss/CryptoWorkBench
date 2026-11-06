using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace CryptoScript.FileEncryption;

internal sealed record EncryptedFileHeader(string Mechanism, string Padding, string Iv,
    string PayloadEncoding = "binary", string Authentication = "none", int TagLength = 0,
    string? Nonce = null, string? Counter = null, string? CounterEncoding = null,
    string? AssociatedData = null, string? PlaintextLength = null);

/// <summary>Version 1.0 framing only. No encryption, key material or source paths.</summary>
internal static class EncryptedFileCodec
{
    internal const int MaximumHeaderLength = 4096;
    private static readonly byte[] Magic = "CWBENC\0\0"u8.ToArray();
    private static readonly string[] Fields = ["format", "mechanism", "padding", "iv",
        "payloadEncoding", "authentication", "tagLength", "nonce", "counter", "counterEncoding",
        "associatedData", "plaintextLength"];

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
            if (header.Nonce is not null) json.WriteString("nonce", header.Nonce);
            if (header.Counter is not null) json.WriteString("counter", header.Counter);
            if (header.CounterEncoding is not null) json.WriteString("counterEncoding", header.CounterEncoding);
            if (header.AssociatedData is not null) json.WriteString("associatedData", header.AssociatedData);
            if (header.PlaintextLength is not null) json.WriteString("plaintextLength", header.PlaintextLength);
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
            if (names.Length < 7 || names.Distinct(StringComparer.Ordinal).Count() != names.Length ||
                names.Except(Fields, StringComparer.Ordinal).Any() ||
                Fields.Take(7).Except(names, StringComparer.Ordinal).Any() ||
                root.GetProperty("format").GetString() != "CryptoWorkBenchEncryptedFile")
                throw new InvalidDataException("Invalid CWE metadata fields.");
            var header = new EncryptedFileHeader(root.GetProperty("mechanism").GetString()!,
                root.GetProperty("padding").GetString()!, root.GetProperty("iv").GetString()!,
                root.GetProperty("payloadEncoding").GetString()!, root.GetProperty("authentication").GetString()!,
                root.GetProperty("tagLength").GetInt32(), Optional("nonce"), Optional("counter"),
                Optional("counterEncoding"), Optional("associatedData"), Optional("plaintextLength"));
            Validate(header);
            return (header, frame);
            string? Optional(string name) => root.TryGetProperty(name, out var value)
                ? value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new InvalidDataException("Expected string.")
                : null;
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or
            InvalidOperationException or FormatException or OverflowException or ArgumentException)
        { throw new InvalidDataException("Invalid CWE metadata.", error); }
    }

    internal static void Validate(EncryptedFileHeader header)
    {
        if (header.Mechanism is null || header.Padding is null)
            throw new InvalidDataException("CWE string fields cannot be null.");
        string m = header.Mechanism;
        bool cbc = m is "AES-CBC" or "DES3-CBC", des = m.StartsWith("DES3-", StringComparison.Ordinal);
        bool aead = m is "AES-GCM" or "AES-CCM", ctr = m == "AES-CTR";
        if (m is not ("AES-CBC" or "AES-ECB" or "AES-CTR" or "AES-GCM" or "AES-CCM" or "DES3-CBC" or "DES3-ECB") ||
            !CryptoScript.Model.PaddingRegistry.TryGet(header.Padding, out _) ||
            (!(cbc || des) && header.Padding != "NONE") || header.PayloadEncoding != "binary" ||
            header.Authentication != (aead ? m[4..] : "none") || header.TagLength != (aead ? 16 : 0))
            throw new InvalidDataException("Unsupported CWE metadata.");
        int ivLength = Binary(header.Iv).Length;
        if (ivLength != (cbc ? des ? 8 : 16 : 0)) throw new InvalidDataException("Invalid CWE IV length.");
        if (aead || ctr)
        {
            int n = Binary(header.Nonce).Length;
            if (ctr && n != 12 || m == "AES-CCM" && (n < 7 || n > 13) || m == "AES-GCM" && n < 1)
                throw new InvalidDataException("Invalid CWE nonce length.");
        }
        else if (header.Nonce is not null) throw new InvalidDataException("Unexpected nonce.");
        if (ctr)
        {
            if (Binary(header.Counter).Length != 4 || header.CounterEncoding != "uint32-be")
                throw new InvalidDataException("Invalid CWE counter.");
        }
        else if (header.Counter is not null || header.CounterEncoding is not null) throw new InvalidDataException("Unexpected counter.");
        if (header.AssociatedData is not null)
        {
            if (!aead) throw new InvalidDataException("Unexpected associated data.");
            _ = Binary(header.AssociatedData);
        }
        if (header.Padding == "ISO-9797-M1") _ = OriginalLength(header);
        else if (header.PlaintextLength is not null) throw new InvalidDataException("Unexpected plaintext length.");
    }

    internal static long OriginalLength(EncryptedFileHeader header)
    {
        if (!long.TryParse(header.PlaintextLength, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out long value) || value < 0 ||
            value.ToString(System.Globalization.CultureInfo.InvariantCulture) != header.PlaintextLength)
            throw new InvalidDataException("Invalid CWE plaintext length.");
        return value;
    }

    private static byte[] Binary(string? text)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(text!);
            if (Convert.ToBase64String(bytes) != text) throw new InvalidDataException("Noncanonical CWE Base64.");
            return bytes;
        }
        catch (Exception e) when (e is FormatException or ArgumentNullException)
        { throw new InvalidDataException("Invalid CWE binary field.", e); }
    }
}
