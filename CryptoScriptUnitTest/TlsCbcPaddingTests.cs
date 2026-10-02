using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class TlsCbcPaddingTests
{
    private const string AesKey = "2B7E151628AED2A6ABF7158809CF4F3C";
    private const string AesIv = "000102030405060708090A0B0C0D0E0F";
    private const string Des3Key = "0123456789ABCDEFFEDCBA98765432100011223344556677";
    private const string Des3Iv = "1234567890ABCDEF";

    // Fixed byte layouts derived directly from RFC 5246, 6.2.3.2.
    // No production or test helper generates the expected values.
    [TestCase(8, "", "0707070707070707")]
    [TestCase(8, "01", "0106060606060606")]
    [TestCase(8, "01020304050607", "0102030405060700")]
    [TestCase(8, "0102030405060708", "01020304050607080707070707070707")]
    [TestCase(8, "010203040506070809", "01020304050607080906060606060606")]
    [TestCase(16, "", "0F0F0F0F0F0F0F0F0F0F0F0F0F0F0F0F")]
    [TestCase(16, "01", "010E0E0E0E0E0E0E0E0E0E0E0E0E0E0E")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F", "0102030405060708090A0B0C0D0E0F00")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F10", "0102030405060708090A0B0C0D0E0F100F0F0F0F0F0F0F0F0F0F0F0F0F0F0F0F")]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F1011", "0102030405060708090A0B0C0D0E0F10110E0E0E0E0E0E0E0E0E0E0E0E0E0E0E")]
    public void Pad_UsesTheMinimalTlsCbcByteLayout(
        int blockSize, string inputHex, string expectedHex)
    {
        var subject = new TlsCbcPadding(blockSize);

        subject.Pad(Convert.FromHexString(inputHex))
            .Should().Equal(Convert.FromHexString(expectedHex));
    }

    [TestCase(8, "01020304050607", 1, 0x00)]
    [TestCase(8, "0102030405060708", 8, 0x07)]
    [TestCase(8, "01020304050607", 9, 0x08)]
    [TestCase(8, "0102030405060708", 16, 0x0F)]
    [TestCase(8, "01020304050607", 25, 0x18)]
    [TestCase(16, "0102030405060708090A0B0C0D0E0F10", 256, 0xFF)]
    public void Unpad_AcceptsSingleAndMultipleBlockTlsPadding(
        int blockSize, string dataHex, int paddingLength, byte paddingValue)
    {
        byte[] data = Convert.FromHexString(dataHex);
        byte[] input = data.Concat(Enumerable.Repeat(paddingValue, paddingLength)).ToArray();
        (input.Length % blockSize).Should().Be(0);

        new TlsCbcPadding(blockSize).Unpad(input).Should().Equal(data);
    }

    [TestCase(8)]
    [TestCase(16)]
    public void Unpad_RejectsEmptyInput(int blockSize)
    {
        Action act = () => new TlsCbcPadding(blockSize).Unpad(Array.Empty<byte>());

        act.Should().Throw<CryptographicException>();
    }

    [TestCase(8)]
    [TestCase(16)]
    public void Unpad_RejectsNonBlockAlignedInput(int blockSize)
    {
        Action act = () => new TlsCbcPadding(blockSize).Unpad(new byte[blockSize - 1]);

        act.Should().Throw<CryptographicException>();
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(6)]
    public void Unpad_RejectsAnInconsistentPaddingByte(int paddingByteOffset)
    {
        byte[] input = Convert.FromHexString("01020304050607080707070707070707");
        input[8 + paddingByteOffset] = 0x06;

        Action act = () => new TlsCbcPadding(8).Unpad(input);

        act.Should().Throw<CryptographicException>();
    }

    [Test]
    public void Unpad_RejectsDeclaredPaddingLongerThanInput()
    {
        byte[] input = Convert.FromHexString("0102030405060708");
        input[^1] = 0x08;

        Action act = () => new TlsCbcPadding(8).Unpad(input);

        act.Should().Throw<CryptographicException>();
    }

    [Test]
    public void Unpad_RejectsMaximumPaddingValueWhenInputIsTooShort()
    {
        byte[] input = new byte[16];
        input[^1] = 0xFF;

        Action act = () => new TlsCbcPadding(16).Unpad(input);

        act.Should().Throw<CryptographicException>();
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(257)]
    public void Constructor_RejectsBlockSizesOutsideTlsEncodingRange(int blockSize)
    {
        Action act = () => _ = new TlsCbcPadding(blockSize);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("blockSizeBytes");
    }

    [Test]
    public void Constructor_AcceptsBlockSize256()
    {
        var subject = new TlsCbcPadding(256);

        byte[] padded = subject.Pad(Array.Empty<byte>());
        padded.Should().HaveCount(256).And.OnlyContain(value => value == 0xFF);
        subject.Unpad(padded).Should().BeEmpty();
    }

    [TestCase("AES-CBC", "", "727B7F12B22722038EA2C4643F2062ED")]
    [TestCase("AES-CBC", "00112233445566778899AABBCCDDEEFF", "B577ED00E35432951E2F6E82CBE27177FB8952B1EB67D9598F54706C66F70922")]
    [TestCase("AES-CBC", "001122", "050CE02671A7A07FBEE94B0FB7EB8921")]
    [TestCase("DES3-CBC", "", "7BEA9446565F91DC")]
    [TestCase("DES3-CBC", "0011223344556677", "1CA1818E471562C1319E474BFEE46A56")]
    [TestCase("DES3-CBC", "001122", "E29B82B92EB12220")]
    [TestCase("DES3-ECB", "", "589DE8BC07A41E80")]
    [TestCase("DES3-ECB", "0011223344556677", "51FB23DC603ADDD1589DE8BC07A41E80")]
    [TestCase("DES3-ECB", "001122", "6A56DCE87D638129")]
    public void SupportedMechanism_EncryptsExpectedCiphertextAndDecrypts(
        string mechanism, string plaintextHex, string expectedCiphertextHex)
    {
        string key = mechanism == "AES-CBC" ? AesKey : Des3Key;
        string iv = mechanism switch
        {
            "AES-CBC" => $",#IV:0x({AesIv})",
            "DES3-CBC" => $",#IV:0x({Des3Iv})",
            _ => string.Empty
        };
        string plaintext = plaintextHex.Length == 0 ? "\"\"" : $"0x({plaintextHex})";
        string script =
            $"KEY k=GenerateKey({mechanism},0x({key})) " +
            $"PARAM p=Parameters(#MECH:{mechanism}{iv},#PAD:TLS-CBC) " +
            $"VAR c=Encrypt(p,k,{plaintext}) VAR clear=Decrypt(p,k,c)";

        var result = Execute(script);

        result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject.Value
            .Should().BeEquivalentTo($"0x({expectedCiphertextHex})",
                options => options.IgnoringCase());
        var clear = result.Statements[3].Should().BeOfType<StringVariableDeclaration>().Subject;
        FormatConversions.ToByteArray(clear.Value, clear.ValueFormat)
            .Should().Equal(Convert.FromHexString(plaintextHex));
    }

    [TestCase("", "589DE8BC07A41E80")]
    [TestCase("0011223344556677", "A46543703A87B92F")]
    [TestCase("001122", "6A56DCE87D638129")]
    public void Des3CbcMac_UsesTlsCbcPaddingWithoutChangingKnownAnswers(
        string messageHex, string expectedMacHex)
    {
        string message = messageHex.Length == 0 ? "\"\"" : $"0x({messageHex})";
        string script =
            $"KEY k=GenerateKey(DES3-CBC,0x({Des3Key})) " +
            "PARAM p=Parameters(#MECH:DES3-CBC,#PAD:TLS-CBC) " +
            $"VAR m=Mac(p,k,{message})";

        Execute(script).Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject.Value
            .Should().BeEquivalentTo($"0x({expectedMacHex})",
                options => options.IgnoringCase());
    }

    [Test]
    public void Des3Retail_ContinuesToRejectTlsCbcPadding()
    {
        Action act = () => Execute("PARAM p=Parameters(#MECH:DES3-RETAIL,#PAD:TLS-CBC)");

        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message
                .Contains("ISO-9797-M1 or ISO-9797-M2"));
    }

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    private static CryptoScript.Model.CryptoScriptProgram Execute(string input)
    {
        var parser = ParserBuilder.StringBuild(input);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
