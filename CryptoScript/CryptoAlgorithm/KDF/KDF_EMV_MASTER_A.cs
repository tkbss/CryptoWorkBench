using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

public sealed class KDF_EMV_MASTER_A : CryptoAlgorithm
{
    internal const string MechanismName = "KDF-EMV-MASTER-A";

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        if (Normalize(mechanism) != MechanismName)
            throw new ArgumentException("Option A requires KDF-EMV-MASTER-A parameters.");
        var result = new ParameterVariableDeclaration { Mechanism = MechanismName };
        result.RecordExplicitParameter("#MECH");
        foreach (string parameter in parameters)
        {
            result.SetParameter(parameter);
            result.RecordExplicitParameter(parameter.Split(':', 2)[0]);
        }
        MechanismParameterContractValidator.Validate(MechanismName, CryptoScriptFunction.Parameters, result);
        return result;
    }

    public override KeyVariableDeclaration Derive(AlgorithmCallArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Arguments.Count != 3)
            throw new ArgumentException("KDF-EMV-MASTER-A Derive requires exactly three arguments.");
        AlgorithmCallArgument parameterArgument = arguments.Arguments[0];
        if (parameterArgument.SourceVariable is null &&
            FormatConversions.ParseString(parameterArgument.Value) == FormatConversions.PAR)
            MechanismParameterContractValidator.ValidateSerializedOptionAParameters(parameterArgument.Value, selectedOptionA: true);
        ParameterVariableDeclaration parameters = AlgorithmArgumentResolver.ResolveParameter(arguments.Arguments[0]);
        if (Normalize(parameters.Mechanism) != MechanismName)
            throw new ArgumentException("Option A requires KDF-EMV-MASTER-A parameters.");
        MechanismParameterContractValidator.Validate(MechanismName, CryptoScriptFunction.Derive, parameters);
        KeyVariableDeclaration key = KdfArgumentResolver.ResolveKey(arguments, 1);
        if (key.KeyType != KeyType.Secret(KeyAlgorithm.Tdea))
            throw new ArgumentException("KDF-EMV-MASTER-A requires a Secret TDEA KEY.");
        string pan = ResolvePan(arguments);
        string? psn = parameters.GetParameters().ContainsKey("#PSN")
            ? parameters.GetParameter("#PSN")[1..^1] : null;

        byte[] keyBytes = DecodeKey(key);
        byte[]? derived = null;
        try
        {
            if (keyBytes.Length != 16)
                throw new ArgumentException("KDF-EMV-MASTER-A requires exactly 16 bytes of issuer key data.");
            if (key.KeySizeInBits.IsKnown && key.KeySizeInBits != new KeySize(128))
                throw new ArgumentException("Issuer KEY size metadata does not match its key data.");
            derived = EmvMasterKeyDerivationOptionA.Derive(keyBytes, pan, psn);
            string value = FormatConversions.ByteArrayToHexString(derived);
            return new KeyVariableDeclaration
            {
                Value = value, KeyValue = value, ValueFormat = FormatConversions.HEX,
                KeyType = KeyType.Secret(KeyAlgorithm.Tdea), KeySizeInBits = new KeySize(128),
                Usage = KeyUsagePolicy.Unspecified, DerivationMechanism = MechanismName,
                Type = new CryptoTypeKey()
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
            if (derived is not null)
                CryptographicOperations.ZeroMemory(derived);
        }
    }

    private static string ResolvePan(AlgorithmCallArguments arguments)
    {
        AlgorithmCallArgument argument = arguments.Arguments[2];
        string? value = null;
        if (argument.Kind == ResolvedCallArgumentKind.OtherLiteral && argument.SourceVariable is null)
            value = argument.Value;
        else if (arguments.IsLegacyDeriveCall && argument.SourceVariable is null)
            value = argument.Value; // Only the explicit string[] adapter has no AST origin.
        else if (argument.Kind == ResolvedCallArgumentKind.Variable &&
                 argument.SourceVariable is StringVariableDeclaration source &&
                 source.Type is CryptoTypeVar && source.ValueFormat == FormatConversions.STR)
            value = source.Value;
        if (value is null || value.Length < 2 || value[0] != '"' || value[^1] != '"')
            throw new ArgumentException("PAN must be a direct normal string literal or a normal string VAR reference.");
        return value[1..^1];
    }

    private static byte[] DecodeKey(KeyVariableDeclaration key)
    {
        string value = string.IsNullOrEmpty(key.KeyValue) ? key.Value : key.KeyValue;
        string format = FormatConversions.ParseString(value);
        if (format != FormatConversions.HEX && key.ValueFormat == FormatConversions.HEX)
            format = FormatConversions.HEX;
        if (format != FormatConversions.HEX)
            throw new ArgumentException("Issuer KEY must contain hexadecimal key data.");
        try { return FormatConversions.ToByteArray(value, format); }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        { throw new ArgumentException("Issuer KEY contains invalid hexadecimal data."); }
    }

    private static string Normalize(string mechanism) =>
        (mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase) ? mechanism[6..] : mechanism).ToUpperInvariant();
}
