using System.Globalization;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

public class KDF_HKDF : CryptoAlgorithm
{
    private const string MechanismName = "KDF-HKDF";

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
        if (!mechanism.Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported KDF mechanism: {mechanism}.");

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string parameter in parameters)
        {
            int separator = parameter.IndexOf(':');
            if (separator <= 0)
                throw new ArgumentException($"Invalid HKDF parameter: {parameter}.");

            string name = parameter[..separator].ToUpperInvariant();
            string value = parameter[(separator + 1)..];
            if (name != "#HASH" && name != "#SALT" && name != "#OUTLEN")
                throw new ArgumentException($"HKDF does not support parameter {name}.");
            values[name] = value;
        }

        if (!values.TryGetValue("#HASH", out string? hash) || !HashLengths.TryGetValue(hash, out int hashLength))
            throw new ArgumentException("HKDF requires #HASH with a supported HASH-* mechanism.");

        if (!values.TryGetValue("#OUTLEN", out string? outputLengthText))
            throw new ArgumentException("HKDF requires #OUTLEN.");
        if (!int.TryParse(outputLengthText, NumberStyles.None, CultureInfo.InvariantCulture, out int outputLength) || outputLength <= 0)
            throw new ArgumentException("HKDF output length must be a positive integer number of bits.");
        if (outputLength % 8 != 0)
            throw new ArgumentException("HKDF output length must be divisible by 8.");
        if (outputLength / 8 > 255 * hashLength)
            throw new ArgumentException($"HKDF output length exceeds the RFC 5869 limit for {hash}.");

        if (values.TryGetValue("#SALT", out string? salt))
        {
            string format = FormatConversions.ParseString(salt);
            if (format != FormatConversions.HEX && format != FormatConversions.B64 && format != FormatConversions.STR)
                throw new ArgumentException("HKDF salt must be a hex, Base64 or string value.");
        }

        var result = new ParameterVariableDeclaration { Mechanism = MechanismName };
        result.SetParameter("HASH", hash);
        if (salt != null)
            result.SetParameter("SALT", salt);
        result.SetParameter("OUTLEN", outputLengthText);
        result.ValueFormat = FormatConversions.PAR;
        return result;
    }

    private static string NormalizeMechanism(string mechanism)
    {
        return mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
            ? mechanism["#MECH:".Length..]
            : mechanism;
    }
}
