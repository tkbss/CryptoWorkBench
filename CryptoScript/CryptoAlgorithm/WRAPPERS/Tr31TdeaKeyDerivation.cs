using CryptoScript.CryptoAlgorithm.DES3;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>TDEA key derivation for TR-31 version B, ANSI X9.143-2022 section 7.2.2.</summary>
public static class Tr31TdeaKeyDerivation
{
    public static (byte[] Kbek, byte[] Kbak) DeriveKeys(byte[] kbpk)
    {
        ArgumentNullException.ThrowIfNull(kbpk);
        DES3.DES3.ValidateKeyLength(kbpk);
        DES3.DES3.ValidateUsableKey(kbpk);
        return (Derive(kbpk, 0), Derive(kbpk, 1));
    }

    private static byte[] Derive(byte[] kbpk, byte usage)
    {
        const int cmacBytes = 8;
        byte[] result = new byte[kbpk.Length];
        // Counter | Usage (2) | Separator | Algorithm (2) | Key length in bits (2).
        // Table 26: 2-key = algorithm 0000 / length 0080; 3-key = 0001 / 00C0.
        byte[] data = [1, 0, usage, 0, 0, (byte)(kbpk.Length == 16 ? 0 : 1), 0, (byte)(kbpk.Length * 8)];
        for (int offset = 0; offset < result.Length; offset += cmacBytes)
        {
            byte[] part = DES3_CMAC.Compute(kbpk, data);
            Array.Copy(part, 0, result, offset, cmacBytes);
            data[0]++;
        }
        return result;
    }
}
