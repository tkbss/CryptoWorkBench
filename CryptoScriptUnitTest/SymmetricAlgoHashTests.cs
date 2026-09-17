using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class SymmetricAlgoHashTests
{
    private const string Sha256Abc = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD";

    public sealed record HashVector(
        string Mechanism,
        string Expected,
        int ExpectedBytes);

    private static readonly HashVector[] KnownAnswerVectors =
    {
        // NIST CAVP byte-oriented ShortMsgKAT response files, Len = 24,
        // Msg = 616263 ("abc"). SHA-1/SHA-2 implement FIPS 180-4; SHA-3 implements FIPS 202.
        // The fixed response values were also independently cross-checked with OpenSSL 3.6.1.
        new("HASH-SHA1",
            "A9993E364706816ABA3E25717850C26C9CD0D89D", 20),
        new("HASH-SHA224",
            "23097D223405D8228642A477BDA255B32AADBCE4BDA0B3F7E36C9DA7", 28),
        new("HASH-SHA256",
            Sha256Abc, 32),
        new("HASH-SHA384",
            "CB00753F45A35E8BB5A03D699AC65007272C32AB0EDED1631A8B605A43FF5BED8086072BA1E7CC2358BAECA134C825A7", 48),
        new("HASH-SHA512",
            "DDAF35A193617ABACC417349AE20413112E6FA4E89A97EA20A9EEEE64B55D39A2192992A274FC1A836BA3C23A3FEEBBD454D4423643CE80E2A9AC94FA54CA49F", 64),
        new("HASH-SHA512-224",
            "4634270F707B6A54DAAE7530460842E20E37ED265CEEE9A43E8924AA", 28),
        new("HASH-SHA512-256",
            "53048E2681941EF99B2E29B76B4C7DABE4C2D0C634FC6D46E0E2F13107E7AF23", 32),
        new("HASH-SHA3-224",
            "E642824C3F8CF24AD09234EE7D3C766FC9A3A5168D0C94AD73B46FDF", 28),
        new("HASH-SHA3-256",
            "3A985DA74FE225B2045C172D6BD390BD855F086E3E9D525B46BFE24511431532", 32),
        new("HASH-SHA3-384",
            "EC01498288516FC926459F58E2C6AD8DF9B473CB0FC08C2596DA7CF0E49BE4B298D88CEA927AC7F539F1EDF228376D25", 48),
        new("HASH-SHA3-512",
            "B751850B1A57168A5693CD924B6B096E08F621827444F70D884F5D0240D2712E10E116E9192AF3C91A7EC57647E3934057340B4CF408D5A56592F8274EEC53F0", 64)
    };

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCaseSource(nameof(KnownAnswerVectors))]
    public void Hash_MatchesNistShortMessageKnownAnswerVector(HashVector vector)
    {
        CryptoScriptProgram result = Execute(
            $"PARAM p=Parameters({vector.Mechanism}) " +
            "VAR hash=Hash(p,\"abc\")");

        var hash = result.Statements[1].Should().BeOfType<StringVariableDeclaration>().Subject;
        hash.Value.Should().BeEquivalentTo($"0x({vector.Expected})", options => options.IgnoringCase());
        hash.Type.Should().BeOfType<CryptoTypeVar>();
        hash.ValueFormat.Should().Be(FormatConversions.HEX);
        FormatConversions.HexStringToByteArray(hash.Value).Should().HaveCount(vector.ExpectedBytes);
        VariableDictionary.Instance().Get("hash").Should().BeSameAs(hash);
    }

    [Test]
    public void Hash_AcceptsHexData()
    {
        AssertSha256Abc("0x(616263)");
    }

    [Test]
    public void Hash_AcceptsBase64Data()
    {
        AssertSha256Abc("b64(YWJj)");
    }

    [Test]
    public void Hash_AcceptsDataFromVar()
    {
        CryptoScriptProgram result = Execute(
            "PARAM p=Parameters(HASH-SHA256) " +
            "VAR data=\"abc\" " +
            "VAR hash=Hash(p,data)");

        ((StringVariableDeclaration)result.Statements[2]).Value.Should().BeEquivalentTo(
            $"0x({Sha256Abc})", options => options.IgnoringCase());
    }

    [Test]
    public void Hash_AcceptsEmptyStringData()
    {
        // NIST CAVP SHA-256 ShortMsgKAT, Len = 0.
        const string expected = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
        CryptoScriptProgram result = Execute(
            "PARAM p=Parameters(HASH-SHA256) " +
            "VAR hash=Hash(p,\"\")");

        ((StringVariableDeclaration)result.Statements[1]).Value.Should().BeEquivalentTo(
            $"0x({expected})", options => options.IgnoringCase());
    }

    [TestCase("VAR hash=Hash(p)")]
    [TestCase("VAR hash=Hash(p,\"abc\",\"extra\")")]
    public void Hash_RejectsWrongArgumentCount(string call)
    {
        Action act = () => Execute($"PARAM p=Parameters(HASH-SHA256) {call}");

        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("wrong number of arguments"));
    }

    [Test]
    public void Hash_RejectsMissingParameterArgument()
    {
        Action act = () => Execute("VAR data=\"abc\" VAR hash=Hash(data,\"abc\")");

        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("Missing argument of type PARAM"));
    }

    [Test]
    public void Hash_RejectsUnsupportedDataFormat()
    {
        Action act = () => Execute(
            "PARAM p=Parameters(HASH-SHA256) " +
            "VAR hash=Hash(p,123)");

        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("wrong data argument"));
    }

    private static void AssertSha256Abc(string data)
    {
        CryptoScriptProgram result = Execute(
            "PARAM p=Parameters(HASH-SHA256) " +
            $"VAR hash=Hash(p,{data})");

        ((StringVariableDeclaration)result.Statements[1]).Value.Should().BeEquivalentTo(
            $"0x({Sha256Abc})", options => options.IgnoringCase());
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
