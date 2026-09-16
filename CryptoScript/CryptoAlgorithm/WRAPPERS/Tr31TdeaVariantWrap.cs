using System.Text;
using CryptoScript.CryptoAlgorithm.DES3;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>Shared TDEA variant-binding wrap core for TR-31 versions A and C.</summary>
internal static class Tr31TdeaVariantWrap
{
    private const int BlockSizeBytes = 8;
    private const int AuthenticationValueLength = 4;

    internal static (byte[] Ciphertext, byte[] Mac, string Block) Wrap(
        string header, byte[] kbpk, byte[] confidentialData)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(kbpk);
        ArgumentNullException.ThrowIfNull(confidentialData);

        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        if (headerBytes.Length < BlockSizeBytes)
            throw new ArgumentException("Header must contain at least 8 bytes.", nameof(header));
        if (header[0] != 'A' && header[0] != 'C')
            throw new NotSupportedException("TR-31 TDEA variant wrap supports only versions A and C.");
        if (confidentialData.Length % BlockSizeBytes != 0)
            throw new ArgumentException("Confidential data must be a multiple of 8 bytes.", nameof(confidentialData));

        var (kbek, kbak) = Tr31TdeaKeyVariants.DeriveKeys(kbpk);
        byte[] iv = headerBytes[..BlockSizeBytes];
        byte[] ciphertext = DES3_CBC.EncryptNoPadding(kbek, iv, confidentialData);
        byte[] dataToAuthenticate = [.. headerBytes, .. ciphertext];
        byte[] mac = DES3_CBC.ComputeMacNoPadding(kbak, dataToAuthenticate, AuthenticationValueLength);
        return (ciphertext, mac, header + Convert.ToHexString(ciphertext) + Convert.ToHexString(mac));
    }
}
