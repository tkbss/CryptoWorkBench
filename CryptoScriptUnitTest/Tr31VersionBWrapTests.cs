using CryptoScript.CryptoAlgorithm.WRAPPERS;

namespace CryptoScriptUnitTest;

public class Tr31VersionBWrapTests
{
    [TestCase(0)]
    [TestCase(1)]
    public void MatchesPublishedBBlockAndEachCryptographicOutput(int index)
    {
        var reference = Tr31ReferenceVectors.All[index];
        byte[] kbpk = Convert.FromHexString(reference.Kbpk);
        byte[] confidential = Tr31ConfidentialData.Create(Convert.FromHexString(reference.Key), 8, 8,
            Convert.FromHexString(reference.ObfuscationPadding + reference.CipherBlockPadding)).ToArray();
        var result = Tr31VersionBWrap.Wrap(reference.Header, kbpk, confidential);
        Assert.Multiple(() =>
        {
            Assert.That(reference.Origin, Is.EqualTo(Tr31VectorOrigin.AnsiVector));
            Assert.That(confidential, Is.EqualTo(Convert.FromHexString(reference.ConfidentialData)));
            Assert.That(result.Mac, Is.EqualTo(Convert.FromHexString(reference.Mac)));
            Assert.That(result.Mac, Has.Length.EqualTo(8));
            Assert.That(result.Ciphertext, Is.EqualTo(Convert.FromHexString(reference.Ciphertext)));
            Assert.That(result.Ciphertext, Has.Length.EqualTo(confidential.Length));
            Assert.That(result.Block, Is.EqualTo(reference.CompleteBlock));
            Assert.That(Convert.ToHexString(kbpk), Is.EqualTo(reference.Kbpk));
        });
    }

    [Test]
    public void TripleLengthKbpkProducesStructurallyCompleteBlock()
    {
        // Synthetic structural case, NOT an ANSI vector or independent cryptographic oracle.
        var reference = Tr31ReferenceVectors.All[0];
        byte[] confidential = Convert.FromHexString(reference.ConfidentialData);
        var result = Tr31VersionBWrap.Wrap(reference.Header,
            Convert.FromHexString("0123456789ABCDEFFEDCBA98765432100011223344556677"), confidential);
        var parsed = TR31Block.FromString(result.Block);
        Assert.Multiple(() =>
        {
            Assert.That(result.Mac, Has.Length.EqualTo(8));
            Assert.That(result.Ciphertext, Has.Length.EqualTo(confidential.Length));
            Assert.That(result.Block, Has.Length.EqualTo(96));
            Assert.That(result.Block, Is.EqualTo(reference.Header + Convert.ToHexString(result.Ciphertext) + Convert.ToHexString(result.Mac)));
            Assert.That(parsed.Header, Is.EqualTo(reference.Header));
            Assert.That(parsed.Cryptogram, Is.EqualTo(result.Ciphertext));
            Assert.That(parsed.Mac, Is.EqualTo(result.Mac));
        });
    }

    [Test]
    public void HeaderIsPreservedAndOptionalBlockBytesAreAuthenticated()
    {
        var reference = Tr31ReferenceVectors.All[1];
        // Deliberately wrong length and lowercase KS data: no correction or normalization.
        string header = reference.Header.Replace("0120", "9999").Replace("4B", "4b");
        var result = Tr31VersionBWrap.Wrap(header, Convert.FromHexString(reference.Kbpk),
            Convert.FromHexString(reference.ConfidentialData));
        Assert.That(result.Block[..header.Length], Is.EqualTo(header));
        Assert.That(result.Mac, Is.Not.EqualTo(Convert.FromHexString(reference.Mac)));
        // Isolate optional-block coverage from the fixed header change.
        var optionalOnly = Tr31VersionBWrap.Wrap(reference.Header.Replace("4B", "4b"),
            Convert.FromHexString(reference.Kbpk), Convert.FromHexString(reference.ConfidentialData));
        Assert.That(optionalOnly.Mac, Is.Not.EqualTo(Convert.FromHexString(reference.Mac)));
    }

    [TestCase(0)]
    [TestCase(15)]
    [TestCase(17)]
    [TestCase(25)]
    public void RejectsInvalidKbpkLength(int length)
    {
        Assert.Throws<ArgumentException>(() => Tr31VersionBWrap.Wrap("B0096P0TE00E0000", new byte[length], new byte[32]));
    }

    [TestCase(1)]
    [TestCase(31)]
    [TestCase(33)]
    public void RejectsUnalignedConfidentialData(int length)
    {
        var reference = Tr31ReferenceVectors.All[0];
        Assert.Throws<ArgumentException>(() => Tr31VersionBWrap.Wrap(reference.Header,
            Convert.FromHexString(reference.Kbpk), new byte[length]));
    }

    [Test]
    public void RejectsNullInputs()
    {
        var reference = Tr31ReferenceVectors.All[0];
        byte[] kbpk = Convert.FromHexString(reference.Kbpk);
        byte[] data = Convert.FromHexString(reference.ConfidentialData);
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => Tr31VersionBWrap.Wrap(null!, kbpk, data));
            Assert.Throws<ArgumentNullException>(() => Tr31VersionBWrap.Wrap(reference.Header, null!, data));
            Assert.Throws<ArgumentNullException>(() => Tr31VersionBWrap.Wrap(reference.Header, kbpk, null!));
        });
    }
}
