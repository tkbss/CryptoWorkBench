using CryptoScript.CryptoAlgorithm;
using FluentAssertions;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

public class Iso9797M3PaddingTests
{
    // Fixed byte layouts derived directly from ISO/IEC 9797-1:2011, 6.3.4.
    // No production or test helper generates the expected values.
    [TestCase(8, "", "00000000000000000000000000000000")]
    [TestCase(8, "01", "00000000000000080100000000000000")]
    [TestCase(8, "01020304050607", "00000000000000380102030405060700")]
    [TestCase(8, "0102030405060708", "00000000000000400102030405060708")]
    [TestCase(8, "010203040506070809", "000000000000004801020304050607080900000000000000")]
    [TestCase(16, "", "0000000000000000000000000000000000000000000000000000000000000000")]
    [TestCase(16, "01", "0000000000000000000000000000000801000000000000000000000000000000")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F", "000000000000000000000000000000780102030405060708090A0B0C0D0E0F00")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F10", "000000000000000000000000000000800102030405060708090A0B0C0D0E0F10")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F1011", "000000000000000000000000000000880102030405060708090A0B0C0D0E0F1011000000000000000000000000000000")]
    public void Pad_MatchesIso9797Method3ByteLayout(
        int blockSize, string inputHex, string expectedHex)
    {
        var subject = new Iso9797M3Padding(blockSize);

        subject.Pad(Convert.FromHexString(inputHex))
            .Should().Equal(Convert.FromHexString(expectedHex));
    }

    [TestCase(8, "")]
    [TestCase(8, "01")]
    [TestCase(8, "01020304050607")]
    [TestCase(8, "0102030405060708")]
    [TestCase(8, "010203040506070809")]
    [TestCase(16, "")]
    [TestCase(16, "01")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F10")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F1011")]
    public void Unpad_OfPaddedData_ReturnsOriginalData(int blockSize, string inputHex)
    {
        var subject = new Iso9797M3Padding(blockSize);
        byte[] input = Convert.FromHexString(inputHex);

        subject.Unpad(subject.Pad(input)).Should().Equal(input);
    }

    [TestCase(8, "")]
    [TestCase(8, "0000000000000000")]
    [TestCase(8, "0000000000000000000000000000000000")]
    [TestCase(8, "00000000000000010000000000000000")]
    [TestCase(8, "00000000000000480000000000000000")]
    [TestCase(8, "00000000000000080100000000000001")]
    [TestCase(8, "000000000000000801000000000000000000000000000000")]
    [TestCase(16, "00000000000000000000000000000000")]
    [TestCase(16, "0100000000000000000000000000000000000000000000000000000000000000")]
    public void Unpad_RejectsInvalidOrNonCanonicalEncoding(int blockSize, string inputHex)
    {
        var subject = new Iso9797M3Padding(blockSize);

        Action act = () => subject.Unpad(Convert.FromHexString(inputHex));

        act.Should().Throw<CryptographicException>();
    }

    [TestCase(8, "00112233445566000000000000000038")]
    [TestCase(8, "01020304050607080000000000000040")]
    [TestCase(16, "01000000000000000000000000000008")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F00000000000000000000000000000078")]
    public void Unpad_RejectsLegacyCryptoScriptSuffixFormat(int blockSize, string legacyHex)
    {
        var subject = new Iso9797M3Padding(blockSize);

        Action act = () => subject.Unpad(Convert.FromHexString(legacyHex));

        act.Should().Throw<CryptographicException>();
    }
}
