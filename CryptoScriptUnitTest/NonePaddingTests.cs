using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

public class NonePaddingTests
{
    private const string AesKey = "2B7E151628AED2A6ABF7158809CF4F3C";
    private const string AesIv = "000102030405060708090A0B0C0D0E0F";
    private const string Des3Key = "0123456789ABCDEFFEDCBA98765432100011223344556677";
    private const string Des3Iv = "1234567890ABCDEF";

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCase(0, false)]
    [TestCase(15, false)]
    [TestCase(16, true)]
    [TestCase(17, false)]
    [TestCase(32, true)]
    public void AesCbcEncrypt_EnforcesPositiveBlockAlignedInput(int length, bool succeeds) =>
        AssertCipherContract("AES-CBC", "Encrypt", length, succeeds);

    [TestCase(0, false)]
    [TestCase(15, false)]
    [TestCase(16, true)]
    [TestCase(17, false)]
    [TestCase(32, true)]
    public void AesCbcDecrypt_EnforcesPositiveBlockAlignedInput(int length, bool succeeds) =>
        AssertCipherContract("AES-CBC", "Decrypt", length, succeeds);

    [TestCase(0, false)]
    [TestCase(7, false)]
    [TestCase(8, true)]
    [TestCase(9, false)]
    [TestCase(16, true)]
    public void Des3CbcEncrypt_EnforcesPositiveBlockAlignedInput(int length, bool succeeds) =>
        AssertCipherContract("DES3-CBC", "Encrypt", length, succeeds);

    [TestCase(0, false)]
    [TestCase(7, false)]
    [TestCase(8, true)]
    [TestCase(9, false)]
    [TestCase(16, true)]
    public void Des3CbcDecrypt_EnforcesPositiveBlockAlignedInput(int length, bool succeeds) =>
        AssertCipherContract("DES3-CBC", "Decrypt", length, succeeds);

    [TestCase(0, false)]
    [TestCase(7, false)]
    [TestCase(8, true)]
    [TestCase(9, false)]
    [TestCase(16, true)]
    public void Des3EcbEncrypt_EnforcesPositiveBlockAlignedInput(int length, bool succeeds) =>
        AssertCipherContract("DES3-ECB", "Encrypt", length, succeeds);

    [TestCase(0, false)]
    [TestCase(7, false)]
    [TestCase(8, true)]
    [TestCase(9, false)]
    [TestCase(16, true)]
    public void Des3EcbDecrypt_EnforcesPositiveBlockAlignedInput(int length, bool succeeds) =>
        AssertCipherContract("DES3-ECB", "Decrypt", length, succeeds);

    [Test]
    public void AesCbcEncrypt_MatchesNistSp80038aFourBlockKnownAnswer()
    {
        const string plaintext =
            "6BC1BEE22E409F96E93D7E117393172A" +
            "AE2D8A571E03AC9C9EB76FAC45AF8E51" +
            "30C81C46A35CE411E5FBC1191A0A52EF" +
            "F69F2445DF4F9B17AD2B417BE66C3710";
        const string ciphertext =
            "7649ABAC8119B246CEE98E9B12E9197D" +
            "5086CB9B507219EE95DB113A917678B2" +
            "73BED6B8E3C1743B7116E69E22229516" +
            "3FF1CAA1681FAC09120ECA307586E1A7";

        ExecuteCipher("AES-CBC", "Encrypt", Convert.FromHexString(plaintext))
            .Should().Equal(Convert.FromHexString(ciphertext));
    }

    [Test]
    public void AesCbcDecrypt_MatchesNistSp80038aDirectFourBlockKnownAnswer()
    {
        const string plaintext =
            "6BC1BEE22E409F96E93D7E117393172A" +
            "AE2D8A571E03AC9C9EB76FAC45AF8E51" +
            "30C81C46A35CE411E5FBC1191A0A52EF" +
            "F69F2445DF4F9B17AD2B417BE66C3710";
        const string ciphertext =
            "7649ABAC8119B246CEE98E9B12E9197D" +
            "5086CB9B507219EE95DB113A917678B2" +
            "73BED6B8E3C1743B7116E69E22229516" +
            "3FF1CAA1681FAC09120ECA307586E1A7";

        ExecuteCipher("AES-CBC", "Decrypt", Convert.FromHexString(ciphertext))
            .Should().Equal(Convert.FromHexString(plaintext));
    }

    [TestCase("Encrypt", "4E6F77206973207468652074696D6520666F7220616C6C20",
        "0B1A2D632A9CB6288AF4F7A1E3CF596E34C0D58362DEAEFE")]
    [TestCase("Decrypt", "0B1A2D632A9CB6288AF4F7A1E3CF596E34C0D58362DEAEFE",
        "4E6F77206973207468652074696D6520666F7220616C6C20")]
    public void Des3Cbc_MatchesDirectThreeBlockKnownAnswers(
        string operation, string input, string expected)
    {
        ExecuteCipher("DES3-CBC", operation, Convert.FromHexString(input))
            .Should().Equal(Convert.FromHexString(expected));
    }

    [TestCase("Encrypt", "4E6F772069732074", "EECA43AEC1E4ED98")]
    [TestCase("Decrypt", "EECA43AEC1E4ED98", "4E6F772069732074")]
    public void Des3Ecb_MatchesDirectOneBlockKnownAnswers(
        string operation, string input, string expected)
    {
        ExecuteCipher("DES3-ECB", operation, Convert.FromHexString(input))
            .Should().Equal(Convert.FromHexString(expected));
    }

    [TestCase("AES-CBC", "00000000000000000000000000000000")]
    [TestCase("AES-CBC", "00000000000000000000000000000001")]
    [TestCase("AES-CBC", "00000000000000000000000000000202")]
    [TestCase("AES-CBC", "00000000000000000808080808080808")]
    [TestCase("AES-CBC", "10101010101010101010101010101010")]
    [TestCase("DES3-CBC", "0000000000000000")]
    [TestCase("DES3-CBC", "0000000000000001")]
    [TestCase("DES3-CBC", "0000000000000202")]
    [TestCase("DES3-CBC", "0808080808080808")]
    [TestCase("DES3-ECB", "0000000000000000")]
    [TestCase("DES3-ECB", "0000000000000001")]
    [TestCase("DES3-ECB", "0000000000000202")]
    [TestCase("DES3-ECB", "0808080808080808")]
    public void Roundtrip_PreservesPaddingLikeTrailingBytes(string mechanism, string plaintext)
    {
        byte[] input = Convert.FromHexString(plaintext);
        byte[] ciphertext = ExecuteCipher(mechanism, "Encrypt", input);

        ExecuteCipher(mechanism, "Decrypt", ciphertext).Should().Equal(input);
    }

    [TestCase("AES-CBC", 16)]
    [TestCase("DES3-CBC", 8)]
    [TestCase("DES3-ECB", 8)]
    public void EncryptionModePad_NoneIsAnIdentityTransformationForValidInput(
        string mechanism, int blockSize)
    {
        var parameter = new ParameterVariableDeclaration { Mechanism = mechanism };
        parameter.SetParameter("MECH", mechanism);
        parameter.SetParameter("PAD", "NONE");
        byte[] input = Enumerable.Range(1, blockSize).Select(value => (byte)value).ToArray();

        byte[] padded = new EncryptionMode().Pad(
            parameter, out PaddingMode padding, input, "Encrypt", blockSize);

        padding.Should().Be(PaddingMode.None);
        padded.Should().BeSameAs(input);
        padded.Should().Equal(input);
    }

    [TestCase(0, false)]
    [TestCase(7, false)]
    [TestCase(8, true)]
    [TestCase(9, false)]
    [TestCase(16, true)]
    public void Des3CbcMac_NoneRetainsItsPositiveBlockAlignedContract(
        int length, bool succeeds)
    {
        string script =
            $"KEY k=GenerateKey(DES3-CBC,0x({Des3Key})) " +
            "PARAM p=Parameters(#MECH:DES3-CBC,#PAD:NONE) " +
            $"VAR m=Mac(p,k,{ToScriptValue(CreateData(length))})";

        AssertScriptContract(script, succeeds);
    }

    [Test]
    public void Des3Retail_StillRejectsNone()
    {
        string script =
            $"KEY k=GenerateKey(DES3-RETAIL,0x({Des3Key})) " +
            "PARAM p=Parameters(#MECH:DES3-RETAIL,#PAD:NONE) " +
            "VAR m=Mac(p,k,0x(0011223344556677))";

        Action act = () => Execute(script);
        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                "ISO-9797-M1 or ISO-9797-M2"));
    }

    private static void AssertCipherContract(
        string mechanism, string operation, int length, bool succeeds)
    {
        Action act = () => ExecuteCipher(mechanism, operation, CreateData(length));

        if (succeeds)
            act.Should().NotThrow();
        else
            act.Should().Throw<SemanticErrorException>();
    }

    private static void AssertScriptContract(string script, bool succeeds)
    {
        Action act = () => Execute(script);

        if (succeeds)
            act.Should().NotThrow();
        else
            act.Should().Throw<SemanticErrorException>();
    }

    private static byte[] ExecuteCipher(string mechanism, string operation, byte[] input)
    {
        string script =
            $"KEY k=GenerateKey({mechanism},0x({GetKey(mechanism)})) " +
            $"PARAM p=Parameters(#MECH:{mechanism}{GetIv(mechanism)},#PAD:NONE) " +
            $"VAR result={operation}(p,k,{ToScriptValue(input)})";

        CryptoScriptProgram result = Execute(script);
        return FormatConversions.HexStringToByteArray(
            result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject.Value);
    }

    private static CryptoScriptProgram Execute(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }

    private static byte[] CreateData(int length) =>
        Enumerable.Range(0, length).Select(value => (byte)value).ToArray();

    private static string GetKey(string mechanism) =>
        mechanism == "AES-CBC" ? AesKey : Des3Key;

    private static string GetIv(string mechanism) => mechanism switch
    {
        "AES-CBC" => $",#IV:0x({AesIv})",
        "DES3-CBC" => $",#IV:0x({Des3Iv})",
        _ => string.Empty
    };

    private static string ToScriptValue(byte[] value) =>
        value.Length == 0 ? "\"\"" : $"0x({Convert.ToHexString(value)})";
}
