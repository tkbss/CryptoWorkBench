using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

internal static class AesDukptDerivation
{
    internal static byte[] CreateDerivationData(
        ushort usage,
        ushort algorithm,
        ushort keyLengthBits,
        ReadOnlySpan<byte> initialKeyId,
        uint transactionCounter,
        bool initialKey)
    {
        if (initialKeyId.Length != 8)
            throw new ArgumentException("Initial Key ID must be exactly 8 bytes.", nameof(initialKeyId));

        byte[] data = new byte[16];
        data[0] = 0x01;
        data[1] = 0x01;
        data[2] = (byte)(usage >> 8);
        data[3] = (byte)usage;
        data[4] = (byte)(algorithm >> 8);
        data[5] = (byte)algorithm;
        data[6] = (byte)(keyLengthBits >> 8);
        data[7] = (byte)keyLengthBits;
        if (initialKey)
        {
            initialKeyId.CopyTo(data.AsSpan(8));
        }
        else
        {
            initialKeyId[4..].CopyTo(data.AsSpan(8, 4));
            data[12] = (byte)(transactionCounter >> 24);
            data[13] = (byte)(transactionCounter >> 16);
            data[14] = (byte)(transactionCounter >> 8);
            data[15] = (byte)transactionCounter;
        }
        return data;
    }

    internal static byte[] DeriveKey(byte[] derivationKey, ushort keyLengthBits, byte[] derivationData)
    {
        if (derivationKey.Length is not (16 or 24 or 32))
            throw new ArgumentException("AES derivation key must be 128, 192 or 256 bits.", nameof(derivationKey));
        if (keyLengthBits is not (128 or 192 or 256))
            throw new ArgumentException("Derived key length must be 128, 192 or 256 bits.", nameof(keyLengthBits));
        if (derivationData.Length != 16)
            throw new ArgumentException("Derivation data must be exactly 16 bytes.", nameof(derivationData));

        int outputLength = keyLengthBits / 8;
        int blockCount = (outputLength + 15) / 16;
        byte[] result = new byte[blockCount * 16];
        for (int block = 0; block < blockCount; block++)
        {
            byte[] blockData = (byte[])derivationData.Clone();
            blockData[1] = checked((byte)(block + 1));
            byte[] encrypted = EncryptBlock(derivationKey, blockData);
            Buffer.BlockCopy(encrypted, 0, result, block * 16, encrypted.Length);
        }
        return result[..outputLength];
    }

    private static byte[] EncryptBlock(byte[] keyBytes, byte[] input)
    {
        string keyValue = FormatConversions.ByteArrayToHexString(keyBytes);
        var key = new KeyVariableDeclaration
        {
            Value = keyValue,
            KeyValue = keyValue,
            ValueFormat = FormatConversions.HEX,
            Type = new CryptoTypeKey()
        };
        var data = new StringVariableDeclaration
        {
            Value = FormatConversions.ByteArrayToHexString(input),
            ValueFormat = FormatConversions.HEX
        };
        var parameters = new ParameterVariableDeclaration { Mechanism = "AES-ECB" };
        StringVariableDeclaration encrypted = new AES_ECB().ModeEncryption(parameters, key, data);
        return FormatConversions.ToByteArray(encrypted.Value, encrypted.ValueFormat);
    }
}
