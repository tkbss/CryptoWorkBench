using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Iso10126GrammarTests
{
    private const string Key = "0x(00112233445566778899AABBCCDDEEFF)";
    private const string Iv = "0x(000102030405060708090A0B0C0D0E0F)";

    private VariableDeclaration[] previousVariables = null!;
    private SyntaxError[] previousSyntaxErrors = null!;
    private bool previousSyntaxErrorOccurred;
    private bool previousLexerErrorOccurred;
    private string previousErrorMessage = null!;

    [SetUp]
    public void SetUp()
    {
        previousVariables = VariableDictionary.Instance().GetVariables().ToArray();
        previousSyntaxErrors = SyntaxErrorList.Instance().ToArray();
        previousSyntaxErrorOccurred = SyntaxErrorListner.SyntaxErrorOccured;
        previousLexerErrorOccurred = LexerErrorListener.LexerErrorOccured;
        previousErrorMessage = SyntaxErrorListner.ErrorMessage.ToString();

        VariableDictionary.Instance().Clear();
        SyntaxErrorList.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.ErrorMessage.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        VariableDictionary.Instance().Clear();
        foreach (VariableDeclaration variable in previousVariables)
        {
            VariableDictionary.Instance().Add(variable);
        }

        SyntaxErrorList.Instance().Clear();
        SyntaxErrorList.Instance().AddRange(previousSyntaxErrors);
        SyntaxErrorListner.SyntaxErrorOccured = previousSyntaxErrorOccurred;
        LexerErrorListener.LexerErrorOccured = previousLexerErrorOccurred;
        SyntaxErrorListner.ErrorMessage.Clear();
        SyntaxErrorListner.ErrorMessage.Append(previousErrorMessage);
    }

    [Test]
    public void LexerAndParserAcceptIso10126AsPadding()
    {
        CryptoScriptParser parser = ParserBuilder.StringBuild(
            "PARAM p=Parameters(AES-CBC,#PAD:ISO-10126)");
        parser.program();

        Assert.Multiple(() =>
        {
            Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
        });
    }

    [TestCase("ISO-10127")]
    [TestCase("UNKNOWN-PADDING")]
    public void LexerOrParserRejectsUnknownHyphenatedPaddingNames(string padding)
    {
        CryptoScriptParser parser = ParserBuilder.StringBuild(
            $"PARAM p=Parameters(AES-CBC,#PAD:{padding})");
        parser.program();

        Assert.That(
            LexerErrorListener.LexerErrorOccured || parser.NumberOfSyntaxErrors > 0,
            Is.True);
    }

    [Test]
    public void Iso10126InsideAStringRemainsAStringLiteral()
    {
        CryptoScriptParser parser = ParserBuilder.StringBuild("VAR value=\"ISO-10126\"");
        CryptoScriptParser.ProgramContext program = parser.program();

        Assert.Multiple(() =>
        {
            Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            Assert.That(program.statement(0).declaration().expression().NORMAL_STRING().GetText(),
                Is.EqualTo("\"ISO-10126\""));
        });
    }

    [Test]
    public void ParametersPreserveIso10126Padding()
    {
        var runner = new CryptoScriptRunner();
        CryptoScriptParser parser = ParserBuilder.StringBuild(
            $"PARAM p=Parameters(AES-CBC,#IV:{Iv},#PAD:ISO-10126)");

        CryptoScriptProgram result = runner.Execute(parser.program());
        var parameters = (ParameterVariableDeclaration)result.Statements.Single();

        Assert.That(parameters.GetParameter("PAD"), Is.EqualTo("ISO-10126"));
    }

    [Test]
    public void Iso10126EncryptDecryptRoundtripSupportsUnalignedPlaintext()
    {
        const string plaintext = "0x(00112233445566778899AABBCCDDEEFF00)";
        var runner = new CryptoScriptRunner();
        string script =
            $"KEY k=GenerateKey(AES-CBC,{Key}) " +
            $"PARAM p=Parameters(AES-CBC,#IV:{Iv},#PAD:ISO-10126) " +
            $"VAR encrypted=Encrypt(p,k,{plaintext}) " +
            "VAR decrypted=Decrypt(p,k,encrypted)";

        CryptoScriptParser parser = ParserBuilder.StringBuild(script);
        CryptoScriptProgram result = runner.Execute(parser.program());
        var encrypted = (StringVariableDeclaration)result.Statements[2];
        var decrypted = (StringVariableDeclaration)result.Statements[3];

        Assert.Multiple(() =>
        {
            Assert.That(FormatConversions.ToByteArray(encrypted.Value, FormatConversions.HEX).Length % 16, Is.Zero);
            Assert.That(decrypted.Value, Is.EqualTo(plaintext).IgnoreCase);
        });
    }
}
