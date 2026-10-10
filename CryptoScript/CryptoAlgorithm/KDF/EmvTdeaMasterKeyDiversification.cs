using CryptoScript.CryptoAlgorithm.DES3;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.KDF;

internal static class EmvTdeaMasterKeyDiversification
{
    // Borrowed inputs: the caller validates the IMK and owns/clears both inputs.
    internal static byte[] Derive(byte[] validatedImkSnapshot, ReadOnlySpan<byte> diversificationBlock)
    {
        ArgumentNullException.ThrowIfNull(validatedImkSnapshot);
        if (validatedImkSnapshot.Length != 16)
            throw new ArgumentException("EMV diversification requires a 16-byte validated IMK.", nameof(validatedImkSnapshot));
        if (diversificationBlock.Length != 8)
            throw new ArgumentException("EMV diversification requires an 8-byte block.", nameof(diversificationBlock));

        byte[]? firstInput = null;
        byte[]? complement = null;
        byte[]? firstBlock = null;
        byte[]? secondBlock = null;
        byte[]? result = null;
        bool returned = false;
        try
        {
            // The existing primitive needs an array; this copy is owned by this method.
            firstInput = diversificationBlock.ToArray();
            complement = CreateComplement(firstInput);
            firstBlock = DES3_ECB.EncryptNoPadding(validatedImkSnapshot, firstInput);
            secondBlock = DES3_ECB.EncryptNoPadding(validatedImkSnapshot, complement);
            result = new byte[16];
            firstBlock.CopyTo(result, 0);
            secondBlock.CopyTo(result, 8);
            DesParameters.SetOddParity(result);
            returned = true;
            return result;
        }
        finally
        {
            Clear(firstInput);
            Clear(complement);
            Clear(firstBlock);
            Clear(secondBlock);
            if (!returned)
                Clear(result);
        }
    }

    internal static byte[] CreateComplement(ReadOnlySpan<byte> input)
    {
        byte[] complement = new byte[input.Length];
        for (int index = 0; index < input.Length; index++)
            complement[index] = (byte)(input[index] ^ 0xFF);
        return complement;
    }

    private static void Clear(byte[]? buffer)
    {
        if (buffer is not null)
            CryptographicOperations.ZeroMemory(buffer);
    }
}
