using CryptoScript.CryptoAlgorithm;
using FluentAssertions;

namespace CryptoScriptUnitTest;

public class Iso9797M1PaddingTests
{
    // Fixed byte layouts derived directly from ISO/IEC 9797-1:2011, 6.3.2.
    // No production or test helper generates the expected values.
    [TestCase(8, "", "0000000000000000")]
    [TestCase(8, "01", "0100000000000000")]
    [TestCase(8, "01020304050607", "0102030405060700")]
    [TestCase(8, "0102030405060708", "0102030405060708")]
    [TestCase(8, "010203040506070809", "01020304050607080900000000000000")]
    [TestCase(16, "", "00000000000000000000000000000000")]
    [TestCase(16, "01", "01000000000000000000000000000000")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F", "0102030405060708090A0B0C0D0E0F00")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F10", "0102030405060708090A0B0C0D0E0F10")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F1011", "0102030405060708090A0B0C0D0E0F1011000000000000000000000000000000")]
    public void Pad_MatchesIso9797Method1ByteLayout(
        int blockSize, string inputHex, string expectedHex)
    {
        var subject = new Iso9797M1Padding(blockSize);

        subject.Pad(Convert.FromHexString(inputHex))
            .Should().Equal(Convert.FromHexString(expectedHex));
    }
}
