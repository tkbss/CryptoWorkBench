using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DukptAesInitialKeyTests
{
    private const string Mechanism = "DUKPT-AES-INITIAL-KEY";
    private const string AnnexBBdk = "FEDCBA9876543210F1F1F1F1F1F1F1F1";
    private const string AnnexBIkid = "1234567890123456";

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
        AlgorithmFactory.Create(Mechanism).Should().BeOfType<DUKPT_AES_INITIAL_KEY>();

        ParameterVariableDeclaration parameter = Parameter($"PARAM p=Parameters({Mechanism})");

        parameter.Mechanism.Should().Be(Mechanism);
        parameter.GetParameters().Keys.Should().Equal("#MECH");
    }

    [Test]
    public void AnnexBAes128InitialKeyMatchesOfficialX924Vector()
    {
        KeyVariableDeclaration result = Derive(AnnexBBdk, AnnexBIkid);

        AssertResult(result, "1273671EA26AC29AFA4D1084127652A1", 128);
    }

    // Independent reference generated with OpenSSL 3.6.1 AES-192-ECB, not an Annex B vector.
    [Test]
    public void Aes192DerivationUsesTwoBlocksAndTruncatesTo24Bytes()
    {
        KeyVariableDeclaration result = Derive(
            "8E73B0F7DA0E6452C810F32B809079E562F8EAD2522C6B7B", AnnexBIkid);

        AssertResult(result,
            "3B1C5B8720988B8E0AD9E52642A92D5CE331ABBFD80DE1C6", 192);
        result.Value[3..^1].Should().HaveLength(48);
    }

    // Independent reference generated with OpenSSL 3.6.1 AES-256-ECB, not an Annex B vector.
    [Test]
    public void Aes256DerivationUsesTwoBlocks()
    {
        KeyVariableDeclaration result = Derive(
            "603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4",
            "FFEEDDCCBBAA9988");

        AssertResult(result,
            "10F8D4F35628FBE0B1D07DE359CAE24B4374A6D4749B4F5F6AC717649E9B3452", 256);
    }

    [TestCase(16, "1234567890123456", 1, "01018001000200801234567890123456")]
    [TestCase(24, "1234567890123456", 1, "01018001000300C01234567890123456")]
    [TestCase(24, "1234567890123456", 2, "01028001000300C01234567890123456")]
    [TestCase(32, "FFEEDDCCBBAA9988", 1, "0101800100040100FFEEDDCCBBAA9988")]
    [TestCase(32, "FFEEDDCCBBAA9988", 2, "0102800100040100FFEEDDCCBBAA9988")]
    public void BuildsX924DerivationData(int keyBytes, string ikid, byte counter, string expected)
    {
        byte[] result = DUKPT_AES_INITIAL_KEY.BuildDerivationData(
            keyBytes, Convert.FromHexString(ikid), counter);

        Convert.ToHexString(result).Should().Be(expected);
    }

    [TestCase("12345678901234")]
    [TestCase("123456789012345678")]
    public void RejectsIkidThatIsNotExactlyEightBytes(string ikid)
    {
        Action action = () => Derive(AnnexBBdk, ikid);

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("exactly 64 bits (8 bytes)"));
    }

    [Test]
    public void RejectsEmptyIkid()
    {
        RegisterKey("bdk", AnnexBBdk);

        Action action = () => Execute(
            $"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,bdk,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("IKID"));
    }

    [TestCase("00112233445566778899AABBCCDDEE")]
    [TestCase("00112233445566778899AABBCCDDEEFF00AA")]
    public void RejectsInvalidBdkLength(string bdk)
    {
        RegisterKey("bdk", bdk);

        Action action = () => Execute(
            $"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,bdk,0x({AnnexBIkid}))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("128, 192 or 256 bits"));
    }

    [Test]
    public void RejectsEmptyBdk()
    {
        VariableDictionary.Instance().Add(new KeyVariableDeclaration
        {
            Id = "bdk", Value = "0x()", KeyValue = "0x()", ValueFormat = FormatConversions.HEX,
            Type = new CryptoTypeKey()
        });

        Action action = () => Execute(
            $"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,bdk,0x({AnnexBIkid}))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("128, 192 or 256 bits"));
    }

    [TestCase("#OUTLEN:128")]
    [TestCase("#PRF:AES-CMAC")]
    [TestCase("#LABEL:\"label\"")]
    [TestCase("#COUNTER:8")]
    public void RejectsAdditionalParameters(string additionalParameter)
    {
        Action action = () => Parameter(
            $"PARAM p=Parameters({Mechanism},{additionalParameter})");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("does not support additional parameters"));
    }

    private static KeyVariableDeclaration Derive(string bdk, string ikid) =>
        Execute($"KEY bdk=GenerateKey(AES-ECB,0x({bdk})) " +
                $"PARAM p=Parameters({Mechanism}) " +
                $"KEY output=Derive(p,bdk,0x({ikid}))")
            .Statements[2].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static ParameterVariableDeclaration Parameter(string script) =>
        Execute(script).Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;

    private static void RegisterKey(string id, string hex)
    {
        string value = $"0x({hex})";
        VariableDictionary.Instance().Add(new KeyVariableDeclaration
        {
            Id = id, Value = value, KeyValue = value, ValueFormat = FormatConversions.HEX,
            KeySize = (hex.Length * 4).ToString(), Type = new CryptoTypeKey()
        });
    }

    private static void AssertResult(KeyVariableDeclaration result, string expectedHex, int bits)
    {
        result.Value.Should().BeEquivalentTo($"0x({expectedHex})", options => options.IgnoringCase());
        result.KeyValue.Should().Be(result.Value);
        result.ValueFormat.Should().Be(FormatConversions.HEX);
        result.KeySize.Should().Be(bits.ToString());
        result.KeySizeInBits.Should().Be(new KeySize(bits));
        result.KeyType.Should().Be(KeyType.Secret(KeyAlgorithm.Aes));
        result.DerivationMechanism.Should().Be(Mechanism);
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
