using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

public sealed class KDF_EMV_MASTER_C : CryptoAlgorithm
{
    internal const string MechanismName = "KDF-EMV-MASTER-C";

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        if (Normalize(mechanism) != MechanismName)
            throw new ArgumentException("Option C requires KDF-EMV-MASTER-C parameters.");
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
            throw new ArgumentException("KDF-EMV-MASTER-C Derive requires exactly three arguments.");
        AlgorithmCallArgument parameterArgument = arguments.Arguments[0];
        if (!arguments.IsLegacyDeriveCall &&
            (parameterArgument.Kind != ResolvedCallArgumentKind.Variable ||
             parameterArgument.SourceVariable is not ParameterVariableDeclaration ||
             parameterArgument.HasReconstructedSourceVariable))
            throw new ArgumentException("KDF-EMV-MASTER-C requires a genuine PARAM variable reference as its first argument.");
        if (parameterArgument.SourceVariable is null &&
            FormatConversions.ParseString(parameterArgument.Value) == FormatConversions.PAR)
            MechanismParameterContractValidator.ValidateSerializedEmvMasterParameters(parameterArgument.Value, MechanismName);
        ParameterVariableDeclaration parameters = AlgorithmArgumentResolver.ResolveParameter(parameterArgument);
        if (Normalize(parameters.Mechanism) != MechanismName)
            throw new ArgumentException("Option C requires KDF-EMV-MASTER-C parameters.");
        MechanismParameterContractValidator.Validate(MechanismName, CryptoScriptFunction.Derive, parameters);
        KeyVariableDeclaration key = KdfArgumentResolver.ResolveKey(arguments, 1);
        if (key.KeyType != KeyType.Secret(KeyAlgorithm.Aes))
            throw new ArgumentException("KDF-EMV-MASTER-C requires a Secret AES KEY.");
        string pan = KDF_EMV_MASTER_A.ResolvePan(arguments);
        string? psn = parameters.GetParameters().ContainsKey("#PSN")
            ? parameters.GetParameter("#PSN")[1..^1] : null;

        byte[]? keyBytes = null;
        byte[]? derived = null;
        try
        {
            keyBytes = KDF_EMV_MASTER_A.DecodeKey(key);
            AES_ECB.ValidateKeyLength(keyBytes);
            KeySize keySize = new(keyBytes.Length * 8);
            if (key.KeySizeInBits.IsKnown && key.KeySizeInBits != keySize)
                throw new ArgumentException("Issuer KEY size metadata does not match its key data.");
            derived = EmvMasterKeyDerivationOptionC.Derive(keyBytes, pan, psn);
            string value = FormatConversions.ByteArrayToHexString(derived);
            return new KeyVariableDeclaration
            {
                Value = value, KeyValue = value, ValueFormat = FormatConversions.HEX,
                KeyType = KeyType.Secret(KeyAlgorithm.Aes), KeySizeInBits = keySize,
                Usage = KeyUsagePolicy.Unspecified, DerivationMechanism = MechanismName,
                Type = new CryptoTypeKey()
            };
        }
        finally
        {
            if (keyBytes is not null)
                CryptographicOperations.ZeroMemory(keyBytes);
            if (derived is not null)
                CryptographicOperations.ZeroMemory(derived);
        }
    }

    private static string Normalize(string mechanism) =>
        (mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase) ? mechanism[6..] : mechanism).ToUpperInvariant();
}
