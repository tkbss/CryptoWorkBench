using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

public sealed class KDF_EMV_AC_SESSION : CryptoAlgorithm
{
    internal const string MechanismName = "KDF-EMV-AC-SESSION";

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(
        string mechanism,
        string[] parameters)
    {
        if (!NormalizeMechanism(mechanism).Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported EMV mechanism: {mechanism}.", nameof(mechanism));
        if (parameters.Length != 0)
            throw new ArgumentException($"{MechanismName} does not support additional parameters.");

        return new ParameterVariableDeclaration
        {
            Mechanism = MechanismName,
            ValueFormat = FormatConversions.PAR
        };
    }

    public override KeyVariableDeclaration Derive(AlgorithmCallArguments parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.Arguments.Count != 3)
            throw new ArgumentException($"{MechanismName} Derive requires exactly three arguments.");

        ParameterVariableDeclaration parameter =
            AlgorithmArgumentResolver.ResolveParameter(parameters.Arguments[0]);
        if (!NormalizeMechanism(parameter.Mechanism).Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Derive requires {MechanismName} parameters.");

        MechanismParameterContractValidator.Validate(
            MechanismName,
            CryptoScriptFunction.Derive,
            parameter);

        KeyVariableDeclaration masterKey = KdfArgumentResolver.ResolveKey(parameters, 1);
        KeyAlgorithm algorithm = masterKey.KeyType.Algorithm;
        if (algorithm is not (KeyAlgorithm.Aes or KeyAlgorithm.Tdea))
            throw new ArgumentException($"{MechanismName} requires an AES or TDEA master KEY.");

        byte[] masterKeyBytes = ResolveMasterKeyBytes(masterKey);
        byte[]? sessionKeyBytes = null;
        try
        {
            ValidateMasterKeyLength(algorithm, masterKeyBytes.Length);
            KeySize keySize = ResolveKeySize(masterKey, masterKeyBytes.Length);
            byte[] atc = ResolveAtc(parameters, 2);
            try
            {
                sessionKeyBytes = EmvAcSessionKeyDerivation.Derive(algorithm, masterKeyBytes, atc);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(atc);
            }

            string value = FormatConversions.ByteArrayToHexString(sessionKeyBytes);
            return new KeyVariableDeclaration
            {
                Value = value,
                KeyValue = value,
                ValueFormat = FormatConversions.HEX,
                KeySizeInBits = keySize,
                KeyType = masterKey.KeyType,
                Usage = KeyUsagePolicy.Unspecified,
                DerivationMechanism = MechanismName,
                Type = new CryptoTypeKey()
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKeyBytes);
            if (sessionKeyBytes is not null)
                CryptographicOperations.ZeroMemory(sessionKeyBytes);
        }
    }

    private static byte[] ResolveMasterKeyBytes(KeyVariableDeclaration masterKey)
    {
        string value = string.IsNullOrEmpty(masterKey.KeyValue)
            ? masterKey.Value
            : masterKey.KeyValue;
        string format = FormatConversions.ParseString(value);
        if (format != FormatConversions.HEX && masterKey.ValueFormat == FormatConversions.HEX)
            format = masterKey.ValueFormat;
        if (format != FormatConversions.HEX)
            throw new ArgumentException($"{MechanismName} master KEY must contain hexadecimal key data.");

        try
        {
            return FormatConversions.ToByteArray(value, format);
        }
        catch (Exception exception) when (
            exception is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new ArgumentException($"{MechanismName} master KEY contains invalid hexadecimal data.");
        }
    }

    private static byte[] ResolveAtc(AlgorithmCallArguments arguments, int index)
    {
        AlgorithmCallArgument argument = arguments.Arguments[index];
        string value;
        if (argument.Kind == ResolvedCallArgumentKind.HexLiteral &&
            argument.SourceVariable is null)
        {
            value = argument.Value ?? throw InvalidAtcArgument();
            if (FormatConversions.ParseString(value) != FormatConversions.HEX)
                throw InvalidAtcArgument();
        }
        else if (argument.Kind == ResolvedCallArgumentKind.Variable &&
                 argument.SourceVariable is StringVariableDeclaration source &&
                 source.Type is CryptoTypeVar &&
                 source.ValueFormat == FormatConversions.HEX)
        {
            value = source.Value;
        }
        else if (arguments.IsLegacyDeriveCall && argument.SourceVariable is null)
        {
            value = argument.Value ?? throw InvalidAtcArgument();
            if (FormatConversions.ParseString(value) != FormatConversions.HEX)
                throw InvalidAtcArgument();
        }
        else
        {
            throw InvalidAtcArgument();
        }

        byte[] atc;
        try
        {
            atc = FormatConversions.ToByteArray(value, FormatConversions.HEX);
        }
        catch (Exception exception) when (
            exception is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new ArgumentException($"{MechanismName} ATC contains invalid hexadecimal data.");
        }

        if (atc.Length == 2)
            return atc;

        CryptographicOperations.ZeroMemory(atc);
        throw new ArgumentException($"{MechanismName} ATC must contain exactly 2 bytes.");
    }

    private static ArgumentException InvalidAtcArgument() =>
        new($"{MechanismName} ATC must be a hexadecimal literal or a VAR containing hexadecimal binary data.");

    private static void ValidateMasterKeyLength(KeyAlgorithm algorithm, int length)
    {
        if (algorithm == KeyAlgorithm.Tdea && length != 16)
            throw new ArgumentException($"{MechanismName} TDEA master KEY must contain exactly 16 bytes.");
        if (algorithm == KeyAlgorithm.Aes && length is not (16 or 24 or 32))
            throw new ArgumentException($"{MechanismName} AES master KEY must contain 16, 24 or 32 bytes.");
    }

    private static KeySize ResolveKeySize(KeyVariableDeclaration masterKey, int length)
    {
        var actualSize = new KeySize(length * 8);
        if (masterKey.KeySizeInBits.IsKnown && masterKey.KeySizeInBits != actualSize)
            throw new ArgumentException($"{MechanismName} master KEY size metadata does not match its key data.");
        return masterKey.KeySizeInBits.IsKnown ? masterKey.KeySizeInBits : actualSize;
    }

    private static string NormalizeMechanism(string mechanism) =>
        mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
            ? mechanism["#MECH:".Length..]
            : mechanism;
}
