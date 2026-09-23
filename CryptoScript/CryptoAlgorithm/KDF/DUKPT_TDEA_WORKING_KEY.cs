using System.Globalization;
using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.CryptoAlgorithm.KDF;

public sealed class DUKPT_TDEA_WORKING_KEY : CryptoAlgorithm
{
    private const string MechanismName = "DUKPT-TDEA-WORKING-KEY";
    private static readonly byte[] KeyMask =
        Convert.FromHexString("C0C0C0C000000000C0C0C0C000000000");

    private static readonly Dictionary<string, WorkingKeyUsage> Usages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PIN"] = new("00000000000000FF00000000000000FF", KeyUsage.PinEncrypt, false),
            ["MAC-REQUEST"] = new("000000000000FF00000000000000FF00", KeyUsage.MacGenerate | KeyUsage.MacVerify, false),
            ["MAC-RESPONSE"] = new("00000000FF00000000000000FF000000", KeyUsage.MacGenerate | KeyUsage.MacVerify, false),
            ["MAC-BOTH"] = new("000000000000FF00000000000000FF00", KeyUsage.MacGenerate | KeyUsage.MacVerify, false),
            ["DATA-REQUEST"] = new("0000000000FF00000000000000FF0000", KeyUsage.Encrypt | KeyUsage.Decrypt, true),
            ["DATA-RESPONSE"] = new("000000FF00000000000000FF00000000", KeyUsage.Encrypt | KeyUsage.Decrypt, true),
            ["DATA-BOTH"] = new("0000000000FF00000000000000FF0000", KeyUsage.Encrypt | KeyUsage.Decrypt, true)
        };

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        if (!NormalizeMechanism(mechanism).Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported DUKPT mechanism: {mechanism}.");

        string? usage = null;
        foreach (string parameter in parameters)
        {
            int separator = parameter.IndexOf(':');
            if (separator <= 0)
                throw new ArgumentException($"Invalid {MechanismName} parameter: {parameter}.");
            string name = parameter[..separator].ToUpperInvariant();
            if (name != "#USAGE")
                throw new ArgumentException($"{MechanismName} does not support parameter {name}.");
            if (usage is not null)
                throw new ArgumentException($"{MechanismName} parameter #USAGE must be supplied only once.");
            usage = parameter[(separator + 1)..];
        }

        usage = RequireUsage(usage);
        var result = new ParameterVariableDeclaration { Mechanism = MechanismName };
        result.SetParameter("USAGE", usage);
        result.ValueFormat = FormatConversions.PAR;
        return result;
    }

    public override KeyVariableDeclaration Derive(string[] parameters)
    {
        ParameterVariableDeclaration parameter = ResolveParameter(parameters[0]);
        if (!NormalizeMechanism(parameter.Mechanism).Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Derive requires {MechanismName} parameters.");
        if (parameter.GetParameters().Keys.Any(name => name is not ("#MECH" or "#USAGE")))
            throw new ArgumentException($"{MechanismName} accepts only #USAGE.");

        string usageName = RequireUsage(parameter.GetParameter("USAGE"));
        WorkingKeyUsage usage = Usages[usageName];
        KeyVariableDeclaration initialKey = ResolveKey(parameters[1]);
        byte[] initialKeyBytes = ResolveKeyBytes(initialKey);
        if (initialKey.KeyType.Algorithm is not (KeyAlgorithm.Tdea or KeyAlgorithm.Unknown))
            throw new ArgumentException($"{MechanismName} requires a TDEA Initial Key.");
        if (initialKeyBytes.Length != 16)
            throw new ArgumentException($"{MechanismName} Initial Key must be exactly 128 bits (16 bytes).");

        byte[] ksn = ResolveDataBytes(parameters[2]);
        if (ksn.Length != 10)
            throw new ArgumentException($"{MechanismName} KSN must be exactly 80 bits (10 bytes).");
        int transactionCounter = ((ksn[7] & 0x1F) << 16) | (ksn[8] << 8) | ksn[9];
        if (transactionCounter == 0)
            throw new ArgumentException($"{MechanismName} Transaction Counter is not valid for host derivation.");

        byte[] currentKey = DeriveCurrentKey(initialKeyBytes, ksn);
        byte[] variantKey = Xor(currentKey, Convert.FromHexString(usage.Variant));
        byte[] workingKey = usage.ApplyOneWayFunction ? ApplyOneWayFunction(variantKey) : variantKey;
        string value = FormatConversions.ByteArrayToHexString(workingKey);
        return new KeyVariableDeclaration
        {
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            KeySize = "128",
            KeyType = KeyType.Secret(KeyAlgorithm.Tdea),
            Usage = KeyUsagePolicy.Restricted(usage.KeyUsage),
            DerivationMechanism = MechanismName,
            Type = new CryptoTypeKey()
        };
    }

    internal static byte[] DeriveCurrentKey(byte[] initialKey, byte[] ksn)
    {
        if (initialKey.Length != 16)
            throw new ArgumentException("Initial Key must be exactly 16 bytes.", nameof(initialKey));
        if (ksn.Length != 10)
            throw new ArgumentException("KSN must be exactly 10 bytes.", nameof(ksn));

        int counter = ((ksn[7] & 0x1F) << 16) | (ksn[8] << 8) | ksn[9];
        byte[] register = (byte[])ksn.Clone();
        register[7] &= 0xE0;
        register[8] = 0;
        register[9] = 0;
        byte[] currentKey = (byte[])initialKey.Clone();
        int workingCounter = 0;
        for (int mask = 0x100000; mask != 0; mask >>= 1)
        {
            if ((counter & mask) == 0)
                continue;
            workingCounter |= mask;
            register[7] = (byte)((register[7] & 0xE0) | ((workingCounter >> 16) & 0x1F));
            register[8] = (byte)(workingCounter >> 8);
            register[9] = (byte)workingCounter;
            currentKey = NonReversibleKeyGeneration(currentKey, register[2..]);
        }
        return currentKey;
    }

    internal static byte[] ApplyOneWayFunction(byte[] variantKey)
    {
        if (variantKey.Length != 16)
            throw new ArgumentException("Variant key must be exactly 16 bytes.", nameof(variantKey));
        return EncryptTdea(variantKey, variantKey[..8])
            .Concat(EncryptTdea(variantKey, variantKey[8..])).ToArray();
    }

    private static byte[] NonReversibleKeyGeneration(byte[] key, byte[] register)
    {
        byte[] right = EncryptRegister(key, register);
        byte[] maskedKey = Xor(key, KeyMask);
        byte[] left = EncryptRegister(maskedKey, register);
        return left.Concat(right).ToArray();
    }

    private static byte[] EncryptRegister(byte[] key, byte[] register)
    {
        byte[] rightHalf = key[8..];
        byte[] input = Xor(register, rightHalf);
        return Xor(EncryptDes(key[..8], input), rightHalf);
    }

    private static byte[] EncryptDes(byte[] key, byte[] input)
    {
        var engine = new DesEngine();
        engine.Init(true, new KeyParameter(key));
        byte[] output = new byte[8];
        engine.ProcessBlock(input, 0, output, 0);
        return output;
    }

    private static byte[] EncryptTdea(byte[] key, byte[] input)
    {
        var engine = new DesEdeEngine();
        engine.Init(true, new KeyParameter(key));
        byte[] output = new byte[8];
        engine.ProcessBlock(input, 0, output, 0);
        return output;
    }

    private static byte[] Xor(byte[] left, byte[] right) =>
        left.Zip(right, (a, b) => (byte)(a ^ b)).ToArray();

    private static string RequireUsage(string? usage)
    {
        if (string.IsNullOrWhiteSpace(usage) || !Usages.ContainsKey(usage))
            throw new ArgumentException($"{MechanismName} requires #USAGE with a supported value.");
        return usage.ToUpperInvariant();
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
            throw new ArgumentException($"{MechanismName} Initial Key must contain hexadecimal TDEA key data.");
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

    private sealed record WorkingKeyUsage(string Variant, KeyUsage KeyUsage, bool ApplyOneWayFunction);
}
