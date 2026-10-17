using System.Security.Cryptography;
using System.Text;
using CryptoScript.CryptoAlgorithm.DES3;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>Version-B authentication and key extraction, without header semantics or script integration.</summary>
internal static class Tr31VersionBUnwrap
{
    internal static byte[] Unwrap(string header, byte[] ciphertext, byte[] receivedMac, byte[] kbpk, bool analysisKeys = false)
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

        var (kbek, kbak) = analysisKeys ? Tr31TdeaKeyDerivation.DeriveKeysForAnalysis(kbpk)
            : Tr31TdeaKeyDerivation.DeriveKeys(kbpk);
        byte[]? confidentialData = null;
        byte[]? calculatedMac = null;
        byte[]? dataToAuthenticate = null;
        try
        {
            confidentialData = analysisKeys ? DES3_CBC.DecryptNoPaddingForAnalysis(kbek, receivedMac, ciphertext)
                : DES3_CBC.DecryptNoPadding(kbek, receivedMac, ciphertext);
            dataToAuthenticate = [.. Encoding.ASCII.GetBytes(header), .. confidentialData];
            calculatedMac = analysisKeys ? DES3_CMAC.ComputeForAnalysis(kbak, dataToAuthenticate)
                : DES3_CMAC.Compute(kbak, dataToAuthenticate);
            if (!CryptographicOperations.FixedTimeEquals(calculatedMac, receivedMac))
                throw new CryptographicException("TR-31 version B authentication failed.");

            // No key extraction or successful result before authentication.
            return Tr31ConfidentialData.ExtractKey(confidentialData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kbek);
            CryptographicOperations.ZeroMemory(kbak);
            if (confidentialData is not null) CryptographicOperations.ZeroMemory(confidentialData);
            if (calculatedMac is not null) CryptographicOperations.ZeroMemory(calculatedMac);
            if (dataToAuthenticate is not null) CryptographicOperations.ZeroMemory(dataToAuthenticate);
        }
    }
}
