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
        if (paddingLength == 0)
            paddingLength = _blockSize;

        byte[] output = new byte[input.Length + paddingLength];

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

        int index = input.Length - 1;

        // Skip zero bytes
        while (index >= 0 && input[index] == 0x00)
            index--;

        if (index < 0 || input[index] != 0x80)
            throw new CryptographicException("Invalid ISO7816 padding.");

        byte[] output = new byte[index];
        Buffer.BlockCopy(input, 0, output, 0, index);
        return output;
    }
}

