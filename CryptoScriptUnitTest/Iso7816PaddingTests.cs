using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;
using Org.BouncyCastle.Crypto.Paddings;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Iso7816PaddingTests
{
    // Fixed byte layouts derived directly from ISO/IEC 7816-4 padding and
    // ISO/IEC 9797-1:2011, 6.3.3. Production code does not generate expectations.
    [TestCase(8, "", "8000000000000000")]
    [TestCase(8, "01", "0180000000000000")]
    [TestCase(8, "01020304050607", "0102030405060780")]
    [TestCase(8, "0102030405060708", "01020304050607088000000000000000")]
    [TestCase(8, "010203040506070809", "01020304050607080980000000000000")]
    [TestCase(16, "", "80000000000000000000000000000000")]
    [TestCase(16, "01", "01800000000000000000000000000000")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F", "0102030405060708090A0B0C0D0E0F80")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F10", "0102030405060708090A0B0C0D0E0F1080000000000000000000000000000000")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F1011", "0102030405060708090A0B0C0D0E0F1011800000000000000000000000000000")]
    public void Pad_MatchesCanonicalByteLayoutAndBouncyCastle(
        int blockSize, string inputHex, string expectedHex)
    {
        byte[] input = Convert.FromHexString(inputHex);
        byte[] expected = Convert.FromHexString(expectedHex);
        var subject = new Iso7816Padding(blockSize);

        byte[] actual = subject.Pad(input);

        actual.Should().Equal(expected);

        int remainder = input.Length % blockSize;
        byte[] bouncyCastleFinalBlock = new byte[blockSize];
        if (remainder > 0)
        {
            Buffer.BlockCopy(input, input.Length - remainder,
                bouncyCastleFinalBlock, 0, remainder);
        }
        new ISO7816d4Padding().AddPadding(bouncyCastleFinalBlock, remainder);
        actual[^blockSize..].Should().Equal(bouncyCastleFinalBlock);
    }

    [TestCase(8, "0102030405068000", "010203040506")]
    [TestCase(16, "0102030405060708090A0B0C0D0E8000", "0102030405060708090A0B0C0D0E")]
    [TestCase(8, "8000000000000000", "")]
    [TestCase(16, "80000000000000000000000000000000", "")]
    public void Unpad_AcceptsCanonicalMarkerInFinalBlock(
        int blockSize, string inputHex, string expectedHex)
    {
        var subject = new Iso7816Padding(blockSize);
        byte[] input = Convert.FromHexString(inputHex);
        byte[] expected = Convert.FromHexString(expectedHex);

        subject.Unpad(input).Should().Equal(expected);

        // Bouncy Castle normally validates only the final cipher block.
        int bouncyCastlePaddingLength = new ISO7816d4Padding()
            .PadCount(input[^blockSize..]);
        bouncyCastlePaddingLength.Should().Be(input.Length - expected.Length);
    }

    [TestCase(8)]
    [TestCase(16)]
    public void Unpad_RejectsEmptyInput(int blockSize)
    {
        Action act = () => new Iso7816Padding(blockSize).Unpad(Array.Empty<byte>());

        act.Should().Throw<CryptographicException>();
    }

    [TestCase(8)]
    [TestCase(16)]
    public void Unpad_RejectsNonBlockAlignedInput(int blockSize)
    {
        Action act = () => new Iso7816Padding(blockSize).Unpad(new byte[blockSize - 1]);

        act.Should().Throw<CryptographicException>();
    }

    [TestCase(8)]
    [TestCase(16)]
    public void Unpad_RejectsOnlyZeroBytes(int blockSize)
    {
        Action act = () => new Iso7816Padding(blockSize).Unpad(new byte[blockSize]);

        act.Should().Throw<CryptographicException>();
    }

    [TestCase(8)]
    [TestCase(16)]
    public void Unpad_RejectsBlockWithoutMarker(int blockSize)
    {
        byte[] input = Enumerable.Repeat((byte)0x11, blockSize).ToArray();

        Action act = () => new Iso7816Padding(blockSize).Unpad(input);

        act.Should().Throw<CryptographicException>();
    }

    [TestCase(8)]
    [TestCase(16)]
    public void Unpad_RejectsWrongLastNonZeroByte(int blockSize)
    {
        byte[] input = new byte[blockSize];
        input[blockSize - 3] = 0x7F;

        Action act = () => new Iso7816Padding(blockSize).Unpad(input);

        act.Should().Throw<CryptographicException>();
    }

    [TestCase(8)]
    [TestCase(16)]
    public void Unpad_RejectsMarkerFollowedByExactlyOneBlockOfZeros(int blockSize)
    {
        byte[] input = Enumerable.Repeat((byte)0x11, blockSize - 1)
            .Append((byte)0x80)
            .Concat(new byte[blockSize])
            .ToArray();

        Action act = () => new Iso7816Padding(blockSize).Unpad(input);

        act.Should().Throw<CryptographicException>();
    }

    [TestCase(8)]
    [TestCase(16)]
    public void Unpad_RejectsMarkerMoreThanOneBlockBeforeEnd(int blockSize)
    {
        byte[] input = Enumerable.Repeat((byte)0x11, blockSize - 1)
            .Append((byte)0x80)
            .Concat(new byte[2 * blockSize])
            .ToArray();

        Action act = () => new Iso7816Padding(blockSize).Unpad(input);

        act.Should().Throw<CryptographicException>();
    }

    [TestCase("AES-CBC", "ISO-7816", "", "4C08220C79D9191022DC6674874CEAF8")]
    [TestCase("AES-CBC", "ISO-9797-M2", "", "4C08220C79D9191022DC6674874CEAF8")]
    [TestCase("AES-CBC", "ISO-7816", "00112233445566778899AABBCCDDEEFF", "B577ED00E35432951E2F6E82CBE271774027DF59B97195C5CAAA741030E55011")]
    [TestCase("AES-CBC", "ISO-9797-M2", "00112233445566778899AABBCCDDEEFF", "B577ED00E35432951E2F6E82CBE271774027DF59B97195C5CAAA741030E55011")]
    [TestCase("AES-CBC", "ISO-7816", "001122", "22EE7E55CFFB0B08D1732660BE09668C")]
    [TestCase("AES-CBC", "ISO-9797-M2", "001122", "22EE7E55CFFB0B08D1732660BE09668C")]
    [TestCase("DES3-CBC", "ISO-7816", "", "AA6E1AFB1CAF4026")]
    [TestCase("DES3-CBC", "ISO-9797-M2", "", "AA6E1AFB1CAF4026")]
    [TestCase("DES3-CBC", "ISO-7816", "0011223344556677", "1CA1818E471562C18DC1875A118EE870")]
    [TestCase("DES3-CBC", "ISO-9797-M2", "0011223344556677", "1CA1818E471562C18DC1875A118EE870")]
    [TestCase("DES3-CBC", "ISO-7816", "001122", "E4E53DD15AA1FBE3")]
    [TestCase("DES3-CBC", "ISO-9797-M2", "001122", "E4E53DD15AA1FBE3")]
    [TestCase("DES3-ECB", "ISO-7816", "", "8667A2C7C9FA095A")]
    [TestCase("DES3-ECB", "ISO-9797-M2", "", "8667A2C7C9FA095A")]
    [TestCase("DES3-ECB", "ISO-7816", "0011223344556677", "51FB23DC603ADDD18667A2C7C9FA095A")]
    [TestCase("DES3-ECB", "ISO-9797-M2", "0011223344556677", "51FB23DC603ADDD18667A2C7C9FA095A")]
    [TestCase("DES3-ECB", "ISO-7816", "001122", "20A36ACDA0831454")]
    [TestCase("DES3-ECB", "ISO-9797-M2", "001122", "20A36ACDA0831454")]
    public void PublicPaddingIdentity_EncryptsExpectedCiphertextAndDecrypts(
        string mechanism, string padding, string plaintextHex, string expectedCiphertextHex)
    {
        string key = mechanism == "AES-CBC"
            ? "2B7E151628AED2A6ABF7158809CF4F3C"
            : "0123456789ABCDEFFEDCBA98765432100011223344556677";
        string iv = mechanism switch
        {
            "AES-CBC" => ",#IV:0x(000102030405060708090A0B0C0D0E0F)",
            "DES3-CBC" => ",#IV:0x(1234567890ABCDEF)",
            _ => string.Empty
        };
        string plaintext = plaintextHex.Length == 0 ? "\"\"" : $"0x({plaintextHex})";
        string script =
            $"KEY k=GenerateKey({mechanism},0x({key})) " +
            $"PARAM p=Parameters(#MECH:{mechanism}{iv},#PAD:{padding}) " +
            $"VAR c=Encrypt(p,k,{plaintext}) VAR clear=Decrypt(p,k,c)";

        var parser = ParserBuilder.StringBuild(script);
        var result = new CryptoScriptRunner().Execute(parser.program());

        result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject.Value
            .Should().BeEquivalentTo($"0x({expectedCiphertextHex})",
                options => options.IgnoringCase());
        var clear = result.Statements[3].Should().BeOfType<StringVariableDeclaration>().Subject;
        FormatConversions.ToByteArray(clear.Value, clear.ValueFormat)
            .Should().Equal(Convert.FromHexString(plaintextHex));
    }

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }
}
