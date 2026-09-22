using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KdfSp800108CounterTests
{
    // Expected results were independently generated with Python cryptography 46.0.1 / hashlib
    // and OpenSSL 3.6.1 CMAC.
    // The official NIST CAVP CounterMode.zip vectors were reviewed, but use an opaque
    // FixedInputData field rather than CryptoScript's mandated Label || 00 || Context || [L]32
    // encoding and therefore cannot be represented exactly by this API. These fixed answers
    // cover CryptoScript's exact encoding without reproducing the production algorithm in tests.
    private const string Key256 = "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void FactoryAndParametersUseDefaultCounterLength()
    {
        AlgorithmFactory.Create("KDF-SP800-108-COUNTER").Should().BeOfType<KDF_SP800_108_COUNTER>();
        ParameterVariableDeclaration parameter = Parameter(
            "PARAM p=Parameters(KDF-SP800-108-COUNTER,#PRF:HMAC-SHA256,#OUTLEN:128)");

        parameter.Mechanism.Should().Be("KDF-SP800-108-COUNTER");
        parameter.GetParameter("COUNTER").Should().Be("32");
        parameter.GetParameters().Keys.Should().BeEquivalentTo("#MECH", "#PRF", "#OUTLEN", "#COUNTER");
    }

    [Test]
    public void HmacSha256OneBlockMatchesIndependentAnswer()
    {
        KeyVariableDeclaration result = Derive(
            "3EDC6B5B8F7AADBD713732B482B8F979286E1EA3B8F8F99C30C884CFE3349B83",
            "#PRF:HMAC-SHA256,#OUTLEN:128,#LABEL:\"label\"", "\"context\"");

        AssertResult(result, "B8E56EF25C9E6D6D97D2170AAA905FAF", 128);
    }

    [Test]
    public void HmacSha256MultipleBlocksAndSixteenBitCounterMatchIndependentAnswer()
    {
        KeyVariableDeclaration result = Derive(Key256,
            "#PRF:HMAC-SHA256,#OUTLEN:512,#COUNTER:16,#LABEL:\"purpose\"", "\"client-server\"");

        AssertResult(result,
            "37B06A172FF1AEB6D615D0CA4391598976D2F8CDB5D16CC034EEEE20A3601500" +
            "D099B05283FE4800D56996E7A4096072798FCA53C144E9BCE395258882A59445", 512);
    }

    [Test]
    public void TruncatesLastBlockAtByteBoundaryWithEmptyLabelAndTwentyFourBitCounter()
    {
        KeyVariableDeclaration result = Derive(string.Concat(Enumerable.Repeat("0B", 32)),
            "#PRF:HMAC-SHA256,#OUTLEN:264,#COUNTER:24,#LABEL:\"\"", "\"ctx\"");

        AssertResult(result, "02E6DF72EBAE298355CD864B147A0E9F496FE132F6E392A9F166CD5554D298B3B9", 264);
    }

    [Test]
    public void SupportsEmptyLabelAndContextWithEightBitCounter()
    {
        KeyVariableDeclaration result = Derive(new string('A', 64),
            "#PRF:HMAC-SHA256,#OUTLEN:256,#COUNTER:8", "\"\"");

        AssertResult(result, "2E9F8B621E49BCFA1CB8A9FFA84887B0C0BEC8FF262595C07236410D677B3AB6", 256);
    }

    [Test]
    public void EightBitCounterAllowsExactly255HmacSha256Blocks()
    {
        KeyVariableDeclaration result = Derive(Key256,
            "#PRF:HMAC-SHA256,#OUTLEN:65280,#COUNTER:8", "\"\"");

        result.Value.Should().StartWith("0x(").And.EndWith(")");
        result.Value[3..^1].Should().HaveLength(65280 / 4);
        result.KeySize.Should().Be("65280");
        result.DerivationMechanism.Should().Be("KDF-SP800-108-COUNTER");
    }

    [TestCase("2B7E151628AED2A6ABF7158809CF4F3C",
        "64EC377C7A14A3D931E7BFD71840A493BC6012B38EC96C68A048053D0D35705B")]
    [TestCase("8E73B0F7DA0E6452C810F32B809079E562F8EAD2522C6B7B",
        "5348E2B69BC841D1FB2C625FABAB9FE63FBCFF68AA5C6EA1EC8B5D34F4EE6A13")]
    [TestCase("603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4",
        "CB32E1B4E6AAF8EA39369DBAD86E8F0AA4DC33AE20C9A63A4A0235BFC2D347FA")]
    public void AesCmacWithSupportedKinLengthMatchesIndependentAnswer(string key, string expected)
    {
        KeyVariableDeclaration result = Derive(key,
            "#PRF:AES-CMAC,#OUTLEN:256,#COUNTER:32,#LABEL:\"aes-label\"", "0x(0102030405)", "AES-CMAC");

        AssertResult(result, expected, 256);
    }

    [TestCase("#PRF:HMAC-SHA256,#OUTLEN:128,#COUNTER:7", "#COUNTER must be 8, 16, 24 or 32")]
    [TestCase("#PRF:HMAC-SHA256,#OUTLEN:0", "#OUTLEN must be greater than zero")]
    [TestCase("#PRF:HMAC-SHA256,#OUTLEN:7", "#OUTLEN must be divisible by 8")]
    // HMAC-SHA256 produces 256-bit blocks: r=8 permits 255 blocks (65280 bits), not 256 (65536 bits).
    [TestCase("#PRF:HMAC-SHA256,#OUTLEN:65536,#COUNTER:8", "n <= 2^r - 1")]
    [TestCase("#PRF:\"HMAC-SHA999\",#OUTLEN:128", "supported HMAC-*")]
    [TestCase("#PRF:DES3-CMAC,#OUTLEN:128", "supported HMAC-*")]
    public void RejectsInvalidParameterContracts(string parameters, string message)
    {
        Action action = () => Parameter($"PARAM p=Parameters(KDF-SP800-108-COUNTER,{parameters})");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(message));
    }

    [Test]
    public void RejectsInvalidAesCmacKinLength()
    {
        RegisterKey("kin", "00112233445566778899AABBCCDDEE", "AES-CMAC");

        Action action = () => Execute(
            "PARAM p=Parameters(KDF-SP800-108-COUNTER,#PRF:AES-CMAC,#OUTLEN:128) " +
            "KEY output=Derive(p,kin,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("128, 192 or 256 bits"));
    }

    [Test]
    public void RejectsManipulatedUnknownPrfDuringDerivation()
    {
        RegisterKey("kin", Key256, "HMAC-SHA256");
        var parameter = new KDF_SP800_108_COUNTER().GenerateParameters(
            "KDF-SP800-108-COUNTER", new[] { "#PRF:HMAC-SHA256", "#OUTLEN:128" });
        parameter.Id = "p";
        parameter.SetParameter("PRF", "HMAC-SHA999");
        VariableDictionary.Instance().Add(parameter);

        Action action = () => Execute("KEY output=Derive(p,kin,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("unknown or not permitted"));
    }

    private static KeyVariableDeclaration Derive(string key, string parameters, string context,
        string keyMechanism = "HMAC-SHA256") =>
        Execute($"KEY kin=GenerateKey({keyMechanism},0x({key})) " +
                $"PARAM p=Parameters(KDF-SP800-108-COUNTER,{parameters}) " +
                $"KEY output=Derive(p,kin,{context})")
            .Statements[2].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static ParameterVariableDeclaration Parameter(string script) =>
        Execute(script).Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;

    private static void RegisterKey(string id, string hex, string mechanism)
    {
        string value = $"0x({hex})";
        VariableDictionary.Instance().Add(new KeyVariableDeclaration
        {
            Id = id, Value = value, KeyValue = value, ValueFormat = FormatConversions.HEX,
            KeySize = (hex.Length * 4).ToString(), Mechanism = mechanism, Type = new CryptoTypeKey()
        });
    }

    private static void AssertResult(KeyVariableDeclaration result, string expectedHex, int bits)
    {
        result.Value.Should().BeEquivalentTo($"0x({expectedHex})", options => options.IgnoringCase());
        result.KeyValue.Should().Be(result.Value);
        result.ValueFormat.Should().Be(FormatConversions.HEX);
        result.KeySize.Should().Be(bits.ToString());
        result.KeySizeInBits.Should().Be(new KeySize(bits));
        result.KeyType.Should().Be(KeyType.Secret(KeyAlgorithm.Unknown));
        result.DerivationMechanism.Should().Be("KDF-SP800-108-COUNTER");
        result.Type.Should().BeOfType<CryptoTypeKey>();
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
