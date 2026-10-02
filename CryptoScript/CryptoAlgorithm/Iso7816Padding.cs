using System;
using System.Security.Cryptography;
namespace CryptoScript.CryptoAlgorithm;
public sealed class Iso7816Padding
{
    private readonly int _blockSize;

    public Iso7816Padding(int blockSizeBytes = 16)
    {
        if (blockSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSizeBytes));

        _blockSize = blockSizeBytes;
    }

    /// <summary>
    /// ISO/IEC 7816-4 padding (0x80 followed by 0x00 bytes)
    /// </summary>
    public byte[] Pad(byte[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        int paddingLength = _blockSize - (input.Length % _blockSize);
        long paddedLength = (long)input.Length + paddingLength;
        if (paddedLength > int.MaxValue)
            throw new ArgumentException("Input is too large for ISO7816 padding.", nameof(input));

        byte[] output = new byte[(int)paddedLength];

        Buffer.BlockCopy(input, 0, output, 0, input.Length);
        output[input.Length] = 0x80;
        // rest is already 0x00

        return output;
    }

    /// <summary>
    /// Removes ISO/IEC 7816-4 padding
    /// </summary>
    public byte[] Unpad(byte[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (input.Length == 0 || input.Length % _blockSize != 0)
            throw new CryptographicException("Invalid ISO7816 padded data length.");

        int firstPaddingIndex = input.Length - _blockSize;
        int index = input.Length - 1;

        // Canonical padding is at most one block long, so the marker must be
        // present in the final block.
        while (index >= firstPaddingIndex && input[index] == 0x00)
            index--;

        if (index < firstPaddingIndex || input[index] != 0x80)
            throw new CryptographicException("Invalid ISO7816 padding.");

        byte[] output = new byte[index];
        Buffer.BlockCopy(input, 0, output, 0, index);
        return output;
    }
}

