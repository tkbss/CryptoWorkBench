using System;

namespace CryptoScript.CryptoAlgorithm;

public sealed class Iso9797M1Padding
{
    private readonly int _blockSizeBytes;

    public Iso9797M1Padding(int blockSizeBytes)
    {
        if (blockSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSizeBytes));

        _blockSizeBytes = blockSizeBytes;
    }

    /// <summary>
    /// ISO/IEC 9797-1 Padding Method 1: right-pad with zeroes to a positive
    /// multiple of the block size. An empty input becomes one full zero block.
    /// </summary>
    public byte[] Pad(byte[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        int remainder = input.Length % _blockSizeBytes;
        long paddedLength = input.Length == 0
            ? _blockSizeBytes
            : remainder == 0
                ? input.Length
                : (long)input.Length + _blockSizeBytes - remainder;
        if (paddedLength > int.MaxValue)
            throw new ArgumentException("Input is too large for ISO9797-M1 padding.", nameof(input));

        byte[] output = new byte[(int)paddedLength];
        Buffer.BlockCopy(input, 0, output, 0, input.Length);
        return output;
    }
}
