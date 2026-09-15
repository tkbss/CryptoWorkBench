using CryptoScript.CryptoAlgorithm.WRAPPERS;

namespace CryptoScriptUnitTest;

public class Tr31TdeaKeyDerivationTests
{
    [TestCase(0)]
    [TestCase(1)]
    public void MatchesBothPublishedBReferences(int index)
    {
        var reference = Tr31ReferenceVectors.All[index];
        byte[] kbpk = Convert.FromHexString(reference.Kbpk);
        var (kbek, kbak) = Tr31TdeaKeyDerivation.DeriveKeys(kbpk);
        Assert.Multiple(() =>
        {
            Assert.That(reference.Origin, Is.EqualTo(Tr31VectorOrigin.AnsiVector));
            Assert.That(kbek, Is.EqualTo(Convert.FromHexString(reference.Kbek)));
            Assert.That(kbak, Is.EqualTo(Convert.FromHexString(reference.Kbak)));
            Assert.That(kbek, Has.Length.EqualTo(16));
            Assert.That(kbak, Has.Length.EqualTo(16));
            Assert.That(Convert.ToHexString(kbpk), Is.EqualTo(reference.Kbpk));
        });
    }

    [Test]
    public void TripleLengthMatchesIndependentReferenceForEachCounterAndUsage()
    {
        // DerivedReferenceVector, NOT a published ANSI example.
        // Independently calculated using Python cryptography CMAC(TripleDES(kbpk)).
        // Each literal below is a separate CMAC result for the stated 8-byte input.
        const string kbpkHex = "0123456789ABCDEFFEDCBA98765432100011223344556677";
        string[] encryptionParts =
        [
            "DCDEE137C75BE3EE", // 01000000000100C0
            "A41EAAC5688122C2", // 02000000000100C0
            "7A7ED6A838D91253"  // 03000000000100C0
        ];
        string[] authenticationParts =
        [
            "8164721B9C33B767", // 01000100000100C0 (counter resets to 01)
            "0FAE1D0EE41B1F3C", // 02000100000100C0
            "672DF58D4841FA98"  // 03000100000100C0
        ];
        var (kbek, kbak) = Tr31TdeaKeyDerivation.DeriveKeys(Convert.FromHexString(kbpkHex));
        Assert.That(kbek, Has.Length.EqualTo(24));
        Assert.That(kbak, Has.Length.EqualTo(24));
        Assert.Multiple(() =>
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.That(Convert.ToHexString(kbek.AsSpan(i * 8, 8)), Is.EqualTo(encryptionParts[i]), $"KBEK counter {i + 1}");
                Assert.That(Convert.ToHexString(kbak.AsSpan(i * 8, 8)), Is.EqualTo(authenticationParts[i]), $"KBAK counter {i + 1}");
            }
        });
    }

    [TestCase(0)]
    [TestCase(8)]
    [TestCase(15)]
    [TestCase(17)]
    [TestCase(23)]
    [TestCase(25)]
    [TestCase(32)]
    public void RejectsUnsupportedKbpkLengths(int length)
    {
        Assert.Throws<ArgumentException>(() => Tr31TdeaKeyDerivation.DeriveKeys(new byte[length]));
    }

    [Test]
    public void RejectsNullKbpk()
    {
        Assert.Throws<ArgumentNullException>(() => Tr31TdeaKeyDerivation.DeriveKeys(null!));
    }

    [TestCase(16)]
    [TestCase(24)]
    public void PreservesExistingDes3WeakKeyPolicy(int length)
    {
        Assert.Throws<ArgumentException>(() => Tr31TdeaKeyDerivation.DeriveKeys(new byte[length]));
    }
}
