using System.Collections.Frozen;
using System.Globalization;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>Builder's supported symmetric transport profile, based on X9.143-2022 tables 1-7.
/// The lexer is deliberately not the authority for field semantics.</summary>
public static class Tr31AttributeRegistry
{
    public sealed record Usage(string Description, string Modes, string Versions, string Algorithms);
    public static IReadOnlyDictionary<string, Usage> KeyUsages { get; } =
        new Dictionary<string, Usage>
        {
            ["B0"] = new("DUKPT base derivation key", "X", "BCD", "AT"),
            ["B1"] = new("DUKPT initial key", "X", "BCD", "AT"),
            ["B2"] = new("Base key for variants (deprecated)", "Y", "BCD", "T"),
            ["B3"] = new("Non-DUKPT derivation key", "X", "BCD", "AT"),
            ["C0"] = new("Card verification key", "CGV", "ABCD", "T"),
            ["D0"] = new("Symmetric data encryption key", "BDE", "ABCD", "AT"),
            ["D2"] = new("Decimalization table encryption key", "BDE", "BCD", "AT"),
            ["D3"] = new("Sensitive data encryption key", "BDE", "D", "A"),
            ["E0"] = new("EMV issuer master: application cryptograms", "X", "BCD", "AT"),
            ["E1"] = new("EMV issuer master: messaging confidentiality", "X", "BCD", "AT"),
            ["E2"] = new("EMV issuer master: messaging integrity", "X", "BCD", "AT"),
            ["E3"] = new("EMV issuer master: authentication code", "X", "BCD", "AT"),
            ["E4"] = new("EMV issuer master: dynamic numbers", "X", "BCD", "AT"),
            ["E5"] = new("EMV issuer master: personalization", "X", "BCD", "AT"),
            ["E6"] = new("EMV issuer master: other", "X", "BCD", "AT"),
            ["F0"] = new("EMV card: application cryptograms", "X", "BCD", "AT"),
            ["F1"] = new("EMV card: messaging confidentiality", "X", "BCD", "AT"),
            ["F2"] = new("EMV card: messaging integrity", "X", "BCD", "AT"),
            ["F3"] = new("EMV card: authentication code", "X", "BCD", "AT"),
            ["F4"] = new("EMV card: dynamic numbers", "X", "BCD", "AT"),
            ["F5"] = new("EMV card: personalization", "X", "BCD", "AT"),
            ["F6"] = new("EMV card: other", "X", "BCD", "AT"),
            ["K0"] = new("Key encryption/wrapping key", "BDE", "ABCD", "AT"),
            ["K1"] = new("X9.143 key block protection key", "BDE", "BCD", "AT"),
            ["K4"] = new("ISO 20038 key block protection key", "BDE", "D", "A"),
            ["M0"] = new("ISO 16609 MAC algorithm 1", "CGV", "ABCD", "T"),
            ["M1"] = new("ISO 9797-1 MAC algorithm 1", "CGV", "ABCD", "T"),
            ["M2"] = new("ISO 9797-1 MAC algorithm 2", "CGV", "ABCD", "T"),
            ["M3"] = new("ISO 9797-1 MAC algorithm 3", "CGV", "ABCD", "T"),
            ["M4"] = new("ISO 9797-1 MAC algorithm 4", "CGV", "ABCD", "T"),
            ["M5"] = new("ISO 9797-1:1999 MAC algorithm 5", "CGV", "ABCD", "T"),
            ["M6"] = new("CMAC / ISO 9797-1:2011 algorithm 5", "CGV", "BCD", "AT"),
            ["M8"] = new("ISO 9797-1:2011 MAC algorithm 6", "CGV", "BCD", "AT"),
            ["P0"] = new("PIN encryption key", "BDE", "ABCD", "AT"),
            ["P2"] = new("Other PIN generation algorithm", "G", "BD", "AT"),
            ["V0"] = new("Other PIN verification algorithm", "CGV", "ABCD", "AT"),
            ["V1"] = new("IBM 3624 PIN verification", "CGV", "ABCD", "T"),
            ["V2"] = new("VISA PVV PIN verification", "CGV", "ABCD", "T"),
            ["V3"] = new("X9.132 PIN verification algorithm 1", "CGV", "D", "A"),
            ["V4"] = new("X9.132 PIN verification algorithm 2", "CGV", "D", "A"),
            ["V5"] = new("X9.132 PIN verification algorithm 3", "CGV", "D", "A")
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlySet<string> Fields { get; } =
        "KBVID KBLEN KEYU ALGO MODEU KEYVN EXP NUMOPTB KEYCTX RSV OPTID OPTBD"
            .Split(' ').ToFrozenSet(StringComparer.Ordinal);
    public static IReadOnlySet<string> KnownOptionalIds { get; } =
        "AL BI CT DA HM IK KC KP KS KV LB PA PB PK TC TS WP".Split(' ').ToFrozenSet(StringComparer.Ordinal);
    public static IReadOnlySet<string> SupportedOptionalIds { get; } =
        "BI DA IK KC KP KS KV LB PA PB PK TC TS WP".Split(' ').ToFrozenSet(StringComparer.Ordinal);

    internal static void ValidateAttributes(string version, string usage, string algorithm, string mode, string export)
    {
        if (version.Length != 1 || !"ABCD".Contains(version))
            throw new ArgumentException("KBVID must be A, B, C or D.");
        if (algorithm is not ("A" or "T") || version != "D" && algorithm != "T")
            throw new ArgumentException("ALGO: the builder supports A/T in version D and T in versions A/B/C.");
        if (!KeyUsages.TryGetValue(usage, out var rule))
            throw new ArgumentException($"KEYU '{usage}' is unknown or unsupported by the symmetric transport profile.");
        if (mode.Length != 1 || !rule.Modes.Contains(mode) || !rule.Versions.Contains(version) ||
            !rule.Algorithms.Contains(algorithm))
            throw new ArgumentException($"KEYU {usage}, ALGO {algorithm}, MODEU {mode}, KBVID {version} is not a supported combination.");
        if (export is not ("E" or "N" or "S"))
            throw new ArgumentException("EXP must be E, N or S.");
    }

    internal static string ValidateOptional(string id, string raw, string version, string usage, string algorithm)
    {
        if (!SupportedOptionalIds.Contains(id))
            throw new ArgumentException($"OPTID {id} is {(KnownOptionalIds.Contains(id) ? "known but unsupported by the symmetric transport profile" : "unknown")}.");
        bool hexLiteral = raw.StartsWith("0x(", StringComparison.Ordinal) && raw.EndsWith(')');
        bool textLiteral = raw.StartsWith('"') && raw.EndsWith('"');
        if (!hexLiteral && !textLiteral)
            throw new ArgumentException($"OPTBD for {id} requires a quoted string or an explicit hex literal.");
        string data = hexLiteral ? raw[3..^1] : DecodeText(raw);
        if (data.Any(c => c < ' ' || c > '~'))
            throw new ArgumentException($"OPTBD {id} must contain printable ASCII.");
        bool textOnly = id is "LB" or "PA" or "PB" or "TC" or "TS" or "DA";
        if (textOnly && hexLiteral)
            throw new ArgumentException($"OPTBD {id} requires text; binary-to-text conversion is not implicit.");
        bool valid = id switch
        {
            "LB" or "PB" => true,
            "PA" => data.Length > 0 && usage is "B3" or "K0" or "P2" or "V0",
            "KV" => data == "0000",
            "WP" => data is "000" or "001" or "002" or "003",
            "BI" => usage == "B1" && IsHex(data) &&
                (algorithm == "A" && data.Length == 10 && data.StartsWith("01") ||
                 algorithm == "T" && data.Length == 12 && data.StartsWith("00")),
            "IK" => usage == "B1" && algorithm == "A" && data.Length == 16 && IsHex(data),
            "KS" => usage == "B1" && algorithm == "T" && data.Length == 20 && IsHex(data) &&
                (ulong.Parse(data[^6..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) & 0x1fffff) == 0,
            "KC" or "KP" or "PK" => IsHex(data) &&
                (data.Length == 8 && data.StartsWith("00") &&
                    (id == "PK" || (id == "KC" ? algorithm == "T" : version != "D")) ||
                 data.Length == 12 && data.StartsWith("01")),
            "TC" or "TS" => DateTime.TryParseExact(data,
                ["yyyyMMddHHmmss'Z'", "yyyyMMddHHmmssff'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.ff'Z'"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "DA" => ValidateDerivations(data, version, usage),
            _ => false
        };
        if (!valid) throw new ArgumentException($"Invalid OPTBD structure or header combination for {id}.");
        return textOnly ? data : data.ToUpperInvariant();
    }

    private static bool ValidateDerivations(string data, string version, string usage)
    {
        if (usage != "B3" || !data.StartsWith("01") ||
            data.Length < 7 || (data.Length - 2) % 5 != 0) return false;
        for (int i = 2; i < data.Length; i += 5)
            ValidateAttributes(version, data.Substring(i, 2), data.Substring(i + 2, 1),
                data.Substring(i + 3, 1), data.Substring(i + 4, 1));
        return true;
    }

    internal static bool IsHex(string value) => value.Length > 0 &&
        value.AsSpan().IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
    internal static string DecodeText(string raw) =>
        Newtonsoft.Json.JsonConvert.DeserializeObject<string>(raw)
        ?? throw new ArgumentException("Expected a string.");
}
