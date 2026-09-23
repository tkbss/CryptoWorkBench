using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KdfEp2PanSurrogateTrxTests
{
    private const string Mechanism = "KDF-EP2-PAN-SURROGATE-TRX";
    private const string MerchantKey = "0123456789ABCDEF23456789ABCDEF01";
    private const string Info = "5413330089020011";
    private const string Expected = "AEA780CDFC3CDA67C0FD0D70D509C9B4C1DD1F40F0C05D922BFD3BC8A01E2E6E";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void FactoryMapsPanSurrogateTransactionKdf()
    {
        AlgorithmFactory.Create(Mechanism).Should().BeOfType<KDF_EP2_PAN_SURROGATE_TRX>();
        AlgorithmFactory.Create(Mechanism.ToLowerInvariant()).Should().BeOfType<KDF_EP2_PAN_SURROGATE_TRX>();
    }

    [Test]
    public void ParametersContainOnlyTheMechanism()
    {
        ParameterVariableDeclaration parameter = Execute($"PARAM p=Parameters({Mechanism})")
            .Statements[^1].Should().BeOfType<ParameterVariableDeclaration>().Subject;

        parameter.Mechanism.Should().Be(Mechanism);
        parameter.GetParameters().Keys.Should().BeEquivalentTo("#MECH");
    }

    [Test]
    public void DerivesEp2Section814PrimaryReferenceVectorFromHexLiteral()
    {
        KeyVariableDeclaration result = Derive($"0x({Info})");

        AssertReferenceResult(result);
    }

    [Test]
    public void DerivesReferenceVectorFromHexVariable()
    {
        CryptoScriptProgram program = Execute(
            $"VAR dolData=0x({Info}) " + ScriptPrefix() +
            "KEY output=Derive(p,merchant,dolData)");

        AssertReferenceResult(program.Statements[^1].Should().BeOfType<KeyVariableDeclaration>().Subject);
    }

    [Test]
    public void StringInfoRemainsUtf8AndIsNotInterpretedAsHex()
    {
        const string expectedUtf8 = "ACD801083F81BBB30BB16DC38AC8035D858B1B57E84970401F6A45407B33C604";

        KeyVariableDeclaration result = Derive($"\"{Info}\"");

        result.Value.Should().BeEquivalentTo($"0x({expectedUtf8})", options => options.IgnoringCase());
        result.Value.Should().NotBeEquivalentTo($"0x({Expected})", options => options.IgnoringCase());
    }

    [Test]
    public void AcceptsEmptyInfo()
    {
        const string expectedEmptyInfo = "0948D97EA78AB614AC24F05D2B435F31D77AAB7B11DBF07E3BD8AE699601A959";

        KeyVariableDeclaration result = Derive("\"\"");

        result.Value.Should().BeEquivalentTo($"0x({expectedEmptyInfo})", options => options.IgnoringCase());
    }

    [TestCase(15)]
    [TestCase(17)]
    [TestCase(32)]
    public void RejectsMerchantKeysThatAreNotExactly16Bytes(int byteLength)
    {
        string key = Convert.ToHexString(new byte[byteLength]);
        Action action = () => Execute(
            $"KEY merchant=GenerateKey(HMAC-SHA256,0x({key})) " +
            $"PARAM p=Parameters({Mechanism}) " +
            "KEY output=Derive(p,merchant,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                $"{Mechanism} merchant key must be exactly 16 bytes"));
    }

    [TestCase("#HASH:HASH-SHA256", "#HASH")]
    [TestCase("#SALT:0x(00)", "#SALT")]
    [TestCase("#OUTLEN:256", "#OUTLEN")]
    [TestCase("#VARIANT:TC", "#VARIANT")]
    [TestCase("#IV:0x(00)", "#IV")]
    public void RejectsEveryAdditionalParameter(string parameter, string expectedName)
    {
        Action action = () => Execute($"PARAM p=Parameters({Mechanism},{parameter})");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                $"does not support parameter {expectedName}"));
    }

    [TestCase($"KEY output=Derive(p,merchant)")]
    [TestCase($"KEY output=Derive(p,merchant,\"\",\"extra\")")]
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
            $"VAR merchant=0x({MerchantKey}) PARAM p=Parameters({Mechanism}) " +
            "KEY output=Derive(p,merchant,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("wrong key argument"));
    }

    private static KeyVariableDeclaration Derive(string info) =>
        Execute(ScriptPrefix() + $"KEY output=Derive(p,merchant,{info})")
            .Statements[^1].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static string ScriptPrefix() =>
        $"KEY merchant=GenerateKey(HMAC-SHA256,0x({MerchantKey})) " +
        $"PARAM p=Parameters({Mechanism}) ";

    private static void AssertReferenceResult(KeyVariableDeclaration result)
    {
        result.Value.Should().BeEquivalentTo($"0x({Expected})", options => options.IgnoringCase());
        FormatConversions.HexStringToByteArray(result.Value).Should().HaveCount(32);
        result.KeySize.Should().Be("256");
        result.KeySizeInBits.Should().Be(new KeySize(256));
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
