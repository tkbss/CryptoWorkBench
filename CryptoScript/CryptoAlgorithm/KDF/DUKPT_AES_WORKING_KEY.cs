using System.Globalization;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

public sealed class DUKPT_AES_WORKING_KEY : CryptoAlgorithm
{
    private const string MechanismName = "DUKPT-AES-WORKING-KEY";
    private const ushort KeyDerivationUsage = 0x8000;

    private static readonly Dictionary<string, ushort> UsageIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PIN"] = 0x1000,
        ["MAC-GENERATE"] = 0x2000,
        ["MAC-VERIFY"] = 0x2001,
        ["MAC-BOTH"] = 0x2002,
        ["DATA-ENCRYPT"] = 0x3000,
        ["DATA-DECRYPT"] = 0x3001,
        ["DATA-BOTH"] = 0x3002
    };

    private static readonly Dictionary<string, WorkingKeyType> WorkingKeyTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["TDEA-2"] = new(0x0000, 128, KeyAlgorithm.Tdea, 128),
            ["TDEA-3"] = new(0x0001, 192, KeyAlgorithm.Tdea, 128),
            ["AES-128"] = new(0x0002, 128, KeyAlgorithm.Aes, 128),
            ["AES-192"] = new(0x0003, 192, KeyAlgorithm.Aes, 192),
            ["AES-256"] = new(0x0004, 256, KeyAlgorithm.Aes, 256),
            ["HMAC-128"] = new(0x0005, 128, KeyAlgorithm.Hmac, 128),
            ["HMAC-192"] = new(0x0005, 192, KeyAlgorithm.Hmac, 192),
            ["HMAC-256"] = new(0x0005, 256, KeyAlgorithm.Hmac, 256)
        };

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        if (!NormalizeMechanism(mechanism).Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported DUKPT mechanism: {mechanism}.");

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string parameter in parameters)
        {
            int separator = parameter.IndexOf(':');
            if (separator <= 0)
                throw new ArgumentException($"Invalid {MechanismName} parameter: {parameter}.");
            string name = parameter[..separator].ToUpperInvariant();
            if (name is not ("#USAGE" or "#KEYTYPE"))
                throw new ArgumentException($"{MechanismName} does not support parameter {name}.");
            if (!values.TryAdd(name, parameter[(separator + 1)..]))
                throw new ArgumentException($"{MechanismName} parameter {name} must be supplied only once.");
        }

        string usage = RequireUsage(values.GetValueOrDefault("#USAGE"));
        string keyType = RequireKeyType(values.GetValueOrDefault("#KEYTYPE"));
        var result = new ParameterVariableDeclaration { Mechanism = MechanismName };
        result.SetParameter("USAGE", usage);
        result.SetParameter("KEYTYPE", keyType);
        result.ValueFormat = FormatConversions.PAR;
        return result;
    }

    public override KeyVariableDeclaration Derive(string[] parameters)
    {
        ParameterVariableDeclaration parameter = ResolveParameter(parameters[0]);
        if (!NormalizeMechanism(parameter.Mechanism).Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Derive requires {MechanismName} parameters.");
        if (parameter.GetParameters().Keys.Any(name =>
                name is not ("#MECH" or "#USAGE" or "#KEYTYPE")))
            throw new ArgumentException($"{MechanismName} accepts only #USAGE and #KEYTYPE.");

        string usageName = RequireUsage(parameter.GetParameter("USAGE"));
        string keyTypeName = RequireKeyType(parameter.GetParameter("KEYTYPE"));
        WorkingKeyType workingKeyType = WorkingKeyTypes[keyTypeName];

        KeyVariableDeclaration initialKey = ResolveKey(parameters[1]);
        byte[] initialKeyBytes = ResolveKeyBytes(initialKey);
        if (initialKey.KeyType.Algorithm != KeyAlgorithm.Aes)
            throw new ArgumentException($"{MechanismName} requires an AES Initial Key.");
        if (initialKeyBytes.Length is not (16 or 24 or 32))
            throw new ArgumentException($"{MechanismName} Initial Key must be 128, 192 or 256 bits.");
        int initialKeyBits = initialKeyBytes.Length * 8;
        if (workingKeyType.RequiredInitialKeyBits > initialKeyBits)
            throw new ArgumentException(
                $"{MechanismName} {keyTypeName} is stronger than the {initialKeyBits}-bit AES Initial Key.");

        byte[] ksn = ResolveDataBytes(parameters[2]);
        if (ksn.Length != 12)
            throw new ArgumentException($"{MechanismName} KSN must be exactly 96 bits (12 bytes).");
        byte[] initialKeyId = ksn[..8];
        uint transactionCounter =
            ((uint)ksn[8] << 24) | ((uint)ksn[9] << 16) | ((uint)ksn[10] << 8) | ksn[11];

        byte[] derivationKey = DeriveIntermediateKey(initialKeyBytes, initialKeyId, transactionCounter);
        byte[] workingData = AesDukptDerivation.CreateDerivationData(
            UsageIndicators[usageName], workingKeyType.AlgorithmIndicator, workingKeyType.KeyLengthBits,
            initialKeyId, transactionCounter, initialKey: false);
        byte[] workingKey = AesDukptDerivation.DeriveKey(
            derivationKey, workingKeyType.KeyLengthBits, workingData);

        string value = FormatConversions.ByteArrayToHexString(workingKey);
        return new KeyVariableDeclaration
        {
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            KeySize = workingKeyType.KeyLengthBits.ToString(CultureInfo.InvariantCulture),
            KeyType = KeyType.Secret(workingKeyType.KeyAlgorithm),
            Usage = KeyUsagePolicy.Restricted(MapUsage(usageName)),
            DerivationMechanism = MechanismName,
            Type = new CryptoTypeKey()
        };
    }

    internal static byte[] DeriveIntermediateKey(
        byte[] initialKey, byte[] initialKeyId, uint transactionCounter)
    {
        WorkingKeyType initialType = initialKey.Length switch
        {
            16 => WorkingKeyTypes["AES-128"],
            24 => WorkingKeyTypes["AES-192"],
            32 => WorkingKeyTypes["AES-256"],
            _ => throw new ArgumentException("Initial Key must be 128, 192 or 256 bits.", nameof(initialKey))
        };
        byte[] derivationKey = (byte[])initialKey.Clone();
        uint workingCounter = 0;
        for (uint mask = 0x80000000; mask != 0; mask >>= 1)
        {
            if ((transactionCounter & mask) == 0)
                continue;
            workingCounter |= mask;
            byte[] derivationData = AesDukptDerivation.CreateDerivationData(
                KeyDerivationUsage, initialType.AlgorithmIndicator, initialType.KeyLengthBits,
                initialKeyId, workingCounter, initialKey: false);
            derivationKey = AesDukptDerivation.DeriveKey(
                derivationKey, initialType.KeyLengthBits, derivationData);
        }
        return derivationKey;
    }

    private static KeyUsage MapUsage(string usage) => usage.ToUpperInvariant() switch
    {
        "PIN" => KeyUsage.PinEncrypt,
        "MAC-GENERATE" => KeyUsage.MacGenerate,
        "MAC-VERIFY" => KeyUsage.MacVerify,
        "MAC-BOTH" => KeyUsage.MacGenerate | KeyUsage.MacVerify,
        "DATA-ENCRYPT" => KeyUsage.Encrypt,
        "DATA-DECRYPT" => KeyUsage.Decrypt,
        "DATA-BOTH" => KeyUsage.Encrypt | KeyUsage.Decrypt,
        _ => throw new InvalidOperationException()
    };

    private static string RequireUsage(string? usage)
    {
        if (string.IsNullOrWhiteSpace(usage) || !UsageIndicators.ContainsKey(usage))
            throw new ArgumentException($"{MechanismName} requires #USAGE with a supported value.");
        return usage.ToUpperInvariant();
    }

    private static string RequireKeyType(string? keyType)
    {
        if (string.IsNullOrWhiteSpace(keyType) || !WorkingKeyTypes.ContainsKey(keyType))
            throw new ArgumentException($"{MechanismName} requires #KEYTYPE with a supported value.");
        return keyType.ToUpperInvariant();
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
            throw new ArgumentException($"{MechanismName} Initial Key must contain hexadecimal AES key data.");
        return ConvertValue(value, format, "Initial Key");
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

    private sealed record WorkingKeyType(
        ushort AlgorithmIndicator,
        ushort KeyLengthBits,
        KeyAlgorithm KeyAlgorithm,
        int RequiredInitialKeyBits);
}
