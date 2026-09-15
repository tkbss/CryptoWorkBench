using System.Security.Cryptography;
using System.Text;
using CryptoScript.CryptoAlgorithm.DES3;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>Version-B authentication and key extraction, without header semantics or script integration.</summary>
internal static class Tr31VersionBUnwrap
{
    internal static byte[] Unwrap(string header, byte[] ciphertext, byte[] receivedMac, byte[] kbpk)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(kbpk);
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(receivedMac);
        int authenticationBytes = TR31Block.GetAuthenticationValueLength('B');
        if (receivedMac.Length != authenticationBytes)
            throw new ArgumentException("Version B authentication value must contain exactly 8 bytes.", nameof(receivedMac));
        if (ciphertext.Length == 0 || ciphertext.Length % authenticationBytes != 0)
            throw new ArgumentException("Ciphertext must be a non-zero multiple of 8 bytes.", nameof(ciphertext));

        var (kbek, kbak) = Tr31TdeaKeyDerivation.DeriveKeys(kbpk);
        byte[] confidentialData = DES3_CBC.DecryptNoPadding(kbek, receivedMac, ciphertext);
        byte[] dataToAuthenticate = [.. Encoding.ASCII.GetBytes(header), .. confidentialData];
        byte[] calculatedMac = DES3_CMAC.Compute(kbak, dataToAuthenticate);
        if (!CryptographicOperations.FixedTimeEquals(calculatedMac, receivedMac))
            throw new CryptographicException("TR-31 version B authentication failed.");

        // No key extraction or successful result before authentication.
        return Tr31ConfidentialData.ExtractKey(confidentialData);
    }
}
