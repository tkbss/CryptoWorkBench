using CryptoScript.CryptoAlgorithm;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.CryptoAlgorithm.KDF;

internal static class HKDFMode
{
    internal static byte[] Extract(string hashMechanism, byte[]? salt, byte[] ikm)
    {
        IDigest digest = DigestFactory.Create(hashMechanism);
        byte[] effectiveSalt = salt ?? new byte[digest.GetDigestSize()];
        return ComputeHmac(digest, effectiveSalt, ikm);
    }

    internal static byte[] Expand(string hashMechanism, byte[] prk, byte[] info, int outputLength)
    {
        if (outputLength <= 0)
            throw new ArgumentException("HKDF output length must be positive.", nameof(outputLength));

        int hashLength = DigestFactory.Create(hashMechanism).GetDigestSize();
        if (prk.Length < hashLength)
            throw new ArgumentException("HKDF-Expand PRK must be at least HashLen bytes.", nameof(prk));
        if (outputLength > 255 * hashLength)
            throw new ArgumentException("HKDF output length exceeds 255 times HashLen.", nameof(outputLength));

        return ExpandCore(hashMechanism, prk, info, outputLength, hashLength);
    }

    internal static byte[] ExpandEp2Sha256(byte[] key, byte[] info, int outputLength)
    {
        if (outputLength <= 0)
            throw new ArgumentException("HKDF output length must be positive.", nameof(outputLength));

        const string hashMechanism = "HASH-SHA256";
        int hashLength = DigestFactory.Create(hashMechanism).GetDigestSize();
        if (key.Length != 16)
            throw new ArgumentException("ep2 HKDF-Expand key must be exactly 16 bytes.", nameof(key));
        if (outputLength > 255 * hashLength)
            throw new ArgumentException("HKDF output length exceeds 255 times HashLen.", nameof(outputLength));

        return ExpandCore(hashMechanism, key, info, outputLength, hashLength);
    }

    private static byte[] ExpandCore(
        string hashMechanism, byte[] key, byte[] info, int outputLength, int hashLength)
    {
        byte[] output = new byte[outputLength];
        byte[] previous = Array.Empty<byte>();
        int outputOffset = 0;
        int blockCount = (outputLength + hashLength - 1) / hashLength;
        for (int block = 1; block <= blockCount; block++)
        {
            byte[] input = new byte[previous.Length + info.Length + 1];
            Buffer.BlockCopy(previous, 0, input, 0, previous.Length);
            Buffer.BlockCopy(info, 0, input, previous.Length, info.Length);
            input[^1] = checked((byte)block);

            previous = ComputeHmac(DigestFactory.Create(hashMechanism), key, input);
            int bytesToCopy = Math.Min(previous.Length, outputLength - outputOffset);
            Buffer.BlockCopy(previous, 0, output, outputOffset, bytesToCopy);
            outputOffset += bytesToCopy;
        }
        return output;
    }

    private static byte[] ComputeHmac(IDigest digest, byte[] key, byte[] data)
    {
        IMac mac = new HMac(digest);
        mac.Init(new KeyParameter(key));
        mac.BlockUpdate(data, 0, data.Length);
        byte[] result = new byte[mac.GetMacSize()];
        mac.DoFinal(result, 0);
        return result;
    }
}
