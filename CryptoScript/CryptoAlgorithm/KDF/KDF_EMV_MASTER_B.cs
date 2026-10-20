using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

public sealed class KDF_EMV_MASTER_B : CryptoAlgorithm
{
    internal const string MechanismName = "KDF-EMV-MASTER-B";

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        if (Normalize(mechanism) != MechanismName)
            throw new ArgumentException("Option B requires KDF-EMV-MASTER-B parameters.");
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
            throw new ArgumentException("KDF-EMV-MASTER-B Derive requires exactly three arguments.");
        AlgorithmCallArgument parameterArgument = arguments.Arguments[0];
        if (parameterArgument.SourceVariable is null &&
            FormatConversions.ParseString(parameterArgument.Value) == FormatConversions.PAR)
            MechanismParameterContractValidator.ValidateSerializedEmvMasterParameters(parameterArgument.Value, MechanismName);
        ParameterVariableDeclaration parameters = AlgorithmArgumentResolver.ResolveParameter(arguments.Arguments[0]);
        if (Normalize(parameters.Mechanism) != MechanismName)
            throw new ArgumentException("Option B requires KDF-EMV-MASTER-B parameters.");
        MechanismParameterContractValidator.Validate(MechanismName, CryptoScriptFunction.Derive, parameters);
        KeyVariableDeclaration key = KdfArgumentResolver.ResolveKey(arguments, 1);
        KeyMetadataValidator.Validate(key, KeyAlgorithm.Tdea, KeyUsage.DeriveKey,
            "Derive", MechanismName, arguments.DiagnosticSink);
        string pan = KDF_EMV_MASTER_A.ResolvePan(arguments);
        string? psn = parameters.GetParameters().ContainsKey("#PSN")
            ? parameters.GetParameter("#PSN")[1..^1] : null;

        byte[] keyBytes = KDF_EMV_MASTER_A.DecodeKey(key);
        byte[]? derived = null;
        try
        {
            if (keyBytes.Length != 16)
                throw new ArgumentException("KDF-EMV-MASTER-B requires exactly 16 bytes of issuer key data.");
            if (key.KeySizeInBits.IsKnown && key.KeySizeInBits != new KeySize(128))
                throw new ArgumentException("Issuer KEY size metadata does not match its key data.");
            CryptoScript.CryptoAlgorithm.DES3.Des3KeyQuality.Report(keyBytes, key.Id,
                "Derive", MechanismName, arguments.DiagnosticSink);
            derived = EmvMasterKeyDerivationOptionB.DeriveForAnalysis(keyBytes, pan, psn);
            string value = FormatConversions.ByteArrayToHexString(derived);
            return new KeyVariableDeclaration
            {
                Value = value, ValueFormat = FormatConversions.HEX,
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

    private static string Normalize(string mechanism) =>
        (mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase) ? mechanism[6..] : mechanism).ToUpperInvariant();
}
