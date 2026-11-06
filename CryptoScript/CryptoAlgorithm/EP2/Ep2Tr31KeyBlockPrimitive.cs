using System.Security.Cryptography;
using System.Text;
using CryptoScript.CryptoAlgorithm.WRAPPERS;

namespace CryptoScript.CryptoAlgorithm.EP2;

/// <summary>EP2 8.3.0 section 8.7 / Table 11, using the shared Version D binding primitives.</summary>
public static class Ep2Tr31KeyBlockPrimitive
{
    public static void ValidateHeader(string header)
    {
        if (header is not ("D011299HX00N0000" or "D01129AHG00N0000" or "D01129BHG00N0000"))
            throw new ArgumentException("EP2 TR-31 requires a complete Table 11 header (99/X, 9A/G or 9B/G).");
    }

    public static string Wrap(byte[] kbpk, byte[] key, string header, byte[]? random = null)
    {
        ValidateKey(kbpk); ValidateKey(key); ValidateHeader(header);
        if (random is not null && random.Length != 14)
            throw new ArgumentException("EP2 TR-31 random filler must contain exactly 14 bytes.");
        byte[]? encKey = null, macKey = null, confidential = null, payload = null, generatedRandom = null;
        Tr31ConfidentialData? data = null;
        try
        {
            // Strict size checks above prevent the generic helper's wrong-sized-random fallback.
            data = Tr31ConfidentialData.Create(key, 16, 0, random ?? (generatedRandom = RandomNumberGenerator.GetBytes(14)));
            confidential = data.ToArray();
            encKey = WrapAESTR31.DeriveKey(kbpk, WrapAESTR31.EncryptionDerivationData(128), 128);
            macKey = WrapAESTR31.DeriveKey(kbpk, WrapAESTR31.MACDerivationData(128), 128);
            payload = [.. Encoding.ASCII.GetBytes(header), .. confidential];
            byte[] mac = WrapAESTR31.ComputeMAC(payload, macKey);
            byte[] cipher = WrapAESTR31.EncryptNoPadding(encKey, mac, confidential);
            return header + Convert.ToHexString(cipher) + Convert.ToHexString(mac);
        }
        finally
        {
            Clear(encKey, macKey, confidential, payload, generatedRandom, data?.Key, data?.ObfuscationPadding, data?.CipherBlockPadding);
        }
    }

    public static byte[] Unwrap(byte[] kbpk, string wire)
    {
        ValidateKey(kbpk);
        if (wire is null || wire.Length != 112 || wire.AsSpan(16).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
            throw new ArgumentException("EP2 TR-31 requires a complete 112-character ASCII key block.");
        ValidateHeader(wire[..16]);
        byte[] cipher = Convert.FromHexString(wire.AsSpan(16, 64));
        byte[] mac = Convert.FromHexString(wire.AsSpan(80, 32));
        byte[]? encKey = null, macKey = null, confidential = null, payload = null, expected = null;
        try
        {
            encKey = WrapAESTR31.DeriveKey(kbpk, WrapAESTR31.EncryptionDerivationData(128), 128);
            macKey = WrapAESTR31.DeriveKey(kbpk, WrapAESTR31.MACDerivationData(128), 128);
            confidential = WrapAESTR31.DecryptNoPadding(encKey, mac, cipher);
            payload = [.. Encoding.ASCII.GetBytes(wire[..16]), .. confidential];
            expected = WrapAESTR31.ComputeMAC(payload, macKey);
            if (!CryptographicOperations.FixedTimeEquals(expected, mac))
                throw new CryptographicException("EP2 TR-31 authentication failed.");
            if (confidential.Length != 32 || confidential[0] != 0 || confidential[1] != 0x80)
                throw new CryptographicException("EP2 TR-31 confidential data must encode exactly 128 key bits.");
            return Tr31ConfidentialData.ExtractKey(confidential);
        }
        finally { Clear(encKey, macKey, confidential, payload, expected); }
    }

    private static void ValidateKey(byte[] key)
    {
        if (key is null || key.Length != 16)
            throw new ArgumentException("EP2 TR-31 requires exactly 16 bytes at each key position.");
    }

    internal static void Clear(params byte[]?[] buffers)
    {
        foreach (var buffer in buffers)
            if (buffer is not null) CryptographicOperations.ZeroMemory(buffer);
    }
}
