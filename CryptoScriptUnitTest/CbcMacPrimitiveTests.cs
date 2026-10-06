using CryptoScript.CryptoAlgorithm;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

public class CbcMacPrimitiveTests
{
    private static readonly byte[] Key = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");
    private static readonly byte[] Block = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

    [Test]
    public void Compute_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() =>
            CbcMacPrimitive.Compute(Aes.Create, Key, Array.Empty<byte>(), 16, 16));
    }

    [Test]
    public void Compute_RejectsPartialInput()
    {
        Assert.Throws<ArgumentException>(() =>
            CbcMacPrimitive.Compute(Aes.Create, Key, Block[..15], 16, 16));
    }

    [Test]
    public void Compute_RejectsInvalidBlockSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CbcMacPrimitive.Compute(Aes.Create, Key, Block, 0, 16));
    }

    [TestCase(0)]
    [TestCase(17)]
    public void Compute_RejectsInvalidMacLength(int macLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CbcMacPrimitive.Compute(Aes.Create, Key, Block, 16, macLength));
    }

    [Test]
    public void Compute_AcceptsOneCompleteBlockWithoutAddingPadding()
    {
        byte[] actual = CbcMacPrimitive.Compute(Aes.Create, Key, Block, 16, 16);

        using Aes aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = Key;
        byte[] expected = aes.EncryptEcb(Block, PaddingMode.None);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Compute_AcceptsMultipleCompleteBlocks()
    {
        byte[] input = [.. Block, .. Block];
        byte[] actual = CbcMacPrimitive.Compute(Aes.Create, Key, input, 16, 16);

        using Aes aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = Key;
        aes.IV = new byte[16];
        byte[] ciphertext = aes.EncryptCbc(input, aes.IV, PaddingMode.None);
        Assert.That(actual, Is.EqualTo(ciphertext[^16..]));
    }
}
