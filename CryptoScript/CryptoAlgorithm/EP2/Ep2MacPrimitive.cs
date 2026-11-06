using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.EP2;

internal static class Ep2MacPrimitive
{
    internal static byte[] Compute(byte[] key, byte[] data, bool prehashMessage)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(data);
        if (key.Length != 32)
            throw new ArgumentException("EP2 Mac requires exactly 32 key bytes.");
        byte[]? hash = null;
        byte[]? mac = null;
        try
        {
            if (prehashMessage)
            {
                var digest = DigestFactory.Create("HASH-SHA256");
                digest.BlockUpdate(data, 0, data.Length);
                hash = new byte[digest.GetDigestSize()];
                digest.DoFinal(hash, 0);
            }
            mac = HMAC.HmacPrimitive.Compute("HMAC-SHA256", key, hash ?? data);
            return mac.AsSpan(0, 8).ToArray();
        }
        finally
        {
            if (hash is not null) CryptographicOperations.ZeroMemory(hash);
            if (mac is not null) CryptographicOperations.ZeroMemory(mac);
        }
    }
}
