using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class HmacSetupTests
{
    private static readonly string[] HmacMechanisms =
    {
        "HMAC-SHA1", "HMAC-SHA224", "HMAC-SHA256", "HMAC-SHA384", "HMAC-SHA512",
        "HMAC-SHA512-224", "HMAC-SHA512-256",
        "HMAC-SHA3-224", "HMAC-SHA3-256", "HMAC-SHA3-384", "HMAC-SHA3-512"
    };

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCaseSource(nameof(HmacMechanisms))]
    public void AlgorithmFactory_MapsEveryHmacMechanismToSharedImplementation(string mechanism)
    {
        AlgorithmFactory.Create(mechanism)
            .Should().BeOfType<CryptoScript.CryptoAlgorithm.HMAC.HMAC>();
    }

    [TestCaseSource(nameof(HmacMechanisms))]
    public void Parameters_CreateMinimalHmacParameterObject(string mechanism)
    {
        CryptoScriptProgram result = Execute($"PARAM p=Parameters({mechanism})");

        var parameter = result.Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;
        parameter.Mechanism.Should().Be(mechanism);
        parameter.GetParameter("MECH").Should().Be(mechanism);
        parameter.GetParameter("IV").Should().BeEmpty();
        parameter.GetParameter("PAD").Should().BeEmpty();
        parameter.GetParameter("MACLEN").Should().BeEmpty();
        parameter.GetParameters().Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string>("#MECH", mechanism));
        VariableDictionary.Instance().Get("p").Should().BeSameAs(parameter);
    }

    [TestCaseSource(nameof(HmacMechanisms))]
    public void GenerateKey_CreatesRequestedRandomKeyForEveryHmacMechanism(string mechanism)
    {
        CryptoScriptProgram result = Execute(
            $"KEY first=GenerateKey({mechanism},136) " +
            $"KEY second=GenerateKey({mechanism},136)");

        var first = result.Statements[0].Should().BeOfType<KeyVariableDeclaration>().Subject;
        var second = result.Statements[1].Should().BeOfType<KeyVariableDeclaration>().Subject;

        first.Mechanism.Should().Be(mechanism);
        first.KeySize.Should().Be("136");
        first.ValueFormat.Should().Be(FormatConversions.HEX);
        first.KeyValue.Should().Be(first.Value);
        FormatConversions.HexStringToByteArray(first.Value).Should().HaveCount(17);
        second.Value.Should().NotBe(first.Value);
        VariableDictionary.Instance().Get("first").Should().BeSameAs(first);
    }

    [Test]
    public void GenerateKey_HmacSha256_CreatesRequested256BitKey()
    {
        CryptoScriptProgram result = Execute("KEY k=GenerateKey(HMAC-SHA256,256)");

        var key = result.Statements[0].Should().BeOfType<KeyVariableDeclaration>().Subject;
        key.KeySize.Should().Be("256");
        FormatConversions.HexStringToByteArray(key.Value).Should().HaveCount(32);
    }

    [TestCase(0)]
    [TestCase(129)]
    public void GenerateKey_RejectsNonPositiveOrNonByteAlignedLengths(int keySize)
    {
        Action act = () => Execute($"KEY k=GenerateKey(HMAC-SHA256,{keySize})");

        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("positive multiple of 8 bits"));
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
