using System.Buffers.Binary;
using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.FileEncryption;

/// <summary>Bounded-memory message processing. Callers own private output and publication.</summary>
internal static class FileCipherStream
{
    internal const int BufferSize = 65536;

    internal static void Process(Stream source, Stream output, KeyVariableDeclaration key,
        EncryptedFileHeader header, byte[] frame, bool encrypt, CancellationToken token)
    {
        byte[] bytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
        long end = source.Length;
        long length = end - source.Position;
        try
        {
            token.ThrowIfCancellationRequested();
            if (header.Mechanism is "AES-GCM" or "AES-CCM")
            {
                long plainLength = encrypt ? length : length - header.TagLength;
                if (plainLength < 0) throw new InvalidDataException("Missing authentication tag.");
                if (header.Mechanism == "AES-GCM") Gcm(source, output, bytes, header, frame, encrypt, length, token);
                else FileCcmStream.Process(source, output, bytes, header, frame, encrypt, plainLength, token);
            }
            else if (header.Mechanism == "AES-CTR") Ctr(source, output, bytes, header, length, token);
            else Blocks(source, output, bytes, header, encrypt, length, token);
            token.ThrowIfCancellationRequested();
            if (source.ReadByte() != -1 || source.Length != end)
                throw new IOException("CWE source changed during processing.");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static void Read(Stream source, byte[] buffer, int count, CancellationToken token)
    {
        int offset = 0;
        while (offset < count)
        {
            token.ThrowIfCancellationRequested();
            int n = source.Read(buffer, offset, count - offset);
            if (n == 0) throw new EndOfStreamException("CWE source changed or is truncated.");
            offset += n;
        }
    }

    private static void Blocks(Stream source, Stream output, byte[] key, EncryptedFileHeader h,
        bool encrypt, long length, CancellationToken token)
    {
        bool des = h.Mechanism.StartsWith("DES3-");
        int block = des ? 8 : 16;
        if (encrypt && h.Padding == "ISO-9797-M1" && EncryptedFileCodec.OriginalLength(h) != length)
            throw new IOException("CWE source length changed after creating M1 metadata.");
        if ((h.Padding == "NONE" || !encrypt) &&
            (length % block != 0 || length == 0 && h.Mechanism != "AES-ECB"))
            throw new ArgumentException("CWE input must be a non-zero multiple of the block size (except AES-ECB).");
        IBlockCipher cipher = des ? new DesEdeEngine() : new AesEngine();
        ICipherParameters parameters = new KeyParameter(key);
        if (h.Mechanism.EndsWith("CBC"))
        {
            cipher = new CbcBlockCipher(cipher);
            parameters = new ParametersWithIV(parameters, Convert.FromBase64String(h.Iv));
        }
        cipher.Init(encrypt, parameters);
        byte[] input = new byte[BufferSize], result = new byte[BufferSize];
        byte[]? tail = null, final = null;
        long dataLength = length;
        try
        {
            if (h.Padding == "ISO-9797-M3")
            {
                if (encrypt)
                {
                    BinaryPrimitives.WriteUInt64BigEndian(input.AsSpan(block - 8), checked((ulong)length * 8));
                    cipher.ProcessBlock(input, 0, result, 0);
                    output.Write(result, 0, block);
                }
                else
                {
                    if (length < block * 2) throw new InvalidDataException("Missing M3 data block.");
                    Read(source, input, block, token);
                    cipher.ProcessBlock(input, 0, result, 0);
                    ulong bits = BinaryPrimitives.ReadUInt64BigEndian(result.AsSpan(block - 8));
                    if (result.AsSpan(0, block - 8).IndexOfAnyExcept((byte)0) >= 0 || bits % 8 != 0 || bits / 8 > long.MaxValue)
                        throw new InvalidDataException("Invalid M3 length block.");
                    dataLength = (long)(bits / 8);
                    length -= block;
                    if (ZeroPaddedLength(dataLength, block) != length) throw new InvalidDataException("Invalid M3 data length.");
                }
            }
            if (!encrypt && h.Padding == "ISO-9797-M1")
            {
                dataLength = EncryptedFileCodec.OriginalLength(h);
                if (ZeroPaddedLength(dataLength, block) != length) throw new InvalidDataException("Invalid M1 data length.");
            }
            long remaining = length, written = 0;
            // TLS permits up to 256 padding octets. Retain that entire suffix for validation.
            int keep = encrypt ? (int)(length % block) : (int)Math.Min(length, 256);
            long bulk = length - keep;
            while (bulk > 0)
            {
                int count = (int)Math.Min(BufferSize, bulk);
                Read(source, input, count, token);
                for (int i = 0; i < count; i += block) cipher.ProcessBlock(input, i, result, i);
                output.Write(result, 0, count);
                written += count; remaining -= count; bulk -= count;
            }
            Read(source, input, (int)remaining, token);
            if (encrypt)
            {
                tail = input.AsSpan(0, (int)remaining).ToArray();
                if (h.Padding is "ISO-9797-M1" or "ISO-9797-M3")
                {
                    final = remaining == 0 && length != 0 ? [] : new Iso9797M1Padding(block).Pad(tail);
                }
                else
                {
                    var p = new ParameterVariableDeclaration { Mechanism = h.Mechanism };
                    p.SetParameter("PAD", h.Padding);
                    final = new EncryptionMode().Pad(p, out var mode, tail, "Encrypt", block);
                    if (mode != PaddingMode.None)
                    {
                        int n = block - tail.Length;
                        final = new byte[block]; tail.CopyTo(final, 0);
                        if (mode == PaddingMode.PKCS7) final.AsSpan(tail.Length).Fill((byte)n);
                        else if (mode == PaddingMode.ISO10126) RandomNumberGenerator.Fill(final.AsSpan(tail.Length, n - 1));
                        final[^1] = (byte)n;
                    }
                }
                for (int i = 0; i < final.Length; i += block) cipher.ProcessBlock(final, i, result, i);
                output.Write(result, 0, final.Length);
            }
            else
            {
                for (int i = 0; i < remaining; i += block) cipher.ProcessBlock(input, i, result, i);
                tail = result.AsSpan(0, (int)remaining).ToArray();
                if (h.Padding is "ISO-9797-M1" or "ISO-9797-M3")
                {
                    int actual = checked((int)(dataLength - written));
                    if (actual < 0 || actual > tail.Length || tail.AsSpan(actual).IndexOfAnyExcept((byte)0) >= 0)
                        throw new InvalidDataException("Invalid zero padding.");
                    output.Write(tail, 0, actual);
                }
                else
                {
                    final = Unpad(tail, h.Padding, block);
                    output.Write(final);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input); CryptographicOperations.ZeroMemory(result);
            if (tail is not null) CryptographicOperations.ZeroMemory(tail);
            if (final is not null) CryptographicOperations.ZeroMemory(final);
        }
    }

    private static long ZeroPaddedLength(long length, int block) => length == 0 ? block : checked((length - 1) / block * block + block);

    private static byte[] Unpad(byte[] tail, string pad, int block)
    {
        if (pad is "PKCS-7" or "ANSI-X923" or "ISO-10126")
        {
            int n = tail.Length == 0 ? 0 : tail[^1];
            if (n < 1 || n > block || n > tail.Length) throw new CryptographicException("Invalid padding length.");
            if (pad != "ISO-10126" && tail.AsSpan(tail.Length - n, n - 1).IndexOfAnyExcept(pad == "PKCS-7" ? (byte)n : (byte)0) >= 0)
                throw new CryptographicException("Invalid padding bytes.");
            return tail[..^n];
        }
        var p = new ParameterVariableDeclaration { Mechanism = "AES-CBC" };
        p.SetParameter("PAD", pad);
        return new EncryptionMode().Unpad(p, tail, "Decrypt", block);
    }

    private static void Ctr(Stream source, Stream output, byte[] key, EncryptedFileHeader h, long length, CancellationToken token)
    {
        byte[] counter = new byte[16], stream = new byte[16], buffer = new byte[BufferSize];
        Convert.FromBase64String(h.Nonce!).CopyTo(counter, 0);
        Convert.FromBase64String(h.Counter!).CopyTo(counter, 12);
        ulong initial = BinaryPrimitives.ReadUInt32BigEndian(counter.AsSpan(12));
        ulong blocks = (ulong)(length / 16) + (length % 16 == 0 ? 0UL : 1UL);
        if (blocks > 0x100000000UL - initial) throw new ArgumentException("CTR counter would overflow.");
        using var aes = Aes.Create(); aes.Key = key; aes.Mode = CipherMode.ECB; aes.Padding = PaddingMode.None;
        using var cipher = aes.CreateEncryptor();
        try
        {
            ulong index = initial;
            while (length > 0)
            {
                int count = (int)Math.Min(buffer.Length, length);
                Read(source, buffer, count, token);
                for (int i = 0; i < count; i += 16)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(counter.AsSpan(12), (uint)index++);
                    cipher.TransformBlock(counter, 0, 16, stream, 0);
                    for (int j = 0; j < Math.Min(16, count - i); j++) buffer[i + j] ^= stream[j];
                }
                output.Write(buffer, 0, count); length -= count;
            }
        }
        finally { CryptographicOperations.ZeroMemory(buffer); CryptographicOperations.ZeroMemory(stream); CryptographicOperations.ZeroMemory(counter); }
    }

    private static void Gcm(Stream source, Stream output, byte[] key, EncryptedFileHeader h,
        byte[] frame, bool encrypt, long length, CancellationToken token)
    {
        long plaintext = encrypt ? length : length - h.TagLength;
        if (plaintext > (1L << 36) - 32) throw new ArgumentException("GCM message exceeds its counter limit.");
        var cipher = new GcmBlockCipher(new AesEngine());
        byte[] input = new byte[BufferSize], result = new byte[BufferSize + 32];
        try
        {
            cipher.Init(encrypt, new AeadParameters(new KeyParameter(key), 128, Convert.FromBase64String(h.Nonce!), frame));
            while (length > 0)
            {
                int count = (int)Math.Min(input.Length, length);
                Read(source, input, count, token);
                int n = cipher.ProcessBytes(input, 0, count, result, 0);
                output.Write(result, 0, n); length -= count;
            }
            token.ThrowIfCancellationRequested();
            int final = cipher.DoFinal(result, 0);
            output.Write(result, 0, final);
        }
        finally { CryptographicOperations.ZeroMemory(input); CryptographicOperations.ZeroMemory(result); cipher.Reset(); }
    }
}
