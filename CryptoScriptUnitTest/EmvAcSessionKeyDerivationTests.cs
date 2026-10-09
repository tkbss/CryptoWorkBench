using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

public class EmvAcSessionKeyDerivationTests
{
    private const string TdeaMasterKey = "4F5276A285D36175DC4F516BBF80CB1A";
    private const string Aes128MasterKey = "2EF6E07ECBA86BCF3C3CFF7BBEBE6F38";
    private const string Aes192MasterKey = "7481B0525D05D393D6DCBADD333C6BC514087C6BF5FA10C4";
    private const string Aes256MasterKey = "14D63F23982740AC65B482BAF5913092D8132BAA4143A24D3CF437232711A507";

    [Test]
    public void Tdea128EmvCoKnownAnswerVectorMatches() =>
        AssertVector(KeyAlgorithm.Tdea, TdeaMasterKey, "5B10C70AFEE94975A345C69888048FF9");

    [Test]
    public void Aes128EmvCoKnownAnswerVectorMatches() =>
        AssertVector(KeyAlgorithm.Aes, Aes128MasterKey, "89F7B697A028A93345BE7A409665B9A4");

    [Test]
    public void Aes192EmvCoKnownAnswerVectorMatches() =>
        AssertVector(
            KeyAlgorithm.Aes,
            Aes192MasterKey,
            "78429FD2061D24B1F8830B2C91D3ED95ED9D4B069A7C2707");

    [Test]
    public void Aes256EmvCoKnownAnswerVectorMatches() =>
        AssertVector(
            KeyAlgorithm.Aes,
            Aes256MasterKey,
            "A99C5840A0CBFBF093DEA740FFFFF5A241BFE9472968CC6E0B9EC76BA280CE0F");

    [TestCase(KeyAlgorithm.Tdea, TdeaMasterKey, 16)]
    [TestCase(KeyAlgorithm.Aes, Aes128MasterKey, 16)]
    [TestCase(KeyAlgorithm.Aes, Aes192MasterKey, 24)]
    [TestCase(KeyAlgorithm.Aes, Aes256MasterKey, 32)]
    public void BoundaryAtcValuesAreAccepted(
        KeyAlgorithm algorithm,
        string masterKey,
        int expectedLength)
    {
        byte[] key = Convert.FromHexString(masterKey);
        byte[] lowerBoundary = EmvAcSessionKeyDerivation.Derive(
            algorithm, key, Convert.FromHexString("0000"));
        byte[] upperBoundary = EmvAcSessionKeyDerivation.Derive(
            algorithm, key, Convert.FromHexString("FFFF"));

        lowerBoundary.Should().HaveCount(expectedLength);
        upperBoundary.Should().HaveCount(expectedLength);
        lowerBoundary.Should().NotEqual(upperBoundary);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    public void RejectsAtcWithInvalidLength(int length)
    {
        Action action = () => EmvAcSessionKeyDerivation.Derive(
            KeyAlgorithm.Aes,
            Convert.FromHexString(Aes128MasterKey),
            new byte[length]);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("atc")
            .WithMessage("*exactly 2 bytes*");
    }

    [TestCase(0)]
    [TestCase(15)]
    [TestCase(17)]
    [TestCase(23)]
    [TestCase(25)]
    [TestCase(31)]
    [TestCase(33)]
    public void RejectsInvalidAesMasterKeyLength(int length)
    {
        Action action = () => EmvAcSessionKeyDerivation.Derive(
            KeyAlgorithm.Aes,
            new byte[length],
            Convert.FromHexString("0001"));

        action.Should().Throw<ArgumentException>()
            .WithParameterName("key")
            .WithMessage("*16, 24 or 32 bytes*");
    }

    [TestCase(0)]
    [TestCase(15)]
    [TestCase(17)]
    [TestCase(24)]
    [TestCase(32)]
    public void RejectsInvalidTdeaMasterKeyLength(int length)
    {
        Action action = () => EmvAcSessionKeyDerivation.Derive(
            KeyAlgorithm.Tdea,
            new byte[length],
            Convert.FromHexString("0001"));

        action.Should().Throw<ArgumentException>()
            .WithParameterName("masterKey")
            .WithMessage("*exactly 16 bytes*");
    }

    [Test]
    public void RejectsWeakTdeaMasterKeyWithoutExposingIt()
    {
        const string keyMaterial = "01010101010101010101010101010101";
        Action action = () => EmvAcSessionKeyDerivation.Derive(
            KeyAlgorithm.Tdea,
            Convert.FromHexString(keyMaterial),
            Convert.FromHexString("0001"));

        ArgumentException exception = action.Should().Throw<ArgumentException>().Which;
        exception.ToString().ToUpperInvariant().Should().NotContain(keyMaterial);
    }

    [TestCase(KeyAlgorithm.Unknown)]
    [TestCase(KeyAlgorithm.Hmac)]
    [TestCase(KeyAlgorithm.Rsa)]
    [TestCase(KeyAlgorithm.Ec)]
    [TestCase((KeyAlgorithm)int.MaxValue)]
    public void RejectsUnsupportedMasterKeyAlgorithm(KeyAlgorithm algorithm)
    {
        Action action = () => EmvAcSessionKeyDerivation.Derive(
            algorithm,
            Convert.FromHexString(Aes128MasterKey),
            Convert.FromHexString("0001"));

        action.Should().Throw<ArgumentException>()
            .WithParameterName("algorithm")
            .WithMessage("*only TDEA and AES*");
    }

    [Test]
    public void RequiresMasterKey()
    {
        Action action = () => EmvAcSessionKeyDerivation.Derive(
            KeyAlgorithm.Aes,
            null!,
            Convert.FromHexString("0001"));

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("masterKey");
    }

    [Test]
    public void RequiresAtc()
    {
        Action action = () => EmvAcSessionKeyDerivation.Derive(
            KeyAlgorithm.Aes,
            Convert.FromHexString(Aes128MasterKey),
            null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("atc");
    }

    [TestCase(Aes192MasterKey, 24)]
    [TestCase(Aes256MasterKey, 32)]
    public void ExtendedAesKeysProduceExactRequestedLength(string masterKey, int expectedLength)
    {
        byte[] result = EmvAcSessionKeyDerivation.Derive(
            KeyAlgorithm.Aes,
            Convert.FromHexString(masterKey),
            Convert.FromHexString("0001"));

        result.Should().HaveCount(expectedLength);
    }

    [Test]
    public void TdeaSessionKeyIsReturnedWithoutOddParityCorrection()
    {
        byte[] result = EmvAcSessionKeyDerivation.Derive(
            KeyAlgorithm.Tdea,
            Convert.FromHexString(TdeaMasterKey),
            Convert.FromHexString("0001"));

        Convert.ToHexString(result).Should().Be("5B10C70AFEE94975A345C69888048FF9");
        HasOddParity(result[3]).Should().BeFalse();
    }

    [TestCase(KeyAlgorithm.Tdea, TdeaMasterKey)]
    [TestCase(KeyAlgorithm.Aes, Aes128MasterKey)]
    [TestCase(KeyAlgorithm.Aes, Aes192MasterKey)]
    [TestCase(KeyAlgorithm.Aes, Aes256MasterKey)]
    public void DoesNotModifyInputBuffers(KeyAlgorithm algorithm, string masterKeyValue)
    {
        byte[] masterKey = Convert.FromHexString(masterKeyValue);
        byte[] atc = Convert.FromHexString("0001");
        byte[] originalMasterKey = (byte[])masterKey.Clone();
        byte[] originalAtc = (byte[])atc.Clone();

        _ = EmvAcSessionKeyDerivation.Derive(algorithm, masterKey, atc);

        masterKey.Should().Equal(originalMasterKey);
        atc.Should().Equal(originalAtc);
    }

    [Test]
    public void ValidationErrorsDoNotExposeKeyMaterial()
    {
        const string keyMaterial = "00112233445566778899AABBCCDDEE";
        byte[] invalidKey = Convert.FromHexString(keyMaterial);

        Action action = () => EmvAcSessionKeyDerivation.Derive(
            KeyAlgorithm.Aes,
            invalidKey,
            Convert.FromHexString("0001"));

        ArgumentException exception = action.Should().Throw<ArgumentException>().Which;
        exception.ToString().ToUpperInvariant().Should().NotContain(keyMaterial);
    }

    private static bool HasOddParity(byte value)
    {
        int bitCount = 0;
        for (int bit = 0; bit < 8; bit++)
            bitCount += (value >> bit) & 1;
        return bitCount % 2 == 1;
    }

    private static void AssertVector(
        KeyAlgorithm algorithm,
        string masterKey,
        string expectedSessionKey)
    {
        byte[] result = EmvAcSessionKeyDerivation.Derive(
            algorithm,
            Convert.FromHexString(masterKey),
            Convert.FromHexString("0001"));

        Convert.ToHexString(result).Should().Be(expectedSessionKey);
    }
}
