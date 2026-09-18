using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KdfEp2PanReceiptTrxTests
{
    private const string Mechanism = "KDF-EP2-PAN-RECEIPT-TRX";
    private const string TerminalKey = "0123456789ABCDEF23456789ABCDEF01";
    private const string DolData = "1322042006015445524D3132333412345678A000000111";
    private const string Expected = "D37FF3ECC3F2EAC21EF3C968A9DE2934";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void FactoryMapsPanReceiptTransactionKdf()
    {
        AlgorithmFactory.Create(Mechanism).Should().BeOfType<KDF_EP2_PAN_RECEIPT_TRX>();
        AlgorithmFactory.Create(Mechanism.ToLowerInvariant()).Should().BeOfType<KDF_EP2_PAN_RECEIPT_TRX>();
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
    public void DerivesEp2Section812ReferenceVectorFromHexLiteral()
    {
        KeyVariableDeclaration result = Derive($"0x({DolData})");

        AssertReferenceResult(result);
    }

    [Test]
    public void DerivesReferenceVectorFromHexVariable()
    {
        CryptoScriptProgram program = Execute(
            $"VAR dolData=0x({DolData}) " + ScriptPrefix() +
            "KEY output=Derive(p,terminalKey,dolData)");

        AssertReferenceResult(program.Statements[^1].Should().BeOfType<KeyVariableDeclaration>().Subject);
    }

    [Test]
    public void DerivesReferenceVectorFromBase64DolData()
    {
        string base64 = Convert.ToBase64String(Convert.FromHexString(DolData));

        KeyVariableDeclaration result = Derive($"b64({base64})");

        AssertReferenceResult(result);
    }

    [Test]
    public void StringDolDataRemainsUtf8AndIsNotInterpretedAsHex()
    {
        const string expectedUtf8 = "3B81E9CFD18448E98954B752B56125C7";

        KeyVariableDeclaration result = Derive($"\"{DolData}\"");

        result.Value.Should().BeEquivalentTo($"0x({expectedUtf8})", options => options.IgnoringCase());
        result.Value.Should().NotBeEquivalentTo($"0x({Expected})", options => options.IgnoringCase());
    }

    [Test]
    public void AcceptsEmptyDolData()
    {
        const string expectedEmpty = "E5736E68E0F1DA85421DB119BC87198F";

        KeyVariableDeclaration result = Derive("\"\"");

        result.Value.Should().BeEquivalentTo($"0x({expectedEmpty})", options => options.IgnoringCase());
    }

    [TestCase(15)]
    [TestCase(17)]
    [TestCase(32)]
    public void RejectsTerminalKeysThatAreNotExactly16Bytes(int byteLength)
    {
        string key = Convert.ToHexString(new byte[byteLength]);
        Action action = () => Execute(
            $"KEY terminalKey=GenerateKey(HMAC-SHA256,0x({key})) " +
            $"PARAM p=Parameters({Mechanism}) " +
            "KEY output=Derive(p,terminalKey,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                $"{Mechanism} terminal key must be exactly 16 bytes"));
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

    [TestCase($"KEY output=Derive(p,terminalKey)")]
    [TestCase($"KEY output=Derive(p,terminalKey,\"\",\"extra\")")]
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
            $"VAR terminalKey=0x({TerminalKey}) PARAM p=Parameters({Mechanism}) " +
            "KEY output=Derive(p,terminalKey,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("wrong key argument"));
    }

    private static KeyVariableDeclaration Derive(string dolData) =>
        Execute(ScriptPrefix() + $"KEY output=Derive(p,terminalKey,{dolData})")
            .Statements[^1].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static string ScriptPrefix() =>
        $"KEY terminalKey=GenerateKey(HMAC-SHA256,0x({TerminalKey})) " +
        $"PARAM p=Parameters({Mechanism}) ";

    private static void AssertReferenceResult(KeyVariableDeclaration result)
    {
        result.Value.Should().BeEquivalentTo($"0x({Expected})", options => options.IgnoringCase());
        FormatConversions.HexStringToByteArray(result.Value).Should().HaveCount(16);
        result.KeySize.Should().Be("128");
        result.Value.Should().Be(result.KeyValue);
        result.ValueFormat.Should().Be(FormatConversions.HEX);
        result.Type.Should().BeOfType<CryptoTypeKey>();
        result.Mechanism.Should().BeEmpty();
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
