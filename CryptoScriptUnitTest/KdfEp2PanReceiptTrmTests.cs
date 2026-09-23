using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KdfEp2PanReceiptTrmTests
{
    private const string Mechanism = "KDF-EP2-PAN-RECEIPT-TRM";
    private const string AcquirerKey = "0123456789ABCDEF23456789ABCDEF01";
    private const string Salt = "0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345";
    private const string TerminalProperties = "5445524D313233340345ABC56876FF1900BCDEF3FCD35914";
    private const string Expected = "F9B286738F4A3848C20C7CB12D3886E7";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void FactoryMapsPanReceiptTerminalKdf()
    {
        AlgorithmFactory.Create(Mechanism).Should().BeOfType<KDF_EP2_PAN_RECEIPT_TRM>();
        AlgorithmFactory.Create(Mechanism.ToLowerInvariant()).Should().BeOfType<KDF_EP2_PAN_RECEIPT_TRM>();
    }

    [Test]
    public void ParametersContainOnlyMechanismAndSalt()
    {
        ParameterVariableDeclaration parameter = Parameter(
            $"PARAM p=Parameters({Mechanism},#SALT:0x({Salt}))");

        parameter.Mechanism.Should().Be(Mechanism);
        parameter.GetParameter("SALT").Should().Be($"0x({Salt})");
        parameter.GetParameters().Keys.Should().BeEquivalentTo("#MECH", "#SALT");
    }

    [Test]
    public void ResolvesSaltFromVariable()
    {
        CryptoScriptProgram program = Execute(
            $"VAR salt=0x({Salt}) " +
            $"PARAM p=Parameters({Mechanism},#SALT:salt)");

        ((ParameterVariableDeclaration)program.Statements[1]).GetParameter("SALT")
            .Should().Be($"0x({Salt})");
    }

    [Test]
    public void DerivesEp2Section813ReferenceVectorFromHexLiteral()
    {
        KeyVariableDeclaration result = Derive($"0x({TerminalProperties})");

        AssertReferenceResult(result);
    }

    [Test]
    public void DerivesReferenceVectorFromHexVariable()
    {
        CryptoScriptProgram program = Execute(
            $"VAR properties=0x({TerminalProperties}) " + ScriptPrefix() +
            "KEY output=Derive(p,acquirerKey,properties)");

        AssertReferenceResult(program.Statements[^1].Should().BeOfType<KeyVariableDeclaration>().Subject);
    }

    [Test]
    public void DerivesReferenceVectorFromBase64TerminalProperties()
    {
        string base64 = Convert.ToBase64String(Convert.FromHexString(TerminalProperties));

        KeyVariableDeclaration result = Derive($"b64({base64})");

        AssertReferenceResult(result);
    }

    [Test]
    public void StringTerminalPropertiesRemainUtf8AndAreNotInterpretedAsHex()
    {
        const string expectedUtf8 = "6C28AFAD39E00FCFAE005E835BDD8D67";

        KeyVariableDeclaration result = Derive($"\"{TerminalProperties}\"");

        result.Value.Should().BeEquivalentTo($"0x({expectedUtf8})", options => options.IgnoringCase());
        result.Value.Should().NotBeEquivalentTo($"0x({Expected})", options => options.IgnoringCase());
    }

    [Test]
    public void AcceptsEmptyTerminalProperties()
    {
        const string expectedEmpty = "8FA6B43A3EF352BF6A9E16BB5627EDB6";

        KeyVariableDeclaration result = Derive("\"\"");

        result.Value.Should().BeEquivalentTo($"0x({expectedEmpty})", options => options.IgnoringCase());
    }

    [TestCase(15)]
    [TestCase(17)]
    [TestCase(32)]
    public void RejectsAcquirerKeysThatAreNotExactly16Bytes(int byteLength)
    {
        string key = Convert.ToHexString(new byte[byteLength]);
        Action action = () => Execute(
            $"KEY acquirerKey=GenerateKey(HMAC-SHA256,0x({key})) " +
            $"PARAM p=Parameters({Mechanism},#SALT:0x({Salt})) " +
            "KEY output=Derive(p,acquirerKey,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                $"{Mechanism} acquirer key must be exactly 16 bytes"));
    }

    [TestCase(31)]
    [TestCase(33)]
    public void RejectsSaltThatIsNotExactly32Bytes(int byteLength)
    {
        string salt = Convert.ToHexString(new byte[byteLength]);
        Action action = () => Parameter(
            $"PARAM p=Parameters({Mechanism},#SALT:0x({salt}))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                $"{Mechanism} #SALT must be exactly 32 bytes"));
    }

    [Test]
    public void RejectsMissingSalt()
    {
        Action action = () => Parameter($"PARAM p=Parameters({Mechanism})");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("requires #SALT"));
    }

    [Test]
    public void RejectsDuplicateSalt()
    {
        Action action = () => Parameter(
            $"PARAM p=Parameters({Mechanism},#SALT:0x({Salt}),#SALT:0x({Salt}))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                "parameter #SALT must not be specified more than once"));
    }

    [Test]
    public void RejectsInvalidParameterFormat()
    {
        var algorithm = new KDF_EP2_PAN_RECEIPT_TRM();

        Action action = () => algorithm.GenerateParameters(Mechanism, ["#SALT"]);

        action.Should().Throw<ArgumentException>()
            .WithMessage($"Invalid {Mechanism} parameter: #SALT.*");
    }

    [TestCase("#HASH:HASH-SHA256", "#HASH")]
    [TestCase("#OUTLEN:128", "#OUTLEN")]
    [TestCase("#VARIANT:TC", "#VARIANT")]
    [TestCase("#IV:0x(00)", "#IV")]
    public void RejectsEveryAdditionalParameter(string parameter, string expectedName)
    {
        Action action = () => Parameter(
            $"PARAM p=Parameters({Mechanism},#SALT:0x({Salt}),{parameter})");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                $"does not support parameter {expectedName}"));
    }

    [TestCase($"KEY output=Derive(p,acquirerKey)")]
    [TestCase($"KEY output=Derive(p,acquirerKey,\"\",\"extra\")")]
    public void RejectsWrongDeriveArgumentCount(string derive)
    {
        Action action = () => Execute(ScriptPrefix() + derive);

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("wrong number of arguments"));
    }

    [Test]
    public void RejectsNonKeySecondArgument()
    {
        Action action = () => Execute(
            $"VAR acquirerKey=0x({AcquirerKey}) " +
            $"PARAM p=Parameters({Mechanism},#SALT:0x({Salt})) " +
            "KEY output=Derive(p,acquirerKey,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("wrong key argument"));
    }

    private static KeyVariableDeclaration Derive(string terminalProperties) =>
        Execute(ScriptPrefix() + $"KEY output=Derive(p,acquirerKey,{terminalProperties})")
            .Statements[^1].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static string ScriptPrefix() =>
        $"KEY acquirerKey=GenerateKey(HMAC-SHA256,0x({AcquirerKey})) " +
        $"PARAM p=Parameters({Mechanism},#SALT:0x({Salt})) ";

    private static ParameterVariableDeclaration Parameter(string script) =>
        Execute(script).Statements[^1].Should().BeOfType<ParameterVariableDeclaration>().Subject;

    private static void AssertReferenceResult(KeyVariableDeclaration result)
    {
        result.Value.Should().BeEquivalentTo($"0x({Expected})", options => options.IgnoringCase());
        FormatConversions.HexStringToByteArray(result.Value).Should().HaveCount(16);
        result.KeySize.Should().Be("128");
        result.KeySizeInBits.Should().Be(new KeySize(128));
        result.KeyType.Should().Be(KeyType.Secret(KeyAlgorithm.Unknown));
        result.Value.Should().Be(result.KeyValue);
        result.ValueFormat.Should().Be(FormatConversions.HEX);
        result.Type.Should().BeOfType<CryptoTypeKey>();
        result.DerivationMechanism.Should().Be(Mechanism);
        result.KeyAttributes.Should().BeEmpty();
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
