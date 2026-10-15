using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.EMV;

public sealed class MAC_EMV_AC : CryptoAlgorithm
{
    internal const string MechanismName = "MAC-EMV-AC";

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters) =>
        EmvAcArgumentContract.GenerateParameters(MechanismName, mechanism, parameters);

    public override StringVariableDeclaration Mac(AlgorithmCallArguments parameters) =>
        EmvAcArgumentContract.Execute(MechanismName, parameters, verify: false);

    // New mechanisms have no value-only compatibility contract. Existing MACs keep theirs.
    public override StringVariableDeclaration Mac(string[] parameters) =>
        throw new ArgumentException("MAC-EMV-AC requires structured arguments with genuine PARAM and KEY references.");
}

internal static class EmvAcArgumentContract
{
    internal static ParameterVariableDeclaration GenerateParameters(
        string expectedMechanism, string mechanism, string[] parameters)
    {
        if (Normalize(mechanism) != expectedMechanism)
            throw new ArgumentException("EMV AC requires parameters for the selected mechanism.");
        var result = new ParameterVariableDeclaration { Mechanism = expectedMechanism };
        result.RecordExplicitParameter("#MECH");
        foreach (string parameter in parameters)
        {
            result.SetParameter(parameter);
            result.RecordExplicitParameter(parameter.Split(':', 2)[0]);
        }
        MechanismParameterContractValidator.Validate(expectedMechanism, CryptoScriptFunction.Parameters, result);
        return result;
    }

    // Shared with ARPC: identity/origin checks only, no value-based reconstruction.
    internal static (ParameterVariableDeclaration Parameters, KeyVariableDeclaration Key) ResolveReferences(
        string mechanism, AlgorithmCallArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Arguments.Count != 3)
            throw new ArgumentException("EMV AC Mac requires exactly three arguments.");
        AlgorithmCallArgument parameterArgument = arguments.Arguments[0];
        if (parameterArgument.Kind != ResolvedCallArgumentKind.Variable ||
            parameterArgument.SourceVariable is not ParameterVariableDeclaration parameters ||
            parameterArgument.HasReconstructedSourceVariable)
            throw new ArgumentException("EMV AC requires a genuine PARAM variable reference as its first argument.");
        if (Normalize(parameters.Mechanism) != mechanism)
            throw new ArgumentException("EMV AC requires parameters for the selected mechanism.");
        MechanismParameterContractValidator.Validate(mechanism, CryptoScriptFunction.Mac, parameters);

        AlgorithmCallArgument keyArgument = arguments.Arguments[1];
        if (keyArgument.Kind != ResolvedCallArgumentKind.Variable ||
            keyArgument.SourceVariable is not KeyVariableDeclaration key ||
            keyArgument.HasReconstructedSourceVariable)
            throw new ArgumentException("EMV AC requires a genuine KEY variable reference.");
        if (key.KeyType != KeyType.Secret(KeyAlgorithm.Aes) && key.KeyType != KeyType.Secret(KeyAlgorithm.Tdea))
            throw new ArgumentException("EMV AC requires a Secret AES or TDEA session KEY.");

        return (parameters, key);
    }

    internal static byte[] DecodeSessionKey(KeyVariableDeclaration key)
    {
        byte[] bytes = DecodeHex(string.IsNullOrEmpty(key.KeyValue) ? key.Value : key.KeyValue, "Session KEY");
        bool returned = false;
        try
        {
            if (key.KeyType.Algorithm == KeyAlgorithm.Aes ? bytes.Length is not (16 or 24 or 32) : bytes.Length != 16)
                throw new ArgumentException("EMV AC session KEY has an unsupported length.");
            if (key.KeySizeInBits.IsKnown && key.KeySizeInBits != new KeySize(bytes.Length * 8))
                throw new ArgumentException("EMV AC KEY size metadata does not match its key data.");
            returned = true;
            return bytes;
        }
        finally
        {
            if (!returned) CryptographicOperations.ZeroMemory(bytes);
        }
    }

    internal static StringVariableDeclaration Execute(string mechanism, AlgorithmCallArguments arguments, bool verify)
    {
        var (parameters, key) = ResolveReferences(mechanism, arguments);
        string dataValue = ResolveData(arguments.Arguments[2]);
        byte[]? keyBytes = null;
        byte[]? data = null;
        byte[]? expected = null;
        byte[]? result = null;
        try
        {
            keyBytes = DecodeSessionKey(key);
            data = DecodeHex(dataValue, "Transaction data");
            string value;
            if (verify)
            {
                expected = DecodeHex(parameters.GetParameter("#EXPECTED"), "Expected cryptogram");
                if (!EmvApplicationCryptogram.VerifyAc(key.KeyType.Algorithm, keyBytes, data, expected))
                    // The existing evaluator converts runtime failures to SemanticErrorException
                    // and rethrows through the public runner. A diagnostic alone would not abort.
                    throw new ArgumentException("EMV Application Cryptogram verification failed.");
                value = "0x(01)";
            }
            else
            {
                result = EmvApplicationCryptogram.ComputeAc(key.KeyType.Algorithm, keyBytes, data);
                value = FormatConversions.ByteArrayToHexString(result);
            }
            return new StringVariableDeclaration
            {
                Type = new CryptoTypeVar(), ValueFormat = FormatConversions.HEX, Value = value
            };
        }
        finally
        {
            if (keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
            if (data is not null) CryptographicOperations.ZeroMemory(data);
            if (expected is not null) CryptographicOperations.ZeroMemory(expected);
            if (result is not null) CryptographicOperations.ZeroMemory(result);
        }
    }

    internal static string ResolveData(AlgorithmCallArgument argument, string inputName = "transaction data")
    {
        if (argument.Kind == ResolvedCallArgumentKind.HexLiteral && argument.SourceVariable is null)
            return argument.Value ?? throw InvalidData(inputName);
        if (argument.Kind == ResolvedCallArgumentKind.Variable &&
            argument.SourceVariable is StringVariableDeclaration source && source.Type is CryptoTypeVar &&
            source.ValueFormat == FormatConversions.HEX && !argument.HasReconstructedSourceVariable)
            return source.Value;
        throw InvalidData(inputName);
    }

    private static ArgumentException InvalidData(string inputName) =>
        new($"EMV AC {inputName} must be a direct HEX literal or a HEX VAR reference.");

    // Validate the entire value before decoding; no partial sensitive output on format errors.
    internal static bool IsHexValue(string? value) =>
        value is not null && value.StartsWith("0x(", StringComparison.Ordinal) && value.EndsWith(')') &&
        value.Length > 4 && (value.Length - 4) % 2 == 0 &&
        value.AsSpan(3, value.Length - 4).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;

    internal static byte[] DecodeHex(string value, string inputName)
    {
        if (!IsHexValue(value))
            throw new ArgumentException($"{inputName} must contain valid HEX binary data.");
        return Convert.FromHexString(value.AsSpan(3, value.Length - 4));
    }

    private static string Normalize(string mechanism) =>
        (mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase) ? mechanism[6..] : mechanism).ToUpperInvariant();
}
