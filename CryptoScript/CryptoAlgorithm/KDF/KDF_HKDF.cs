using System.Globalization;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

public class KDF_HKDF : CryptoAlgorithm
{
    private const string MechanismName = "KDF-HKDF";
    private const string ExtractMechanismName = "HKDF-EXTRACT";
    private const string ExpandMechanismName = "HKDF-EXPAND";

    private static readonly IReadOnlyDictionary<string, int> HashLengths =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["HASH-SHA1"] = 20,
            ["HASH-SHA224"] = 28,
            ["HASH-SHA256"] = 32,
            ["HASH-SHA384"] = 48,
            ["HASH-SHA512"] = 64,
            ["HASH-SHA512-224"] = 28,
            ["HASH-SHA512-256"] = 32,
            ["HASH-SHA3-224"] = 28,
            ["HASH-SHA3-256"] = 32,
            ["HASH-SHA3-384"] = 48,
            ["HASH-SHA3-512"] = 64
        };

    public override ParameterVariableDeclaration GenerateParameters(string mechanism)
    {
        return GenerateParameters(mechanism, Array.Empty<string>());
    }

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        mechanism = NormalizeMechanism(mechanism);
        bool isExtract = mechanism.Equals(ExtractMechanismName, StringComparison.OrdinalIgnoreCase);
        bool isExpand = mechanism.Equals(ExpandMechanismName, StringComparison.OrdinalIgnoreCase);
        if (!mechanism.Equals(MechanismName, StringComparison.OrdinalIgnoreCase) && !isExtract && !isExpand)
            throw new ArgumentException($"Unsupported KDF mechanism: {mechanism}.");
        string canonicalMechanism = isExtract ? ExtractMechanismName : isExpand ? ExpandMechanismName : MechanismName;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string parameter in parameters)
        {
            int separator = parameter.IndexOf(':');
            if (separator <= 0)
                throw new ArgumentException($"Invalid HKDF parameter: {parameter}.");

            string name = parameter[..separator].ToUpperInvariant();
            string value = parameter[(separator + 1)..];
            bool supported = name == "#HASH" || (!isExpand && name == "#SALT") || (!isExtract && name == "#OUTLEN");
            if (!supported)
                throw new ArgumentException($"HKDF does not support parameter {name}.");
            values[name] = value;
        }

        if (!values.TryGetValue("#HASH", out string? hash) || !HashLengths.TryGetValue(hash, out int hashLength))
            throw new ArgumentException("HKDF requires #HASH with a supported HASH-* mechanism.");

        values.TryGetValue("#OUTLEN", out string? outputLengthText);
        if (!isExtract)
        {
            if (outputLengthText == null)
                throw new ArgumentException("HKDF requires #OUTLEN.");
            if (!int.TryParse(outputLengthText, NumberStyles.None, CultureInfo.InvariantCulture, out int outputLength) || outputLength <= 0)
                throw new ArgumentException("HKDF output length must be a positive integer number of bits.");
            if (outputLength % 8 != 0)
                throw new ArgumentException("HKDF output length must be divisible by 8.");
            if (outputLength / 8 > 255 * hashLength)
                throw new ArgumentException($"HKDF output length exceeds the RFC 5869 limit for {hash}.");
        }

        if (values.TryGetValue("#SALT", out string? salt))
        {
            string format = FormatConversions.ParseString(salt);
            if (format != FormatConversions.HEX && format != FormatConversions.B64 && format != FormatConversions.STR)
                throw new ArgumentException("HKDF salt must be a hex, Base64 or string value.");
        }

        var result = new ParameterVariableDeclaration { Mechanism = canonicalMechanism };
        result.SetParameter("HASH", hash);
        if (salt != null)
            result.SetParameter("SALT", salt);
        if (outputLengthText != null)
            result.SetParameter("OUTLEN", outputLengthText);
        result.ValueFormat = FormatConversions.PAR;
        return result;
    }

    public override KeyVariableDeclaration Derive(string[] parameters)
    {
        ParameterVariableDeclaration parameter = ResolveParameter(parameters[0]);
        string mechanism = NormalizeMechanism(parameter.Mechanism);
        bool isExtract = mechanism.Equals(ExtractMechanismName, StringComparison.OrdinalIgnoreCase);
        bool isExpand = mechanism.Equals(ExpandMechanismName, StringComparison.OrdinalIgnoreCase);
        if (!mechanism.Equals(MechanismName, StringComparison.OrdinalIgnoreCase) && !isExtract && !isExpand)
            throw new ArgumentException("Derive requires KDF-HKDF parameters.");
        string canonicalMechanism = isExtract ? ExtractMechanismName : isExpand ? ExpandMechanismName : MechanismName;

        KeyVariableDeclaration ikm = ResolveKey(parameters[1]);
        StringVariableDeclaration info = ResolveData(parameters[2]);
        string hash = parameter.GetParameter("HASH");
        int hashLength = DigestFactory.Create(hash).GetDigestSize();
        int outputLengthBits = hashLength * 8;
        if (!isExtract)
        {
            string outputLengthText = parameter.GetParameter("OUTLEN");
            if (!int.TryParse(outputLengthText, NumberStyles.None, CultureInfo.InvariantCulture, out outputLengthBits) ||
                outputLengthBits <= 0 || outputLengthBits % 8 != 0)
                throw new ArgumentException("HKDF #OUTLEN must be a positive multiple of 8 bits.");
            if (outputLengthBits > 255 * hashLength * 8)
                throw new ArgumentException("HKDF #OUTLEN exceeds 255 times HashLen.");
        }

        byte[] ikmBytes = ResolveKeyBytes(ikm);
        byte[] infoBytes = FormatConversions.ToByteArray(info.Value, info.ValueFormat);
        if (isExtract && infoBytes.Length != 0)
            throw new ArgumentException("HKDF-EXTRACT does not use info data; the info argument must be empty.");
        bool hasSalt = parameter.GetParameters().ContainsKey("#SALT");
        byte[]? salt = hasSalt ? ResolveSalt(parameter.GetParameter("SALT")) : null;

        byte[] result;
        if (isExtract)
        {
            result = HKDFMode.Extract(hash, salt, ikmBytes);
        }
        else if (isExpand)
        {
            result = HKDFMode.Expand(hash, ikmBytes, infoBytes, outputLengthBits / 8);
        }
        else
        {
            byte[] prk = HKDFMode.Extract(hash, salt, ikmBytes);
            result = HKDFMode.Expand(hash, prk, infoBytes, outputLengthBits / 8);
        }
        string value = FormatConversions.ByteArrayToHexString(result);
        return new KeyVariableDeclaration
        {
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            KeySize = outputLengthBits.ToString(CultureInfo.InvariantCulture),
            KeyType = KeyType.Secret(KeyAlgorithm.Unknown),
            Mechanism = string.Empty,
            DerivationMechanism = canonicalMechanism,
            Type = new CryptoTypeKey()
        };
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
            throw new ArgumentException("HKDF IKM KEY must contain a valid hex, Base64 or string value.");

        return ConvertToBytes(value, format, "HKDF IKM KEY");
    }

    private static byte[] ResolveSalt(string value)
    {
        string format = FormatConversions.ParseString(value);
        if (!IsDataFormat(format))
            throw new ArgumentException("HKDF #SALT must contain a valid hex, Base64 or string value.");
        return ConvertToBytes(value, format, "HKDF #SALT");
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
        if (format == FormatConversions.HEX || format == FormatConversions.B64 || format == FormatConversions.STR)
            return new StringVariableDeclaration { Value = value, ValueFormat = format };
        throw new ArgumentException("wrong data argument");
    }

    private static bool IsDataFormat(string format) =>
        format == FormatConversions.HEX || format == FormatConversions.B64 || format == FormatConversions.STR;

    private static string NormalizeMechanism(string mechanism)
    {
        return mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
            ? mechanism["#MECH:".Length..]
            : mechanism;
    }
}
