using System.Security.Cryptography;
using Des3Algorithm = CryptoScript.CryptoAlgorithm.DES3.DES3;

namespace CryptoScript.CryptoAlgorithm.KDF;

// EMV Contact Book 2 v4.4 A1.4.2 formula review completed on 13 October 2026
// for commit 881ec9219ca7f558603e98acb3696b7e5219201e; not EMV product certification.
internal static class EmvMasterKeyDerivationOptionB
{
    internal static byte[] Derive(byte[] issuerMasterKey, string pan, string? psn)
    {
        ArgumentNullException.ThrowIfNull(issuerMasterKey);
        byte[]? keySnapshot = null;
        byte[]? diversificationBlock = null;
        byte[]? hashInput = null;
        byte[]? hash = null;
        try
        {
            keySnapshot = (byte[])issuerMasterKey.Clone();
            if (keySnapshot.Length != 16)
                throw new ArgumentException("EMV Option B issuer master key must contain exactly 16 bytes.", nameof(issuerMasterKey));
            Des3Algorithm.ValidateKeyLength(keySnapshot);
            Des3Algorithm.ValidateUsableKey(keySnapshot);
            ArgumentNullException.ThrowIfNull(pan);
            // Provisional technical input limit, not a normative conformity claim.
            ValidateDigits(pan, 1, 19, nameof(pan));
            psn ??= "00";
            ValidateDigits(psn, 2, 2, nameof(psn));

            if (pan.Length <= 16)
                diversificationBlock = EmvMasterKeyDerivationOptionA.CreateDiversificationBlock(pan, psn);
            else
            {
                hashInput = CreateHashInput(pan, psn);
                hash = ComputeHash(hashInput);
                diversificationBlock = Decimalize(hash);
            }
            return EmvTdeaMasterKeyDiversification.Derive(keySnapshot, diversificationBlock);
        }
        finally
        {
            Clear(keySnapshot);
            Clear(diversificationBlock);
            Clear(hashInput);
            Clear(hash);
        }
    }

    private static void ValidateDigits(string value, int minimum, int maximum, string parameterName)
    {
        if (value.Length < minimum || value.Length > maximum ||
            value.Any(character => character is < '0' or > '9'))
            throw new ArgumentException(
                $"EMV Option B {parameterName} must contain {minimum}-{maximum} ASCII decimal digits.",
                parameterName);
    }

    private static byte[] CreateHashInput(string pan, string psn)
    {
        int paddingDigits = pan.Length % 2;
        byte[] input = new byte[(pan.Length + psn.Length + paddingDigits) / 2];
        // Virtual 0 || PAN || PSN for odd PAN lengths; no sensitive string copies.
        for (int digit = paddingDigits; digit < input.Length * 2; digit++)
        {
            int index = digit - paddingDigits;
            int nibble = (index < pan.Length ? pan[index] : psn[index - pan.Length]) - '0';
            input[digit / 2] |= (byte)(nibble << (digit % 2 == 0 ? 4 : 0));
        }
        return input;
    }

    private static byte[] ComputeHash(byte[] input)
    {
        var digest = DigestFactory.Create("HASH-SHA1");
        byte[]? hash = null;
        bool returned = false;
        try
        {
            hash = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(input, 0, input.Length);
            digest.DoFinal(hash, 0);
            digest.Reset();
            returned = true;
            return hash;
        }
        finally
        {
            if (!returned)
            {
                Clear(hash);
                digest.Reset();
            }
        }
    }

    private static byte[] Decimalize(byte[] hash)
    {
        if (hash.Length != 20)
            throw new ArgumentException("EMV Option B decimalization requires a 20-byte SHA-1 digest.", nameof(hash));
        byte[] block = new byte[8];
        int digits = 0;
        for (int pass = 0; pass < 2 && digits < 16; pass++)
        {
            for (int index = 0; index < hash.Length * 2 && digits < 16; index++)
            {
                int nibble = (hash[index / 2] >> (index % 2 == 0 ? 4 : 0)) & 0x0F;
                if (pass == 0 ? nibble > 9 : nibble < 10)
                    continue;
                int digit = pass == 0 ? nibble : nibble - 10;
                block[digits / 2] |= (byte)(digit << (digits % 2 == 0 ? 4 : 0));
                digits++;
            }
        }
        return block;
    }

    private static void Clear(byte[]? buffer)
    {
        if (buffer is not null)
            CryptographicOperations.ZeroMemory(buffer);
    }
}
