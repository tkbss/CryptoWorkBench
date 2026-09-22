using System.Globalization;
using CryptoScript.CryptoAlgorithm.HMAC;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

public sealed class KDF_SP800_108_COUNTER : CryptoAlgorithm
{
    private const string MechanismName = "KDF-SP800-108-COUNTER";
    private static readonly HashSet<string> HmacPrfs = new(StringComparer.OrdinalIgnoreCase)
    {
        "HMAC-SHA1", "HMAC-SHA224", "HMAC-SHA256", "HMAC-SHA384", "HMAC-SHA512",
        "HMAC-SHA512-224", "HMAC-SHA512-256", "HMAC-SHA3-224", "HMAC-SHA3-256",
        "HMAC-SHA3-384", "HMAC-SHA3-512"
    };

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
    {
        if (!NormalizeMechanism(mechanism).Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported KDF mechanism: {mechanism}.");

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string parameter in parameters)
        {
            int separator = parameter.IndexOf(':');
            if (separator <= 0)
                throw new ArgumentException($"Invalid SP800-108 parameter: {parameter}.");

            string name = parameter[..separator].ToUpperInvariant();
            if (name is not ("#PRF" or "#OUTLEN" or "#COUNTER" or "#LABEL"))
                throw new ArgumentException($"KDF-SP800-108-COUNTER does not support parameter {name}.");
            values[name] = parameter[(separator + 1)..];
        }

        if (!values.TryGetValue("#PRF", out string? prf) || !IsSupportedPrf(prf))
            throw new ArgumentException("KDF-SP800-108-COUNTER requires #PRF with a supported HMAC-* mechanism or AES-CMAC.");
        if (!values.TryGetValue("#OUTLEN", out string? outputLengthText))
            throw new ArgumentException("KDF-SP800-108-COUNTER requires #OUTLEN.");
        uint outputLength = ParseOutputLength(outputLengthText);

        string counterText = values.TryGetValue("#COUNTER", out string? suppliedCounter) ? suppliedCounter : "32";
        int counterLength = ParseCounterLength(counterText);
        ValidateIterationCount(outputLength, GetPrfOutputLengthBits(prf), counterLength);

        values.TryGetValue("#LABEL", out string? label);
        if (label != null)
            ResolveBinaryValue(label, "#LABEL");

        var result = new ParameterVariableDeclaration { Mechanism = MechanismName };
        result.SetParameter("PRF", prf);
        result.SetParameter("OUTLEN", outputLengthText);
        result.SetParameter("COUNTER", counterText);
        if (label != null)
            result.SetParameter("LABEL", label);
        result.ValueFormat = FormatConversions.PAR;
        return result;
    }

    public override KeyVariableDeclaration Derive(string[] parameters)
    {
        ParameterVariableDeclaration parameter = ResolveParameter(parameters[0]);
        if (!NormalizeMechanism(parameter.Mechanism).Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Derive requires KDF-SP800-108-COUNTER parameters.");

        string prf = parameter.GetParameter("PRF");
        if (!IsSupportedPrf(prf))
            throw new ArgumentException("KDF-SP800-108-COUNTER #PRF is unknown or not permitted.");
        uint outputLength = ParseOutputLength(parameter.GetParameter("OUTLEN"));
        int counterLength = ParseCounterLength(parameter.GetParameter("COUNTER"));
        int prfOutputLength = GetPrfOutputLengthBits(prf);
        ulong iterations = ValidateIterationCount(outputLength, prfOutputLength, counterLength);

        byte[] key = ResolveKeyBytes(ResolveKey(parameters[1]));
        ValidatePrfKey(prf, key);
        byte[] context = ResolveBinaryValue(parameters[2], "Context");
        byte[] label = parameter.GetParameters().ContainsKey("#LABEL")
            ? ResolveBinaryValue(parameter.GetParameter("LABEL"), "#LABEL")
            : Array.Empty<byte>();

        int outputBytes = checked((int)(outputLength / 8));
        byte[] result = new byte[outputBytes];
        int written = 0;
        for (uint i = 1; i <= iterations; i++)
        {
            byte[] input = BuildPrfInput(i, counterLength, label, context, outputLength);
            byte[] block = InvokeExistingPrf(prf, key, input);
            int bytesToCopy = Math.Min(block.Length, result.Length - written);
            Buffer.BlockCopy(block, 0, result, written, bytesToCopy);
            written += bytesToCopy;
        }

        string value = FormatConversions.ByteArrayToHexString(result);
        return new KeyVariableDeclaration
        {
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            KeySize = outputLength.ToString(CultureInfo.InvariantCulture),
            KeyType = KeyType.Secret(KeyAlgorithm.Unknown),
            Mechanism = string.Empty,
            DerivationMechanism = MechanismName,
            Type = new CryptoTypeKey()
        };
    }

    private static byte[] InvokeExistingPrf(string prf, byte[] key, byte[] input)
    {
        var parameter = new ParameterVariableDeclaration { Mechanism = prf };
        var keyVariable = new KeyVariableDeclaration
        {
            Value = FormatConversions.ByteArrayToHexString(key),
            KeyValue = FormatConversions.ByteArrayToHexString(key),
            ValueFormat = FormatConversions.HEX,
            Type = new CryptoTypeKey()
        };
        var data = new StringVariableDeclaration
        {
            Value = FormatConversions.ByteArrayToHexString(input),
            ValueFormat = FormatConversions.HEX
        };
        StringVariableDeclaration mac = prf.Equals("AES-CMAC", StringComparison.OrdinalIgnoreCase)
            ? new AES_CMAC().ModeMac(parameter, keyVariable, data)
            : new HMACMode().ModeMac(parameter, keyVariable, data);
        return FormatConversions.ToByteArray(mac.Value, mac.ValueFormat);
    }

    private static byte[] BuildPrfInput(uint counter, int counterLength, byte[] label, byte[] context, uint outputLength)
    {
        int counterBytes = counterLength / 8;
        byte[] input = new byte[counterBytes + label.Length + 1 + context.Length + 4];
        for (int offset = 0; offset < counterBytes; offset++)
            input[counterBytes - 1 - offset] = (byte)(counter >> (offset * 8));
        Buffer.BlockCopy(label, 0, input, counterBytes, label.Length);
        Buffer.BlockCopy(context, 0, input, counterBytes + label.Length + 1, context.Length);
        int lengthOffset = input.Length - 4;
        input[lengthOffset] = (byte)(outputLength >> 24);
        input[lengthOffset + 1] = (byte)(outputLength >> 16);
        input[lengthOffset + 2] = (byte)(outputLength >> 8);
        input[lengthOffset + 3] = (byte)outputLength;
        return input;
    }

    private static uint ParseOutputLength(string value)
    {
        if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint bits) || bits == 0)
            throw new ArgumentException("KDF-SP800-108-COUNTER #OUTLEN must be greater than zero.");
        if (bits % 8 != 0)
            throw new ArgumentException("KDF-SP800-108-COUNTER #OUTLEN must be divisible by 8.");
        if (bits / 8 > int.MaxValue)
            throw new ArgumentException("KDF-SP800-108-COUNTER #OUTLEN exceeds the implementation limit.");
        return bits;
    }

    private static int ParseCounterLength(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int bits) ||
            bits is not (8 or 16 or 24 or 32))
            throw new ArgumentException("KDF-SP800-108-COUNTER #COUNTER must be 8, 16, 24 or 32 bits.");
        return bits;
    }

    private static ulong ValidateIterationCount(uint outputLength, int prfOutputLength, int counterLength)
    {
        ulong iterations = ((ulong)outputLength + (uint)prfOutputLength - 1) / (uint)prfOutputLength;
        ulong maximum = counterLength == 32 ? uint.MaxValue : (1UL << counterLength) - 1;
        if (iterations > maximum)
            throw new ArgumentException("KDF-SP800-108-COUNTER requires n <= 2^r - 1.");
        return iterations;
    }

    private static int GetPrfOutputLengthBits(string prf) =>
        prf.Equals("AES-CMAC", StringComparison.OrdinalIgnoreCase)
            ? 128
            : DigestFactory.Create(prf).GetDigestSize() * 8;

    private static bool IsSupportedPrf(string? prf) =>
        prf != null && (HmacPrfs.Contains(prf) || prf.Equals("AES-CMAC", StringComparison.OrdinalIgnoreCase));

    private static void ValidatePrfKey(string prf, byte[] key)
    {
        if (key.Length == 0)
            throw new ArgumentException("KDF-SP800-108-COUNTER KIN must not be empty.");
        if (prf.Equals("AES-CMAC", StringComparison.OrdinalIgnoreCase) && key.Length is not (16 or 24 or 32))
            throw new ArgumentException("AES-CMAC KIN must be 128, 192 or 256 bits.");
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
        if (!IsBinaryFormat(format) && IsBinaryFormat(key.ValueFormat))
            format = key.ValueFormat;
        if (!IsBinaryFormat(format))
            throw new ArgumentException("KDF-SP800-108-COUNTER KIN must contain valid hex, Base64 or string data.");
        return ConvertToBytes(value, format, "KIN");
    }

    private static byte[] ResolveBinaryValue(string value, string name)
    {
        string format = FormatConversions.ParseString(value);
        if (!IsBinaryFormat(format))
            throw new ArgumentException($"KDF-SP800-108-COUNTER {name} must contain hex, Base64 or string data.");
        return ConvertToBytes(value, format, name);
    }

    private static byte[] ConvertToBytes(string value, string format, string name)
    {
        try
        {
            return FormatConversions.ToByteArray(value, format);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new ArgumentException($"KDF-SP800-108-COUNTER {name} contains an invalid value.", exception);
        }
    }

    private static bool IsBinaryFormat(string format) =>
        format == FormatConversions.HEX || format == FormatConversions.B64 || format == FormatConversions.STR;

    private static string NormalizeMechanism(string mechanism) =>
        mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
            ? mechanism["#MECH:".Length..]
            : mechanism;
}
