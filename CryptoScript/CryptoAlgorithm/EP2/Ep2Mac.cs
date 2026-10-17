using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.EP2;

/// <summary>EP2 8.3.0 sections 5.3, 5.6 and 8.2. Inputs are prepared by the caller.</summary>
public sealed class Ep2Mac : CryptoAlgorithm
{
    internal const string MessageMechanism = "EP2-MESSAGE-MAC";
    internal const string CertificateMechanism = "EP2-TRANSACTION-CERTIFICATE";
    private readonly string mechanism;

    public Ep2Mac(string mechanism)
    {
        this.mechanism = Normalize(mechanism);
        if (this.mechanism is not (MessageMechanism or CertificateMechanism))
            throw new ArgumentException("Unsupported EP2 MAC mechanism.");
    }

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        if (Normalize(mechanism) != this.mechanism || parameters.Length != 0)
            throw new ArgumentException("EP2 MAC requires its selected mechanism and no additional parameters.");
        var result = new ParameterVariableDeclaration { Mechanism = this.mechanism };
        result.RecordExplicitParameter("#MECH");
        MechanismParameterContractValidator.Validate(this.mechanism, CryptoScriptFunction.Parameters, result);
        return result;
    }

    // The base class has legacy empty-result stubs. EP2 must reject unsupported
    // operations even when a host calls the algorithm API without runtime dispatch.
    public override StringVariableDeclaration Hash(string[] parameters) => throw UnsupportedOperation();
    public override StringVariableDeclaration Encrypt(string[] parameters) => throw UnsupportedOperation();
    public override StringVariableDeclaration Decrypt(string[] parameters) => throw UnsupportedOperation();
    public override StringVariableDeclaration Wrap(string[] parameters) => throw UnsupportedOperation();
    public override VariableDeclaration Unwrap(string[] parameters) => throw UnsupportedOperation();

    private static NotSupportedException UnsupportedOperation() =>
        new("EP2 MAC mechanisms support only Parameters and Mac.");

    public override StringVariableDeclaration Mac(string[] parameters) =>
        throw new ArgumentException("EP2 MAC requires structured arguments with genuine PARAM and KEY references.");

    public override StringVariableDeclaration Mac(AlgorithmCallArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Arguments.Count != 3)
            throw new ArgumentException("EP2 Mac requires exactly three arguments.");
        var p = arguments.Arguments[0];
        if (!IsOriginalReference(p) || p.SourceVariable is not ParameterVariableDeclaration parameters)
            throw new ArgumentException("EP2 Mac requires a genuine PARAM reference at position 0.");
        if (Normalize(parameters.Mechanism) != mechanism)
            throw new ArgumentException("EP2 Mac requires parameters for the selected mechanism.");
        MechanismParameterContractValidator.Validate(mechanism, CryptoScriptFunction.Mac, parameters);
        var k = arguments.Arguments[1];
        if (!IsOriginalReference(k) || k.SourceVariable is not KeyVariableDeclaration key)
            throw new ArgumentException("EP2 Mac requires a genuine KEY reference at position 1.");
        KeyMetadataValidator.Validate(key, KeyAlgorithm.Hmac, KeyUsage.MacGenerate,
            "Mac", mechanism, arguments.DiagnosticSink);
        var d = arguments.Arguments[2];
        string dataValue;
        if (d.Kind == ResolvedCallArgumentKind.HexLiteral && d.SourceVariable is null && !d.HasReconstructedSourceVariable)
            dataValue = d.Value ?? string.Empty;
        else if (IsOriginalReference(d) && d.SourceVariable is StringVariableDeclaration data &&
                 data.Type is CryptoTypeVar && data.ValueFormat == FormatConversions.HEX)
            dataValue = data.Value;
        else
            throw new ArgumentException("EP2 Mac data must be a direct HEX literal or a genuine HEX VAR reference.");
        byte[]? keyBytes = null;
        byte[]? input = null;
        byte[]? result = null;
        try
        {
            keyBytes = DecodeHex(string.IsNullOrEmpty(key.KeyValue) ? key.Value : key.KeyValue);
            if (keyBytes.Length != 32 || key.KeySizeInBits.IsKnown && key.KeySizeInBits != new KeySize(256))
                throw new ArgumentException("EP2 Mac requires exactly 32 key bytes and consistent key size metadata.");
            input = DecodeHex(dataValue);
            result = Ep2MacPrimitive.Compute(keyBytes, input, mechanism == MessageMechanism);
            return new StringVariableDeclaration
            {
                Type = new CryptoTypeVar(), ValueFormat = FormatConversions.HEX,
                Value = FormatConversions.ByteArrayToHexString(result)
            };
        }
        finally
        {
            if (keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
            if (input is not null) CryptographicOperations.ZeroMemory(input);
            if (result is not null) CryptographicOperations.ZeroMemory(result);
        }
    }

    private static bool IsOriginalReference(AlgorithmCallArgument argument) =>
        argument.Kind == ResolvedCallArgumentKind.Variable && !argument.HasReconstructedSourceVariable;

    private static byte[] DecodeHex(string value)
    {
        if (value is null || value.Length < 4 || !value.StartsWith("0x(", StringComparison.Ordinal) ||
            !value.EndsWith(')') || (value.Length - 4) % 2 != 0 ||
            value.AsSpan(3, value.Length - 4).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
            throw new ArgumentException("EP2 Mac requires valid HEX binary data.");
        return Convert.FromHexString(value.AsSpan(3, value.Length - 4));
    }

    private static string Normalize(string value) =>
        (value.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase) ? value[6..] : value).ToUpperInvariant();
}
