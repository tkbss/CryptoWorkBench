using System.Collections.ObjectModel;
using System.Globalization;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>Immutable header specification. No complete wire header exists until Wrap resolves KBLEN.</summary>
public sealed class Tr31HeaderDefinition
{
    private readonly OptionalBlock[] optionalBlocks;
    private readonly string optionalWire;
    public IReadOnlyDictionary<string, string> Attributes { get; }
    // OptionalBlock is a legacy mutable DTO. Return defensive copies at this boundary.
    public IReadOnlyList<OptionalBlock> OptionalBlocks => Array.AsReadOnly(optionalBlocks.Select(Clone).ToArray());
    public int? ExpectedLength { get; }
    public bool IsLengthResolved => false;
    public string Version => Attributes["KBVID"];
    public string Algorithm => Attributes["ALGO"];
    public int HeaderLength => 16 + optionalWire.Length;

    private Tr31HeaderDefinition(Dictionary<string, string> attributes, List<OptionalBlock> blocks, int? expected)
    {
        Attributes = new ReadOnlyDictionary<string, string>(attributes);
        optionalBlocks = blocks.Select(Clone).ToArray();
        optionalWire = string.Concat(optionalBlocks.Select(b => b.Serialize()));
        ExpectedLength = expected;
    }

    public static Tr31HeaderDefinition Create(IEnumerable<KeyValuePair<string, string>> fields)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        var suppliedBlocks = new List<(string Id, string Raw)>();
        string? pending = null;
        foreach (var (name, raw) in fields)
        {
            if (!Tr31AttributeRegistry.Fields.Contains(name))
                throw new ArgumentException($"Unknown TR-31 attribute {name}.");
            if (pending is not null && name != "OPTBD")
                throw new ArgumentException($"OPTID {pending} must be immediately followed by OPTBD.");
            if (name == "OPTID") { pending = raw; continue; }
            if (name == "OPTBD")
            {
                if (pending is null) throw new ArgumentException("OPTBD requires a preceding OPTID.");
                suppliedBlocks.Add((pending, raw));
                pending = null;
                continue;
            }
            string value = raw.StartsWith('"') ? Tr31AttributeRegistry.DecodeText(raw) : raw;
            if (!attributes.TryAdd(name, value))
                throw new ArgumentException($"Duplicate TR-31 attribute {name}.");
        }
        if (pending is not null) throw new ArgumentException($"OPTID {pending} is missing OPTBD.");
        foreach (string required in new[] { "KBVID", "KEYU", "ALGO", "MODEU", "EXP" })
            if (!attributes.ContainsKey(required)) throw new ArgumentException($"Missing required attribute {required}.");
        attributes.TryAdd("KEYVN", "00");
        attributes.TryAdd("KEYCTX", "0");
        attributes.TryAdd("RSV", "0");
        string version = attributes["KBVID"], usage = attributes["KEYU"], algorithm = attributes["ALGO"];
        Tr31AttributeRegistry.ValidateAttributes(version, usage, algorithm, attributes["MODEU"], attributes["EXP"]);
        if (attributes["KEYVN"].Length != 2 || !attributes["KEYVN"].All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException("KEYVN must contain exactly two ASCII alphanumeric characters.");
        // X9.143-2022 table 1 defines context independently of the binding version.
        if (attributes["KEYCTX"] is not ("0" or "1" or "2"))
            throw new ArgumentException("KEYCTX must be 0, 1 or 2.");
        if (attributes["RSV"] != "0") throw new ArgumentException("RSV must be 0.");
        int? expected = attributes.TryGetValue("KBLEN", out var length) ? Decimal(length, "KBLEN", 16, 9999) : null;
        int? expectedCount = attributes.TryGetValue("NUMOPTB", out var count) ? Decimal(count, "NUMOPTB", 0, 99) : null;
        var blocks = new List<OptionalBlock>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, raw) in suppliedBlocks)
        {
            if (!ids.Add(id)) throw new ArgumentException($"Duplicate OPTID {id}.");
            if (blocks.LastOrDefault()?.ID == "PB") throw new ArgumentException("PB must be the last Optional Block.");
            string data = Tr31AttributeRegistry.ValidateOptional(id, raw, version, usage, algorithm);
            var block = new OptionalBlock { ID = id, Data = data };
            block.Length = block.Serialize().Length;
            blocks.Add(block);
        }
        int blockSize = version == "D" ? 16 : 8;
        int optionalLength = blocks.Sum(b => b.Length);
        if (optionalLength % blockSize != 0)
        {
            if (ids.Contains("PB")) throw new ArgumentException("Explicit PB does not align the Optional Blocks.");
            int paddingLength = blockSize - optionalLength % blockSize;
            if (paddingLength < 4) paddingLength += blockSize;
            blocks.Add(new OptionalBlock { ID = "PB", Length = paddingLength, Data = new string('0', paddingLength - 4) });
        }
        if (blocks.Count > 99 || expectedCount.HasValue && expectedCount.Value != blocks.Count)
            throw new ArgumentException("NUMOPTB conflicts with the serialized block count (including PB), or exceeds 99.");
        attributes["NUMOPTB"] = blocks.Count.ToString("D2", CultureInfo.InvariantCulture);
        var definition = new Tr31HeaderDefinition(attributes, blocks, expected);
        if (definition.HeaderLength + 2 * TR31Block.GetAuthenticationValueLength(version[0]) > 9999)
            throw new ArgumentException("TR-31 header exceeds the four-digit total length limit.");
        return definition;
    }

    public string SerializeForWrap(int confidentialDataLength)
    {
        int total = checked(HeaderLength + 2 * (confidentialDataLength + TR31Block.GetAuthenticationValueLength(Version[0])));
        if (confidentialDataLength <= 0 || confidentialDataLength % (Version == "D" ? 16 : 8) != 0 || total > 9999)
            throw new ArgumentException("TR-31 total length or confidential-data alignment is invalid.");
        if (ExpectedLength is int expected && expected != total)
            throw new ArgumentException($"KBLEN expected {expected}, but Wrap produces {total} characters.");
        return Serialize(total.ToString("D4", CultureInfo.InvariantCulture));
    }

    /// <summary>After authentication: exact comparison, with only the unresolved decimal length filled from the wire.</summary>
    public void ValidateAuthenticatedHeader(string header)
    {
        if (header.Length < 16) throw new ArgumentException("Incomplete authenticated header.");
        int length = Decimal(header.Substring(1, 4), "KBLEN", 16, 9999);
        if (ExpectedLength is int expected && expected != length || header != Serialize(header.Substring(1, 4)))
            throw new ArgumentException("Authenticated TR-31 header does not match the TR31H definition.");
    }

    private string Serialize(string length) => Version + length + Attributes["KEYU"] + Algorithm +
        Attributes["MODEU"] + Attributes["KEYVN"] + Attributes["EXP"] + Attributes["NUMOPTB"] +
        Attributes["KEYCTX"] + Attributes["RSV"] + optionalWire;
    public override string ToString() => "TR31H { " +
        string.Join(", ", Attributes.Where(p => p.Key != "KBLEN").Select(p => $"{p.Key}:{p.Value}")) +
        $", KBLEN:auto (unresolved{(ExpectedLength is int n ? $"; expected {n}" : "")})" +
        string.Concat(optionalBlocks.Select(b => $", OPTID:{b.ID}, OPTBD:{Newtonsoft.Json.JsonConvert.SerializeObject(b.Data)}")) + " }";
    private static OptionalBlock Clone(OptionalBlock b) => new() { ID = b.ID, Data = b.Data, Length = b.Length };
    private static int Decimal(string value, string name, int min, int max)
    {
        if (value.Length == 0 || value.Any(c => !char.IsAsciiDigit(c)) ||
            !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < min || n > max)
            throw new ArgumentException($"{name} must be decimal in {min}..{max}.");
        return n;
    }
}
