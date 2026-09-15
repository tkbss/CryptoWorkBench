using CryptoScript.CryptoAlgorithm.WRAPPERS;

namespace CryptoScriptUnitTest;

public class Tr31ConfidentialDataTests
{
    [TestCase(0)]
    [TestCase(1)]
    public void BuildsAndExtractsBothBReferencePayloads(int index)
    {
        var reference = Tr31ReferenceVectors.All[index];
        byte[] key = Convert.FromHexString(reference.Key);
        var data = Tr31ConfidentialData.Create(key, 8, 24 - key.Length,
            Convert.FromHexString(reference.ObfuscationPadding + reference.CipherBlockPadding));

        Assert.Multiple(() =>
        {
            Assert.That(data.KeyLength, Is.EqualTo(new byte[] { 0, 0x80 }));
            Assert.That(data.Key, Is.EqualTo(key));
            Assert.That(data.ObfuscationPadding, Is.EqualTo(Convert.FromHexString(reference.ObfuscationPadding)));
            Assert.That(data.ObfuscationPadding, Has.Length.EqualTo(8));
            Assert.That(data.CipherBlockPadding, Is.EqualTo(Convert.FromHexString(reference.CipherBlockPadding)));
            Assert.That(data.CipherBlockPadding, Has.Length.EqualTo(6));
            Assert.That(data.ToArray(), Has.Length.EqualTo(32));
            Assert.That(data.ToArray(), Is.EqualTo(Convert.FromHexString(reference.ConfidentialData)));
            Assert.That(Tr31ConfidentialData.ExtractKey(data.ToArray()), Is.EqualTo(key));
        });
    }

    [Test]
    public void TripleLengthTdeaNeedsOnlySixBlockPaddingBytes()
    {
        // Structural example, not a published ANSI vector.
        byte[] key = Convert.FromHexString("0123456789ABCDEFFEDCBA98765432100011223344556677");
        byte[] padding = Convert.FromHexString("A0A1A2A3A4A5");
        var data = Tr31ConfidentialData.Create(key, 8, 0, padding);
        Assert.Multiple(() =>
        {
            Assert.That(data.KeyLength, Is.EqualTo(new byte[] { 0, 0xC0 }));
            Assert.That(data.ObfuscationPadding, Is.Empty);
            Assert.That(data.CipherBlockPadding, Is.EqualTo(padding));
            Assert.That(data.ToArray(), Has.Length.EqualTo(32));
            Assert.That(data.ToArray(), Is.EqualTo(Convert.FromHexString(
                "00C00123456789ABCDEFFEDCBA98765432100011223344556677A0A1A2A3A4A5")));
            Assert.That(Tr31ConfidentialData.ExtractKey(data.ToArray()), Is.EqualTo(key));
        });
    }

    [TestCase(null)]
    [TestCase("AA")]
    public void RandomFallbackPreservesPaddingRegions(string? supplied)
    {
        byte[] key = Convert.FromHexString(Tr31ReferenceVectors.All[0].Key);
        var data = Tr31ConfidentialData.Create(key, 8, 8,
            supplied == null ? null : Convert.FromHexString(supplied));
        Assert.Multiple(() =>
        {
            Assert.That(data.ObfuscationPadding, Has.Length.EqualTo(8));
            Assert.That(data.CipherBlockPadding, Has.Length.EqualTo(6));
            Assert.That(data.ToArray(), Has.Length.EqualTo(32));
            Assert.That(Tr31ConfidentialData.ExtractKey(data.ToArray()), Is.EqualTo(key));
        });
    }

    [Test]
    public void AlreadyAlignedDataDoesNotAddAnotherPaddingBlock()
    {
        var data = Tr31ConfidentialData.Create(new byte[14], 8, 0, []);
        Assert.That(data.CipherBlockPadding, Is.Empty);
        Assert.That(data.ToArray(), Has.Length.EqualTo(16));
    }

    [TestCase("00")]
    [TestCase("00800102")]
    [TestCase("0009FFFF")]
    public void ExtractionRejectsMissingTruncatedOrNonByteAlignedKey(string encoded)
    {
        // Only the new structural extractor; legacy D unwrap intentionally does not use it.
        Assert.Throws<ArgumentException>(() => Tr31ConfidentialData.ExtractKey(Convert.FromHexString(encoded)));
    }
}
