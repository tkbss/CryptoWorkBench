using System;
using System.Security.Cryptography;
namespace CryptoScript.CryptoAlgorithm;

public sealed class Iso9797M3Padding
{
    private readonly int _blockSizeBytes;
    private const int LengthFieldBytes = 8; // 64-bit length

    public Iso9797M3Padding(int blockSizeBytes)
    {
        if (blockSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSizeBytes));

        if (blockSizeBytes < LengthFieldBytes)
            throw new ArgumentException("Block size must be >= 8 bytes for ISO9797-M3.");

        _blockSizeBytes = blockSizeBytes;
    }

    /// <summary>
    /// ISO/IEC 9797-1 Padding Method 3
    /// Appends 64-bit message length (in bits, big-endian)
    /// </summary>
    public byte[] Pad(byte[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        ulong bitLength = checked((ulong)input.Length * 8);

        int totalLen = input.Length + LengthFieldBytes;
        int paddedLen =
            ((totalLen + _blockSizeBytes - 1) / _blockSizeBytes) * _blockSizeBytes;

        byte[] output = new byte[paddedLen];

        Buffer.BlockCopy(input, 0, output, 0, input.Length);

        // Write length field (big-endian, unsigned)
        for (int i = 0; i < LengthFieldBytes; i++)
        {
            output[paddedLen - 1 - i] = (byte)(bitLength >> (8 * i));
        }

        return output;
    }

    /// <summary>
    /// Removes ISO/IEC 9797-1 Padding Method 3
    /// </summary>
    public byte[] Unpad(byte[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (input.Length < LengthFieldBytes ||
            input.Length % _blockSizeBytes != 0)
        {
            throw new CryptographicException("Invalid ISO9797-M3 padded data length.");
        }

        // Read length field (big-endian)
        ulong bitLength = 0;
        for (int i = 0; i < LengthFieldBytes; i++)
        {
            bitLength = (bitLength << 8) | input[input.Length - LengthFieldBytes + i];
        }

        if ((bitLength & 7) != 0)
            throw new CryptographicException("Invalid ISO9797-M3 bit length.");

        ulong byteLength = bitLength / 8;

        if (byteLength > (ulong)(input.Length - LengthFieldBytes))
            throw new CryptographicException("Invalid ISO9797-M3 length field.");

        byte[] output = new byte[byteLength];
        Buffer.BlockCopy(input, 0, output, 0, (int)byteLength);

        return output;
    }
}
