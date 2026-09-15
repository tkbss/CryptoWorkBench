using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.CryptoAlgorithm.WRAPPERS;

namespace CryptoScriptUnitTest;

public class Tr31VersionBUnwrapTests
{
    [TestCase(0)]
    [TestCase(1)]
    public void UnwrapsPublishedVectorAndRecoversExactConfidentialData(int index)
    {
        var v = Tr31ReferenceVectors.All[index];
        byte[] cipher = Convert.FromHexString(v.Ciphertext);
        byte[] mac = Convert.FromHexString(v.Mac);
        Assert.That(DES3_CBC.DecryptNoPadding(Convert.FromHexString(v.Kbek), mac, cipher),
            Is.EqualTo(Convert.FromHexString(v.ConfidentialData)));
        Assert.That(Tr31VersionBUnwrap.Unwrap(v.Header, cipher, mac, Convert.FromHexString(v.Kbpk)),
            Is.EqualTo(Convert.FromHexString(v.Key)));
    }

    [TestCase(0, "header")]
    [TestCase(0, "cipher")]
    [TestCase(0, "mac")]
    [TestCase(0, "kbpk")]
    [TestCase(1, "header")]
    [TestCase(1, "cipher")]
    [TestCase(1, "mac")]
    [TestCase(1, "kbpk")]
    [TestCase(1, "optional")]
    public void RejectsTamperingWithAuthenticationFailure(int index, string field)
    {
        var v = Tr31ReferenceVectors.All[index];
        string header = v.Header;
        byte[] cipher = Convert.FromHexString(v.Ciphertext);
        byte[] mac = Convert.FromHexString(v.Mac);
        byte[] kbpk = Convert.FromHexString(v.Kbpk);
        switch (field)
        {
            case "header": header = header[..9] + (header[9] == '0' ? "1" : "0") + header[10..]; break;
            case "optional": header = header[..^1] + "1"; break;
            case "cipher": cipher[0] ^= 1; break;
            case "mac": mac[0] ^= 1; break;
            case "kbpk":
                kbpk = Convert.FromHexString(Tr31ReferenceVectors.All[1 - index].Kbpk);
                Assert.DoesNotThrow(() => Tr31TdeaKeyDerivation.DeriveKeys(kbpk));
                break;
        }
        var error = Assert.Throws<CryptographicException>(() => Tr31VersionBUnwrap.Unwrap(header, cipher, mac, kbpk));
        Assert.That(error!.Message, Is.EqualTo("TR-31 version B authentication failed."));
    }

    [TestCase(16, 16)]
    [TestCase(24, 24)]
    public void SyntheticRoundtripExtractsOnlyKeyBytes(int keyBytes, int kbpkBytes)
    {
        // Synthetic roundtrip, not a published ANSI vector.
        byte[] key = Enumerable.Range(32, keyBytes).Select(i => (byte)i).ToArray();
        byte[] kbpk = Convert.FromHexString("0123456789ABCDEFFEDCBA98765432100011223344556677")[..kbpkBytes];
        byte[] padding = Enumerable.Repeat((byte)0xA5, 24 - keyBytes + 6).ToArray();
        byte[] data = Tr31ConfidentialData.Create(key, 8, 24 - keyBytes, padding).ToArray();
        const string header = "B0096D0TB00E0000";
        var wrapped = Tr31VersionBWrap.Wrap(header, kbpk, data);
        Assert.That(Tr31VersionBUnwrap.Unwrap(header, wrapped.Ciphertext, wrapped.Mac, kbpk), Is.EqualTo(key));
    }

    [Test]
    public void AuthenticationPrecedesExtractionAndAuthenticatedMalformedLengthIsRejected()
    {
        var v = Tr31ReferenceVectors.All[0];
        byte[] kbpk = Convert.FromHexString(v.Kbpk);
        byte[] malformed = Convert.FromHexString(v.ConfidentialData);
        malformed[0] = 0xFF;
        malformed[1] = 0xF8; // Byte-aligned encoded length exceeds the available data.
        var wrapped = Tr31VersionBWrap.Wrap(v.Header, kbpk, malformed);
        Assert.Throws<ArgumentException>(() => Tr31VersionBUnwrap.Unwrap(v.Header, wrapped.Ciphertext, wrapped.Mac, kbpk));
        // Same malformed plaintext but altered authenticated header: MAC failure must win.
        Assert.Throws<CryptographicException>(() => Tr31VersionBUnwrap.Unwrap(v.Header + "X", wrapped.Ciphertext, wrapped.Mac, kbpk));
    }

    [TestCase("header")]
    [TestCase("cipher")]
    [TestCase("mac")]
    [TestCase("kbpk")]
    public void RejectsNullInputs(string field)
    {
        var v = Tr31ReferenceVectors.All[0];
        Assert.Throws<ArgumentNullException>(() => Tr31VersionBUnwrap.Unwrap(
            field == "header" ? null! : v.Header,
            field == "cipher" ? null! : Convert.FromHexString(v.Ciphertext),
            field == "mac" ? null! : Convert.FromHexString(v.Mac),
            field == "kbpk" ? null! : Convert.FromHexString(v.Kbpk)));
    }

    [TestCase("kbpk", 0)]
    [TestCase("kbpk", 15)]
    [TestCase("kbpk", 17)]
    [TestCase("kbpk", 25)]
    [TestCase("cipher", 0)]
    [TestCase("cipher", 7)]
    [TestCase("cipher", 33)]
    [TestCase("mac", 0)]
    [TestCase("mac", 7)]
    [TestCase("mac", 9)]
    public void RejectsInvalidLengths(string field, int length)
    {
        var v = Tr31ReferenceVectors.All[0];
        Assert.Throws<ArgumentException>(() => Tr31VersionBUnwrap.Unwrap(v.Header,
            field == "cipher" ? new byte[length] : Convert.FromHexString(v.Ciphertext),
            field == "mac" ? new byte[length] : Convert.FromHexString(v.Mac),
            field == "kbpk" ? new byte[length] : Convert.FromHexString(v.Kbpk)));
    }
}
