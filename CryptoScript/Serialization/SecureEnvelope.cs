using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace CryptoScript.Serialization;

internal sealed class SecureLoadException(string category) : Exception("SecureLoad failed: " + category + ".")
{
    internal string Category { get; } = category;
}

// Contains only structurally checked, still UNTRUSTED data until Unprotect succeeds.
internal sealed record SecureEnvelope(SecureSaveOptions Options, byte[] Salt, byte[] Nonce, byte[] Tag,
    SecureManifestEntry[] Manifest, byte[] Payload) : IDisposable
{
    public void Dispose() => CryptographicOperations.ZeroMemory(Payload);

    internal static SecureEnvelope Parse(byte[] bytes)
    {
        byte[]? payload = null;
        try
        {
            if (bytes.Length > SecureSaveOptions.FileLimit) Fail("ResourceLimitExceeded");
            _ = SecureBuffer.Utf8.GetCharCount(bytes); // Strict UTF-8, no replacement characters.
            // Bound DOM metadata before allocating it. A valid 4096-root envelope fits this budget.
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 8 });
            int tokens = 0; long manifestStart = -1; string? property = null;
            while (reader.Read())
            {
                if (++tokens > 100000) Fail("ResourceLimitExceeded");
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueSpan.Length > 256) Fail("InvalidEnvelope");
                    property = reader.GetString();
                }
                if (reader.TokenType == JsonTokenType.StartArray && reader.CurrentDepth == 1 && property == "manifest")
                    manifestStart = reader.TokenStartIndex;
                if (manifestStart >= 0 && reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == 1 &&
                    reader.BytesConsumed - manifestStart > SecureSaveOptions.ManifestLimit) Fail("ResourceLimitExceeded");
                if (reader.TokenType == JsonTokenType.String && !(reader.CurrentDepth == 1 && property == "payload") &&
                    reader.ValueSpan.Length > 6 * 256) Fail("ResourceLimitExceeded");
            }
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            Fields(root, "format", "version", "mode", "kdf", "cipher", "manifest", "payloadEncoding", "payload", "tag");
            if (String(root.GetProperty("format"), 64) != SecureContainerCryptography.Format || Number(root.GetProperty("version")) != 1)
                Fail("UnsupportedFormat");
            string mode = String(root.GetProperty("mode"), 32);
            if (mode is not ("AES-256-GCM" or "AES-256-GMAC")) Fail("UnsupportedFormat");
            var kdf = root.GetProperty("kdf");
            if (kdf.ValueKind != JsonValueKind.Object || !kdf.TryGetProperty("id", out var id)) Fail("InvalidEnvelope");
            string algorithm = String(kdf.GetProperty("id"), 32);
            bool pbkdf = algorithm == "PBKDF2-HMAC-SHA256";
            if (!pbkdf && algorithm != "ARGON2ID") Fail("UnsupportedFormat");
            Fields(kdf, pbkdf ? ["id", "salt", "iterations", "outputLength"] :
                ["id", "version", "salt", "memoryKiB", "iterations", "parallelism", "outputLength"]);
            if (Number(kdf.GetProperty("outputLength")) != 32 || !pbkdf && Number(kdf.GetProperty("version")) != 19)
                Fail("InvalidEnvelope");
            int Cost(string name) { ulong value = Number(kdf.GetProperty(name)); if (value > int.MaxValue) Fail("ResourceLimitExceeded"); return (int)value; }
            var options = new SecureSaveOptions(mode == "AES-256-GMAC", pbkdf,
                pbkdf ? 65536 : Cost("memoryKiB"), pbkdf ? 3 : Cost("iterations"),
                pbkdf ? 4 : Cost("parallelism"), pbkdf ? Cost("iterations") : 600000);
            try { options.Validate(); } catch (SecureSaveException) { Fail("ResourceLimitExceeded"); }
            var cipher = root.GetProperty("cipher"); Fields(cipher, "nonce", "tagLength");
            if (Number(cipher.GetProperty("tagLength")) != 16 || String(root.GetProperty("payloadEncoding"), 16) != options.Encoding)
                Fail("InvalidEnvelope");
            byte[] salt = Base64(kdf.GetProperty("salt"), 16, true), nonce = Base64(cipher.GetProperty("nonce"), 12, true),
                tag = Base64(root.GetProperty("tag"), 16, true);
            var manifest = root.GetProperty("manifest");
            if (manifest.ValueKind != JsonValueKind.Array) Fail("InvalidEnvelope");
            if (manifest.GetArrayLength() > SecureSaveOptions.RootLimit ||
                SecureBuffer.Utf8.GetByteCount(manifest.GetRawText()) > SecureSaveOptions.ManifestLimit) Fail("ResourceLimitExceeded");
            var entries = new List<SecureManifestEntry>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in manifest.EnumerateArray())
            {
                Fields(entry, "identifier", "type", "key");
                string name = String(entry.GetProperty("identifier"), 256), type = String(entry.GetProperty("type"), 8);
                if (name.Length == 0 || !char.IsAsciiLetter(name[0]) || name.Any(c => !char.IsAsciiLetterOrDigit(c)) || !names.Add(name) ||
                    type is not ("VAR" or "KEY" or "PARAM" or "PATH" or "TR31H" or "SECRET")) Fail("InvalidEnvelope");
                var key = entry.GetProperty("key"); SecureKeyManifest? projection = null;
                if (type == "KEY")
                {
                    Fields(key, "algorithm", "bitLength", "kcv");
                    string a = String(key.GetProperty("algorithm"), 16);
                    ulong bits = Number(key.GetProperty("bitLength"));
                    if (a is not ("Unknown" or "Aes" or "Tdea" or "Hmac" or "Rsa" or "Ec") || bits is < 1 or > 134217728) Fail("InvalidEnvelope");
                    var value = key.GetProperty("kcv");
                    string? kcv = value.ValueKind == JsonValueKind.Null ? null : String(value, 36);
                    if (kcv is not null && (a is not ("Aes" or "Tdea") || kcv.Length != (a == "Aes" ? 36 : 20) ||
                        !kcv.StartsWith("0x(", StringComparison.Ordinal) || kcv[^1] != ')' ||
                        kcv[3..^1].Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))) Fail("InvalidEnvelope");
                    projection = new(a, bits, kcv);
                }
                else if (key.ValueKind != JsonValueKind.Null) Fail("InvalidEnvelope");
                entries.Add(new(name, type, projection));
            }
            _ = SecureManifest.Frame(entries);
            payload = options.Gmac ? SecureBuffer.Utf8.GetBytes(String(root.GetProperty("payload"), SecureSaveOptions.PayloadLimit)) :
                Base64(root.GetProperty("payload"), SecureSaveOptions.PayloadLimit, false);
            if (payload.Length == 0) Fail("InvalidEnvelope");
            return new(options, salt, nonce, tag, entries.ToArray(), payload);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or
            System.Text.DecoderFallbackException or System.Text.EncoderFallbackException or OverflowException or SecureSaveException)
        { if (payload is not null) CryptographicOperations.ZeroMemory(payload); throw new SecureLoadException("InvalidEnvelope"); }
        catch { if (payload is not null) CryptographicOperations.ZeroMemory(payload); throw; }
    }

    private static void Fields(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) Fail("InvalidEnvelope");
        var remaining = names.ToHashSet(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!remaining.Remove(property.Name)) Fail("InvalidEnvelope");
        if (remaining.Count != 0) Fail("InvalidEnvelope");
    }
    private static string String(JsonElement element, int limit)
    {
        if (element.ValueKind != JsonValueKind.String) Fail("InvalidEnvelope");
        string value = element.GetString()!;
        if (SecureBuffer.Utf8.GetByteCount(value) > limit) Fail("ResourceLimitExceeded");
        return value;
    }
    private static ulong Number(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number) Fail("InvalidEnvelope");
        string text = element.GetRawText();
        if (text.Length == 0 || text.Any(c => c is < '0' or > '9') || text.Length > 1 && text[0] == '0') Fail("InvalidEnvelope");
        if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value)) Fail("ResourceLimitExceeded");
        return value;
    }
    private static byte[] Base64(JsonElement element, int limit, bool exact)
    {
        string value = String(element, 4 * ((limit + 2) / 3));
        byte[] result = Convert.FromBase64String(value);
        if (result.Length > limit || exact && result.Length != limit || Convert.ToBase64String(result) != value)
        { CryptographicOperations.ZeroMemory(result); Fail("InvalidEnvelope"); }
        return result;
    }
    private static void Fail(string category) => throw new SecureLoadException(category);
}
