using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class SymmetricAlgoAesCbcMacIntegrationTests
{
    private const string Key = "2B7E151628AED2A6ABF7158809CF4F3C";
    private const string Message =
        "6BC0BCE12A459991E134741A7F9E1925" +
        "AE2D8A571E03AC9C9EB76FAC45AF8E51" +
        "30C81C46A35CE411E5FBC1191A0A52EF" +
        "F69F2445DF4F9B17AD2B417BE66C3710";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorList.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.ErrorMessage.Clear();
    }

    [Test]
    public void PublicPipeline_MatchesFullAndLeftTruncatedReferenceValues()
    {
        Execute(
            $"KEY k=GenerateKey(AES-CBC-MAC,0x({Key})) " +
            "PARAM full=Parameters(#MECH:AES-CBC-MAC,#PAD:NONE,#MACLEN:\"16\") " +
            "PARAM short=Parameters(#MECH:AES-CBC-MAC,#PAD:NONE,#MACLEN:\"8\") " +
            $"VAR fullMac=Mac(full,k,0x({Message})) VAR shortMac=Mac(short,k,0x({Message}))");

        Hex("fullMac").Should().Be("3FF1CAA1681FAC09120ECA307586E1A7");
        Hex("shortMac").Should().Be("3FF1CAA1681FAC09");
    }

    [Test]
    public void Parameters_DefaultToPkcs7AndFullMacWithoutAnIv()
    {
        Execute("KEY k=GenerateKey(AES-CBC-MAC,0x(000102030405060708090A0B0C0D0E0F)) " +
                "PARAM p=Parameters(#MECH:AES-CBC-MAC) VAR mac=Mac(p,k,\"\")");

        ParameterVariableDeclaration parameters = Parameters("p");
        Assert.Multiple(() =>
        {
            Assert.That(parameters.GetParameter("PAD"), Is.EqualTo("PKCS-7"));
            Assert.That(parameters.GetParameter("MACLEN"), Is.EqualTo("16"));
            Assert.That(parameters.GetParameter("IV"), Is.Empty);
            Assert.That(Hex("mac"), Is.EqualTo("954F64F2E4E86E9EEE82D20216684899"));
        });
    }

    [TestCase(128, 16)]
    [TestCase(192, 24)]
    [TestCase(256, 32)]
    public void GenerateKey_AcceptsEveryAesKeySize(int bits, int bytes)
    {
        Execute($"KEY k=GenerateKey(AES-CBC-MAC,{bits})");
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("k");
        FormatConversions.ToByteArray(key.Value, key.ValueFormat).Should().HaveCount(bytes);
    }

    [TestCase("#IV:0x(00000000000000000000000000000000)")]
    [TestCase("#NONCE:0x(00)")]
    [TestCase("#ADATA:\"data\"")]
    [TestCase("#COUNTER:0x(00000000)")]
    public void Parameters_RejectUnsupportedNamedParameters(string parameter)
    {
        Action act = () => Execute($"PARAM p=Parameters(#MECH:AES-CBC-MAC,{parameter})");
        act.Should().Throw<SemanticErrorException>();
    }

    [TestCase("NONE")]
    [TestCase("PKCS-7")]
    [TestCase("ANSI-X923")]
    [TestCase("ISO-7816")]
    [TestCase("ISO-9797-M1")]
    [TestCase("ISO-9797-M2")]
    [TestCase("ISO-9797-M3")]
    [TestCase("TLS-CBC")]
    public void Parameters_AcceptEverySupportedPadding(string padding)
    {
        Execute($"PARAM p=Parameters(#MECH:AES-CBC-MAC,#PAD:{padding})");
        Parameters("p").GetParameter("PAD").Should().Be(padding);
    }

    [Test]
    public void Parameters_RejectIso10126BeforeMacExecution()
    {
        Action act = () => Execute("PARAM p=Parameters(#MECH:AES-CBC-MAC,#PAD:ISO-10126)");
        act.Should().Throw<SemanticErrorException>();
    }

    [TestCase("7")]
    [TestCase("17")]
    public void Parameters_RejectInvalidMacLength(string length)
    {
        Action act = () => Execute($"PARAM p=Parameters(#MECH:AES-CBC-MAC,#MACLEN:\"{length}\")");
        act.Should().Throw<SemanticErrorException>();
    }

    [TestCase("")]
    [TestCase("001122")]
    public void None_RejectsEmptyAndPartialInput(string data)
    {
        Execute($"KEY k=GenerateKey(AES-CBC-MAC,0x({Key})) PARAM p=Parameters(#MECH:AES-CBC-MAC,#PAD:NONE)");
        string value = data.Length == 0 ? "\"\"" : $"0x({data})";

        Action act = () => Execute($"VAR mac=Mac(p,k,{value})");
        act.Should().Throw<SemanticErrorException>();
    }

    [Test]
    public void PublicContract_IsMacOnlyAndKeepsAesCbcMacRejected()
    {
        Execute($"KEY k=GenerateKey(AES-CBC-MAC,0x({Key})) PARAM p=Parameters(#MECH:AES-CBC-MAC,#PAD:NONE)");
        Assert.Throws<SemanticErrorException>(() => Execute($"VAR c=Encrypt(p,k,0x({Message}))"));
        Assert.Throws<SemanticErrorException>(() => Execute($"VAR c=Decrypt(p,k,0x({Message}))"));

        Execute("KEY cbcKey=GenerateKey(AES-CBC,128) PARAM cbc=Parameters(AES-CBC)");
        Parameters("cbc").GetParameter("IV").Should().NotBeEmpty();
        Assert.Throws<SemanticErrorException>(() => Execute("VAR tag=Mac(cbc,cbcKey,0x(00000000000000000000000000000000))"));
    }

    private static ParameterVariableDeclaration Parameters(string name) =>
        (ParameterVariableDeclaration)VariableDictionary.Instance().Get(name);

    private static string Hex(string name)
    {
        var value = (StringVariableDeclaration)VariableDictionary.Instance().Get(name);
        return Convert.ToHexString(FormatConversions.ToByteArray(value.Value, value.ValueFormat));
    }

    private static void Execute(string script)
    {
        var context = ParserBuilder.StringBuild(script).program();
        Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        new CryptoScriptRunner().Execute(context);
    }
}
