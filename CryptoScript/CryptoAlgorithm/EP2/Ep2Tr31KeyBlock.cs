using System.Text.RegularExpressions;
using CryptoScript.Model;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.EP2;

public sealed class Ep2Tr31KeyBlock : CryptoAlgorithm
{
    internal const string Mechanism = "EP2-TR31-KEY-BLOCK";

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) => GenerateParameters(mechanism, []);
    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        if (Normalize(mechanism) != Mechanism) throw new ArgumentException("EP2 TR-31 requires its matching mechanism.");
        var result = new ParameterVariableDeclaration { Mechanism = Mechanism };
        result.RecordExplicitParameter("#MECH");
        foreach (string parameter in parameters)
        {
            result.SetParameter(parameter);
            result.RecordExplicitParameter(parameter.Split(':', 2)[0]);
        }
        MechanismParameterContractValidator.Validate(Mechanism, CryptoScriptFunction.Parameters, result);
        return result;
    }

    internal static void ValidateValues(ParameterVariableDeclaration p)
    {
        if (p.ExplicitParameterNames.GroupBy(x => x.TrimStart('#'), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new ArgumentException("EP2 TR-31 does not allow duplicate parameters.");
        if (p.GetParameters().ContainsKey("#BLKH")) Ep2Tr31KeyBlockPrimitive.ValidateHeader(ReadHeader(p));
        if (p.GetParameters().ContainsKey("#RND"))
        {
            byte[] filler = DecodeHex(p.GetParameter("#RND"));
            try { if (filler.Length != 14) throw new ArgumentException("EP2 TR-31 requires exactly 14 random filler bytes."); }
            finally { Ep2Tr31KeyBlockPrimitive.Clear(filler); }
        }
    }

    public override StringVariableDeclaration Wrap(AlgorithmCallArguments arguments)
    {
        var (p, kbpk) = Resolve(arguments, CryptoScriptFunction.Wrap);
        KeyVariableDeclaration key = RequireKey(arguments.Arguments[2]);
        KeyMetadataValidator.Validate(key, KeyAlgorithm.Hmac, KeyUsage.None, "Wrap", Mechanism, arguments.DiagnosticSink);
        byte[]? protection = null, material = null, random = null;
        try
        {
            protection = DecodeKey(kbpk); material = DecodeKey(key);
            if (p.GetParameters().ContainsKey("#RND")) random = DecodeHex(p.GetParameter("#RND"));
            string wire = Ep2Tr31KeyBlockPrimitive.Wrap(protection, material, ReadHeader(p), random);
            var composite = new TR31String(wire[..16], Convert.FromHexString(wire[16..80]), Convert.FromHexString(wire[80..]));
            return new StringVariableDeclaration { Type = new CryptoTypeVar(), ValueFormat = FormatConversions.TR31, Value = composite.ToString() };
        }
        finally { Ep2Tr31KeyBlockPrimitive.Clear(protection, material, random); }
    }

    public override KeyVariableDeclaration Unwrap(AlgorithmCallArguments arguments)
    {
        var (p, kbpk) = Resolve(arguments, CryptoScriptFunction.Unwrap);
        var input = arguments.Arguments[2];
        if (!Original(input) || input.SourceVariable is not StringVariableDeclaration block || block.Type is not CryptoTypeVar ||
            (block.ValueFormat != FormatConversions.STR && block.ValueFormat != FormatConversions.TR31))
            throw new ArgumentException("EP2 TR-31 Unwrap requires a genuine full-wire or composite VAR reference.");
        string wire = ReadWire(block.Value);
        byte[]? protection = null, key = null;
        try
        {
            protection = DecodeKey(kbpk);
            key = Ep2Tr31KeyBlockPrimitive.Unwrap(protection, wire);
            // An optional expected header is checked against the authenticated header, never substituted into it.
            if (p.GetParameters().ContainsKey("#BLKH") && ReadHeader(p) != wire[..16])
                throw new ArgumentException("EP2 TR-31 authenticated header does not match the expected header.");
            string hex = FormatConversions.ByteArrayToHexString(key);
            return new KeyVariableDeclaration { Type = new CryptoTypeKey(), Value = hex,
                KeySize = "128", KeyType = KeyType.Secret(KeyAlgorithm.Hmac),
                KeyAttributes = [new OptionalBlock { ID = "HDR", Data = wire[..16] }] };
        }
        finally { Ep2Tr31KeyBlockPrimitive.Clear(protection, key); }
    }

    private static (ParameterVariableDeclaration, KeyVariableDeclaration) Resolve(AlgorithmCallArguments args, CryptoScriptFunction function)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Arguments.Count != 3) throw new ArgumentException("EP2 TR-31 requires exactly three arguments.");
        var first = args.Arguments[0];
        if (!Original(first) || first.SourceVariable is not ParameterVariableDeclaration p || Normalize(p.Mechanism) != Mechanism)
            throw new ArgumentException("EP2 TR-31 requires a genuine matching PARAM reference at position 0.");
        MechanismParameterContractValidator.Validate(Mechanism, function, p);
        var kbpk = RequireKey(args.Arguments[1]);
        KeyMetadataValidator.Validate(kbpk, KeyAlgorithm.Aes,
            function == CryptoScriptFunction.Wrap ? KeyUsage.WrapKey : KeyUsage.UnwrapKey,
            function.ToString(), Mechanism, args.DiagnosticSink);
        return (p, kbpk);
    }

    private static bool Original(AlgorithmCallArgument a) => a.Kind == ResolvedCallArgumentKind.Variable && !a.HasReconstructedSourceVariable;
    private static KeyVariableDeclaration RequireKey(AlgorithmCallArgument a) => Original(a) && a.SourceVariable is KeyVariableDeclaration k
        ? k : throw new ArgumentException("EP2 TR-31 requires genuine KEY references at key positions.");
    private static byte[] DecodeKey(KeyVariableDeclaration key)
    {
        byte[] bytes = DecodeHex(key.Value);
        if (bytes.Length == 16 && (!key.KeySizeInBits.IsKnown || key.KeySizeInBits == new KeySize(128))) return bytes;
        Ep2Tr31KeyBlockPrimitive.Clear(bytes);
        throw new ArgumentException("EP2 TR-31 requires 16 key bytes with consistent known size metadata.");
    }
    private static byte[] DecodeHex(string value)
    {
        if (value is null || value.Length < 4 || !value.StartsWith("0x(", StringComparison.Ordinal) || value[^1] != ')' ||
            (value.Length - 4) % 2 != 0 || value.AsSpan(3, value.Length - 4).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
            throw new ArgumentException("EP2 TR-31 requires complete HEX data.");
        return Convert.FromHexString(value.AsSpan(3, value.Length - 4));
    }
    private static string ReadHeader(ParameterVariableDeclaration p)
    {
        string value = p.GetParameter("#BLKH");
        if (value.Length != 18 || value[0] != '"' || value[^1] != '"')
            throw new ArgumentException("EP2 TR-31 BLKH must be a quoted 16-character header.");
        return value[1..^1];
    }
    private static string ReadWire(string value)
    {
        // Anchor every character: the generic reader intentionally accepts some legacy trailing data.
        if (value.Length == 114 && value[0] == '"' && value[^1] == '"') return value[1..^1];
        var match = Regex.Match(value, "\\A\"([^\"]{16})\"0x\\(([0-9A-Fa-f]{64})\\)0x\\(([0-9A-Fa-f]{32})\\)\\z", RegexOptions.CultureInvariant);
        if (!match.Success) throw new ArgumentException("EP2 TR-31 requires a complete full-wire or composite key block.");
        return match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value;
    }
    private static string Normalize(string value) => (value.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase) ? value[6..] : value).ToUpperInvariant();

    public override StringVariableDeclaration Wrap(string[] parameters) => throw Unsupported();
    public override VariableDeclaration Unwrap(string[] parameters) => throw Unsupported();
    public override StringVariableDeclaration Encrypt(string[] parameters) => throw Unsupported();
    public override StringVariableDeclaration Decrypt(string[] parameters) => throw Unsupported();
    public override StringVariableDeclaration Hash(string[] parameters) => throw Unsupported();
    public override StringVariableDeclaration Mac(string[] parameters) => throw Unsupported();
    public override BlockHeaderVariableDeclaration GenerateBlockHeader(string[] parameters) => throw Unsupported();
    public override BlockHeaderVariableDeclaration GenerateBlockHeader(string mechanism) => throw Unsupported();
    private static NotSupportedException Unsupported() => new("EP2 TR-31 supports only Parameters and structured Wrap/Unwrap calls with genuine references.");
}
