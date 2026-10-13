using CryptoScript.CryptoAlgorithm.AES;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.KDF;

// EMV Contact Book 2 v4.4 A1.4.3, page 140, including Bulletin 162's full-block inversion.
// Formula checked directly against the official documents; not EMV product certification.
internal static class EmvMasterKeyDerivationOptionC
{
    internal static byte[] Derive(byte[] issuerMasterKey, string pan, string? psn)
    {
        ArgumentNullException.ThrowIfNull(issuerMasterKey);
        ArgumentNullException.ThrowIfNull(pan);

        byte[]? keySnapshot = null;
        byte[]? diversificationBlock = null;
        byte[]? complementBlock = null;
        byte[]? firstBlock = null;
        byte[]? secondBlock = null;
        byte[]? result = null;
        bool resultReturned = false;
        try
        {
            keySnapshot = (byte[])issuerMasterKey.Clone();
            AES_ECB.ValidateKeyLength(keySnapshot);
            // Provisional CryptoScript input contract, not a normative PAN-length restriction.
            ValidateDigits(pan, 1, 19, nameof(pan));
            psn ??= "00";
            ValidateDigits(psn, 2, 2, nameof(psn));

            diversificationBlock = CreateDiversificationBlock(pan, psn);
            result = new byte[keySnapshot.Length];
            firstBlock = AES_ECB.EncryptNoPadding(keySnapshot, diversificationBlock);
            firstBlock.CopyTo(result, 0);
            if (keySnapshot.Length > 16)
            {
                complementBlock = CreateComplement(diversificationBlock);
                secondBlock = AES_ECB.EncryptNoPadding(keySnapshot, complementBlock);
                secondBlock.AsSpan(0, result.Length - firstBlock.Length)
                    .CopyTo(result.AsSpan(firstBlock.Length));
            }

            resultReturned = true;
            return result;
        }
        finally
        {
            Clear(keySnapshot);
            Clear(diversificationBlock);
            Clear(complementBlock);
            Clear(firstBlock);
            Clear(secondBlock);
            if (!resultReturned)
                Clear(result);
        }
    }

    private static void ValidateDigits(string value, int minimum, int maximum, string parameterName)
    {
        if (value.Length < minimum || value.Length > maximum ||
            value.Any(character => character is < '0' or > '9'))
            throw new ArgumentException(
                $"EMV Option C {parameterName} must contain {minimum}-{maximum} ASCII decimal digits.",
                parameterName);
    }

    private static byte[] CreateDiversificationBlock(string pan, string psn)
    {
        byte[] block = new byte[16];
        // Pack a virtual PAN || PSN into 32 digits, left-padded with zero nibbles.
        // Retain the complete PAN; do not allocate additional strings containing PAN/PSN.
        int start = 32 - pan.Length - psn.Length;
        for (int digit = 0; digit < pan.Length + psn.Length; digit++)
        {
            int nibble = (digit < pan.Length ? pan[digit] : psn[digit - pan.Length]) - '0';
            int position = start + digit;
            block[position / 2] |= (byte)(nibble << (position % 2 == 0 ? 4 : 0));
        }
        return block;
    }

    private static byte[] CreateComplement(byte[] input)
    {
        byte[] complement = new byte[16];
        for (int index = 0; index < complement.Length; index++)
            complement[index] = (byte)~input[index];
        return complement;
    }

    private static void Clear(byte[]? buffer)
    {
        if (buffer is not null)
            CryptographicOperations.ZeroMemory(buffer);
    }
}
