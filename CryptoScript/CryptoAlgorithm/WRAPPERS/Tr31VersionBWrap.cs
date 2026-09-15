using System.Runtime.CompilerServices;
using System.Text;
using CryptoScript.CryptoAlgorithm.DES3;

[assembly: InternalsVisibleTo("CryptoScriptUnitTest")]

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>Version-B binding only. The caller supplies the complete header and padded confidential data.</summary>
internal static class Tr31VersionBWrap
{
    internal static (byte[] Ciphertext, byte[] Mac, string Block) Wrap(
        string header, byte[] kbpk, byte[] confidentialData)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(kbpk);
        ArgumentNullException.ThrowIfNull(confidentialData);
        int blockSize = TR31Block.GetAuthenticationValueLength('B');
        if (confidentialData.Length % blockSize != 0)
            throw new ArgumentException("Confidential data must be a multiple of 8 bytes.", nameof(confidentialData));

        var (kbek, kbak) = Tr31TdeaKeyDerivation.DeriveKeys(kbpk);
        byte[] dataToAuthenticate = [.. Encoding.ASCII.GetBytes(header), .. confidentialData];
        byte[] mac = DES3_CMAC.Compute(kbak, dataToAuthenticate);
        byte[] ciphertext = DES3_CBC.EncryptNoPadding(kbek, mac, confidentialData);
        return (ciphertext, mac, header + Convert.ToHexString(ciphertext) + Convert.ToHexString(mac));
    }
}
