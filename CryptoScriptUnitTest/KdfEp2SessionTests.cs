using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KdfEp2SessionTests
{
    private const string SessionKey = "0123456789ABCDEF23456789ABCDEF01";
    private const string Salt = "0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345";

    private static readonly object[] VariantVectors =
    {
        new object[] { "TC", 32, "CDA5C89A6B4F073779AB3B882C5CDFEFE756621E2FD4C4AC46C9FFCB9915C5CC" },
        new object[] { "MAC-SEND", 32, "58CA2B7D3B3517F06F6DDEE849E086E209896BC0EB19C365B30F63C281B6B3E5" },
        new object[] { "MAC-RECEIVE", 32, "9D1B46684B2F286665CF2DDE45C49F13066F97C955BEFA948DA5D49BEE22865D" },
        new object[] { "ENCRYPTION", 16, "29DDFF143885D21CD2425D1FDDA2C229" },
        new object[] { "PIN", 16, "009466E104125701B680F1708A830906" },
        new object[] { "KEY-ENCRYPTION", 16, "81DA3CE9EC6416CAA557BFF9485E9E70" }
    };

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void FactoryMapsEp2SessionKdf()
    {
        AlgorithmFactory.Create("KDF-EP2-SESSION")
            .Should().BeOfType<CryptoScript.CryptoAlgorithm.KDF.KDF_EP2_SESSION>();
    }

    [TestCaseSource(nameof(VariantVectors))]
    public void DerivesEveryVariantUsingFixedReferenceValues(string variant, int outputLength, string expected)
    {
        KeyVariableDeclaration result = Derive(variant);

        result.Value.Should().BeEquivalentTo($"0x({expected})", options => options.IgnoringCase());
        FormatConversions.HexStringToByteArray(result.Value).Should().HaveCount(outputLength);
        result.KeySize.Should().Be((outputLength * 8).ToString());
        result.KeySizeInBits.Should().Be(new KeySize(outputLength * 8));
        result.KeyType.Should().Be(KeyType.Secret(KeyAlgorithm.Unknown));
        result.Value.Should().Be(result.KeyValue);
        result.ValueFormat.Should().Be(FormatConversions.HEX);
        result.Mechanism.Should().BeEmpty();
        result.DerivationMechanism.Should().Be("KDF-EP2-SESSION");
        result.Type.Should().BeOfType<CryptoTypeKey>();
    }

    [Test]
    public void ParametersContainOnlyMechanismSaltAndVariant()
    {
        ParameterVariableDeclaration parameter = Parameter(
            $"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({Salt}),#VARIANT:TC)");

        parameter.Mechanism.Should().Be("KDF-EP2-SESSION");
        parameter.GetParameter("SALT").Should().Be($"0x({Salt})");
        parameter.GetParameter("VARIANT").Should().Be("TC");
        parameter.GetParameters().Keys.Should().BeEquivalentTo("#MECH", "#SALT", "#VARIANT");
    }

    [Test]
    public void ResolvesSaltFromVariable()
    {
        CryptoScriptProgram program = Execute(
            $"VAR salt=0x({Salt}) " +
            "PARAM p=Parameters(KDF-EP2-SESSION,#SALT:salt,#VARIANT:TC)");

        ((ParameterVariableDeclaration)program.Statements[1]).GetParameter("SALT").Should().Be($"0x({Salt})");
    }

    [TestCase(15)]
    [TestCase(17)]
    public void RejectsInvalidSessionKeyLength(int byteLength)
    {
        string key = Convert.ToHexString(new byte[byteLength]);
        Action action = () => Execute(
            $"KEY session=GenerateKey(HMAC-SHA256,0x({key})) " +
            $"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({Salt}),#VARIANT:TC) " +
            "KEY output=Derive(p,session,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("session key must be exactly 16 bytes"));
    }

    [TestCase(31)]
    [TestCase(33)]
    public void RejectsInvalidSaltLength(int byteLength)
    {
        string salt = Convert.ToHexString(new byte[byteLength]);
        Action action = () => Parameter(
            $"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({salt}),#VARIANT:TC)");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("#SALT must be exactly 32 bytes"));
    }

    [TestCase($"PARAM p=Parameters(KDF-EP2-SESSION,#VARIANT:TC)", "requires #SALT")]
    [TestCase($"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({Salt}))", "requires #VARIANT")]
    [TestCase($"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({Salt}),#VARIANT:UNKNOWN)", "supported session-key variant")]
    [TestCase($"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({Salt}),#VARIANT:TC,#HASH:HASH-SHA256)", "does not support parameter #HASH")]
    [TestCase($"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({Salt}),#VARIANT:TC,#OUTLEN:256)", "does not support parameter #OUTLEN")]
    public void RejectsInvalidParameterContracts(string script, string expectedMessage)
    {
        Action action = () => Execute(script);

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(expectedMessage));
    }

    [TestCase(
        $"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({Salt}),#SALT:0x({Salt}),#VARIANT:TC)",
        "parameter #SALT must not be specified more than once")]
    [TestCase(
        $"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({Salt}),#VARIANT:TC,#VARIANT:PIN)",
        "parameter #VARIANT must not be specified more than once")]
    public void RejectsDuplicateParameters(string script, string expectedMessage)
    {
        Action action = () => Execute(script);

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(expectedMessage));
    }

    [TestCase("TC")]
    [TestCase("PIN")]
    [TestCase("ENCRYPTION")]
    public void SimpleVariantNamesRemainValidIdentifiersOutsideVariantParameters(string identifier)
    {
        CryptoScriptProgram program = Execute($"VAR {identifier}=\"ordinary identifier\"");

        program.Statements.Should().ContainSingle();
        VariableDictionary.Instance().Get(identifier).Should().NotBeNull();
    }

    [Test]
    public void RejectsNonEmptyDataArgument()
    {
        Action action = () => Execute(
            $"KEY session=GenerateKey(HMAC-SHA256,0x({SessionKey})) " +
            $"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({Salt}),#VARIANT:TC) " +
            "KEY output=Derive(p,session,\"user info\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("defines info through #VARIANT"));
    }

    private static KeyVariableDeclaration Derive(string variant) =>
        Execute(
            $"KEY session=GenerateKey(HMAC-SHA256,0x({SessionKey})) " +
            $"PARAM p=Parameters(KDF-EP2-SESSION,#SALT:0x({Salt}),#VARIANT:{variant}) " +
            "KEY output=Derive(p,session,\"\")")
        .Statements[^1].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static ParameterVariableDeclaration Parameter(string script) =>
        Execute(script).Statements[^1].Should().BeOfType<ParameterVariableDeclaration>().Subject;

    private static CryptoScriptProgram Execute(string input)
    {
        var parser = ParserBuilder.StringBuild(input);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
