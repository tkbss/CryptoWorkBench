using System;
using System.Security.Cryptography;
namespace CryptoScript.CryptoAlgorithm;

public sealed class Iso9797M3Padding
{
    private readonly int _blockSizeBytes;
    private const int EncodedLengthBytes = sizeof(ulong);

    public Iso9797M3Padding(int blockSizeBytes)
    {
        if (blockSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSizeBytes));

        if (blockSizeBytes < EncodedLengthBytes)
            throw new ArgumentException("Block size must be >= 8 bytes for ISO9797-M3.");

        _blockSizeBytes = blockSizeBytes;
    }

    /// <summary>
    /// ISO/IEC 9797-1 Padding Method 3
    /// Prepends a block-sized, big-endian bit length and right-pads the data with zeroes.
    /// </summary>
    public byte[] Pad(byte[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        ulong bitLength = checked((ulong)input.Length * 8UL);
        long dataBlockCount = input.Length == 0
            ? 1
            : ((long)input.Length + _blockSizeBytes - 1) / _blockSizeBytes;
        long paddedLength = checked((dataBlockCount + 1) * _blockSizeBytes);
        if (paddedLength > int.MaxValue)
            throw new ArgumentException("Input is too large for ISO9797-M3 padding.", nameof(input));

        byte[] output = new byte[(int)paddedLength];
        Buffer.BlockCopy(input, 0, output, _blockSizeBytes, input.Length);

        // The length is an n-bit block. CryptoScript inputs are byte arrays, so the
        // practical length is encoded in the rightmost 64 bits of that block.
        for (int i = 0; i < EncodedLengthBytes; i++)
        {
            output[_blockSizeBytes - 1 - i] = (byte)(bitLength >> (8 * i));
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

        if ((long)input.Length < 2L * _blockSizeBytes ||
            input.Length % _blockSizeBytes != 0)
        {
            throw new CryptographicException("Invalid ISO9797-M3 padded data length.");
        }

        int encodedLengthOffset = _blockSizeBytes - EncodedLengthBytes;
        for (int i = 0; i < encodedLengthOffset; i++)
        {
            if (input[i] != 0)
                throw new CryptographicException("ISO9797-M3 length exceeds the supported range.");
        }

        // Read the rightmost 64 bits of the block-sized length field (big-endian).
        ulong bitLength = 0;
        for (int i = encodedLengthOffset; i < _blockSizeBytes; i++)
        {
            bitLength = (bitLength << 8) | input[i];
        }

        if ((bitLength & 7) != 0)
            throw new CryptographicException("Invalid ISO9797-M3 bit length.");

        ulong byteLength = bitLength / 8;
        if (byteLength > int.MaxValue)
            throw new CryptographicException("Invalid ISO9797-M3 length field.");

        ulong actualDataRegionLength = (ulong)(input.Length - _blockSizeBytes);
        ulong expectedDataRegionLength = byteLength == 0
            ? (ulong)_blockSizeBytes
            : ((byteLength + (ulong)_blockSizeBytes - 1) / (ulong)_blockSizeBytes) *
              (ulong)_blockSizeBytes;
        if (byteLength > actualDataRegionLength ||
            expectedDataRegionLength != actualDataRegionLength)
        {
            throw new CryptographicException("Invalid ISO9797-M3 length field.");
        }

        int dataLength = (int)byteLength;
        int paddingOffset = _blockSizeBytes + dataLength;
        for (int i = paddingOffset; i < input.Length; i++)
        {
            if (input[i] != 0)
                throw new CryptographicException("Invalid ISO9797-M3 zero padding.");
        }

        byte[] output = new byte[dataLength];
        Buffer.BlockCopy(input, _blockSizeBytes, output, 0, dataLength);

        return output;
    }
}
