using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm.WRAPPERS;

namespace CryptoScriptUnitTest;

public class Tr31TdeaVariantUnwrapTests
{
    [Test]
    public void UnwrapsCorrectedCReference()
    {
        var reference = Tr31ReferenceVectors.All[2];
        byte[] key = Tr31TdeaVariantUnwrap.Unwrap(reference.Header,
            Convert.FromHexString(reference.Ciphertext), Convert.FromHexString(reference.Mac),
            Convert.FromHexString(reference.Kbpk));

        Assert.Multiple(() =>
        {
            Assert.That(reference.Origin, Is.EqualTo(Tr31VectorOrigin.CorrectedAnsiReferenceVector));
            Assert.That(key, Is.EqualTo(Convert.FromHexString(reference.Key)));
        });
    }

    [Test]
    public void UnwrapsDerivedCorrectedAReference()
    {
        // Derived/corrected A reference, not an unchanged ANSI vector.
        var reference = Tr31ReferenceVectors.All[3];
        byte[] key = Tr31TdeaVariantUnwrap.Unwrap(reference.Header,
            Convert.FromHexString(reference.Ciphertext), Convert.FromHexString(reference.Mac),
            Convert.FromHexString(reference.Kbpk));

        Assert.Multiple(() =>
        {
            Assert.That(reference.Origin, Is.EqualTo(Tr31VectorOrigin.DerivedReferenceVector));
            Assert.That(key, Is.EqualTo(Convert.FromHexString(reference.Key)));
        });
    }

    [TestCase("header")]
    [TestCase("ciphertext")]
    [TestCase("mac")]
    [TestCase("kbpk")]
    public void RejectsTamperingWithAuthenticationFailure(string field)
    {
        var reference = Tr31ReferenceVectors.All[2];
        string header = reference.Header;
        byte[] ciphertext = Convert.FromHexString(reference.Ciphertext);
        byte[] mac = Convert.FromHexString(reference.Mac);
        byte[] kbpk = Convert.FromHexString(reference.Kbpk);

        switch (field)
        {
            case "header": header = header[..9] + (header[9] == '0' ? "1" : "0") + header[10..]; break;
            case "ciphertext": ciphertext[0] ^= 1; break;
            case "mac": mac[0] ^= 1; break;
            case "kbpk": kbpk = Convert.FromHexString(Tr31ReferenceVectors.All[3].Kbpk); break;
        }

        var error = Assert.Throws<CryptographicException>(() =>
            Tr31TdeaVariantUnwrap.Unwrap(header, ciphertext, mac, kbpk));
        Assert.That(error!.Message, Is.EqualTo("TR-31 TDEA variant authentication failed."));
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(5)]
    public void RejectsIncorrectAuthenticationValueLength(int length)
    {
        var reference = Tr31ReferenceVectors.All[2];
        Assert.Throws<ArgumentException>(() => Tr31TdeaVariantUnwrap.Unwrap(reference.Header,
            Convert.FromHexString(reference.Ciphertext), new byte[length], Convert.FromHexString(reference.Kbpk)));
    }

    [TestCase(0)]
    [TestCase(7)]
    [TestCase(33)]
    public void RejectsInvalidCiphertextLength(int length)
    {
        var reference = Tr31ReferenceVectors.All[2];
        Assert.Throws<ArgumentException>(() => Tr31TdeaVariantUnwrap.Unwrap(reference.Header,
            new byte[length], Convert.FromHexString(reference.Mac), Convert.FromHexString(reference.Kbpk)));
    }

    [TestCase('B')]
    [TestCase('D')]
    public void RejectsNonVariantBindingVersions(char version)
    {
        var reference = Tr31ReferenceVectors.All[2];
        string header = version + reference.Header[1..];
        Assert.Throws<NotSupportedException>(() => Tr31TdeaVariantUnwrap.Unwrap(header,
            Convert.FromHexString(reference.Ciphertext), Convert.FromHexString(reference.Mac),
            Convert.FromHexString(reference.Kbpk)));
    }

    [Test]
    public void TripleLengthKbpkRoundtripExtractsOnlyKeyBytes()
    {
        // Synthetic roundtrip, not an ANSI vector.
        const string header = "C0096D0TB00E0000";
        byte[] kbpk = Convert.FromHexString("0123456789ABCDEFFEDCBA98765432100011223344556677");
        byte[] key = Enumerable.Range(32, 16).Select(i => (byte)i).ToArray();
        byte[] padding = Enumerable.Repeat((byte)0xA5, 14).ToArray();
        byte[] confidentialData = Tr31ConfidentialData.Create(key, 8, 8, padding).ToArray();
        var wrapped = Tr31TdeaVariantWrap.Wrap(header, kbpk, confidentialData);

        Assert.That(Tr31TdeaVariantUnwrap.Unwrap(header, wrapped.Ciphertext, wrapped.Mac, kbpk), Is.EqualTo(key));
    }

    [Test]
    public void AuthenticationPrecedesDecryptionAndExtraction()
    {
        var reference = Tr31ReferenceVectors.All[2];
        byte[] kbpk = Convert.FromHexString(reference.Kbpk);
        byte[] malformed = Convert.FromHexString(reference.ConfidentialData);
        malformed[0] = 0xFF;
        malformed[1] = 0xF8;
        var wrapped = Tr31TdeaVariantWrap.Wrap(reference.Header, kbpk, malformed);

        Assert.Throws<ArgumentException>(() => Tr31TdeaVariantUnwrap.Unwrap(
            reference.Header, wrapped.Ciphertext, wrapped.Mac, kbpk));
        wrapped.Mac[0] ^= 1;
        Assert.Throws<CryptographicException>(() => Tr31TdeaVariantUnwrap.Unwrap(
            reference.Header, wrapped.Ciphertext, wrapped.Mac, kbpk));
    }

    [TestCase("")]
    [TestCase("A123456")]
    public void RejectsHeaderShorterThanEightBytes(string header)
    {
        var reference = Tr31ReferenceVectors.All[2];
        Assert.Throws<ArgumentException>(() => Tr31TdeaVariantUnwrap.Unwrap(header,
            Convert.FromHexString(reference.Ciphertext), Convert.FromHexString(reference.Mac),
            Convert.FromHexString(reference.Kbpk)));
    }
}
