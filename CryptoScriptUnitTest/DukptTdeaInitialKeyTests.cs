using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DukptTdeaInitialKeyTests
{
    private const string Mechanism = "DUKPT-TDEA-INITIAL-KEY";
    private const string Bdk = "0123456789ABCDEFFEDCBA9876543210";
    private const string Ksn = "FFFF9876543210E00001";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void FactoryAndParametersExposeParameterlessMechanism()
    {
        AlgorithmFactory.Create(Mechanism).Should().BeOfType<DUKPT_TDEA_INITIAL_KEY>();
        ParameterVariableDeclaration parameter = Parameter($"PARAM p=Parameters({Mechanism})");
        parameter.Mechanism.Should().Be(Mechanism);
        parameter.GetParameters().Keys.Should().Equal("#MECH");
    }

    [Test]
    public void OfficialInitialKeyVectorMatchesAnsiX924()
    {
        KeyVariableDeclaration result = Derive(Bdk, Ksn);

        result.Value.Should().BeEquivalentTo("0x(6AC292FAA1315B4D858AB3A3D7D5933A)",
            options => options.IgnoringCase());
        result.KeySize.Should().Be("128");
        result.KeySizeInBits.Should().Be(new KeySize(128));
        result.KeyType.Should().Be(KeyType.Secret(KeyAlgorithm.Tdea));
        result.DerivationMechanism.Should().Be(Mechanism);
    }

    [Test]
    public void ClearsAllTwentyOneCounterBits()
    {
        Derive(Bdk, "FFFF9876543210FFFFFF").Value.Should().Be(Derive(Bdk, "FFFF9876543210E00000").Value);
        Convert.ToHexString(DUKPT_TDEA_INITIAL_KEY.BuildInputBlock(Convert.FromHexString("FFFF9876543210FFFFFF")))
            .Should().Be("FFFF9876543210E0");
    }

    [TestCase("0123456789ABCDEFFEDCBA98765432")]
    [TestCase("0123456789ABCDEFFEDCBA98765432100000000000000000")]
    public void RejectsBdkThatIsNotExactlySixteenBytes(string bdk)
    {
        Action action = () => DeriveWithRegisteredKey(bdk, Ksn);
        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("exactly 128 bits (16 bytes)"));
    }

    [TestCase("FFFF9876543210E000")]
    [TestCase("FFFF9876543210E0000100")]
    public void RejectsKsnThatIsNotExactlyTenBytes(string ksn)
    {
        Action action = () => Derive(Bdk, ksn);
        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("exactly 80 bits (10 bytes)"));
    }

    [TestCase("#OUTLEN:128")]
    [TestCase("#PRF:AES-CMAC")]
    [TestCase("#COUNTER:1")]
    public void RejectsAdditionalParameters(string additionalParameter)
    {
        Action action = () => Parameter($"PARAM p=Parameters({Mechanism},{additionalParameter})");
        action.Should().Throw<SemanticErrorException>();
    }

    [Test]
    public void RejectsUnknownKeyTypeParameterInGrammar()
    {
        var parser = ParserBuilder.StringBuild($"PARAM p=Parameters({Mechanism},#KEYTYPE:AES)");

        parser.program();

        Assert.That(SyntaxErrorListner.SyntaxErrorOccured || LexerErrorListener.LexerErrorOccured, Is.True);
    }

    [Test]
    public void ExistingAesDukptMechanismStillDerivesItsOfficialVector()
    {
        CryptoScriptProgram program = Execute(
            "KEY bdk=GenerateKey(AES-ECB,0x(FEDCBA9876543210F1F1F1F1F1F1F1F1)) " +
            "PARAM p=Parameters(DUKPT-AES-INITIAL-KEY) " +
            "KEY output=Derive(p,bdk,0x(1234567890123456))");

        ((KeyVariableDeclaration)program.Statements[2]).Value.Should().BeEquivalentTo(
            "0x(1273671EA26AC29AFA4D1084127652A1)", options => options.IgnoringCase());
    }

    [Test]
    public void DerivedTdeaInitialKeyRemainsUsableByDes3()
    {
        CryptoScriptProgram program = Execute(
            $"KEY bdk=GenerateKey(DES3-ECB,0x({Bdk})) " +
            $"PARAM derive=Parameters({Mechanism}) " +
            $"KEY initial=Derive(derive,bdk,0x({Ksn})) " +
            "PARAM encrypt=Parameters(DES3-ECB,#PAD:NONE) " +
            "VAR cipher=Encrypt(encrypt,initial,0x(4E6F772069732074))");

        program.Statements[^1].Should().BeOfType<StringVariableDeclaration>();
    }

    private static KeyVariableDeclaration Derive(string bdk, string ksn) =>
        Execute($"KEY bdk=GenerateKey(DES3-ECB,0x({bdk})) " +
                $"PARAM p=Parameters({Mechanism}) " +
                $"KEY output=Derive(p,bdk,0x({ksn}))")
            .Statements[2].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static KeyVariableDeclaration DeriveWithRegisteredKey(string bdk, string ksn)
    {
        string value = $"0x({bdk})";
        VariableDictionary.Instance().Add(new KeyVariableDeclaration
        {
            Id = "bdk", Value = value, KeyValue = value, ValueFormat = FormatConversions.HEX,
            Type = new CryptoTypeKey()
        });
        return Execute($"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,bdk,0x({ksn}))")
            .Statements[1].Should().BeOfType<KeyVariableDeclaration>().Subject;
    }

    private static ParameterVariableDeclaration Parameter(string script) =>
        Execute(script).Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;

    private static CryptoScriptProgram Execute(string input)
    {
        var parser = ParserBuilder.StringBuild(input);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
