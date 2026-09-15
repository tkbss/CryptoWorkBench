using CryptoScript.CryptoAlgorithm.WRAPPERS;

namespace CryptoScriptUnitTest;

public class Tr31TdeaKeyVariantsTests
{
    [TestCase(2)]
    [TestCase(3)]
    public void MatchesExistingVersionReference(int referenceIndex)
    {
        var reference = Tr31ReferenceVectors.All[referenceIndex];
        var expectedOrigin = referenceIndex == 2
            ? Tr31VectorOrigin.CorrectedAnsiReferenceVector
            : Tr31VectorOrigin.DerivedReferenceVector;
        byte[] kbpk = Convert.FromHexString(reference.Kbpk);
        byte[] originalKbpk = (byte[])kbpk.Clone();

        var (kbek, kbak) = Tr31TdeaKeyVariants.DeriveKeys(kbpk);

        Assert.Multiple(() =>
        {
            Assert.That(reference.Origin, Is.EqualTo(expectedOrigin));
            Assert.That(kbek, Is.EqualTo(Convert.FromHexString(reference.Kbek)));
            Assert.That(kbak, Is.EqualTo(Convert.FromHexString(reference.Kbak)));
            Assert.That(kbpk, Is.EqualTo(originalKbpk));
        });
    }

    [Test]
    public void AppliesVariantsToEveryByteOfSyntheticTripleLengthKbpk()
    {
        // Synthetic input and independently calculated XOR expectations; not an ANSI vector.
        byte[] kbpk = Convert.FromHexString("000102030405060708090A0B0C0D0E0F1011121314151617");

        var (kbek, kbak) = Tr31TdeaKeyVariants.DeriveKeys(kbpk);

        Assert.Multiple(() =>
        {
            Assert.That(Convert.ToHexString(kbek), Is.EqualTo(
                "45444746414043424D4C4F4E49484B4A5554575651505352"));
            Assert.That(Convert.ToHexString(kbak), Is.EqualTo(
                "4D4C4F4E49484B4A45444746414043425D5C5F5E59585B5A"));
            Assert.That(kbek, Has.Length.EqualTo(24));
            Assert.That(kbak, Has.Length.EqualTo(24));
        });
    }

    [TestCase(15)]
    [TestCase(25)]
    public void RejectsUnsupportedKbpkLengths(int length)
    {
        Assert.Throws<ArgumentException>(() => Tr31TdeaKeyVariants.DeriveKeys(new byte[length]));
    }

    [Test]
    public void RejectsNullKbpk()
    {
        Assert.Throws<ArgumentNullException>(() => Tr31TdeaKeyVariants.DeriveKeys(null!));
    }

    [TestCase(16)]
    [TestCase(24)]
    public void PreservesExistingDes3WeakKeyPolicy(int length)
    {
        Assert.Throws<ArgumentException>(() => Tr31TdeaKeyVariants.DeriveKeys(new byte[length]));
    }
}
