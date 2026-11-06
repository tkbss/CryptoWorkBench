using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CryptoScript.FileEncryption;

/// <summary>Length-aware CCM, RFC 3610 section 2; fixed 16-byte tag, no packet buffering.</summary>
internal static class FileCcmStream
{
    internal static void Process(Stream source, Stream destination, byte[] key, EncryptedFileHeader h,
        byte[] aad, bool encrypt, long length, CancellationToken token)
    {
        byte[] nonce = Convert.FromBase64String(h.Nonce!);
        int q = 15 - nonce.Length;
        if (length < 0 || q < 8 && (ulong)length >= (1UL << (8 * q)))
            throw new ArgumentException("CCM message exceeds the nonce-dependent length limit.");
        using var aes = Aes.Create(); aes.Key = key; aes.Mode = CipherMode.ECB; aes.Padding = PaddingMode.None;
        using var cipher = aes.CreateEncryptor();
        byte[] mac = new byte[16], block = new byte[16], counter = new byte[16], mask = new byte[16], s0 = new byte[16];
        byte[] buffer = new byte[FileCipherStream.BufferSize], tag = new byte[16];
        int filled = 0;
        try
        {
            // B0: Adata=1, M'=7 (16-byte tag), L'=q-1.
            block[0] = (byte)(0x40 | 0x38 | (q - 1)); nonce.CopyTo(block, 1);
            PutLength(block, (ulong)length, q); MacBlock();
            // CWE headers are at most 4112 octets, hence the two-octet AAD length form.
            BinaryPrimitives.WriteUInt16BigEndian(block, checked((ushort)aad.Length)); filled = 2;
            AddMac(aad); FinishMacRegion();
            counter[0] = (byte)(q - 1); nonce.CopyTo(counter, 1);
            cipher.TransformBlock(counter, 0, 16, s0, 0);
            ulong index = 1;
            long remaining = length;
            while (remaining > 0)
            {
                int count = (int)Math.Min(buffer.Length, remaining);
                FileCipherStream.Read(source, buffer, count, token);
                if (encrypt) AddMac(buffer.AsSpan(0, count));
                for (int i = 0; i < count; i += 16)
                {
                    PutLength(counter, index++, q);
                    cipher.TransformBlock(counter, 0, 16, mask, 0);
                    for (int j = 0; j < Math.Min(16, count - i); j++) buffer[i + j] ^= mask[j];
                }
                if (!encrypt) AddMac(buffer.AsSpan(0, count));
                destination.Write(buffer, 0, count); remaining -= count;
            }
            FinishMacRegion();
            for (int i = 0; i < 16; i++) mac[i] ^= s0[i];
            token.ThrowIfCancellationRequested();
            if (encrypt) destination.Write(mac);
            else
            {
                FileCipherStream.Read(source, tag, 16, token);
                if (!CryptographicOperations.FixedTimeEquals(mac, tag))
                    throw new CryptographicException("CCM authentication failed.");
            }
        }
        finally
        {
            foreach (byte[] bytes in new[] { mac, block, counter, mask, s0, buffer, tag }) CryptographicOperations.ZeroMemory(bytes);
        }

        void MacBlock()
        {
            for (int i = 0; i < 16; i++) block[i] ^= mac[i];
            cipher.TransformBlock(block, 0, 16, mac, 0);
            Array.Clear(block); filled = 0;
        }
        void AddMac(ReadOnlySpan<byte> bytes)
        {
            while (!bytes.IsEmpty)
            {
                int n = Math.Min(16 - filled, bytes.Length);
                bytes[..n].CopyTo(block.AsSpan(filled)); filled += n; bytes = bytes[n..];
                if (filled == 16) MacBlock();
            }
        }
        void FinishMacRegion() { if (filled != 0) MacBlock(); }
    }

    private static void PutLength(byte[] block, ulong value, int count)
    {
        for (int i = 15; i >= 16 - count; i--) { block[i] = (byte)value; value >>= 8; }
    }
}
