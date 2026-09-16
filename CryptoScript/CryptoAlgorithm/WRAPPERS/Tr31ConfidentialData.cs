using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>Binary key length, key and two distinct padding regions; no binding or cipher logic.</summary>
public sealed class Tr31ConfidentialData
{
    private const int LengthFieldBytes = 2;

    public byte[] KeyLength { get; }
    public byte[] Key { get; }
    public byte[] ObfuscationPadding { get; }
    public byte[] CipherBlockPadding { get; }

    private Tr31ConfidentialData(byte[] key, byte[] obfuscationPadding, byte[] cipherBlockPadding)
    {
        int bits = checked(key.Length * 8);
        KeyLength = [(byte)(bits >> 8), (byte)bits];
        Key = key;
        ObfuscationPadding = obfuscationPadding;
        CipherBlockPadding = cipherBlockPadding;
    }

    /// <summary>
    /// Padding consumes supplied bytes in wire order: obfuscation first, then block padding.
    /// As in the existing wrapper, absent or wrong-sized random data is replaced in full.
    /// </summary>
    public static Tr31ConfidentialData Create(byte[] key, int blockSize,
        int obfuscationPaddingLength, byte[]? random = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length > ushort.MaxValue / 8)
            throw new ArgumentException("Key bit length exceeds the two-byte length field.", nameof(key));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockSize);
        ArgumentOutOfRangeException.ThrowIfNegative(obfuscationPaddingLength);

        int unalignedLength = checked(LengthFieldBytes + key.Length + obfuscationPaddingLength);
        int blockPaddingLength = (blockSize - unalignedLength % blockSize) % blockSize;
        int paddingLength = checked(obfuscationPaddingLength + blockPaddingLength);
        if (random == null || random.Length != paddingLength)
        {
            random = new byte[paddingLength];
            RandomNumberGenerator.Fill(random);
        }

        return new Tr31ConfidentialData(key.ToArray(), random[..obfuscationPaddingLength],
            random[obfuscationPaddingLength..]);
    }

    public byte[] ToArray() => [.. KeyLength, .. Key, .. ObfuscationPadding, .. CipherBlockPadding];

    /// <summary>
    /// Extracts a byte-aligned key using the encoded bit length. Padding bytes are ignored;
    /// their split cannot be inferred without the caller's padding policy.
    /// </summary>
    public static byte[] ExtractKey(byte[] confidentialData)
    {
        ArgumentNullException.ThrowIfNull(confidentialData);
        if (confidentialData.Length < LengthFieldBytes)
            throw new ArgumentException("Missing key length field.", nameof(confidentialData));
        int bits = (confidentialData[0] << 8) | confidentialData[1];
        if (bits % 8 != 0)
            throw new ArgumentException("Key length must be byte-aligned.", nameof(confidentialData));
        int keyBytes = bits / 8;
        if (keyBytes > confidentialData.Length - LengthFieldBytes)
            throw new ArgumentException("Encoded key length exceeds confidential data.", nameof(confidentialData));
        return confidentialData[LengthFieldBytes..(LengthFieldBytes + keyBytes)];
    }
}
