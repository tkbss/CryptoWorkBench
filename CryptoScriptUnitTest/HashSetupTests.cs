using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class HashSetupTests
{
    private static readonly string[] HashMechanisms =
    {
        "HASH-SHA1", "HASH-SHA224", "HASH-SHA256", "HASH-SHA384", "HASH-SHA512",
        "HASH-SHA512-224", "HASH-SHA512-256",
        "HASH-SHA3-224", "HASH-SHA3-256", "HASH-SHA3-384", "HASH-SHA3-512"
    };

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCaseSource(nameof(HashMechanisms))]
    public void AlgorithmFactory_MapsEveryHashMechanismToSharedImplementation(string mechanism)
    {
        AlgorithmFactory.Create(mechanism)
            .Should().BeOfType<CryptoScript.CryptoAlgorithm.HASH.HASH>();
    }

    [TestCaseSource(nameof(HashMechanisms))]
    public void Parameters_CreateMinimalHashParameterObject(string mechanism)
    {
        CryptoScriptProgram result = Execute($"PARAM p=Parameters({mechanism})");

        var parameter = result.Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;
        parameter.Type.Should().BeOfType<CryptoTypeParameters>();
        parameter.Mechanism.Should().Be(mechanism);
        parameter.Value.Should().Be($"#MECH:{mechanism}");
        parameter.ValueFormat.Should().Be(FormatConversions.PAR);
        parameter.GetParameter("MECH").Should().Be(mechanism);
        parameter.GetParameter("IV").Should().BeEmpty();
        parameter.GetParameter("PAD").Should().BeEmpty();
        parameter.GetParameter("MACLEN").Should().BeEmpty();
        parameter.GetParameters().Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string>("#MECH", mechanism));
        VariableDictionary.Instance().Get("p").Should().BeSameAs(parameter);
    }

    [Test]
    public void AlgorithmFactory_DoesNotMapUnknownHashPrefixToHashImplementation()
    {
        AlgorithmFactory.Create("HASH-SHA999")
            .Should().NotBeOfType<CryptoScript.CryptoAlgorithm.HASH.HASH>();
    }

    [Test]
    public void Parameters_UnknownHashMechanismRemainsInvalidCryptoScript()
    {
        var parser = ParserBuilder.StringBuild("PARAM p=Parameters(HASH-SHA999)");

        parser.program();

        (parser.NumberOfSyntaxErrors > 0 || LexerErrorListener.LexerErrorOccured)
            .Should().BeTrue();
    }

    [Test]
    public void Parameters_RejectAdditionalHashParameters()
    {
        Action act = () => Execute("PARAM p=Parameters(HASH-SHA256,#IV:0x(0011223344556677))");

        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("HASH does not support additional parameters."));
    }

    [Test]
    public void GenerateKey_IsNotSupportedForHashMechanisms()
    {
        Action act = () => Execute("KEY k=GenerateKey(HASH-SHA256,256)");

        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("HASH does not support key generation."));
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
