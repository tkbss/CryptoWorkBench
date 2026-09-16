using CryptoScript.CryptoAlgorithm.WRAPPERS;

namespace CryptoScriptUnitTest;

public class Tr31TdeaVariantWrapTests
{
    [Test]
    public void MatchesCorrectedCReferenceAtEveryOutputStage()
    {
        var reference = Tr31ReferenceVectors.All[2];
        var result = Tr31TdeaVariantWrap.Wrap(reference.Header,
            Convert.FromHexString(reference.Kbpk), Convert.FromHexString(reference.ConfidentialData));

        Assert.Multiple(() =>
        {
            Assert.That(reference.Origin, Is.EqualTo(Tr31VectorOrigin.CorrectedAnsiReferenceVector));
            Assert.That(result.Ciphertext, Is.EqualTo(Convert.FromHexString(reference.Ciphertext)));
            Assert.That(result.Mac, Is.EqualTo(Convert.FromHexString(reference.Mac)));
            Assert.That(result.Block, Is.EqualTo(reference.CompleteBlock));
        });
    }

    [Test]
    public void MatchesDerivedCorrectedAReference()
    {
        // Derived/corrected A reference, not an unchanged ANSI vector.
        var reference = Tr31ReferenceVectors.All[3];
        var result = Tr31TdeaVariantWrap.Wrap(reference.Header,
            Convert.FromHexString(reference.Kbpk), Convert.FromHexString(reference.ConfidentialData));

        Assert.Multiple(() =>
        {
            Assert.That(reference.Origin, Is.EqualTo(Tr31VectorOrigin.DerivedReferenceVector));
            Assert.That(result.Ciphertext, Is.EqualTo(Convert.FromHexString(reference.Ciphertext)));
            Assert.That(result.Mac, Is.EqualTo(Convert.FromHexString(reference.Mac)));
            Assert.That(result.Block, Is.EqualTo(reference.CompleteBlock));
        });
    }

    [Test]
    public void TripleLengthKbpkMatchesIndependentSyntheticReference()
    {
        // Synthetic reference independently calculated for this test; not an ANSI vector.
        const string header = "A0088P0TE00E0000";
        byte[] kbpk = Convert.FromHexString("0123456789ABCDEFFEDCBA98765432100011223344556677");
        byte[] confidentialData = Convert.FromHexString(
            "0080F039121BEC83D26B169BDCD5B22AAF8F249F30A2B39A7D6B720DF563BB07");

        var result = Tr31TdeaVariantWrap.Wrap(header, kbpk, confidentialData);

        Assert.Multiple(() =>
        {
            Assert.That(Convert.ToHexString(result.Ciphertext), Is.EqualTo(
                "D3088BEDC91F4A1D20AA287AC3E50AD1936F919E89349A4F8593FCE4002CD2DC"));
            Assert.That(Convert.ToHexString(result.Mac), Is.EqualTo("55DE2F4F"));
            Assert.That(result.Block, Is.EqualTo(
                header + "D3088BEDC91F4A1D20AA287AC3E50AD1936F919E89349A4F8593FCE4002CD2DC55DE2F4F"));
        });
    }

    [TestCase(1)]
    [TestCase(31)]
    [TestCase(33)]
    public void RejectsUnalignedConfidentialData(int length)
    {
        var reference = Tr31ReferenceVectors.All[2];
        Assert.Throws<ArgumentException>(() => Tr31TdeaVariantWrap.Wrap(reference.Header,
            Convert.FromHexString(reference.Kbpk), new byte[length]));
    }

    [TestCase("")]
    [TestCase("A123456")]
    public void RejectsHeaderShorterThanEightBytes(string header)
    {
        var reference = Tr31ReferenceVectors.All[2];
        Assert.Throws<ArgumentException>(() => Tr31TdeaVariantWrap.Wrap(header,
            Convert.FromHexString(reference.Kbpk), Convert.FromHexString(reference.ConfidentialData)));
    }

    [TestCase('B')]
    [TestCase('D')]
    public void RejectsNonVariantBindingVersions(char version)
    {
        var reference = Tr31ReferenceVectors.All[2];
        string header = version + reference.Header[1..];
        Assert.Throws<NotSupportedException>(() => Tr31TdeaVariantWrap.Wrap(header,
            Convert.FromHexString(reference.Kbpk), Convert.FromHexString(reference.ConfidentialData)));
    }
}
