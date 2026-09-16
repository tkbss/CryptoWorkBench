using System.Security.Cryptography;
using System.Text;
using CryptoScript.CryptoAlgorithm.DES3;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>Shared TDEA variant-binding authentication and key extraction for TR-31 versions A and C.</summary>
internal static class Tr31TdeaVariantUnwrap
{
    private const int BlockSizeBytes = 8;
    private const int AuthenticationValueLength = 4;

    internal static byte[] Unwrap(string header, byte[] ciphertext, byte[] receivedMac, byte[] kbpk)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(receivedMac);
        ArgumentNullException.ThrowIfNull(kbpk);

        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        if (headerBytes.Length < BlockSizeBytes)
            throw new ArgumentException("Header must contain at least 8 bytes.", nameof(header));
        if (header[0] != 'A' && header[0] != 'C')
            throw new NotSupportedException("TR-31 TDEA variant unwrap supports only versions A and C.");
        if (receivedMac.Length != AuthenticationValueLength)
            throw new ArgumentException("Variant-binding authentication value must contain exactly 4 bytes.", nameof(receivedMac));
        if (ciphertext.Length == 0 || ciphertext.Length % BlockSizeBytes != 0)
            throw new ArgumentException("Ciphertext must be a non-zero multiple of 8 bytes.", nameof(ciphertext));

        var (kbek, kbak) = Tr31TdeaKeyVariants.DeriveKeys(kbpk);
        byte[] dataToAuthenticate = [.. headerBytes, .. ciphertext];
        byte[] calculatedMac = DES3_CBC.ComputeMacNoPadding(kbak, dataToAuthenticate, AuthenticationValueLength);
        if (!CryptographicOperations.FixedTimeEquals(calculatedMac, receivedMac))
            throw new CryptographicException("TR-31 TDEA variant authentication failed.");

        byte[] iv = headerBytes[..BlockSizeBytes];
        byte[] confidentialData = DES3_CBC.DecryptNoPadding(kbek, iv, ciphertext);
        return Tr31ConfidentialData.ExtractKey(confidentialData);
    }
}
