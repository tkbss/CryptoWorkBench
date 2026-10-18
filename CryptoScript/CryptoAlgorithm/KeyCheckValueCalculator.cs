using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm;

public enum KeyCheckValueMethod { KCV, KCVC, KCVK }

/// <summary>Full-block analysis values, derived from current material, never persisted or cached.</summary>
public static class KeyCheckValueCalculator
{
    public static string? Calculate(KeyVariableDeclaration key, KeyCheckValueMethod method)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!Enum.IsDefined(method)) throw new ArgumentOutOfRangeException(nameof(method));
        if (key.KeyType.MaterialKind != KeyMaterialKind.Secret ||
            key.KeyType.Algorithm is not (KeyAlgorithm.Aes or KeyAlgorithm.Tdea)) return null;

        byte[]? material = null, input = null, result = null;
        try
        {
            // Legacy TR-31 AES objects contain tagged HEX with no ValueFormat.
            // Resolve the encoding, never the algorithm, from that tag.
            string format = string.IsNullOrEmpty(key.ValueFormat) ? FormatConversions.ParseString(key.Value) : key.ValueFormat;
            if (format == FormatConversions.HEX)
            {
                string hex = key.Value;
                if (hex.StartsWith("0x(", StringComparison.Ordinal))
                {
                    if (!hex.EndsWith(')')) return null;
                    hex = hex[3..^1];
                }
                material = Convert.FromHexString(hex);
            }
            else material = FormatConversions.ToByteArray(key.Value, format);
            bool aes = key.KeyType.Algorithm == KeyAlgorithm.Aes;
            if (aes ? material.Length is not (16 or 24 or 32) : material.Length is not (16 or 24))
                return null;
            input = new byte[aes ? 16 : 8];
            if (method == KeyCheckValueMethod.KCVK)
                material.AsSpan(0, input.Length).CopyTo(input);
            result = method == KeyCheckValueMethod.KCVC
                ? aes ? AES_CMAC.ComputeMac(material, input) : DES3_CMAC.ComputeForAnalysis(material, input)
                : aes ? AES_ECB.EncryptNoPadding(material, input) : DES3_ECB.EncryptNoPaddingForAnalysis(material, input);
            return FormatConversions.ByteArrayToHexString(result);
        }
        // Incomplete or malformed imported model objects have no applicable check value.
        catch (FormatException) { return null; }
        catch (ArgumentException) { return null; }
        finally
        {
            if (material is not null) CryptographicOperations.ZeroMemory(material);
            if (input is not null) CryptographicOperations.ZeroMemory(input);
            if (result is not null) CryptographicOperations.ZeroMemory(result);
        }
    }
}
