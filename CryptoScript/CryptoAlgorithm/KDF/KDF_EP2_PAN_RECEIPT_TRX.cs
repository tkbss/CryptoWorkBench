using System.Globalization;
using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;

namespace CryptoScript.CryptoAlgorithm.KDF;

public sealed class KDF_EP2_PAN_RECEIPT_TRX : CryptoAlgorithm
{
    private const string MechanismName = "KDF-EP2-PAN-RECEIPT-TRX";
    private const string HashMechanism = "HASH-SHA256";
    private const int KeyLengthBytes = 16;
    private const int ExpandedLengthBytes = 32;
    private const int OutputLengthBytes = 16;

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        mechanism = NormalizeMechanism(mechanism);
        if (!mechanism.Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported KDF mechanism: {mechanism}.");
        if (parameters.Length != 0)
        {
            string parameterName = ParameterName(parameters[0]);
            throw new ArgumentException($"{MechanismName} does not support parameter {parameterName}.");
        }

        return new ParameterVariableDeclaration
        {
            Mechanism = MechanismName,
            ValueFormat = FormatConversions.PAR
        };
    }

    public override KeyVariableDeclaration Derive(string[] parameters)
    {
        ParameterVariableDeclaration parameter = ResolveParameter(parameters[0]);
        if (!NormalizeMechanism(parameter.Mechanism).Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Derive requires {MechanismName} parameters.");

        byte[] key = ResolveKeyBytes(ResolveKey(parameters[1]));
        if (key.Length != KeyLengthBytes)
            throw new ArgumentException($"{MechanismName} terminal key must be exactly 16 bytes.");

        StringVariableDeclaration data = ResolveData(parameters[2]);
        byte[] dolData = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
        byte[] dolHash = ComputeDolHash(dolData);
        byte[] expanded = HKDFMode.ExpandEp2Sha256(key, dolHash, ExpandedLengthBytes);
        byte[] output = expanded.AsSpan(0, OutputLengthBytes).ToArray();
        string value = FormatConversions.ByteArrayToHexString(output);

        return new KeyVariableDeclaration
        {
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            KeySize = (OutputLengthBytes * 8).ToString(CultureInfo.InvariantCulture),
            KeyType = KeyType.Secret(KeyAlgorithm.Unknown),
            Mechanism = string.Empty,
            DerivationMechanism = MechanismName,
            Type = new CryptoTypeKey()
        };
    }

    private static byte[] ComputeDolHash(byte[] dolData)
    {
        IDigest digest = DigestFactory.Create(HashMechanism);
        digest.BlockUpdate(dolData, 0, dolData.Length);
        byte[] result = new byte[digest.GetDigestSize()];
        digest.DoFinal(result, 0);
        return result;
    }

    private static string ParameterName(string parameter)
    {
        int separator = parameter.IndexOf(':');
        return separator <= 0 ? parameter : parameter[..separator].ToUpperInvariant();
    }

    private static ParameterVariableDeclaration ResolveParameter(string value)
    {
        if (VariableDictionary.Instance().Get(value) is ParameterVariableDeclaration declared)
            return declared;
        if (FormatConversions.ParseString(value) == FormatConversions.PAR)
        {
            var parameter = new ParameterVariableDeclaration();
            parameter.SetInstance(value);
            return parameter;
        }
        throw new ArgumentException("wrong parameter argument");
    }

    private static KeyVariableDeclaration ResolveKey(string value)
    {
        KeyVariableDeclaration[] matches = VariableDictionary.Instance().GetVariables()
            .OfType<KeyVariableDeclaration>()
            .Where(key => key.Value == value)
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new ArgumentException("wrong key argument"),
            _ => throw new ArgumentException("Ambiguous KEY argument: multiple KEY variables have the same value.")
        };
    }

    private static byte[] ResolveKeyBytes(KeyVariableDeclaration key)
    {
        string value = string.IsNullOrEmpty(key.KeyValue) ? key.Value : key.KeyValue;
        string format = FormatConversions.ParseString(value);
        if (!IsDataFormat(format) && IsDataFormat(key.ValueFormat))
            format = key.ValueFormat;
        if (!IsDataFormat(format))
            throw new ArgumentException($"{MechanismName} terminal key must contain a valid hex, Base64 or string value.");
        return ConvertToBytes(value, format, $"{MechanismName} terminal key");
    }

    private static byte[] ConvertToBytes(string value, string format, string argumentName)
    {
        try
        {
            return FormatConversions.ToByteArray(value, format);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new ArgumentException($"{argumentName} contains an invalid value.", exception);
        }
    }

    private static StringVariableDeclaration ResolveData(string value)
    {
        string format = FormatConversions.ParseString(value);
        if (IsDataFormat(format))
            return new StringVariableDeclaration { Value = value, ValueFormat = format };
        throw new ArgumentException("wrong data argument");
    }

    private static bool IsDataFormat(string format) =>
        format == FormatConversions.HEX || format == FormatConversions.B64 || format == FormatConversions.STR;

    private static string NormalizeMechanism(string mechanism) =>
        mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
            ? mechanism["#MECH:".Length..]
            : mechanism;
}
