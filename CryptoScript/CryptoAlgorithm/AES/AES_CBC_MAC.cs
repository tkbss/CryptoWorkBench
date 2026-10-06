using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm;

internal sealed class AES_CBC_MAC : EncryptionMode
{
    private const int BlockSizeBytes = 16;
    private static readonly HashSet<string> SupportedPaddings = new(StringComparer.OrdinalIgnoreCase)
    {
        "NONE", "PKCS-7", "ANSI-X923", "ISO-7816", "ISO-9797-M1",
        "ISO-9797-M2", "ISO-9797-M3", "TLS-CBC"
    };

    public override StringVariableDeclaration ModeMac(
        ParameterVariableDeclaration parameter,
        KeyVariableDeclaration key,
        StringVariableDeclaration data)
    {
        byte[] keyBytes = GetValidatedKey(key);
        int macLength = GetMacLength(parameter);
        ValidateNamedParameters(parameter);
        string paddingName = parameter.GetParameter("PAD");
        if (paddingName.Equals("ISO-10126", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("AES-CBC-MAC does not support ISO-10126 padding.");

        byte[] dataBytes = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
        if (paddingName.Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            if (dataBytes.Length == 0)
                throw new ArgumentException("AES-CBC-MAC with PAD=NONE requires non-empty input containing at least one 16-byte block.");
            if (dataBytes.Length % BlockSizeBytes != 0)
                throw new ArgumentException("AES-CBC-MAC with PAD=NONE requires input length to be a multiple of 16 bytes.");
        }

        byte[] input = PadToCompleteBlocks(parameter, dataBytes, "Mac", BlockSizeBytes);
        byte[] mac = CbcMacPrimitive.Compute(
            Aes.Create, keyBytes, input, BlockSizeBytes, macLength);
        return CreateResult(mac);
    }

    internal static void ValidateParameters(ParameterVariableDeclaration parameter)
    {
        ValidateNamedParameters(parameter);
        GetMacLength(parameter);
        string padding = parameter.GetParameter("PAD");
        if (padding.Equals("ISO-10126", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("AES-CBC-MAC does not support ISO-10126 padding.");
        if (!SupportedPaddings.Contains(padding))
            throw new ArgumentException($"AES-CBC-MAC does not support {padding} padding.");
    }

    private static void ValidateNamedParameters(ParameterVariableDeclaration parameter)
    {
        string? unsupported = parameter.GetParameters().Keys.FirstOrDefault(name =>
            name is not ("#MECH" or "#PAD" or "#MACLEN"));
        if (unsupported is not null)
            throw new ArgumentException($"AES-CBC-MAC does not support parameter {unsupported}.");
    }

    private static byte[] GetValidatedKey(KeyVariableDeclaration key)
    {
        byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
        if (keyBytes.Length is not (16 or 24 or 32))
            throw new ArgumentException("AES-CBC-MAC key must contain exactly 16, 24, or 32 bytes.");
        return keyBytes;
    }

    private static int GetMacLength(ParameterVariableDeclaration parameter)
    {
        string value = parameter.GetParameter("MACLEN");
        if (value == string.Empty)
            return BlockSizeBytes;
        if (!int.TryParse(value.Trim('"'), out int length) || length < 8 || length > BlockSizeBytes)
            throw new ArgumentException("AES-CBC-MAC length must be between 8 and 16 bytes.");
        return length;
    }

    private static StringVariableDeclaration CreateResult(byte[] value)
    {
        string encoded = FormatConversions.ByteArrayToHexString(value);
        return new StringVariableDeclaration
        {
            Value = encoded,
            ValueFormat = FormatConversions.ParseString(encoded),
            Type = new CryptoTypeVar()
        };
    }
}
