using System.Security.Cryptography;
using System.Text;
using CryptoScript.CryptoAlgorithm.DES3;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>Shared TDEA variant-binding authentication and key extraction for TR-31 versions A and C.</summary>
internal static class Tr31TdeaVariantUnwrap
{
    private const int BlockSizeBytes = 8;
    private const int AuthenticationValueLength = 4;

    internal static byte[] Unwrap(string header, byte[] ciphertext, byte[] receivedMac, byte[] kbpk, bool analysisKeys = false)
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

        var (kbek, kbak) = analysisKeys ? Tr31TdeaKeyVariants.DeriveKeysForAnalysis(kbpk)
            : Tr31TdeaKeyVariants.DeriveKeys(kbpk);
        byte[]? confidentialData = null;
        byte[]? calculatedMac = null;
        try
        {
            byte[] dataToAuthenticate = [.. headerBytes, .. ciphertext];
            calculatedMac = analysisKeys ? DES3_CBC.ComputeMacNoPaddingForAnalysis(kbak, dataToAuthenticate, AuthenticationValueLength)
                : DES3_CBC.ComputeMacNoPadding(kbak, dataToAuthenticate, AuthenticationValueLength);
            if (!CryptographicOperations.FixedTimeEquals(calculatedMac, receivedMac))
                throw new CryptographicException("TR-31 TDEA variant authentication failed.");

            byte[] iv = headerBytes[..BlockSizeBytes];
            confidentialData = analysisKeys ? DES3_CBC.DecryptNoPaddingForAnalysis(kbek, iv, ciphertext)
                : DES3_CBC.DecryptNoPadding(kbek, iv, ciphertext);
            return Tr31ConfidentialData.ExtractKey(confidentialData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kbek);
            CryptographicOperations.ZeroMemory(kbak);
            if (confidentialData is not null) CryptographicOperations.ZeroMemory(confidentialData);
            if (calculatedMac is not null) CryptographicOperations.ZeroMemory(calculatedMac);
        }
    }
}
