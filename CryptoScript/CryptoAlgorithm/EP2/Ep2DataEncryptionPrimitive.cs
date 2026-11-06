using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.EP2;

/// <summary>EP2 8.3: AES-128 CTR with an isolated, big-endian 24-bit counter.</summary>
internal static class Ep2DataEncryptionPrimitive
{
    internal const long MaximumDataLength = (1L << 24) * 16;

    internal static void ValidateDataLength(long length)
    {
        if (length < 0 || length > MaximumDataLength)
            throw new ArgumentException("EP2 data exceeds the 24-bit counter capacity.");
    }

    internal static void ValidateOffset(byte offset, string direction)
    {
        int maximum = direction switch
        {
            "SEND" => 7,
            "RECEIVE" => 1,
            _ => throw new ArgumentException("EP2 direction must be SEND or RECEIVE.")
        };
        if (offset > maximum)
            throw new ArgumentException("EP2 field offset is RFU for the selected direction.");
    }

    internal static byte[] CounterBlock(ReadOnlySpan<byte> nonce, byte offset, uint counter)
    {
        if (nonce.Length != 12)
            throw new ArgumentException("EP2 nonce must contain exactly 12 bytes.");
        if (counter >= 1U << 24)
            throw new ArgumentException("EP2 counter exceeds 24 bits.");
        byte[] block = new byte[16];
        nonce.CopyTo(block);
        block[12] = offset;
        block[13] = (byte)(counter >> 16);
        block[14] = (byte)(counter >> 8);
        block[15] = (byte)counter;
        return block;
    }

    internal static byte[] Transform(byte[] key, byte[] nonce, byte offset, string direction, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(data);
        if (key.Length != 16)
            throw new ArgumentException("EP2 encryption requires exactly 16 key bytes.");
        ValidateDataLength(data.LongLength);
        ValidateOffset(offset, direction);
        byte[] counter = CounterBlock(nonce, offset, 0);
        byte[] stream = new byte[16];
        byte[] output = new byte[data.Length];
        try
        {
            // Same portable AES ECB primitive used by generic AES-CTR, with no padding.
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            using var encryptor = aes.CreateEncryptor();
            for (int position = 0; position < data.Length; position += 16)
            {
                uint blockIndex = (uint)(position / 16);
                // Assign only the counter field. Never carry into the offset or nonce.
                counter[13] = (byte)(blockIndex >> 16);
                counter[14] = (byte)(blockIndex >> 8);
                counter[15] = (byte)blockIndex;
                encryptor.TransformBlock(counter, 0, 16, stream, 0);
                int count = Math.Min(16, data.Length - position);
                for (int i = 0; i < count; i++)
                    output[position + i] = (byte)(data[position + i] ^ stream[i]);
            }
            return output;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(output);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(counter);
            CryptographicOperations.ZeroMemory(stream);
        }
    }
}
