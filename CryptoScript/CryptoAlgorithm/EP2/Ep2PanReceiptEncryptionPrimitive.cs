using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.EP2;

/// <summary>EP2 5.5.2 AES-128 ECB with mandatory ISO-7816 padding.</summary>
internal static class Ep2PanReceiptEncryptionPrimitive
{
    internal static byte[] Transform(byte[] key, byte[] input, bool encrypt)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(input);
        if (key.Length != 16)
            throw new ArgumentException("EP2 PAN receipt encryption requires exactly 16 key bytes.");
        if (!encrypt && (input.Length == 0 || input.Length % 16 != 0))
            throw new ArgumentException("EP2 PAN receipt ciphertext must contain a positive multiple of 16 bytes.");

        var padding = new Iso7816Padding(16);
        byte[]? padded = null;
        try
        {
            // AES_ECB's internal helpers accept one block only. Use the same portable
            // provider here for the complete padded message, without altering generic ECB.
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            if (encrypt)
            {
                padded = padding.Pad(input);
                return aes.EncryptEcb(padded, PaddingMode.None);
            }
            padded = aes.DecryptEcb(input, PaddingMode.None);
            return padding.Unpad(padded);
        }
        finally
        {
            if (padded is not null) CryptographicOperations.ZeroMemory(padded);
        }
    }
}
