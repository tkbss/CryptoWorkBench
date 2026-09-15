namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>TDEA key variants for TR-31 variant binding.</summary>
public static class Tr31TdeaKeyVariants
{
    private const byte EncryptionVariant = 0x45;
    private const byte AuthenticationVariant = 0x4D;

    public static (byte[] Kbek, byte[] Kbak) DeriveKeys(byte[] kbpk)
    {
        ArgumentNullException.ThrowIfNull(kbpk);
        DES3.DES3.ValidateKeyLength(kbpk);
        DES3.DES3.ValidateUsableKey(kbpk);
        return (ApplyVariant(kbpk, EncryptionVariant), ApplyVariant(kbpk, AuthenticationVariant));
    }

    private static byte[] ApplyVariant(byte[] kbpk, byte variant)
    {
        byte[] result = new byte[kbpk.Length];
        for (int i = 0; i < kbpk.Length; i++)
            result[i] = (byte)(kbpk[i] ^ variant);
        return result;
    }
}
