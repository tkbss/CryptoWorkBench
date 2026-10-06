using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Iso9797M3PaddingTests
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

    [Test]
    public void NonCanonicalIso9797M3_IsRejectedByThePublicScriptPath()
    {
        string script =
            $"PARAM p=Parameters(#MECH:AES-CBC,#IV:{AesIv},#PAD:ISO9797M3)";
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
                "Unknown parameter value : ISO9797M3"));
    }

    [TestCase("010200")]
    [TestCase("01020000")]
    public void AesCbc_RoundtripPreservesTrailingZeroBytes(string plaintextHex)
    {
        var result = Execute(
            $"KEY k=GenerateKey(AES-CBC,{AesKey}) " +
            $"PARAM p=Parameters(#MECH:AES-CBC,#IV:{AesIv},#PAD:ISO-9797-M3) " +
            $"VAR c=Encrypt(p,k,0x({plaintextHex})) VAR clear=Decrypt(p,k,c)");

        result.Statements[3].Should().BeOfType<StringVariableDeclaration>().Subject.Value
            .Should().BeEquivalentTo(
                $"0x({plaintextHex})",
                options => options.IgnoringCase());
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
