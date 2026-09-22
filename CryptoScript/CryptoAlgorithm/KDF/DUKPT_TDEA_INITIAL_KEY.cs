using System.Globalization;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

public sealed class DUKPT_TDEA_INITIAL_KEY : CryptoAlgorithm
{
    private const string MechanismName = "DUKPT-TDEA-INITIAL-KEY";
    private static readonly byte[] KeyMask =
        Convert.FromHexString("C0C0C0C000000000C0C0C0C000000000");

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        if (!NormalizeMechanism(mechanism).Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported DUKPT mechanism: {mechanism}.");
        if (parameters.Length != 0)
            throw new ArgumentException($"{MechanismName} does not support additional parameters.");

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
        if (parameter.GetParameters().Keys.Any(name => !name.Equals("#MECH", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"{MechanismName} does not support additional parameters.");

        byte[] bdk = ResolveKeyBytes(ResolveKey(parameters[1]));
        if (bdk.Length != 16)
            throw new ArgumentException($"{MechanismName} BDK must be exactly 128 bits (16 bytes).");

        byte[] ksn = ResolveDataBytes(parameters[2]);
        if (ksn.Length != 10)
            throw new ArgumentException($"{MechanismName} KSN must be exactly 80 bits (10 bytes).");

        byte[] inputBlock = BuildInputBlock(ksn);
        byte[] variantBdk = bdk.Zip(KeyMask, (value, mask) => (byte)(value ^ mask)).ToArray();
        byte[] initialKey = EncryptBlock(bdk, inputBlock).Concat(EncryptBlock(variantBdk, inputBlock)).ToArray();
        string value = FormatConversions.ByteArrayToHexString(initialKey);
        return new KeyVariableDeclaration
        {
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            KeySize = (initialKey.Length * 8).ToString(CultureInfo.InvariantCulture),
            KeyType = KeyType.Secret(KeyAlgorithm.Tdea),
            Mechanism = string.Empty,
            DerivationMechanism = MechanismName,
            Type = new CryptoTypeKey()
        };
    }

    internal static byte[] BuildInputBlock(byte[] ksn)
    {
        if (ksn.Length != 10)
            throw new ArgumentException("KSN must be exactly 10 bytes.", nameof(ksn));

        byte[] clearedKsn = (byte[])ksn.Clone();
        clearedKsn[7] &= 0xE0;
        clearedKsn[8] = 0;
        clearedKsn[9] = 0;
        return clearedKsn[..8];
    }

    private static byte[] EncryptBlock(byte[] keyBytes, byte[] input)
    {
        string keyValue = FormatConversions.ByteArrayToHexString(keyBytes);
        var key = new KeyVariableDeclaration
        {
            Value = keyValue,
            KeyValue = keyValue,
            ValueFormat = FormatConversions.HEX,
            Type = new CryptoTypeKey()
        };
        var data = new StringVariableDeclaration
        {
            Value = FormatConversions.ByteArrayToHexString(input),
            ValueFormat = FormatConversions.HEX
        };
        var des3Parameters = new ParameterVariableDeclaration { Mechanism = "DES3-ECB" };
        des3Parameters.SetParameter("PAD", "NONE");
        StringVariableDeclaration encrypted = new DES3_ECB().ModeEncryption(des3Parameters, key, data);
        return FormatConversions.ToByteArray(encrypted.Value, encrypted.ValueFormat);
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
            .OfType<KeyVariableDeclaration>().Where(key => key.Value == value).ToArray();
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
        if (format != FormatConversions.HEX && key.ValueFormat == FormatConversions.HEX)
            format = key.ValueFormat;
        if (format != FormatConversions.HEX)
            throw new ArgumentException($"{MechanismName} BDK must contain hexadecimal TDEA key data.");
        return ConvertValue(value, format, "BDK");
    }

    private static byte[] ResolveDataBytes(string value)
    {
        string format;
        if (VariableDictionary.Instance().Get(value) is StringVariableDeclaration declared)
        {
            value = declared.Value;
            format = declared.ValueFormat;
        }
        else
        {
            format = FormatConversions.ParseString(value);
        }
        if (format != FormatConversions.HEX && format != FormatConversions.B64 && format != FormatConversions.STR)
            throw new ArgumentException($"{MechanismName} KSN must contain hex, Base64 or string data.");
        return ConvertValue(value, format, "KSN");
    }

    private static byte[] ConvertValue(string value, string format, string name)
    {
        try
        {
            return FormatConversions.ToByteArray(value, format);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new ArgumentException($"{MechanismName} {name} contains an invalid value.", exception);
        }
    }

    private static string NormalizeMechanism(string mechanism) =>
        mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
            ? mechanism["#MECH:".Length..]
            : mechanism;
}
