using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class HkdfMechanismTests
{
    private const string Ikm = "0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B";
    private const string Salt = "000102030405060708090A0B0C";
    private const string Info = "F0F1F2F3F4F5F6F7F8F9";
    private const string Prk = "077709362C2E32DF0DDC3F0DC47BBA6390B6C73BB50F9C3122EC844AD7C2B3E5";
    private const string Okm = "3CB25F25FAACD57A90434F64D0362F2A2D2D0A90CF1A5A4C5DB02D56ECC4C5BF34007208D5B887185865";

    private static readonly string[] SupportedHashes =
    {
        "HASH-SHA1", "HASH-SHA224", "HASH-SHA256", "HASH-SHA384", "HASH-SHA512",
        "HASH-SHA512-224", "HASH-SHA512-256", "HASH-SHA3-224", "HASH-SHA3-256",
        "HASH-SHA3-384", "HASH-SHA3-512"
    };

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void ExtractMatchesRfc5869Case1()
    {
        KeyVariableDeclaration result = Result(
            $"KEY input=GenerateKey(HMAC-SHA256,0x({Ikm})) " +
            $"PARAM p=Parameters(HKDF-EXTRACT,#HASH:HASH-SHA256,#SALT:0x({Salt})) " +
            "KEY output=Derive(p,input,\"\")");

        AssertResult(result, Prk, 256, "HKDF-EXTRACT");
    }

    [Test]
    public void ExpandMatchesRfc5869Case1WithoutExtractingAgain()
    {
        KeyVariableDeclaration result = Result(
            $"KEY input=GenerateKey(HMAC-SHA256,0x({Prk})) " +
            $"PARAM p=Parameters(HKDF-EXPAND,#HASH:HASH-SHA256,#OUTLEN:336) " +
            $"KEY output=Derive(p,input,0x({Info}))");

        AssertResult(result, Okm, 336, "HKDF-EXPAND");
    }

    [Test]
    public void ExtractAndExpandMatchRfc5869Sha1Case4()
    {
        CryptoScriptProgram program = Execute(
            "KEY ikm=GenerateKey(HMAC-SHA1,0x(0B0B0B0B0B0B0B0B0B0B0B)) " +
            $"PARAM extract=Parameters(HKDF-EXTRACT,#HASH:HASH-SHA1,#SALT:0x({Salt})) " +
            "KEY prk=Derive(extract,ikm,\"\") " +
            "PARAM expand=Parameters(HKDF-EXPAND,#HASH:HASH-SHA1,#OUTLEN:336) " +
            $"KEY okm=Derive(expand,prk,0x({Info}))");

        AssertResult((KeyVariableDeclaration)program.Statements[2],
            "9B6C18C432A7BF8F0E71C8EB88F4B30BAA2BA243", 160, "HKDF-EXTRACT");
        AssertResult((KeyVariableDeclaration)program.Statements[4],
            "085A01EA1B10F36933068B56EFA5AD81A4F14B822F5B091568A9CDD4F155FDA2C22E422478D305F3F896",
            336, "HKDF-EXPAND");
    }

    [TestCase(8)]
    [TestCase(256)]
    [TestCase(336)]
    public void ExpandSupportsValidOutputLengthsAndEmptyInfo(int bits)
    {
        KeyVariableDeclaration result = Result(
            $"KEY input=GenerateKey(HMAC-SHA256,0x({Prk})) " +
            $"PARAM p=Parameters(HKDF-EXPAND,#HASH:HASH-SHA256,#OUTLEN:{bits}) " +
            "KEY output=Derive(p,input,\"\")");

        FormatConversions.HexStringToByteArray(result.Value).Should().HaveCount(bits / 8);
    }

    [TestCase("HASH-SHA256", 32)]
    [TestCase("HASH-SHA512", 64)]
    public void ExpandAcceptsPrkWithExactlyHashLen(string hash, int hashLength)
    {
        KeyVariableDeclaration result = ExpandWithPrk(hash, new byte[hashLength], 8);

        FormatConversions.HexStringToByteArray(result.Value).Should().HaveCount(1);
    }

    [TestCase("HASH-SHA256", 33)]
    [TestCase("HASH-SHA512", 65)]
    public void ExpandAcceptsPrkLongerThanHashLen(string hash, int prkLength)
    {
        KeyVariableDeclaration result = ExpandWithPrk(hash, new byte[prkLength], 8);

        FormatConversions.HexStringToByteArray(result.Value).Should().HaveCount(1);
    }

    [TestCase("HASH-SHA256", 31)]
    [TestCase("HASH-SHA512", 63)]
    public void ExpandRejectsPrkShorterThanSelectedHashLen(string hash, int prkLength)
    {
        Action action = () => ExpandWithPrk(hash, new byte[prkLength], 8);

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("PRK must be at least HashLen bytes"));
    }

    [TestCase("")]
    [TestCase("#SALT:\"\",")]
    public void ExtractTreatsMissingAndEmptySaltAccordingToRfc5869(string salt)
    {
        KeyVariableDeclaration result = Result(
            $"KEY input=GenerateKey(HMAC-SHA256,0x({Ikm})) " +
            $"PARAM p=Parameters(HKDF-EXTRACT,#HASH:HASH-SHA256{(salt.Length == 0 ? string.Empty : "," + salt.TrimEnd(','))}) " +
            "KEY output=Derive(p,input,\"\")");

        AssertResult(result, "19EF24A32C717B167F33A91D6F648BDF96596776AFDB6377AC434C1C293CCB04", 256, "HKDF-EXTRACT");
    }

    [Test]
    public void ExtractAcceptsEmptyInfoPlaceholder()
    {
        KeyVariableDeclaration result = Result(
            $"KEY input=GenerateKey(HMAC-SHA256,0x({Ikm})) " +
            "PARAM p=Parameters(HKDF-EXTRACT,#HASH:HASH-SHA256) " +
            "KEY output=Derive(p,input,\"\")");

        result.DerivationMechanism.Should().Be("HKDF-EXTRACT");
    }

    [Test]
    public void ExtractRejectsNonEmptyInfo()
    {
        Action action = () => Execute(
            $"KEY input=GenerateKey(HMAC-SHA256,0x({Ikm})) " +
            "PARAM p=Parameters(HKDF-EXTRACT,#HASH:HASH-SHA256) " +
            "KEY output=Derive(p,input,\"context\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("HKDF-EXTRACT does not use info data"));
    }

    [TestCaseSource(nameof(SupportedHashes))]
    public void BothMechanismsSupportEveryExistingHkdfHash(string hash)
    {
        CryptoScriptProgram program = Execute(
            $"KEY ikm=GenerateKey(HMAC-SHA256,0x({Ikm})) " +
            $"PARAM extract=Parameters(HKDF-EXTRACT,#HASH:{hash}) " +
            "KEY prk=Derive(extract,ikm,\"\") " +
            $"PARAM expand=Parameters(HKDF-EXPAND,#HASH:{hash},#OUTLEN:8) " +
            "KEY okm=Derive(expand,prk,\"\")");

        var prk = (KeyVariableDeclaration)program.Statements[2];
        prk.KeySize.Should().Be((CryptoScript.CryptoAlgorithm.DigestFactory.Create(hash).GetDigestSize() * 8).ToString());
        FormatConversions.HexStringToByteArray(((KeyVariableDeclaration)program.Statements[4]).Value).Should().HaveCount(1);
    }

    [Test]
    public void ExpandRejectsMoreThan255HashBlocks()
    {
        Action action = () => Execute(
            $"KEY input=GenerateKey(HMAC-SHA256,0x({Prk})) " +
            $"PARAM p=Parameters(HKDF-EXPAND,#HASH:HASH-SHA256,#OUTLEN:{(255 * 32 + 1) * 8})");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("RFC 5869 limit"));
    }

    [Test]
    public void ExpandAcceptsExactly255HashBlocks()
    {
        const int maximumBits = 255 * 32 * 8;

        KeyVariableDeclaration result = ExpandWithPrk("HASH-SHA256", Convert.FromHexString(Prk), maximumBits);

        FormatConversions.HexStringToByteArray(result.Value).Should().HaveCount(255 * 32);
    }

    [TestCase(0, "positive integer")]
    [TestCase(7, "divisible by 8")]
    public void ExpandRejectsInvalidOutputLength(int bits, string expectedMessage)
    {
        Action action = () => Execute(
            $"KEY input=GenerateKey(HMAC-SHA256,0x({Prk})) " +
            $"PARAM p=Parameters(HKDF-EXPAND,#HASH:HASH-SHA256,#OUTLEN:{bits})");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(expectedMessage));
    }

    [Test]
    public void ExpandRequiresOutputLength()
    {
        Action action = () => Execute(
            $"KEY input=GenerateKey(HMAC-SHA256,0x({Prk})) " +
            "PARAM p=Parameters(HKDF-EXPAND,#HASH:HASH-SHA256)");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("requires #OUTLEN"));
    }

    [Test]
    public void ParameterContractsAreSpecificToEachMechanism()
    {
        AlgorithmFactory.Create("HKDF-EXTRACT").Should().BeOfType<CryptoScript.CryptoAlgorithm.KDF.KDF_HKDF>();
        AlgorithmFactory.Create("HKDF-EXPAND").Should().BeOfType<CryptoScript.CryptoAlgorithm.KDF.KDF_HKDF>();
        Action extractWithLength = () => Execute("PARAM p=Parameters(HKDF-EXTRACT,#HASH:HASH-SHA256,#OUTLEN:256)");
        Action expandWithSalt = () => Execute("PARAM p=Parameters(HKDF-EXPAND,#HASH:HASH-SHA256,#SALT:\"\",#OUTLEN:256)");
        extractWithLength.Should().Throw<SemanticErrorException>();
        expandWithSalt.Should().Throw<SemanticErrorException>();
    }

    private static KeyVariableDeclaration Result(string script) =>
        Execute(script).Statements[^1].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static KeyVariableDeclaration ExpandWithPrk(string hash, byte[] prk, int outputLengthBits) => Result(
        $"KEY input=GenerateKey(HMAC-SHA256,0x({Convert.ToHexString(prk)})) " +
        $"PARAM p=Parameters(HKDF-EXPAND,#HASH:{hash},#OUTLEN:{outputLengthBits}) " +
        "KEY output=Derive(p,input,\"\")");

    private static void AssertResult(KeyVariableDeclaration result, string expectedHex, int bits, string mechanism)
    {
        result.Value.Should().BeEquivalentTo($"0x({expectedHex})", options => options.IgnoringCase());
        result.KeySize.Should().Be(bits.ToString());
        result.DerivationMechanism.Should().Be(mechanism);
    }

    private static CryptoScriptProgram Execute(string input)
    {
        var parser = ParserBuilder.StringBuild(input);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
