namespace CryptoScriptUnitTest;

// Only guards against transcription errors in frozen data. Does not exercise a TR31 implementation.
public class Tr31ReferenceDataTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void FrozenReferenceComponentsAreInternallyConsistent(int index)
    {
        var vector = Tr31ReferenceVectors.All[index];
        byte[] key = Convert.FromHexString(vector.Key);
        byte[] confidential = Convert.FromHexString(vector.ConfidentialData);
        Assert.Multiple(() =>
        {
            Assert.That(vector.CompleteBlock, Is.EqualTo(vector.Header + vector.Ciphertext + vector.Mac));
            Assert.That(vector.CompleteBlock.Length, Is.EqualTo(int.Parse(vector.Header.Substring(1, 4))));
            Assert.That(vector.ConfidentialData, Is.EqualTo((key.Length * 8).ToString("X4") +
                vector.Key + vector.ObfuscationPadding + vector.CipherBlockPadding));
            Assert.That(confidential, Has.Length.EqualTo(32));
            Assert.That(Convert.FromHexString(vector.Ciphertext), Has.Length.EqualTo(confidential.Length));
            Assert.That(Convert.FromHexString(vector.Kbek), Has.Length.EqualTo(Convert.FromHexString(vector.Kbpk).Length));
            Assert.That(Convert.FromHexString(vector.Kbak), Has.Length.EqualTo(Convert.FromHexString(vector.Kbpk).Length));
            Assert.That(Convert.FromHexString(vector.Mac), Has.Length.EqualTo(vector.Header[0] == 'B' ? 8 : 4));
            Assert.That(vector.Origin, Is.EqualTo(index < 2 ? Tr31VectorOrigin.AnsiVector :
                index == 2 ? Tr31VectorOrigin.CorrectedAnsiReferenceVector : Tr31VectorOrigin.DerivedReferenceVector));
        });
    }
}
