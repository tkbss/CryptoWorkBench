using CryptoScript.CryptoAlgorithm.PINBLOCK;

namespace CryptoScriptUnitTest;

public class PinBlockFormat4FieldCodecTests
{
    private const string RandomField = "0123456789ABCDEF";

    [Test]
    public void PinField_MatchesSyntheticByteVector()
    {
        byte[] encoded = PinBlockFormat4FieldCodec.EncodePinField("1234", RandomField);

        Assert.That(encoded,
            Is.EqualTo(Convert.FromHexString("441234AAAAAAAAAA0123456789ABCDEF")));
    }

    [TestCase("1234")]
    [TestCase("01234")]
    [TestCase("012345678901")]
    [TestCase("00001234")]
    public void PinField_RoundTripPreservesPinAndRandomField(string pin)
    {
        byte[] encoded = PinBlockFormat4FieldCodec.EncodePinField(pin, RandomField);
        DecodedPinBlockFormat4Fields decoded = PinBlockFormat4FieldCodec.DecodePinField(encoded);

        Assert.Multiple(() =>
        {
            Assert.That(encoded, Has.Length.EqualTo(16));
            Assert.That(decoded.Pin, Is.EqualTo(pin));
            Assert.That(decoded.RandomField, Is.EqualTo(RandomField));
        });
    }

    [Test]
    public void PinField_PreservesLeadingZeroAndLowercaseRandomNibbles()
    {
        DecodedPinBlockFormat4Fields decoded = PinBlockFormat4FieldCodec.DecodePinField(
            PinBlockFormat4FieldCodec.EncodePinField("01234", "00abcdef01234567"));

        Assert.Multiple(() =>
        {
            Assert.That(decoded.Pin, Is.EqualTo("01234"));
            Assert.That(decoded.RandomField, Is.EqualTo("00ABCDEF01234567"));
        });
    }

    [TestCase("123")]
    [TestCase("1234567890123")]
    [TestCase("12A4")]
    public void PinField_RejectsInvalidPin(string pin)
    {
        Assert.That(() => PinBlockFormat4FieldCodec.EncodePinField(pin, RandomField),
            Throws.ArgumentException);
    }

    [TestCase(15)]
    [TestCase(17)]
    public void PinField_RejectsInvalidRandomLength(int length)
    {
        Assert.That(() => PinBlockFormat4FieldCodec.EncodePinField("1234", new string('0', length)),
            Throws.ArgumentException);
    }

    [Test]
    public void PinField_RejectsNonHexRandomNibble()
    {
        Assert.That(() => PinBlockFormat4FieldCodec.EncodePinField("1234", "0123456789ABCDEG"),
            Throws.ArgumentException);
    }

    [TestCase("341234AAAAAAAAAA0123456789ABCDEF")]
    [TestCase("431234AAAAAAAAAA0123456789ABCDEF")]
    [TestCase("4D1234AAAAAAAAAA0123456789ABCDEF")]
    [TestCase("441A34AAAAAAAAAA0123456789ABCDEF")]
    [TestCase("441234AAAAAAAABA0123456789ABCDEF")]
    public void PinField_DecodeRejectsMalformedFields(string field)
    {
        Assert.That(() => PinBlockFormat4FieldCodec.DecodePinField(Convert.FromHexString(field)),
            Throws.TypeOf<FormatException>());
    }

    [TestCase(15)]
    [TestCase(17)]
    public void PinField_DecodeRejectsWrongByteLength(int length)
    {
        Assert.That(() => PinBlockFormat4FieldCodec.DecodePinField(new byte[length]),
            Throws.ArgumentException);
    }

    [Test]
    public void PanField_MatchesSyntheticByteVector()
    {
        byte[] encoded = PinBlockFormat4FieldCodec.EncodePanField("1234567890123456");

        Assert.That(encoded,
            Is.EqualTo(Convert.FromHexString("41234567890123456000000000000000")));
    }

    [TestCase("1234567890", "00012345678900000000000000000000")]
    [TestCase("123456789012", "01234567890120000000000000000000")]
    [TestCase("1234567890123", "11234567890123000000000000000000")]
    [TestCase("1234567890123456789", "71234567890123456789000000000000")]
    public void PanField_EncodesLengthAlignmentAndPadding(string pan, string expected)
    {
        byte[] encoded = PinBlockFormat4FieldCodec.EncodePanField(pan);

        Assert.Multiple(() =>
        {
            Assert.That(encoded, Has.Length.EqualTo(16));
            Assert.That(encoded, Is.EqualTo(Convert.FromHexString(expected)));
            Assert.That(() => PinBlockFormat4FieldCodec.ValidatePanField(encoded), Throws.Nothing);
        });
    }

    [TestCase("123456789")]
    [TestCase("12345678901234567890")]
    [TestCase("123456789A")]
    public void PanField_RejectsInvalidPan(string pan)
    {
        Assert.That(() => PinBlockFormat4FieldCodec.EncodePanField(pan), Throws.ArgumentException);
    }

    [TestCase("81234567890123456789000000000000")]
    [TestCase("0123456789012A000000000000000000")]
    [TestCase("01234567890120000000000000000001")]
    public void PanField_ValidationRejectsMalformedFields(string field)
    {
        Assert.That(() => PinBlockFormat4FieldCodec.ValidatePanField(Convert.FromHexString(field)),
            Throws.TypeOf<FormatException>());
    }

    [TestCase(15)]
    [TestCase(17)]
    public void PanField_ValidationRejectsWrongByteLength(int length)
    {
        Assert.That(() => PinBlockFormat4FieldCodec.ValidatePanField(new byte[length]),
            Throws.ArgumentException);
    }
}
