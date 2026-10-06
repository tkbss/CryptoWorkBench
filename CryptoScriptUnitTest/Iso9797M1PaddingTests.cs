using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

public class Iso9797M1PaddingTests
{
    private const string AesKey = "0x(2B7E151628AED2A6ABF7158809CF4F3C)";
    private const string AesIv = "0x(000102030405060708090A0B0C0D0E0F)";

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

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
    [TestCase(16, "0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20", "0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20")]
    public void Pad_MatchesIso9797Method1ByteLayout(
        int blockSize, string inputHex, string expectedHex)
    {
        var subject = new Iso9797M1Padding(blockSize);

        subject.Pad(Convert.FromHexString(inputHex))
            .Should().Equal(Convert.FromHexString(expectedHex));
    }

    [TestCase(0, 16)]
    [TestCase(1, 16)]
    [TestCase(15, 16)]
    [TestCase(16, 16)]
    [TestCase(17, 32)]
    [TestCase(32, 32)]
    public void AesCbcEncrypt_ProducesExpectedM1CiphertextLength(
        int plaintextLength, int expectedCiphertextLength)
    {
        string plaintext = HexValue(plaintextLength);

        var result = Execute(AesDeclarations("ISO-9797-M1") + $"VAR c=Encrypt(p,k,{plaintext})");

        ResultBytes(result).Should().HaveCount(expectedCiphertextLength);
    }

    // Fixed values were produced from the manually padded block inputs with
    // AES-CBC/NoPadding, independently of CryptoScript's M1 padding path.
    [TestCase("", "50FE67CC996D32B6DA0937E99BAFEC60")]
    [TestCase("010203", "ACD3C7AE20D0D53E7D22A92B13181A9F")]
    [TestCase("00112233445566778899AABBCCDDEEFF", "B577ED00E35432951E2F6E82CBE27177")]
    public void AesCbcEncrypt_MatchesFixedM1KnownAnswers(string plaintextHex, string ciphertextHex)
    {
        string plaintext = plaintextHex.Length == 0 ? "\"\"" : $"0x({plaintextHex})";

        var result = Execute(AesDeclarations("ISO-9797-M1") + $"VAR c=Encrypt(p,k,{plaintext})");

        result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject.Value
            .Should().BeEquivalentTo($"0x({ciphertextHex})", options => options.IgnoringCase());
    }

    [TestCase("50FE67CC996D32B6DA0937E99BAFEC60", "00000000000000000000000000000000")]
    [TestCase("ACD3C7AE20D0D53E7D22A92B13181A9F", "01020300000000000000000000000000")]
    [TestCase("B577ED00E35432951E2F6E82CBE27177", "00112233445566778899AABBCCDDEEFF")]
    public void AesCbcDecrypt_FixedM1KnownAnswersRetainRawZeroBytes(
        string ciphertextHex, string expectedPlaintextHex)
    {
        var result = Execute(
            AesDeclarations("ISO-9797-M1") + $"VAR clear=Decrypt(p,k,0x({ciphertextHex}))");

        result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject.Value
            .Should().BeEquivalentTo($"0x({expectedPlaintextHex})", options => options.IgnoringCase());
    }

    [TestCase(0)]
    [TestCase(15)]
    [TestCase(17)]
    public void AesCbcDecrypt_RejectsInvalidM1CiphertextLengths(int ciphertextLength)
    {
        Action act = () => Execute(
            AesDeclarations("ISO-9797-M1") + $"VAR clear=Decrypt(p,k,{HexValue(ciphertextLength)})");

        act.Should().Throw<SemanticErrorException>();
    }

    [TestCase(16)]
    [TestCase(32)]
    public void AesCbcDecrypt_AcceptsBlockAlignedNonEmptyM1CiphertextLengths(int ciphertextLength)
    {
        var result = Execute(
            AesDeclarations("ISO-9797-M1") + $"VAR clear=Decrypt(p,k,{HexValue(ciphertextLength)})");

        ResultBytes(result).Should().HaveCount(ciphertextLength);
    }

    [Test]
    public void AesCbc_EmptyInputDistinguishesM1FromNone()
    {
        var encrypted = Execute(AesDeclarations("ISO-9797-M1") + "VAR c=Encrypt(p,k,\"\")");
        ResultBytes(encrypted).Should().HaveCount(16);

        Action decryptM1 = () => Execute(
            AesDeclarations("ISO-9797-M1") + "VAR clear=Decrypt(p,k,\"\")");
        Action encryptNone = () => Execute(
            AesDeclarations("NONE") + "VAR c=Encrypt(p,k,\"\")");
        Action decryptNone = () => Execute(
            AesDeclarations("NONE") + "VAR clear=Decrypt(p,k,\"\")");

        decryptM1.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                "PAD=ISO-9797-M1 requires ciphertext length to be a non-zero multiple of 16 bytes."));
        encryptNone.Should().Throw<SemanticErrorException>();
        decryptNone.Should().Throw<SemanticErrorException>();
    }

    [Test]
    public void NonCanonicalIso9797M1_IsRejectedByThePublicScriptPath()
    {
        string script =
            $"PARAM p=Parameters(#MECH:AES-CBC,#IV:{AesIv},#PAD:ISO9797M1)";
        CryptoScriptParser parser = ParserBuilder.StringBuild(script);
        var context = parser.program();

        Assert.Multiple(() =>
        {
            parser.NumberOfSyntaxErrors.Should().Be(0);
            SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
            LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        });

        Action act = () => new CryptoScriptRunner().Execute(context);
        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                "Unknown parameter value : ISO9797M1"));
    }

    [Test]
    public void AesCbc_TrailingZeroInputRetainsTheFullM1PaddedBlockOnDecrypt()
    {
        var result = Execute(
            AesDeclarations("ISO-9797-M1") +
            "VAR c=Encrypt(p,k,0x(010200)) VAR clear=Decrypt(p,k,c)");

        result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject.Value
            .Should().BeEquivalentTo(
                "0x(2A44191729C23D6F078E5A56E6114B36)",
                options => options.IgnoringCase());
        result.Statements[3].Should().BeOfType<StringVariableDeclaration>().Subject.Value
            .Should().BeEquivalentTo(
                "0x(01020000000000000000000000000000)",
                options => options.IgnoringCase());
    }

    private static string AesDeclarations(string padding) =>
        $"KEY k=GenerateKey(AES-CBC,{AesKey}) " +
        $"PARAM p=Parameters(#MECH:AES-CBC,#IV:{AesIv},#PAD:{padding}) ";

    private static string HexValue(int length) =>
        length == 0 ? "\"\"" : $"0x({new string('0', length * 2)})";

    private static byte[] ResultBytes(CryptoScript.Model.CryptoScriptProgram result) =>
        FormatConversions.HexStringToByteArray(
            result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject.Value);

    private static CryptoScript.Model.CryptoScriptProgram Execute(string input)
    {
        var parser = ParserBuilder.StringBuild(input);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
