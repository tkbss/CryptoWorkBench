using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DukptTdeaWorkingKeyTests
{
    private const string Mechanism = "DUKPT-TDEA-WORKING-KEY";
    private const string InitialKey = "6AC292FAA1315B4D858AB3A3D7D5933A";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void FactoryAndParametersExposeUsageOnly()
    {
        AlgorithmFactory.Create(Mechanism).Should().BeOfType<DUKPT_TDEA_WORKING_KEY>();
        ParameterVariableDeclaration parameter = Parameter($"PARAM p=Parameters({Mechanism},#USAGE:PIN)");
        parameter.Mechanism.Should().Be(Mechanism);
        parameter.GetParameters().Should().ContainKeys("#MECH", "#USAGE").And.HaveCount(2);
    }

    [TestCase("PIN", "042666B49184CF5C68DE9628D0397B36")]
    [TestCase("MAC-REQUEST", "042666B4918430A368DE9628D03984C9")]
    [TestCase("MAC-RESPONSE", "042666B46E84CFA368DE96282F397BC9")]
    [TestCase("DATA-REQUEST", "448D3F076D8304036A55A3D7E0055A78")]
    public void OfficialAnnexCCounterOneWorkingKeyVectorsMatch(string usage, string expected)
    {
        Derive(usage, "FFFF9876543210E00001").Value.Should().BeEquivalentTo(
            $"0x({expected})", options => options.IgnoringCase());
    }

    [TestCase("FFFF9876543210E00001", "042666B49184CFA368DE9628D0397BC9")]
    [TestCase("FFFF9876543210E00002", "C46551CEF9FD24B0AA9AD834130D3BC7")]
    [TestCase("FFFF9876543210E00003", "0DF3D9422ACA56E547676D07AD6BADFA")]
    [TestCase("FFFF9876543210EFF800", "F9CDFEBF4F5B1D9EB3EC12454527E176")]
    public void OfficialAnnexCCurrentKeyVectorsMatch(string ksn, string expected)
    {
        Convert.ToHexString(DUKPT_TDEA_WORKING_KEY.DeriveCurrentKey(
            Convert.FromHexString(InitialKey), Convert.FromHexString(ksn))).Should().Be(expected);
    }

    [Test]
    public void MultiBitCounterUsesCumulativeIntermediateKeys()
    {
        byte[] key = Convert.FromHexString(InitialKey);
        byte[] counterThree = DUKPT_TDEA_WORKING_KEY.DeriveCurrentKey(
            key, Convert.FromHexString("FFFF9876543210E00003"));
        byte[] counterOne = DUKPT_TDEA_WORKING_KEY.DeriveCurrentKey(
            key, Convert.FromHexString("FFFF9876543210E00001"));

        Convert.ToHexString(counterThree).Should().Be("0DF3D9422ACA56E547676D07AD6BADFA");
        counterThree.Should().NotEqual(counterOne);
    }

    [TestCase("MAC-BOTH", "MAC-REQUEST")]
    [TestCase("DATA-BOTH", "DATA-REQUEST")]
    public void BothAliasesUseRequestVariant(string alias, string request)
    {
        Derive(alias, "FFFF9876543210E00001").Value.Should()
            .Be(Derive(request, "FFFF9876543210E00001").Value);
    }

    [TestCase("MAC-REQUEST", "MAC-RESPONSE")]
    [TestCase("DATA-REQUEST", "DATA-RESPONSE")]
    public void RequestAndResponseVariantsDiffer(string request, string response)
    {
        Derive(request, "FFFF9876543210E00001").Value.Should()
            .NotBe(Derive(response, "FFFF9876543210E00001").Value);
    }

    [Test]
    public void DataVariantUsesOneWayFunction()
    {
        byte[] current = DUKPT_TDEA_WORKING_KEY.DeriveCurrentKey(
            Convert.FromHexString(InitialKey), Convert.FromHexString("FFFF9876543210E00001"));
        byte[] variant = current.Zip(Convert.FromHexString("0000000000FF00000000000000FF0000"),
            (value, mask) => (byte)(value ^ mask)).ToArray();

        Convert.ToHexString(DUKPT_TDEA_WORKING_KEY.ApplyOneWayFunction(variant))
            .Should().Be("448D3F076D8304036A55A3D7E0055A78");
        Derive("DATA-REQUEST", "FFFF9876543210E00001").Value.Should()
            .NotBe($"0x({Convert.ToHexString(variant)})");
    }

    [TestCase("PIN", KeyUsage.PinEncrypt)]
    [TestCase("MAC-REQUEST", KeyUsage.MacGenerate | KeyUsage.MacVerify)]
    [TestCase("MAC-RESPONSE", KeyUsage.MacGenerate | KeyUsage.MacVerify)]
    [TestCase("MAC-BOTH", KeyUsage.MacGenerate | KeyUsage.MacVerify)]
    [TestCase("DATA-REQUEST", KeyUsage.Encrypt | KeyUsage.Decrypt)]
    [TestCase("DATA-RESPONSE", KeyUsage.Encrypt | KeyUsage.Decrypt)]
    [TestCase("DATA-BOTH", KeyUsage.Encrypt | KeyUsage.Decrypt)]
    public void ResultCarriesTypedMetadata(string usage, KeyUsage expectedUsage)
    {
        KeyVariableDeclaration result = Derive(usage, "FFFF9876543210E00001");

        result.KeyType.Should().Be(KeyType.Secret(KeyAlgorithm.Tdea));
        result.KeySize.Should().Be("128");
        result.KeySizeInBits.Should().Be(new KeySize(128));
        result.DerivationMechanism.Should().Be(Mechanism);
        result.Usage.Should().Be(KeyUsagePolicy.Restricted(expectedUsage));
    }

    [TestCase("")]
    [TestCase("UNKNOWN")]
    [TestCase("MAC-GENERATE")]
    [TestCase("DATA-ENCRYPT")]
    public void RejectsMissingOrUnsupportedUsage(string usage)
    {
        string suffix = usage.Length == 0 ? string.Empty : $",#USAGE:{usage}";
        Action action = () => Parameter($"PARAM p=Parameters({Mechanism}{suffix})");
        action.Should().Throw<SemanticErrorException>();
    }

    [TestCase("#KEYTYPE:TDEA-2")]
    [TestCase("#OUTLEN:128")]
    [TestCase("#COUNTER:1")]
    public void RejectsAdditionalParameters(string parameter)
    {
        Action action = () => Parameter($"PARAM p=Parameters({Mechanism},#USAGE:PIN,{parameter})");
        action.Should().Throw<SemanticErrorException>();
    }

    [TestCase("FFFF9876543210E000")]
    [TestCase("FFFF9876543210E0000100")]
    public void RejectsKsnThatIsNotExactlyTenBytes(string ksn)
    {
        Action action = () => Derive("PIN", ksn);
        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("exactly 80 bits (10 bytes)"));
    }

    [Test]
    public void RejectsZeroTransactionCounter()
    {
        Action action = () => Derive("PIN", "FFFF9876543210E00000");
        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("Transaction Counter"));
    }

    [Test]
    public void AcceptsHostCounterWithMoreThanTenSetBitsAndConfinesItToKsnLowTwentyOneBits()
    {
        KeyVariableDeclaration result = Derive("PIN", "FFFF9876543210FFFFFF");

        result.KeyValue.Should().MatchRegex("^0x\\([0-9a-f]{32}\\)$");
        result.KeyType.Should().Be(KeyType.Secret(KeyAlgorithm.Tdea));
        result.KeySizeInBits.Should().Be(new KeySize(128));
    }

    [TestCase("0123456789ABCDEFFEDCBA98765432")]
    [TestCase("0123456789ABCDEFFEDCBA98765432100000000000000000")]
    public void RejectsInitialKeyThatIsNotExactlySixteenBytes(string key)
    {
        Action action = () => DeriveWithRegisteredKey(key, KeyType.Secret(KeyAlgorithm.Tdea));
        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("exactly 128 bits (16 bytes)"));
    }

    [TestCase(KeyAlgorithm.Aes)]
    [TestCase(KeyAlgorithm.Hmac)]
    [TestCase(KeyAlgorithm.Rsa)]
    [TestCase(KeyAlgorithm.Ec)]
    public void RejectsNonTdeaInitialKeyTypes(KeyAlgorithm algorithm)
    {
        KeyType keyType = algorithm is KeyAlgorithm.Rsa or KeyAlgorithm.Ec
            ? KeyType.Public(algorithm)
            : KeyType.Secret(algorithm);
        Action action = () => DeriveWithRegisteredKey(InitialKey, keyType);
        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("TDEA Initial Key"));
    }

    [Test]
    public void AcceptsUnknownLegacyInitialKeyType()
    {
        DeriveWithRegisteredKey(InitialKey, KeyType.Secret(KeyAlgorithm.Unknown))
            .KeyType.Should().Be(KeyType.Secret(KeyAlgorithm.Tdea));
    }

    private static KeyVariableDeclaration Derive(string usage, string ksn) =>
        Execute($"KEY initial=GenerateKey(DES3-ECB,0x({InitialKey})) " +
                $"PARAM p=Parameters({Mechanism},#USAGE:{usage}) " +
                $"KEY output=Derive(p,initial,0x({ksn}))")
            .Statements[2].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static KeyVariableDeclaration DeriveWithRegisteredKey(string key, KeyType keyType)
    {
        string value = $"0x({key})";
        VariableDictionary.Instance().Add(new KeyVariableDeclaration
        {
            Id = "initial", Value = value, KeyValue = value, ValueFormat = FormatConversions.HEX,
            KeyType = keyType, Type = new CryptoTypeKey()
        });
        return Execute($"PARAM p=Parameters({Mechanism},#USAGE:PIN) " +
                       "KEY output=Derive(p,initial,0x(FFFF9876543210E00001))")
            .Statements[1].Should().BeOfType<KeyVariableDeclaration>().Subject;
    }

    private static ParameterVariableDeclaration Parameter(string script) =>
        Execute(script).Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;

    private static CryptoScriptProgram Execute(string input)
    {
        var parser = ParserBuilder.StringBuild(input);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
