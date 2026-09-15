using System.Text;
using CryptoScript.CryptoAlgorithm.WRAPPERS;

namespace CryptoScriptUnitTest;

public class Tr31BlockParsingTests
{
    [TestCase(0, 8)]
    [TestCase(1, 8)]
    [TestCase(2, 4)]
    [TestCase(3, 4)]
    public void ParsesReferenceBlockWithoutPerformingCryptography(int index, int authenticationBytes)
    {
        var reference = Tr31ReferenceVectors.All[index];
        var block = TR31Block.FromString(reference.CompleteBlock);

        Assert.Multiple(() =>
        {
            Assert.That(block.Header, Is.EqualTo(reference.Header[..16]));
            Assert.That(block.HeaderDataToMac, Is.EqualTo(Encoding.ASCII.GetBytes(reference.Header)));
            Assert.That(block.Cryptogram, Is.EqualTo(Convert.FromHexString(reference.Ciphertext)));
            Assert.That(block.Mac, Is.EqualTo(Convert.FromHexString(reference.Mac)));
            Assert.That(block.Mac, Has.Length.EqualTo(authenticationBytes));
        });
        if (reference.Header.Length == 16)
            Assert.That(block.OptionalBlocks, Is.Empty);
        else
        {
            Assert.That(block.OptionalBlocks, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(block.OptionalBlocks![0].ID, Is.EqualTo("KS"));
                Assert.That(block.OptionalBlocks[0].Data, Is.EqualTo("00604B120F9292800000"));
            });
        }
    }

    [Test]
    public void ParsesExistingDVectorWithSixteenByteAuthenticationValue()
    {
        const string header = "D0112P0AE00E0000";
        const string cipher = "B82679114F470F540165EDFBF7E250FCEA43F810D215F8D207E2E417C07156A2";
        const string mac = "7E8E31DA05F7425509593D03A457DC34";
        var block = TR31Block.FromString(header + cipher + mac);
        Assert.Multiple(() =>
        {
            Assert.That(block.Header, Is.EqualTo(header));
            Assert.That(block.HeaderDataToMac, Is.EqualTo(Encoding.ASCII.GetBytes(header)));
            Assert.That(block.OptionalBlocks, Is.Empty);
            Assert.That(block.Cryptogram, Is.EqualTo(Convert.FromHexString(cipher)));
            Assert.That(block.Mac, Is.EqualTo(Convert.FromHexString(mac)));
            Assert.That(block.Mac, Has.Length.EqualTo(16));
        });
    }

    // Synthetic structural boundary cases, not authenticated or valid key blocks.
    [TestCase('A', 4)]
    [TestCase('B', 8)]
    [TestCase('C', 4)]
    [TestCase('D', 16)]
    [TestCase('Z', 16)]
    public void UsesVersionLengthAtAuthenticationOnlyBoundary(char version, int bytes)
    {
        string header = $"{version}{16 + bytes * 2:D4}D0TB00E0000";
        byte[] authentication = Enumerable.Range(1, bytes).Select(i => (byte)i).ToArray();
        var block = TR31Block.FromString(header + Convert.ToHexString(authentication));
        Assert.Multiple(() =>
        {
            Assert.That(block.Cryptogram, Is.Empty);
            Assert.That(block.Mac, Is.EqualTo(authentication));
        });

        string shorterHeader = $"{version}{16 + (bytes - 1) * 2:D4}D0TB00E0000";
        var shorter = TR31Block.FromString(shorterHeader + Convert.ToHexString(authentication[..^1]));
        Assert.Multiple(() =>
        {
            Assert.That(shorter.Cryptogram, Is.Null);
            Assert.That(shorter.Mac, Is.Null);
        });
    }
}
