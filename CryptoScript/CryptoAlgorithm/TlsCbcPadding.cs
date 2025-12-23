using System;
using System.Security.Cryptography;

public sealed class TlsCbcPadding
{
    private readonly int _blockSizeBytes;

    public TlsCbcPadding(int blockSizeBytes)
    {
        if (blockSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSizeBytes));

        _blockSizeBytes = blockSizeBytes;
    }

    /// <summary>
    /// TLS CBC padding (TLS 1.0–1.2):
    /// Appends N bytes, each with value (N-1), so that total length is a multiple
    /// of the block size. Minimum padding length is 1 byte.
    ///
    /// Note: TLS allows adding extra full blocks of padding (not required). This
    /// implementation produces the minimal-length padding.
    /// </summary>
    public byte[] Pad(byte[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        int mod = input.Length % _blockSizeBytes;

        // TLS requires at least 1 byte padding.
        // Choose the smallest N >= 1 such that (len + N) % blockSize == 0.
        int paddingLen = (mod == 0) ? _blockSizeBytes : (_blockSizeBytes - mod);
        if (paddingLen < 1)
            throw new CryptographicException("Invalid TLS-CBC padding length.");

        byte padByte = (byte)(paddingLen - 1);

        byte[] output = new byte[input.Length + paddingLen];
        Buffer.BlockCopy(input, 0, output, 0, input.Length);

        for (int i = input.Length; i < output.Length; i++)
            output[i] = padByte;

        return output;
    }

    /// <summary>
    /// Removes TLS CBC padding (TLS 1.0–1.2).
    /// Validates that the last (p+1) bytes all equal p, where p is the last byte.
    /// </summary>
    public byte[] Unpad(byte[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (input.Length == 0 || (input.Length % _blockSizeBytes) != 0)
            throw new CryptographicException("Invalid TLS-CBC padded data length.");

        byte p = input[input.Length - 1];
        int paddingLen = p + 1;

        if (paddingLen < 1 || paddingLen > input.Length)
            throw new CryptographicException("Invalid TLS-CBC padding length.");

        // Check padding bytes (optionally constant-time-ish)
        int bad = 0;
        int start = input.Length - paddingLen;
        for (int i = start; i < input.Length; i++)
            bad |= (input[i] ^ p);

        if (bad != 0)
            throw new CryptographicException("Invalid TLS-CBC padding bytes.");

        byte[] output = new byte[start];
        Buffer.BlockCopy(input, 0, output, 0, start);
        return output;
    }
}


