using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm;

internal static class CbcMacPrimitive
{
    internal static byte[] Compute(
        Func<SymmetricAlgorithm> algorithmFactory,
        byte[] key,
        byte[] input,
        int blockSizeBytes,
        int macLength)
    {
        ArgumentNullException.ThrowIfNull(algorithmFactory);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(input);
        if (blockSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSizeBytes), "CBC-MAC block size must be greater than zero.");
        if (input.Length == 0)
            throw new ArgumentException("CBC-MAC input must contain at least one complete block.", nameof(input));
        if (input.Length % blockSizeBytes != 0)
            throw new ArgumentException("CBC-MAC input length must be a multiple of the block size.", nameof(input));
        if (macLength <= 0 || macLength > blockSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(macLength), "CBC-MAC length must be between 1 byte and the block size.");

        using SymmetricAlgorithm algorithm = algorithmFactory() ??
            throw new InvalidOperationException("CBC-MAC algorithm factory returned null.");
        if (algorithm.BlockSize / 8 != blockSizeBytes)
            throw new ArgumentException("CBC-MAC block size does not match the selected algorithm.", nameof(blockSizeBytes));
        algorithm.Mode = CipherMode.CBC;
        algorithm.Padding = PaddingMode.None;
        algorithm.Key = key;
        algorithm.IV = new byte[blockSizeBytes];

        using ICryptoTransform encryptor = algorithm.CreateEncryptor();
        byte[] encrypted = encryptor.TransformFinalBlock(input, 0, input.Length);
        return encrypted[(encrypted.Length - blockSizeBytes)..][..macLength];
    }
}
