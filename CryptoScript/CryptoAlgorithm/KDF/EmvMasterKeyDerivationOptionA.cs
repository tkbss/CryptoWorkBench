using CryptoScript.CryptoAlgorithm.DES3;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;
using Des3Algorithm = CryptoScript.CryptoAlgorithm.DES3.DES3;

namespace CryptoScript.CryptoAlgorithm.KDF;

// Option A is independently corroborated; normative verification against EMV v4.4 is pending.
internal static class EmvMasterKeyDerivationOptionA
{
    internal static byte[] Derive(byte[] issuerMasterKey, string pan, string? psn)
    {
        ArgumentNullException.ThrowIfNull(issuerMasterKey);

        byte[]? keySnapshot = null;
        byte[]? firstInput = null;
        byte[]? secondInput = null;
        byte[]? firstBlock = null;
        byte[]? secondBlock = null;
        byte[]? result = null;
        bool returned = false;
        try
        {
            keySnapshot = (byte[])issuerMasterKey.Clone();
            if (keySnapshot.Length != 16)
                throw new ArgumentException("EMV Option A issuer master key must contain exactly 16 bytes.", nameof(issuerMasterKey));
            Des3Algorithm.ValidateKeyLength(keySnapshot);
            Des3Algorithm.ValidateUsableKey(keySnapshot);

            ArgumentNullException.ThrowIfNull(pan);
            // The 1-19 digit limit is a provisional technical input contract, not a conformity claim.
            ValidateDigits(pan, 1, 19, nameof(pan));
            psn ??= "00";
            ValidateDigits(psn, 2, 2, nameof(psn));

            firstInput = CreateDiversificationBlock(pan, psn);
            secondInput = CreateComplement(firstInput);
            firstBlock = DES3_ECB.EncryptNoPadding(keySnapshot, firstInput);
            secondBlock = DES3_ECB.EncryptNoPadding(keySnapshot, secondInput);
            result = new byte[16];
            firstBlock.CopyTo(result, 0);
            secondBlock.CopyTo(result, 8);
            DesParameters.SetOddParity(result);
            returned = true;
            return result;
        }
        finally
        {
            Clear(keySnapshot);
            Clear(firstInput);
            Clear(secondInput);
            Clear(firstBlock);
            Clear(secondBlock);
            if (!returned)
                Clear(result);
        }
    }

    private static void ValidateDigits(string value, int minimum, int maximum, string parameterName)
    {
        if (value.Length < minimum || value.Length > maximum ||
            value.Any(character => character is < '0' or > '9'))
            throw new ArgumentException(
                $"EMV Option A {parameterName} must contain {minimum}-{maximum} ASCII decimal digits.",
                parameterName);
    }

    private static byte[] CreateDiversificationBlock(string pan, string psn)
    {
        byte[] block = new byte[8];
        // Read a virtual PAN || PSN, left-padded or truncated to its rightmost 16 digits.
        // Avoid allocating additional strings containing the diversification data.
        int start = pan.Length + psn.Length - 16;
        for (int digit = 0; digit < 16; digit++)
        {
            int index = start + digit;
            int nibble = index < 0 ? 0 :
                (index < pan.Length ? pan[index] : psn[index - pan.Length]) - '0';
            block[digit / 2] |= (byte)(nibble << (digit % 2 == 0 ? 4 : 0));
        }
        return block;
    }

    private static byte[] CreateComplement(byte[] input)
    {
        byte[] complement = new byte[input.Length];
        for (int index = 0; index < input.Length; index++)
            complement[index] = (byte)(input[index] ^ 0xFF);
        return complement;
    }

    private static void Clear(byte[]? buffer)
    {
        if (buffer is not null)
            CryptographicOperations.ZeroMemory(buffer);
    }
}
