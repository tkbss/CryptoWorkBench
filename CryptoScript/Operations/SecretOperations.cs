using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.SecretSharing;
using CryptoScript.Variables;
using CryptoScript.CryptoAlgorithm.WRAPPERS;

namespace CryptoScript.Operations;

public static class SecretOperations
{
    public static VariableDeclaration Share(OperationInvocation invocation)
    {
        if (invocation.Arguments.Count != 2 || invocation.Arguments[0].SourceVariable is not ParameterVariableDeclaration p ||
            invocation.Arguments[1].SourceVariable is not { Type: CryptoTypeKey } key)
            throw new ArgumentException("Share requires (PARAM, KEY) variable references.");
        var settings = SecretParameters.Read(p);
        var typedKey = key as KeyVariableDeclaration;
        var metadata = typedKey is null ? null : new SecretKeyMetadata(typedKey.KeyType, typedKey.Usage,
            typedKey.DerivationMechanism, typedKey.KeyAttributes.Select(a => new SecretKeyAttribute(a.ID, a.Length, a.Data)));
        byte[] material = DecodeKey(key.Value);
        try
        {
            if (typedKey is not null && typedKey.KeySizeInBits.IsKnown && typedKey.KeySizeInBits.Bits != (ulong)material.Length * 8)
                throw new ArgumentException("Key size metadata conflicts with the key material.");
            return new SecretVariableDeclaration(SecretSharingEngine.Share(settings.Descriptor(checked(material.Length * 8)),
                material, settings.Format, metadata));
        }
        finally { CryptographicOperations.ZeroMemory(material); }
    }

    public static VariableDeclaration Secret(OperationInvocation invocation)
    {
        if (invocation.Arguments.Count < 2 || invocation.Arguments[0].SourceVariable is not ParameterVariableDeclaration p)
            throw new ArgumentException("Secret requires a PARAM reference and at least one share.");
        var settings = SecretParameters.Read(p);
        var arguments = invocation.Arguments.Skip(1).ToArray();
        bool separate = settings.Format == "SEPARATE-X";
        if (separate && arguments.Length % 2 != 0) throw new ArgumentException("SEPARATE-X requires integer/HEX pairs.");
        if (arguments.Length / (separate ? 2 : 1) > settings.Parts) throw new ArgumentException("Too many shares.");
        var shares = new List<SecretShare>();
        for (int i = 0; i < arguments.Length; i += separate ? 2 : 1)
        {
            int x = 0;
            if (separate)
            {
                if (arguments[i].Kind != ResolvedCallArgumentKind.OtherLiteral)
                    throw new ArgumentException("SEPARATE-X requires a literal integer index.");
                x = SecretParameters.Integer(arguments[i].Value);
            }
            var argument = arguments[i + (separate ? 1 : 0)];
            if (argument.Kind != ResolvedCallArgumentKind.HexLiteral)
                throw new ArgumentException("Share payloads must be HEX literals.");
            byte[] bytes = DecodeHex(argument.Value);
            try
            {
                if (separate) shares.Add(new SecretShare(x, bytes));
                else
                {
                    if (bytes.Length < 2) throw new ArgumentException("Share must contain an index and nonempty payload.");
                    shares.Add(settings.Format == "X-Y" ? new SecretShare(bytes[0], bytes.AsSpan(1))
                        : new SecretShare(bytes[^1], bytes.AsSpan(0, bytes.Length - 1)));
                }
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        return new SecretVariableDeclaration(new SecretSet(settings.Descriptor(checked(shares[0].Bytes.Length * 8)), shares, settings.Format));
    }

    public static VariableDeclaration Restore(OperationInvocation invocation)
    {
        if (invocation.Arguments.Count != 1 || invocation.Arguments[0].SourceVariable is not SecretVariableDeclaration source)
            throw new ArgumentException("Restore requires one SECRET variable reference.");
        byte[] material = SecretSharingEngine.Restore(source.SecretSet);
        try
        {
            var metadata = source.SecretSet.OriginalKeyMetadata;
            string value = FormatConversions.ByteArrayToHexString(material);
            return new KeyVariableDeclaration
            {
                Type = new CryptoTypeKey(), Value = value, KeyValue = value, ValueFormat = FormatConversions.HEX,
                KeySizeInBits = new KeySize(checked(material.Length * 8)),
                KeyType = metadata?.KeyType ?? KeyType.Secret(KeyAlgorithm.Unknown),
                Usage = metadata?.Usage ?? KeyUsagePolicy.Unspecified,
                DerivationMechanism = metadata?.DerivationMechanism ?? string.Empty,
                KeyAttributes = metadata?.Attributes.Select(a => new OptionalBlock
                    { ID = a.Id, Length = a.Length, Data = a.Data }).ToList() ?? new()
            };
        }
        finally { CryptographicOperations.ZeroMemory(material); }
    }

    internal static byte[] DecodeHex(string? text)
    {
        if (text is null || text.Length < 6 || !text.StartsWith("0x(", StringComparison.Ordinal) || text[^1] != ')' ||
            (text.Length - 4) % 2 != 0 || text.AsSpan(3, text.Length - 4).IndexOfAnyExcept("0123456789ABCDEFabcdef") >= 0)
            throw new ArgumentException("Expected a nonempty HEX literal containing whole bytes.");
        return Convert.FromHexString(text.AsSpan(3, text.Length - 4));
    }

    private static byte[] DecodeKey(string text)
    {
        if (!text.StartsWith("b64(", StringComparison.Ordinal)) return DecodeHex(text);
        if (!text.EndsWith(')')) throw new ArgumentException("Expected binary KEY material.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(text[4..^1]); }
        catch (FormatException) { throw new ArgumentException("Invalid Base64 KEY material."); }
        if (bytes.Length == 0) throw new ArgumentException("KEY material must not be empty.");
        return bytes;
    }

    public static VariableDeclaration RequireTypedArguments(string[] _) =>
        throw new ArgumentException("Secret sharing operations require structured variable references.");
}
