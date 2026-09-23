using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DukptAesWorkingKeyTests
{
    private const string Mechanism = "DUKPT-AES-WORKING-KEY";
    private const string InitialKey128 = "1273671EA26AC29AFA4D1084127652A1";
    private const string InitialKey192 = "3B1C5B8720988B8E0AD9E52642A92D5CE331ABBFD80DE1C6";
    private const string InitialKey256 = "CE9CE0C101D1138F97FB6CAD4DF045A7083D4EAE2D35A31789D01CCF0949550F";
    private const string Ksn1 = "123456789012345600000001";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void FactoryAndGrammarExposeWorkingKeyMechanismAndParameters()
    {
        AlgorithmFactory.Create(Mechanism).Should().BeOfType<DUKPT_AES_WORKING_KEY>();

        ParameterVariableDeclaration parameter = Parameter("PIN", "AES-128");

        parameter.Mechanism.Should().Be(Mechanism);
        parameter.GetParameter("USAGE").Should().Be("PIN");
        parameter.GetParameter("KEYTYPE").Should().Be("AES-128");
    }

    [Test]
    public void AnnexBIntermediateDerivationKeyForCounterOneMatchesOfficialVector()
    {
        byte[] result = DUKPT_AES_WORKING_KEY.DeriveIntermediateKey(
            Convert.FromHexString(InitialKey128),
            Convert.FromHexString("1234567890123456"),
            1);

        Convert.ToHexString(result).Should().Be("4F21B565BAD9835E112B6465635EAE44");
    }

    [TestCase("PIN", "AF8CB133A78F8DC2D1359F18527593FB")]
    [TestCase("MAC-GENERATE", "A2DC23DE6FDE0824A2BC321E08E4B8B7")]
    [TestCase("MAC-VERIFY", "DBB463945B286C07CD3AD82EE96FD9C9")]
    [TestCase("MAC-BOTH", "85675439D18D7F1158BD8E3EAA3D502B")]
    [TestCase("DATA-ENCRYPT", "A35C412EFD41FDB98B69797C02DCD08F")]
    [TestCase("DATA-DECRYPT", "16292C6EA8F64C5420A0584BFBC577BE")]
    [TestCase("DATA-BOTH", "A308E080DD15A1B741F1721BF67DE11C")]
    public void AnnexBAllUsageVectorsForAes128CounterOneMatch(string usage, string expected)
    {
        KeyVariableDeclaration result = Derive(InitialKey128, Ksn1, usage, "AES-128");

        result.Value.Should().BeEquivalentTo($"0x({expected})", options => options.IgnoringCase());
    }

    [Test]
    public void AnnexBMultiBitCounterIsProcessedMostSignificantBitFirst()
    {
        KeyVariableDeclaration result = Derive(
            InitialKey128, "123456789012345600000003", "PIN", "AES-128");

        result.Value.Should().BeEquivalentTo(
            "0x(7D69F01F3B45449F62C7816ECE723268)", options => options.IgnoringCase());
        Convert.ToHexString(DUKPT_AES_WORKING_KEY.DeriveIntermediateKey(
                Convert.FromHexString(InitialKey128), Convert.FromHexString("1234567890123456"), 3))
            .Should().Be("031504E530365CF81264238540518318");
    }

    [TestCase("TDEA-2", "630C706D9546E47D4449313F61C4D4AB")]
    [TestCase("TDEA-3", "EA8B3F37EB9B15831167EF2977FD8762D9B5913F35766F6A")]
    public void AnnexBVectorsCoverTdeaWorkingKeyTypes(string keyType, string expected)
    {
        KeyVariableDeclaration result = Derive(InitialKey128, Ksn1, "PIN", keyType);

        result.Value.Should().BeEquivalentTo($"0x({expected})", options => options.IgnoringCase());
        result.KeyType.Should().Be(KeyType.Secret(KeyAlgorithm.Tdea));
    }

    [Test]
    public void AnnexBAes256InitialAndWorkingKeyMatchOfficialVector()
    {
        KeyVariableDeclaration result = Derive(InitialKey256, Ksn1, "PIN", "AES-256");

        result.Value.Should().BeEquivalentTo(
            "0x(8C1AB7BEE973829E30242E0BBBDD4946D540C98FC1B5BDCF94790001A23FD502)",
            options => options.IgnoringCase());
    }

    [TestCase("TDEA-2", 128, KeyAlgorithm.Tdea)]
    [TestCase("TDEA-3", 192, KeyAlgorithm.Tdea)]
    [TestCase("AES-128", 128, KeyAlgorithm.Aes)]
    [TestCase("AES-192", 192, KeyAlgorithm.Aes)]
    [TestCase("AES-256", 256, KeyAlgorithm.Aes)]
    [TestCase("HMAC-128", 128, KeyAlgorithm.Hmac)]
    [TestCase("HMAC-192", 192, KeyAlgorithm.Hmac)]
    [TestCase("HMAC-256", 256, KeyAlgorithm.Hmac)]
    public void Aes256InitialKeySupportsEveryWorkingKeyType(
        string keyType, int bits, KeyAlgorithm algorithm)
    {
        KeyVariableDeclaration result = Derive(InitialKey256, Ksn1, "MAC-GENERATE", keyType);

        result.KeyType.Should().Be(KeyType.Secret(algorithm));
        result.KeySizeInBits.Should().Be(new KeySize(bits));
        FormatConversions.HexStringToByteArray(result.KeyValue).Should().HaveCount(bits / 8);
    }

    [Test]
    public void Aes192InitialKeySupportsAes192WorkingKey()
    {
        KeyVariableDeclaration result = Derive(InitialKey192, Ksn1, "DATA-BOTH", "AES-192");

        result.KeyType.Should().Be(KeyType.Secret(KeyAlgorithm.Aes));
        result.KeySizeInBits.Should().Be(new KeySize(192));
        FormatConversions.HexStringToByteArray(result.KeyValue).Should().HaveCount(24);
    }

    [TestCase(InitialKey128, "TDEA-2")]
    [TestCase(InitialKey128, "TDEA-3")]
    [TestCase(InitialKey128, "AES-128")]
    [TestCase(InitialKey128, "HMAC-128")]
    [TestCase(InitialKey192, "TDEA-2")]
    [TestCase(InitialKey192, "TDEA-3")]
    [TestCase(InitialKey192, "AES-128")]
    [TestCase(InitialKey192, "AES-192")]
    [TestCase(InitialKey192, "HMAC-128")]
    [TestCase(InitialKey192, "HMAC-192")]
    [TestCase(InitialKey256, "TDEA-2")]
    [TestCase(InitialKey256, "TDEA-3")]
    [TestCase(InitialKey256, "AES-128")]
    [TestCase(InitialKey256, "AES-192")]
    [TestCase(InitialKey256, "AES-256")]
    [TestCase(InitialKey256, "HMAC-128")]
    [TestCase(InitialKey256, "HMAC-192")]
    [TestCase(InitialKey256, "HMAC-256")]
    public void StrengthMatrixAcceptsPermittedCombinations(string initialKey, string keyType)
    {
        Action action = () => Derive(initialKey, Ksn1, "MAC-GENERATE", keyType);

        action.Should().NotThrow();
    }

    [TestCase(InitialKey128, "AES-192")]
    [TestCase(InitialKey128, "AES-256")]
    [TestCase(InitialKey128, "HMAC-192")]
    [TestCase(InitialKey128, "HMAC-256")]
    [TestCase(InitialKey192, "AES-256")]
    [TestCase(InitialKey192, "HMAC-256")]
    public void StrengthMatrixRejectsStrongerWorkingKeys(string initialKey, string keyType)
    {
        Action action = () => Derive(initialKey, Ksn1, "MAC-GENERATE", keyType);

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("stronger than"));
    }

    [TestCase("PIN", KeyUsage.PinEncrypt)]
    [TestCase("MAC-GENERATE", KeyUsage.MacGenerate)]
    [TestCase("MAC-VERIFY", KeyUsage.MacVerify)]
    [TestCase("MAC-BOTH", KeyUsage.MacGenerate | KeyUsage.MacVerify)]
    [TestCase("DATA-ENCRYPT", KeyUsage.Encrypt)]
    [TestCase("DATA-DECRYPT", KeyUsage.Decrypt)]
    [TestCase("DATA-BOTH", KeyUsage.Encrypt | KeyUsage.Decrypt)]
    public void ResultContainsRestrictedUsageMetadata(string usage, KeyUsage expected)
    {
        KeyVariableDeclaration result = Derive(InitialKey128, Ksn1, usage, "AES-128");

        result.Usage.Should().Be(KeyUsagePolicy.Restricted(expected));
        result.DerivationMechanism.Should().Be(Mechanism);
    }

    [TestCase("1234567890123456000000")]
    [TestCase("12345678901234560000000000")]
    public void RejectsKsnThatIsNotExactlyTwelveBytes(string ksn)
    {
        Action action = () => Derive(InitialKey128, ksn, "PIN", "AES-128");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("exactly 96 bits (12 bytes)"));
    }

    [TestCase("PARAM p=Parameters(DUKPT-AES-WORKING-KEY)", "#USAGE")]
    [TestCase("PARAM p=Parameters(DUKPT-AES-WORKING-KEY,#USAGE:PIN)", "#KEYTYPE")]
    [TestCase("PARAM p=Parameters(DUKPT-AES-WORKING-KEY,#KEYTYPE:AES-128)", "#USAGE")]
    [TestCase("PARAM p=Parameters(DUKPT-AES-WORKING-KEY,#USAGE:UNKNOWN,#KEYTYPE:AES-128)", "#USAGE")]
    [TestCase("PARAM p=Parameters(DUKPT-AES-WORKING-KEY,#USAGE:PIN,#KEYTYPE:UNKNOWN)", "#KEYTYPE")]
    public void RejectsMissingOrUnknownRequiredParameters(string script, string expected)
    {
        Action action = () => Execute(script);

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(expected));
    }

    private static ParameterVariableDeclaration Parameter(string usage, string keyType) =>
        Execute($"PARAM p=Parameters({Mechanism},#USAGE:{usage},#KEYTYPE:{keyType})")
            .Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;

    private static KeyVariableDeclaration Derive(
        string initialKey, string ksn, string usage, string keyType) =>
        Execute($"KEY ik=GenerateKey(AES-ECB,0x({initialKey})) " +
                $"PARAM p=Parameters({Mechanism},#USAGE:{usage},#KEYTYPE:{keyType}) " +
                $"KEY wk=Derive(p,ik,0x({ksn}))")
            .Statements[2].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static CryptoScriptProgram Execute(string input)
    {
        var parser = ParserBuilder.StringBuild(input);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
