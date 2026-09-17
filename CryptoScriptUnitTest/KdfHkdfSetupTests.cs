using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KdfHkdfSetupTests
{
    private static readonly object[] MaximumLengths =
    {
        new object[] { "HASH-SHA1", 20 * 255 * 8 },
        new object[] { "HASH-SHA224", 28 * 255 * 8 },
        new object[] { "HASH-SHA256", 32 * 255 * 8 },
        new object[] { "HASH-SHA384", 48 * 255 * 8 },
        new object[] { "HASH-SHA512", 64 * 255 * 8 },
        new object[] { "HASH-SHA512-224", 28 * 255 * 8 },
        new object[] { "HASH-SHA512-256", 32 * 255 * 8 },
        new object[] { "HASH-SHA3-224", 28 * 255 * 8 },
        new object[] { "HASH-SHA3-256", 32 * 255 * 8 },
        new object[] { "HASH-SHA3-384", 48 * 255 * 8 },
        new object[] { "HASH-SHA3-512", 64 * 255 * 8 }
    };

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void FactoryMapsKdfHkdfToSharedImplementation()
    {
        AlgorithmFactory.Create("KDF-HKDF").Should().BeOfType<CryptoScript.CryptoAlgorithm.KDF.KDF_HKDF>();
    }

    [TestCase("HASH-SHA256", 336)]
    [TestCase("HASH-SHA512", 512)]
    public void CreatesValidParameters(string hash, int outputLength)
    {
        var parameter = Execute(
            $"PARAM p=Parameters(KDF-HKDF,#HASH:{hash},#SALT:0x(00010203),#OUTLEN:{outputLength})")
            .Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;

        parameter.Mechanism.Should().Be("KDF-HKDF");
        parameter.GetParameter("HASH").Should().Be(hash);
        parameter.GetParameter("SALT").Should().Be("0x(00010203)");
        parameter.GetParameter("OUTLEN").Should().Be(outputLength.ToString());
        parameter.GetParameters().Keys.Should().BeEquivalentTo("#MECH", "#HASH", "#SALT", "#OUTLEN");
    }

    [Test]
    public void MissingSaltDoesNotCreateSaltParameter()
    {
        var parameter = Parameter("PARAM p=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#OUTLEN:256)");

        parameter.GetParameter("SALT").Should().BeEmpty();
        parameter.GetParameters().Should().NotContainKey("#SALT");
    }

    [TestCase("\"\"")]
    [TestCase("0x(00010203)")]
    [TestCase("b64(AAECAw==)")]
    public void AcceptsSupportedSaltLiterals(string salt)
    {
        Parameter($"PARAM p=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#SALT:{salt},#OUTLEN:256)")
            .GetParameter("SALT").Should().Be(salt);
    }

    [Test]
    public void ResolvesSaltFromVariable()
    {
        var parameter = Execute(
            "VAR salt=b64(AAECAw==) PARAM p=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#SALT:salt,#OUTLEN:256)")
            .Statements[1].Should().BeOfType<ParameterVariableDeclaration>().Subject;

        parameter.GetParameter("SALT").Should().Be("b64(AAECAw==)");
    }

    [TestCase("PARAM p=Parameters(KDF-HKDF,#OUTLEN:256)", "requires #HASH")]
    [TestCase("PARAM p=Parameters(KDF-HKDF,#HASH:HMAC-SHA256,#OUTLEN:256)", "supported HASH-*")]
    [TestCase("PARAM p=Parameters(KDF-HKDF,#HASH:\"HASH-SHA999\",#OUTLEN:256)", "supported HASH-*")]
    [TestCase("PARAM p=Parameters(KDF-HKDF,#HASH:HASH-SHA256)", "requires #OUTLEN")]
    [TestCase("PARAM p=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#OUTLEN:0)", "positive integer")]
    [TestCase("PARAM p=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#OUTLEN:7)", "divisible by 8")]
    [TestCase("PARAM p=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#OUTLEN:256,#IV:0x(0000000000000000))", "does not support parameter #IV")]
    public void RejectsInvalidParameterContracts(string script, string message)
    {
        Action action = () => Execute(script);

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(message));
    }

    [TestCaseSource(nameof(MaximumLengths))]
    public void AcceptsHashSpecificMaximumLength(string hash, int maximumBits)
    {
        Parameter($"PARAM p=Parameters(KDF-HKDF,#HASH:{hash},#OUTLEN:{maximumBits})")
            .GetParameter("OUTLEN").Should().Be(maximumBits.ToString());
    }

    [TestCaseSource(nameof(MaximumLengths))]
    public void RejectsLengthAboveHashSpecificMaximum(string hash, int maximumBits)
    {
        Action action = () => Parameter(
            $"PARAM p=Parameters(KDF-HKDF,#HASH:{hash},#OUTLEN:{maximumBits + 8})");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("RFC 5869 limit"));
    }

    [Test]
    public void NegativeLengthRemainsSyntacticallyInvalid()
    {
        var parser = ParserBuilder.StringBuild(
            "PARAM p=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#OUTLEN:-8)");

        parser.program();

        (parser.NumberOfSyntaxErrors > 0 || LexerErrorListener.LexerErrorOccured).Should().BeTrue();
    }

    private static ParameterVariableDeclaration Parameter(string input) =>
        Execute(input).Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;

    private static CryptoScriptProgram Execute(string input)
    {
        var parser = ParserBuilder.StringBuild(input);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
