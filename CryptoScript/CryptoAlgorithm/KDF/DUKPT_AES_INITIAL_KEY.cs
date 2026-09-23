using System.Globalization;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

public sealed class DUKPT_AES_INITIAL_KEY : CryptoAlgorithm
{
    private const string MechanismName = "DUKPT-AES-INITIAL-KEY";

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

        KeyVariableDeclaration bdk = ResolveKey(parameters[1]);
        byte[] bdkBytes = ResolveKeyBytes(bdk);
        if (bdkBytes.Length is not (16 or 24 or 32))
            throw new ArgumentException($"{MechanismName} BDK must be 128, 192 or 256 bits.");

        byte[] initialKeyId = ResolveDataBytes(parameters[2]);
        if (initialKeyId.Length != 8)
            throw new ArgumentException($"{MechanismName} IKID must be exactly 64 bits (8 bytes).");

        byte[] derivationData = BuildDerivationData(bdkBytes.Length, initialKeyId, 1);
        byte[] initialKey = AesDukptDerivation.DeriveKey(
            bdkBytes, checked((ushort)(bdkBytes.Length * 8)), derivationData);
        string value = FormatConversions.ByteArrayToHexString(initialKey);
        return new KeyVariableDeclaration
        {
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            KeySize = (bdkBytes.Length * 8).ToString(CultureInfo.InvariantCulture),
            KeyType = KeyType.Secret(KeyAlgorithm.Aes),
            DerivationMechanism = MechanismName,
            Type = new CryptoTypeKey()
        };
    }

    internal static byte[] BuildDerivationData(int keyLengthBytes, byte[] initialKeyId, byte counter)
    {
        if (keyLengthBytes is not (16 or 24 or 32))
            throw new ArgumentException("AES key length must be 16, 24 or 32 bytes.", nameof(keyLengthBytes));
        if (initialKeyId.Length != 8)
            throw new ArgumentException("IKID must be exactly 8 bytes.", nameof(initialKeyId));

        ushort algorithm = keyLengthBytes switch
        {
            16 => 0x0002,
            24 => 0x0003,
            32 => 0x0004,
            _ => throw new InvalidOperationException()
        };
        ushort keyLengthBits = checked((ushort)(keyLengthBytes * 8));
        byte[] data = AesDukptDerivation.CreateDerivationData(
            0x8001, algorithm, keyLengthBits, initialKeyId, 0, initialKey: true);
        data[1] = counter;
        return data;
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
            throw new ArgumentException($"{MechanismName} BDK must contain hexadecimal AES key data.");
        return ConvertHex(value, "BDK");
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
            throw new ArgumentException($"{MechanismName} IKID must contain hex, Base64 or string data.");
        try
        {
            return FormatConversions.ToByteArray(value, format);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new ArgumentException($"{MechanismName} IKID contains an invalid value.", exception);
        }
    }

    private static byte[] ConvertHex(string value, string name)
    {
        try
        {
            return FormatConversions.ToByteArray(value, FormatConversions.HEX);
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
