using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.EP2;

/// <summary>Object-aware EP2 data encryption; nonce lifecycle belongs to the caller.</summary>
public sealed class Ep2DataEncryption : CryptoAlgorithm
{
    internal const string Mechanism = "EP2-DATA-ENCRYPTION";

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        if (Normalize(mechanism) != Mechanism)
            throw new ArgumentException("EP2 encryption requires its selected mechanism.");
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

    internal static void ValidateValues(ParameterVariableDeclaration parameters)
    {
        if (parameters.ExplicitParameterNames.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new ArgumentException("EP2 encryption does not allow duplicate parameters.");
        string nonce = parameters.GetParameter("#NONCE");
        string offset = parameters.GetParameter("#OFFSET");
        if (!IsHex(nonce) || nonce.Length != 28)
            throw new ArgumentException("EP2 nonce must be a HEX value containing exactly 12 bytes.");
        if (!IsHex(offset) || offset.Length != 6)
            throw new ArgumentException("EP2 offset must be a HEX value containing exactly one byte.");
        string direction = parameters.GetParameter("#DIRECTION");
        if (direction is not ("\"SEND\"" or "\"RECEIVE\""))
            throw new ArgumentException("EP2 direction must be the normal string literal SEND or RECEIVE.");
        Ep2DataEncryptionPrimitive.ValidateOffset(Convert.ToByte(offset.Substring(3, 2), 16), direction[1..^1]);
    }

    public override StringVariableDeclaration Encrypt(AlgorithmCallArguments arguments) => Transform(arguments, true);
    public override StringVariableDeclaration Decrypt(AlgorithmCallArguments arguments) => Transform(arguments, false);
    public override StringVariableDeclaration Encrypt(string[] parameters) => throw LegacyCall();
    public override StringVariableDeclaration Decrypt(string[] parameters) => throw LegacyCall();
    private static ArgumentException LegacyCall() =>
        new("EP2 encryption requires structured arguments with genuine PARAM and KEY references.");

    public override StringVariableDeclaration Hash(string[] parameters) => throw Unsupported();
    public override StringVariableDeclaration Mac(string[] parameters) => throw Unsupported();
    public override StringVariableDeclaration Wrap(string[] parameters) => throw Unsupported();
    public override VariableDeclaration Unwrap(string[] parameters) => throw Unsupported();
    public override BlockHeaderVariableDeclaration GenerateBlockHeader(string[] parameters) => throw Unsupported();
    public override BlockHeaderVariableDeclaration GenerateBlockHeader(string mechanism) => throw Unsupported();
    private static NotSupportedException Unsupported() =>
        new("EP2 data encryption supports only Parameters, Encrypt and Decrypt.");

    private static StringVariableDeclaration Transform(AlgorithmCallArguments arguments, bool encrypt)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Arguments.Count != 3)
            throw new ArgumentException("EP2 encryption requires exactly three arguments.");
        var p = arguments.Arguments[0];
        if (!IsOriginalReference(p) || p.SourceVariable is not ParameterVariableDeclaration parameters ||
            Normalize(parameters.Mechanism) != Mechanism)
            throw new ArgumentException("EP2 encryption requires a genuine matching PARAM reference at position 0.");
        MechanismParameterContractValidator.Validate(Mechanism,
            encrypt ? CryptoScriptFunction.Encrypt : CryptoScriptFunction.Decrypt, parameters);
        var k = arguments.Arguments[1];
        if (!IsOriginalReference(k) || k.SourceVariable is not KeyVariableDeclaration key)
            throw new ArgumentException("EP2 encryption requires a genuine KEY reference at position 1.");
        KeyMetadataValidator.Validate(key, KeyAlgorithm.Aes, encrypt ? KeyUsage.Encrypt : KeyUsage.Decrypt,
            encrypt ? "Encrypt" : "Decrypt", Mechanism, arguments.DiagnosticSink);
        var d = arguments.Arguments[2];
        string dataValue;
        if (d.Kind == ResolvedCallArgumentKind.HexLiteral && d.SourceVariable is null && !d.HasReconstructedSourceVariable)
            dataValue = d.Value ?? string.Empty;
        else if (IsOriginalReference(d) && d.SourceVariable is StringVariableDeclaration data &&
                 data.Type is CryptoTypeVar && data.ValueFormat == FormatConversions.HEX)
            dataValue = data.Value;
        else
            throw new ArgumentException("EP2 encryption data must be a direct HEX literal or a genuine HEX VAR reference.");
        byte[]? keyBytes = null;
        byte[]? input = null;
        byte[]? output = null;
        try
        {
            keyBytes = DecodeHex(string.IsNullOrEmpty(key.KeyValue) ? key.Value : key.KeyValue);
            if (keyBytes.Length != 16 || key.KeySizeInBits.IsKnown && key.KeySizeInBits != new KeySize(128))
                throw new ArgumentException("EP2 encryption requires exactly 16 key bytes and consistent key size metadata.");
            Ep2DataEncryptionPrimitive.ValidateDataLength(Math.Max(0, ((long)dataValue.Length - 4) / 2));
            input = DecodeHex(dataValue);
            output = Ep2DataEncryptionPrimitive.Transform(keyBytes, DecodeHex(parameters.GetParameter("#NONCE")),
                Convert.ToByte(parameters.GetParameter("#OFFSET").Substring(3, 2), 16),
                parameters.GetParameter("#DIRECTION")[1..^1], input);
            return new StringVariableDeclaration
            {
                Type = new CryptoTypeVar(), ValueFormat = FormatConversions.HEX,
                Value = FormatConversions.ByteArrayToHexString(output)
            };
        }
        finally
        {
            if (keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
            if (input is not null) CryptographicOperations.ZeroMemory(input);
            if (output is not null) CryptographicOperations.ZeroMemory(output);
        }
    }

    private static bool IsOriginalReference(AlgorithmCallArgument argument) =>
        argument.Kind == ResolvedCallArgumentKind.Variable && !argument.HasReconstructedSourceVariable;

    private static bool IsHex(string value) => value is not null && value.Length >= 4 &&
        value.StartsWith("0x(", StringComparison.Ordinal) && value.EndsWith(')') &&
        (value.Length - 4) % 2 == 0 && value.AsSpan(3, value.Length - 4).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;

    private static byte[] DecodeHex(string value)
    {
        if (!IsHex(value))
            throw new ArgumentException("EP2 encryption requires valid HEX binary data.");
        return Convert.FromHexString(value.AsSpan(3, value.Length - 4));
    }

    private static string Normalize(string value) =>
        (value.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase) ? value[6..] : value).ToUpperInvariant();
}
