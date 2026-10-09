using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.Variables;
using System.Security.Cryptography;
using Des3Algorithm = CryptoScript.CryptoAlgorithm.DES3.DES3;

namespace CryptoScript.CryptoAlgorithm.KDF;

internal static class EmvAcSessionKeyDerivation
{
    private const int AtcLengthBytes = 2;
    private const int TdeaKeyLengthBytes = 16;

    internal static byte[] Derive(KeyAlgorithm algorithm, byte[] masterKey, byte[] atc)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        ArgumentNullException.ThrowIfNull(atc);

        byte[]? masterKeySnapshot = null;
        byte[]? atcSnapshot = null;
        try
        {
            masterKeySnapshot = (byte[])masterKey.Clone();
            atcSnapshot = (byte[])atc.Clone();

            if (atcSnapshot.Length != AtcLengthBytes)
                throw new ArgumentException("EMV ATC must contain exactly 2 bytes.", nameof(atc));

            return algorithm switch
            {
                KeyAlgorithm.Tdea => DeriveTdea(masterKeySnapshot, atcSnapshot),
                KeyAlgorithm.Aes => DeriveAes(masterKeySnapshot, atcSnapshot),
                _ => throw new ArgumentException(
                    "EMV AC session key derivation supports only TDEA and AES master keys.",
                    nameof(algorithm))
            };
        }
        finally
        {
            if (masterKeySnapshot is not null)
                CryptographicOperations.ZeroMemory(masterKeySnapshot);
            if (atcSnapshot is not null)
                CryptographicOperations.ZeroMemory(atcSnapshot);
        }
    }

    private static byte[] DeriveTdea(byte[] masterKey, byte[] atc)
    {
        if (masterKey.Length != TdeaKeyLengthBytes)
            throw new ArgumentException(
                "EMV TDEA master key must contain exactly 16 bytes.",
                nameof(masterKey));

        Des3Algorithm.ValidateKeyLength(masterKey);
        Des3Algorithm.ValidateUsableKey(masterKey);

        byte[] firstInput = CreateTdeaInput(atc, 0xF0);
        byte[] secondInput = CreateTdeaInput(atc, 0x0F);
        byte[] sessionKey = new byte[TdeaKeyLengthBytes];
        byte[]? firstBlock = null;
        byte[]? secondBlock = null;
        try
        {
            firstBlock = DES3_ECB.EncryptNoPadding(masterKey, firstInput);
            secondBlock = DES3_ECB.EncryptNoPadding(masterKey, secondInput);
            firstBlock.CopyTo(sessionKey, 0);
            secondBlock.CopyTo(sessionKey, 8);
            return sessionKey;
        }
        finally
        {
            if (firstBlock is not null)
                CryptographicOperations.ZeroMemory(firstBlock);
            if (secondBlock is not null)
                CryptographicOperations.ZeroMemory(secondBlock);
        }
    }

    private static byte[] DeriveAes(byte[] masterKey, byte[] atc)
    {
        AES_ECB.ValidateKeyLength(masterKey);

        if (masterKey.Length == 16)
            return AES_ECB.EncryptNoPadding(masterKey, CreateAes128Input(atc));

        byte[]? firstBlock = null;
        byte[]? secondBlock = null;
        byte[]? sessionKey = null;
        bool sessionKeyReturned = false;
        try
        {
            firstBlock = AES_ECB.EncryptNoPadding(masterKey, CreateAesInput(atc, 0xF0));
            secondBlock = AES_ECB.EncryptNoPadding(masterKey, CreateAesInput(atc, 0x0F));
            sessionKey = new byte[masterKey.Length];
            firstBlock.CopyTo(sessionKey, 0);
            secondBlock.AsSpan(0, sessionKey.Length - firstBlock.Length)
                .CopyTo(sessionKey.AsSpan(firstBlock.Length));
            sessionKeyReturned = true;
            return sessionKey;
        }
        finally
        {
            if (firstBlock is not null)
                CryptographicOperations.ZeroMemory(firstBlock);
            if (secondBlock is not null)
                CryptographicOperations.ZeroMemory(secondBlock);
            if (!sessionKeyReturned && sessionKey is not null)
                CryptographicOperations.ZeroMemory(sessionKey);
        }
    }

    private static byte[] CreateTdeaInput(byte[] atc, byte discriminator)
    {
        byte[] input = new byte[8];
        atc.CopyTo(input, 0);
        input[2] = discriminator;
        return input;
    }

    private static byte[] CreateAes128Input(byte[] atc)
    {
        byte[] input = new byte[16];
        atc.CopyTo(input, 0);
        return input;
    }

    private static byte[] CreateAesInput(byte[] atc, byte discriminator)
    {
        byte[] input = CreateAes128Input(atc);
        input[2] = discriminator;
        return input;
    }
}
