using System.Buffers.Binary;
using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.FileEncryption;

// One message per instance. Padding is applied once, after the entire message.
internal sealed class StreamingBlockMac : IDisposable
{
    private readonly IBlockCipher cipher;
    private readonly IBlockCipher? retailDecrypt, retailEncrypt;
    private readonly byte[] block, chain;
    private readonly string padding;
    private readonly int macLength;
    private int used;
    private long consumed;
    private long? expectedLength;

    internal StreamingBlockMac(ParameterVariableDeclaration parameters, byte[] key)
    {
        bool aes = parameters.Mechanism == "AES-CBC-MAC";
        bool retail = parameters.Mechanism == "DES3-RETAIL";
        padding = parameters.GetParameter("PAD").ToUpperInvariant();
        if (aes) AES_CBC_MAC.ValidateParameters(parameters);
        if (padding is not ("NONE" or "PKCS-7" or "ANSI-X923" or "ISO-7816" or
            "ISO-9797-M1" or "ISO-9797-M2" or "ISO-9797-M3" or "TLS-CBC") ||
            retail && padding is not ("ISO-9797-M1" or "ISO-9797-M2"))
            throw new ArgumentException("Unsupported streaming MAC padding.");
        if (retail && parameters.GetParameter("IV") != string.Empty)
            throw new ArgumentException("DES3-RETAIL does not use an IV.");
        int size = aes ? 16 : 8;
        string length = parameters.GetParameter("MACLEN");
        macLength = length == string.Empty && !retail ? size :
            int.TryParse(length.Trim('"'), out int parsed) ? parsed : 0;
        if (macLength < (aes ? 8 : 4) || macLength > size)
            throw new ArgumentException("Invalid streaming MAC length.");
        block = new byte[size]; chain = new byte[size];
        cipher = aes ? new AesEngine() : retail ? new DesEngine() : new DesEdeEngine();
        cipher.Init(true, retail ? new KeyParameter(key, 0, 8) : new KeyParameter(key));
        if (retail)
        {
            retailDecrypt = new DesEngine(); retailEncrypt = new DesEngine();
            retailDecrypt.Init(false, new KeyParameter(key, 8, 8));
            retailEncrypt.Init(true, new KeyParameter(key, key.Length == 16 ? 0 : 16, 8));
        }
    }

    internal int MacSize => macLength;
    internal void Begin(Stream source)
    {
        if (padding != "ISO-9797-M3") return;
        if (!source.CanSeek || source.Position != 0 || source.Length < 0)
            throw new IOException("ISO-9797-M3 requires a reliable length from the opened source at position zero.");
        expectedLength = source.Length;
        ulong bits = checked((ulong)expectedLength.Value * 8UL);
        BinaryPrimitives.WriteUInt64BigEndian(block.AsSpan(block.Length - 8), bits);
        ProcessBlock();
    }

    internal void CheckLength(Stream source, bool end)
    {
        if (expectedLength is not long length) return;
        if (source.Length != length || source.Position != consumed || consumed > length || end && consumed != length)
            throw new IOException("ISO-9797-M3 source length changed or the read was incomplete.");
    }

    internal void Update(byte[] input, int count)
    {
        consumed = checked(consumed + count);
        int offset = 0;
        while (offset < count)
        {
            int take = Math.Min(block.Length - used, count - offset);
            input.AsSpan(offset, take).CopyTo(block.AsSpan(used));
            used += take; offset += take;
            if (used == block.Length) ProcessBlock();
        }
    }

    private void ProcessBlock()
    {
        for (int i = 0; i < block.Length; i++) chain[i] ^= block[i];
        cipher.ProcessBlock(chain, 0, chain, 0);
        CryptographicOperations.ZeroMemory(block); used = 0;
    }

    internal void Finish(byte[] result)
    {
        int remaining = block.Length - used;
        switch (padding)
        {
            case "NONE":
                if (consumed == 0 || used != 0)
                    throw new ArgumentException("CBC-MAC with NONE requires nonempty complete blocks.");
                break;
            case "ISO-9797-M1":
            case "ISO-9797-M3":
                if (used != 0 || consumed == 0) ProcessBlock();
                break;
            case "ISO-7816":
            case "ISO-9797-M2":
                block[used] = 0x80; ProcessBlock(); break;
            case "PKCS-7":
                block.AsSpan(used).Fill((byte)remaining); ProcessBlock(); break;
            case "ANSI-X923":
                block[^1] = (byte)remaining; ProcessBlock(); break;
            case "TLS-CBC":
                block.AsSpan(used).Fill((byte)(remaining - 1)); ProcessBlock(); break;
        }
        retailDecrypt?.ProcessBlock(chain, 0, chain, 0);
        retailEncrypt?.ProcessBlock(chain, 0, chain, 0);
        chain.AsSpan(0, macLength).CopyTo(result);
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(block); CryptographicOperations.ZeroMemory(chain);
        // Bouncy Castle engines do not expose key-schedule erasure.
    }
}
