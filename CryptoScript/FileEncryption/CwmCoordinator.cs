using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.Model;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScript.FileEncryption;

internal static class CwmCoordinator
{
    internal const int MaximumContainerBytes = 65536;
    internal const string Salt = "CryptoWorkBench/CWM/v1/HKDF-SHA256";
    internal const string Info = "CryptoWorkBench/CWM/v1/metadataAuth/HMAC-SHA256";

    internal static VariableDeclaration Execute(OperationInvocation invocation, bool verify)
    {
        if (invocation.Arguments.Count != 3 || invocation.Arguments.Any(a => a.Kind != ResolvedCallArgumentKind.Variable) ||
            invocation.Arguments[0].SourceVariable is not ParameterVariableDeclaration p ||
            invocation.Arguments[1].SourceVariable is not KeyVariableDeclaration ||
            invocation.Arguments[2].SourceVariable is not PathVariableDeclaration path)
            throw new ArgumentException("MacPack/MacVerify require exactly PARAM, KEY, PATH variable references.");
        var args = new AlgorithmCallArguments(invocation.Arguments.Select(a => new AlgorithmCallArgument(a.Value, a.SourceVariable, a.Kind)), invocation.DiagnosticSink);
        if (!MechanismRegistry.TryGet(p.Mechanism, out var entry) || p.Mechanism != entry!.CanonicalName)
            throw new ArgumentException("CWM requires a canonical mechanism name.");
        // Validate the caller's mechanism and key before any filesystem operation.
        int macLength = FileMacCoordinator.Validate(args, verify);
        // Preparation below repeats technical checks, but must not emit duplicate metadata warnings.
        args = new AlgorithmCallArguments(args.Arguments);
        var effective = Parameters(p);
        invocation.CancellationToken.ThrowIfCancellationRequested();
        string full = SnapshotByteFilePublisher.ValidatePath(path);
        if (verify && !full.EndsWith(".cwm", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("MacVerify requires a .cwm file.");
        using var publication = new FilePublication(Path.GetDirectoryName(full)!);
        return verify ? Verify(args, p, effective, macLength, publication, Path.GetFileName(full), invocation.CancellationToken)
            : Pack(args, p, effective, publication, Path.GetFileName(full), invocation.CancellationToken);
    }

    internal static PathVariableDeclaration Pack(AlgorithmCallArguments args, ParameterVariableDeclaration p,
        Dictionary<string, object> effective, FilePublication publication, string name, CancellationToken token,
        Action? beforePublish = null, int maximumCandidates = 1000000)
    {
        SafeName(name);
        using var source = publication.OpenSource(name);
        var (mac, size) = FileMacCoordinator.MacOpened(args, source, token);
        if (size > CwmJcs.MaximumInteger) throw new IOException("CWM file size exceeds the safe integer range.");
        var fileMac = JsonSerializer.SerializeToElement(new { file = name, size, mechanism = p.Mechanism,
            parameters = effective, mac = Convert.ToHexString(FormatConversions.ToByteArray(mac.Value, mac.ValueFormat)) });
        byte[] auth = Authenticate(args, fileMac);
        byte[] bytes;
        try
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(new { format = "CWM", version = 1, fileMac,
                metadataAuth = new { mechanism = "HMAC-SHA256", canonicalization = "RFC8785-JCS",
                    keyDerivation = "HKDF-SHA256", mac = Convert.ToHexString(auth) } });
        }
        finally { CryptographicOperations.ZeroMemory(auth); }
        if (bytes.Length > MaximumContainerBytes) throw new IOException("CWM container too large.");
        string temporary = $".cwm-{Guid.NewGuid():N}.tmp";
        bool ownsTemporary = false;
        try
        {
            using var output = publication.CreateTemporary(temporary);
            ownsTemporary = true;
            token.ThrowIfCancellationRequested();
            output.Write(bytes); output.Flush(flushToDisk: true);
            beforePublish?.Invoke();
            for (int i = 0; i < maximumCandidates; i++)
            {
                token.ThrowIfCancellationRequested();
                string candidate = name + (i == 0 ? "" : "." + i.ToString(CultureInfo.InvariantCulture)) + ".cwm";
                var result = new PathVariableDeclaration { Type = new CryptoTypePath(), Value = Path.Combine(publication.CurrentDirectory(), candidate) };
                if (publication.TryPublish(output, temporary, candidate)) return result;
            }
            throw new IOException("CWM output name candidates exhausted.");
        }
        finally { if (ownsTemporary) publication.DeleteTemporary(temporary); }
    }

    private static VariableDeclaration Verify(AlgorithmCallArguments args, ParameterVariableDeclaration p,
        Dictionary<string, object> effective, int macLength, FilePublication publication, string name, CancellationToken token)
    {
        using var input = publication.OpenSource(name);
        // Limit reads even if the file grows after opening; never ReadToEnd on untrusted input.
        byte[] bytes = new byte[MaximumContainerBytes + 1];
        int used = 0, count;
        do
        {
            token.ThrowIfCancellationRequested();
            count = input.Read(bytes, used, bytes.Length - used); used += count;
            if (used > MaximumContainerBytes) throw new FormatException("CWM container too large.");
        } while (count != 0);
        // Reject invalid UTF-8 before the JSON parser can substitute replacement characters.
        _ = new UTF8Encoding(false, true).GetCharCount(bytes, 0, used);
        using var doc = JsonDocument.Parse(bytes.AsMemory(0, used), new JsonDocumentOptions { MaxDepth = 8 });
        JsonElement root = doc.RootElement;
        Fields(root, "format", "version", "fileMac", "metadataAuth");
        if (Text(root, "format") != "CWM" || root.GetProperty("version").GetRawText() != "1")
            throw new FormatException("Unsupported CWM format/version.");
        JsonElement fm = root.GetProperty("fileMac"), auth = root.GetProperty("metadataAuth");
        Fields(fm, "file", "size", "mechanism", "parameters", "mac");
        Fields(auth, "mechanism", "canonicalization", "keyDerivation", "mac");
        if (Text(auth, "mechanism") != "HMAC-SHA256" || Text(auth, "canonicalization") != "RFC8785-JCS" ||
            Text(auth, "keyDerivation") != "HKDF-SHA256") throw new FormatException("Unsupported CWM metadata algorithm.");
        string file = Text(fm, "file");
        SafeName(file);
        if (!fm.GetProperty("size").TryGetInt64(out long size) || size < 0 || size > CwmJcs.MaximumInteger)
            throw new FormatException("Invalid CWM file size.");
        string mechanism = Text(fm, "mechanism");
        byte[] expectedFile = Hex(fm, "mac"), expectedAuth = Hex(auth, "mac");
        ValidateFileSchema(mechanism, fm.GetProperty("parameters"), expectedFile.Length);
        if (expectedAuth.Length != 32) throw new FormatException("CWM metadata MAC must be 32 bytes.");
        byte[] actualAuth = Authenticate(args, fm); // Also rejects duplicate parameters and malformed Unicode.
        try
        {
            token.ThrowIfCancellationRequested();
            if (!CryptographicOperations.FixedTimeEquals(expectedAuth, actualAuth)) return Result(false);
        }
        finally { CryptographicOperations.ZeroMemory(actualAuth); }
        token.ThrowIfCancellationRequested();
        if (mechanism != p.Mechanism || !CwmJcs.Canonicalize(fm.GetProperty("parameters")).AsSpan()
            .SequenceEqual(CwmJcs.Canonicalize(JsonSerializer.SerializeToElement(effective))))
            throw new ArgumentException("CWM mechanism/parameters disagree with PARAM.");
        if (expectedFile.Length != macLength) throw new FormatException("Invalid CWM file MAC length.");
        // Only authenticated references reach this secure, directory-relative opening.
        using var source = publication.OpenSource(file);
        if (source.Length != size) return Result(false);
        var (actualFile, actualSize) = FileMacCoordinator.MacOpened(args, source, token, verify: true);
        byte[] actual = FormatConversions.ToByteArray(actualFile.Value, actualFile.ValueFormat);
        bool matches = CryptographicOperations.FixedTimeEquals(expectedFile, actual);
        return Result(matches & (size == actualSize));
    }

    private static VariableDeclaration Result(bool valid) => new ValVariableDeclaration(valid);

    // Validate the stored schema independently of the caller's PARAM and key.
    // This must run before authentication can return false, and never opens a file.
    private static void ValidateFileSchema(string mechanism, JsonElement parameters, int tagLength)
    {
        if (!MechanismRegistry.TryGet(mechanism, out var entry) || mechanism != entry!.CanonicalName ||
            !FileMacCoordinator.Supports(entry))
            throw new FormatException("Unsupported CWM file mechanism.");
        int length;
        if (mechanism is "AES-CBC-MAC" or "DES3-CBC" or "DES3-RETAIL")
        {
            bool aes = mechanism == "AES-CBC-MAC", retail = mechanism == "DES3-RETAIL";
            Fields(parameters, retail ? ["padding", "macLength"] : ["padding", "macLength", "iv"]);
            string padding = Text(parameters, "padding");
            if (padding is not ("NONE" or "PKCS-7" or "ANSI-X923" or "ISO-7816" or
                "ISO-9797-M1" or "ISO-9797-M2" or "ISO-9797-M3" or "TLS-CBC") ||
                retail && padding is not ("ISO-9797-M1" or "ISO-9797-M2"))
                throw new FormatException("Invalid CWM padding.");
            var size = parameters.GetProperty("macLength");
            if (size.ValueKind != JsonValueKind.Number || !size.TryGetInt32(out length) ||
                length < (aes ? 8 : 4) || length > (aes ? 16 : 8))
                throw new FormatException("Invalid CWM MAC length parameter.");
            if (!retail && Text(parameters, "iv") != new string('0', aes ? 32 : 16))
                throw new FormatException("CWM CBC-MAC requires a zero IV.");
        }
        else if (mechanism == "AES-GMAC")
        {
            Fields(parameters, "nonce");
            // GMAC admits any nonempty byte nonce; the container itself bounds its size.
            string nonce = Text(parameters, "nonce");
            if (nonce.Length == 0 || nonce.Length % 2 != 0 || nonce.Any(c => !char.IsAsciiHexDigitUpper(c)))
                throw new FormatException("Invalid CWM nonce.");
            length = 16;
        }
        else
        {
            Fields(parameters);
            length = mechanism switch
            {
                "AES-CMAC" => 16,
                "DES3-CMAC" => 8,
                _ => DigestFactory.Create(mechanism).GetDigestSize()
            };
        }
        if (tagLength != length) throw new FormatException("Invalid CWM file MAC length.");
    }

    internal static byte[] Authenticate(AlgorithmCallArguments args, JsonElement fileMac)
    {
        var key = AlgorithmArgumentResolver.ResolveKey(args.Arguments[1]);
        byte[] material = FormatConversions.ToByteArray(key.Value, key.ValueFormat), derived = new byte[32];
        try
        {
            HKDF.DeriveKey(HashAlgorithmName.SHA256, material, derived, Encoding.UTF8.GetBytes(Salt), Encoding.UTF8.GetBytes(Info));
            return HMACSHA256.HashData(derived, CwmJcs.Canonicalize(fileMac));
        }
        finally { CryptographicOperations.ZeroMemory(material); CryptographicOperations.ZeroMemory(derived); }
    }

    internal static Dictionary<string, object> Parameters(ParameterVariableDeclaration p)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (p.Mechanism == "AES-GMAC")
        {
            string nonce = p.GetParameter("NONCE");
            result.Add("nonce", Convert.ToHexString(FormatConversions.ToByteArray(nonce, FormatConversions.ParseString(nonce))));
        }
        if (p.Mechanism is "AES-CBC-MAC" or "DES3-CBC" or "DES3-RETAIL")
        {
            result.Add("padding", p.GetParameter("PAD").ToUpperInvariant());
            string length = p.GetParameter("MACLEN");
            result.Add("macLength", length == "" ? (p.Mechanism == "AES-CBC-MAC" ? 16 : 8) : int.Parse(length.Trim('"'), CultureInfo.InvariantCulture));
            if (p.Mechanism != "DES3-RETAIL") result.Add("iv", new string('0', p.Mechanism == "AES-CBC-MAC" ? 32 : 16));
        }
        return result;
    }

    private static void Fields(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object) throw new FormatException("CWM object required.");
        string[] actual = obj.EnumerateObject().Select(p => p.Name).ToArray();
        if (actual.Length != names.Length || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            names.Except(actual, StringComparer.Ordinal).Any()) throw new FormatException("Unexpected/duplicate CWM fields.");
    }
    private static string Text(JsonElement obj, string name) => obj.GetProperty(name).GetString()
        ?? throw new FormatException("CWM string required.");
    private static byte[] Hex(JsonElement obj, string name)
    {
        string text = Text(obj, name);
        if (text.Length == 0 || text.Length > 128 || text.Length % 2 != 0 || text.Any(c => !char.IsAsciiHexDigitUpper(c)))
            throw new FormatException("CWM requires uppercase, unprefixed whole-byte HEX.");
        return Convert.FromHexString(text);
    }
    private static void SafeName(string name)
    {
        _ = new UTF8Encoding(false, true).GetByteCount(name);
        // v1 deliberately admits a single relative component, on every supported OS.
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ') ||
            name.Any(c => c < ' ' || "/\\:<>\"|?*".Contains(c)) || Path.IsPathRooted(name))
            throw new FormatException("Unsafe CWM file reference.");
    }
}
