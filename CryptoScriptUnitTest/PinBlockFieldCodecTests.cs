using CryptoScript.CryptoAlgorithm.PINBLOCK;

namespace CryptoScriptUnitTest;

public class PinBlockFieldCodecTests
{
    private const string Pin = "1234";
    private const string Pan = "1234567890123456";
    private const string TransactionField = "0123456789";
    private const string FillField = "ABCDEFABCD";

    [Test]
    public void Format0_MatchesReferenceVector()
    {
        byte[] encoded = PinBlockFieldCodec.EncodeFormat0(Pin, Pan);

        Assert.Multiple(() =>
        {
            Assert.That(encoded, Is.EqualTo(Convert.FromHexString("0412719876FEDCBA")));
            Assert.That(Xor(encoded, Convert.FromHexString("0000456789012345")),
                Is.EqualTo(Convert.FromHexString("041234FFFFFFFFFF")));
            Assert.That(Xor(encoded, Convert.FromHexString("041234FFFFFFFFFF")),
                Is.EqualTo(Convert.FromHexString("0000456789012345")));
        });
    }

    [Test]
    public void Format1_MatchesReferenceVector()
    {
        byte[] encoded = PinBlockFieldCodec.EncodeFormat1(Pin, TransactionField);

        Assert.That(encoded, Is.EqualTo(Convert.FromHexString("1412340123456789")));
    }

    [Test]
    public void Format2_MatchesReferenceVector()
    {
        byte[] encoded = PinBlockFieldCodec.EncodeFormat2(Pin);

        Assert.That(encoded, Is.EqualTo(Convert.FromHexString("241234FFFFFFFFFF")));
    }

    [Test]
    public void Format3_MatchesReferenceVector()
    {
        byte[] encoded = PinBlockFieldCodec.EncodeFormat3(Pin, Pan, FillField);

        Assert.Multiple(() =>
        {
            Assert.That(encoded, Is.EqualTo(Convert.FromHexString("341271CC44EE8888")));
            Assert.That(Xor(encoded, Convert.FromHexString("0000456789012345")),
                Is.EqualTo(Convert.FromHexString("341234ABCDEFABCD")));
        });
    }

    [TestCase("1234")]
    [TestCase("12345")]
    [TestCase("123456789012")]
    [TestCase("00001234")]
    public void AllFormats_RoundTripBoundaryAndLeadingZeroPins(string pin)
    {
        string variableField = new('A', 14 - pin.Length);

        DecodedPinBlockFields format0 = PinBlockFieldCodec.DecodeFormat0(
            PinBlockFieldCodec.EncodeFormat0(pin, Pan), Pan);
        DecodedPinBlockFields format1 = PinBlockFieldCodec.DecodeFormat1(
            PinBlockFieldCodec.EncodeFormat1(pin, variableField));
        DecodedPinBlockFields format2 = PinBlockFieldCodec.DecodeFormat2(
            PinBlockFieldCodec.EncodeFormat2(pin));
        DecodedPinBlockFields format3 = PinBlockFieldCodec.DecodeFormat3(
            PinBlockFieldCodec.EncodeFormat3(pin, Pan, variableField), Pan);

        Assert.Multiple(() =>
        {
            Assert.That(format0.Pin, Is.EqualTo(pin));
            Assert.That(format0.TransactionField, Is.Null);
            Assert.That(format0.FillField, Is.Null);
            Assert.That(format1.Pin, Is.EqualTo(pin));
            Assert.That(format1.TransactionField, Is.EqualTo(variableField));
            Assert.That(format1.FillField, Is.Null);
            Assert.That(format2.Pin, Is.EqualTo(pin));
            Assert.That(format2.TransactionField, Is.Null);
            Assert.That(format2.FillField, Is.Null);
            Assert.That(format3.Pin, Is.EqualTo(pin));
            Assert.That(format3.TransactionField, Is.Null);
            Assert.That(format3.FillField, Is.EqualTo(variableField));
        });
    }

    [TestCase("12345678")]
    [TestCase("123456789012")]
    [TestCase("1234567890123")]
    [TestCase("1234567890123456")]
    [TestCase("1234567890123456789")]
    public void PanLengthsFromEightThroughNineteen_RoundTripFormats0And3(string pan)
    {
        byte[] format0 = PinBlockFieldCodec.EncodeFormat0(Pin, pan);
        byte[] format3 = PinBlockFieldCodec.EncodeFormat3(Pin, pan, FillField);

        Assert.Multiple(() =>
        {
            Assert.That(PinBlockFieldCodec.DecodeFormat0(format0, pan).Pin, Is.EqualTo(Pin));
            Assert.That(PinBlockFieldCodec.DecodeFormat3(format3, pan).Pin, Is.EqualTo(Pin));
        });
    }

    [Test]
    public void ShortPanComponent_IsLeftPaddedWithZeros()
    {
        const string shortPan = "12345678";
        byte[] encoded = PinBlockFieldCodec.EncodeFormat0(Pin, shortPan);
        byte[] pinField = Convert.FromHexString("041234FFFFFFFFFF");

        Assert.That(Xor(encoded, pinField), Is.EqualTo(Convert.FromHexString("0000000001234567")));
    }

    [TestCase("1234", "0123456789")]
    [TestCase("123456789012", "AF")]
    [TestCase("1234", "ABCDEF0123")]
    [TestCase("1234", "abcdef0123")]
    public void Format1_PreservesValidTransactionNibbles(string pin, string transactionField)
    {
        DecodedPinBlockFields decoded = PinBlockFieldCodec.DecodeFormat1(
            PinBlockFieldCodec.EncodeFormat1(pin, transactionField));

        Assert.That(decoded.TransactionField, Is.EqualTo(transactionField.ToUpperInvariant()));
    }

    [TestCase("1234", "ABCDEFABCD")]
    [TestCase("123456789012", "AF")]
    [TestCase("1234", "abcdefabcd")]
    public void Format3_PreservesValidFillNibbles(string pin, string fillField)
    {
        DecodedPinBlockFields decoded = PinBlockFieldCodec.DecodeFormat3(
            PinBlockFieldCodec.EncodeFormat3(pin, Pan, fillField), Pan);

        Assert.That(decoded.FillField, Is.EqualTo(fillField.ToUpperInvariant()));
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(13)]
    public void Encode_RejectsInvalidPinLength(int length)
    {
        string pin = new('1', length);

        Assert.That(() => PinBlockFieldCodec.EncodeFormat2(pin), Throws.ArgumentException);
    }

    [Test]
    public void Encode_RejectsNonDecimalPinWithoutDisclosingIt()
    {
        const string invalidPin = "12A4";

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            PinBlockFieldCodec.EncodeFormat2(invalidPin))!;

        Assert.That(error.Message, Does.Not.Contain(invalidPin));
    }

    [TestCase("1234567")]
    [TestCase("12345678901234567890")]
    [TestCase("1234567A90123456")]
    public void Formats0And3_RejectInvalidPanWithoutDisclosingIt(string pan)
    {
        ArgumentException format0 = Assert.Throws<ArgumentException>(() =>
            PinBlockFieldCodec.EncodeFormat0(Pin, pan))!;
        ArgumentException format3 = Assert.Throws<ArgumentException>(() =>
            PinBlockFieldCodec.EncodeFormat3(Pin, pan, FillField))!;

        Assert.Multiple(() =>
        {
            Assert.That(format0.Message, Does.Not.Contain(pan));
            Assert.That(format3.Message, Does.Not.Contain(pan));
        });
    }

    [TestCase("012345678")]
    [TestCase("01234567890")]
    [TestCase("012345678G")]
    public void Format1_RejectsInvalidTransactionField(string transactionField)
    {
        Assert.That(() => PinBlockFieldCodec.EncodeFormat1(Pin, transactionField),
            Throws.ArgumentException);
    }

    [TestCase("ABCDEFABC")]
    [TestCase("ABCDEFABCDE")]
    [TestCase("ABCDEFABC9")]
    [TestCase("ABCDEFABCG")]
    public void Format3_RejectsInvalidFillField(string fillField)
    {
        Assert.That(() => PinBlockFieldCodec.EncodeFormat3(Pin, Pan, fillField),
            Throws.ArgumentException);
    }

    [TestCase(7)]
    [TestCase(9)]
    public void Decode_RejectsWrongBlockLength(int length)
    {
        byte[] block = new byte[length];

        Assert.Multiple(() =>
        {
            Assert.That(() => PinBlockFieldCodec.DecodeFormat0(block, Pan), Throws.ArgumentException);
            Assert.That(() => PinBlockFieldCodec.DecodeFormat1(block), Throws.ArgumentException);
            Assert.That(() => PinBlockFieldCodec.DecodeFormat2(block), Throws.ArgumentException);
            Assert.That(() => PinBlockFieldCodec.DecodeFormat3(block, Pan), Throws.ArgumentException);
        });
    }

    [Test]
    public void Decode_RejectsUnexpectedFormatIdentifiers()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => PinBlockFieldCodec.DecodeFormat0(
                PinBlockFieldCodec.EncodeFormat3(Pin, Pan, FillField), Pan), Throws.TypeOf<FormatException>());
            Assert.That(() => PinBlockFieldCodec.DecodeFormat1(
                PinBlockFieldCodec.EncodeFormat2(Pin)), Throws.TypeOf<FormatException>());
            Assert.That(() => PinBlockFieldCodec.DecodeFormat2(
                PinBlockFieldCodec.EncodeFormat1(Pin, TransactionField)), Throws.TypeOf<FormatException>());
            Assert.That(() => PinBlockFieldCodec.DecodeFormat3(
                PinBlockFieldCodec.EncodeFormat0(Pin, Pan), Pan), Throws.TypeOf<FormatException>());
        });
    }

    [TestCase("231234FFFFFFFFFF")]
    [TestCase("2D1234FFFFFFFFFF")]
    public void Decode_RejectsInvalidPinLength(string block)
    {
        Assert.That(() => PinBlockFieldCodec.DecodeFormat2(Convert.FromHexString(block)),
            Throws.TypeOf<FormatException>());
    }

    [Test]
    public void Decode_RejectsNonDecimalPinNibble()
    {
        byte[] block = Convert.FromHexString("241A34FFFFFFFFFF");

        Assert.That(() => PinBlockFieldCodec.DecodeFormat2(block), Throws.TypeOf<FormatException>());
    }

    [Test]
    public void Formats0And2_RejectInvalidFixedFillNibble()
    {
        byte[] invalidPlainField = Convert.FromHexString("041234FFFFFFFFFE");
        byte[] panField = Convert.FromHexString("0000456789012345");

        Assert.Multiple(() =>
        {
            Assert.That(() => PinBlockFieldCodec.DecodeFormat0(Xor(invalidPlainField, panField), Pan),
                Throws.TypeOf<FormatException>());
            Assert.That(() => PinBlockFieldCodec.DecodeFormat2(
                Convert.FromHexString("241234FFFFFFFFFE")), Throws.TypeOf<FormatException>());
        });
    }

    [Test]
    public void Format3_RejectsFillNibbleBelowA()
    {
        byte[] invalidPlainField = Convert.FromHexString("341234ABCDEFABC9");
        byte[] panField = Convert.FromHexString("0000456789012345");

        Assert.That(() => PinBlockFieldCodec.DecodeFormat3(Xor(invalidPlainField, panField), Pan),
            Throws.TypeOf<FormatException>());
    }

    [Test]
    public void Decode_DoesNotMutateInputBlock()
    {
        byte[] block = PinBlockFieldCodec.EncodeFormat3(Pin, Pan, FillField);
        byte[] original = (byte[])block.Clone();

        PinBlockFieldCodec.DecodeFormat3(block, Pan);

        Assert.That(block, Is.EqualTo(original));
    }

    [Test]
    public void ValidationErrors_DoNotContainSensitiveInputsOrBlockContents()
    {
        const string invalidPin = "12A4";
        const string invalidPan = "1234567A90123456";
        byte[] invalidBlock = Convert.FromHexString("241A34FFFFFFFFFF");

        Exception pinError = Assert.Throws<ArgumentException>(() =>
            PinBlockFieldCodec.EncodeFormat2(invalidPin))!;
        Exception panError = Assert.Throws<ArgumentException>(() =>
            PinBlockFieldCodec.EncodeFormat0(Pin, invalidPan))!;
        Exception blockError = Assert.Throws<FormatException>(() =>
            PinBlockFieldCodec.DecodeFormat2(invalidBlock))!;

        Assert.Multiple(() =>
        {
            Assert.That(pinError.Message, Does.Not.Contain(invalidPin));
            Assert.That(panError.Message, Does.Not.Contain(invalidPan));
            Assert.That(blockError.Message, Does.Not.Contain(Convert.ToHexString(invalidBlock)));
        });
    }

    private static byte[] Xor(byte[] left, byte[] right)
    {
        byte[] result = new byte[left.Length];
        for (int index = 0; index < result.Length; index++)
            result[index] = (byte)(left[index] ^ right[index]);
        return result;
    }
}
