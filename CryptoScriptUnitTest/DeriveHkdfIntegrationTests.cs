using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DeriveHkdfIntegrationTests
{
    private static readonly string[] SupportedHashes =
    {
        "HASH-SHA1", "HASH-SHA224", "HASH-SHA256", "HASH-SHA384", "HASH-SHA512",
        "HASH-SHA512-224", "HASH-SHA512-256", "HASH-SHA3-224", "HASH-SHA3-256",
        "HASH-SHA3-384", "HASH-SHA3-512"
    };

    private const string Ikm = "0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B";
    private const string Salt = "000102030405060708090A0B0C";
    private const string Info = "F0F1F2F3F4F5F6F7F8F9";
    private const string RfcCase1Okm = "3CB25F25FAACD57A90434F64D0362F2A2D2D0A90CF1A5A4C5DB02D56ECC4C5BF34007208D5B887185865";
    private const string RfcCase3Okm = "8DA4E775A563C18F715F802A063C5A31B8A11F5C5EE1879EC3454E5F3C738D2D9D201395FAA4B61A96C8";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCase("0x(F0F1F2F3F4F5F6F7F8F9)")]
    [TestCase("b64(8PHy8/T19vf4+Q==)")]
    public void DeriveAcceptsHexAndBase64Info(string info)
    {
        KeyVariableDeclaration okm = RfcCase1(info).Statements[2]
            .Should().BeOfType<KeyVariableDeclaration>().Subject;

        AssertRfcCase1Result(okm);
    }

    [Test]
    public void DeriveAcceptsInfoFromVar()
    {
        CryptoScriptProgram result = Execute(
            KeyScript("HASH-SHA256", $"#SALT:0x({Salt}),", 336) +
            $"VAR info=0x({Info}) KEY okm=Derive(hkdf,ikm,info)");

        AssertRfcCase1Result((KeyVariableDeclaration)result.Statements[3]);
    }

    [Test]
    public void DeriveAcceptsUtf8StringInfo()
    {
        CryptoScriptProgram result = Execute(
            KeyScript("HASH-SHA256", $"#SALT:0x({Salt}),", 256) +
            "KEY okm=Derive(hkdf,ikm,\"context\")");

        var okm = (KeyVariableDeclaration)result.Statements[2];
        FormatConversions.HexStringToByteArray(okm.Value).Should().HaveCount(32);
        AssertKeyMetadata(okm, "256");
    }

    [TestCase("")]
    [TestCase("#SALT:\"\",")]
    public void MissingAndExplicitlyEmptySaltSupportEmptyInfo(string saltParameter)
    {
        CryptoScriptProgram result = Execute(
            KeyScript("HASH-SHA256", saltParameter, 336) +
            "KEY okm=Derive(hkdf,ikm,\"\")");

        ((KeyVariableDeclaration)result.Statements[2]).Value.Should().BeEquivalentTo(
            $"0x({RfcCase3Okm})", options => options.IgnoringCase());
    }

    [Test]
    public void DeriveSupportsSha512()
    {
        CryptoScriptProgram result = Execute(
            KeyScript("HASH-SHA512", "#SALT:b64(AAECAw==),", 512) +
            "KEY okm=Derive(hkdf,ikm,\"context\")");

        var okm = (KeyVariableDeclaration)result.Statements[2];
        FormatConversions.HexStringToByteArray(okm.Value).Should().HaveCount(64);
        AssertKeyMetadata(okm, "512");
    }

    [TestCaseSource(nameof(SupportedHashes))]
    public void DeriveSupportsEveryHashAcceptedByHkdfParameters(string hash)
    {
        CryptoScriptProgram result = Execute(
            KeyScript(hash, string.Empty, 8) + "KEY okm=Derive(hkdf,ikm,\"\")");

        var okm = (KeyVariableDeclaration)result.Statements[2];
        FormatConversions.HexStringToByteArray(okm.Value).Should().HaveCount(1);
        AssertKeyMetadata(okm, "8");
    }

    [Test]
    public void DeriveUsesCanonicalKeyValueWhenValueFormatIsMissing()
    {
        RegisterIkm("ikm", Ikm, string.Empty);

        CryptoScriptProgram result = Execute(
            $"PARAM hkdf=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#SALT:0x({Salt}),#OUTLEN:336) " +
            $"KEY okm=Derive(hkdf,ikm,0x({Info}))");

        AssertRfcCase1Result((KeyVariableDeclaration)result.Statements[1]);
    }

    [Test]
    public void DeriveRejectsAmbiguousKeyValues()
    {
        RegisterIkm("first", Ikm, FormatConversions.HEX);
        RegisterIkm("second", Ikm, FormatConversions.HEX);

        Action action = () => Execute(
            "PARAM hkdf=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#OUTLEN:256) " +
            "KEY okm=Derive(hkdf,second,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("Ambiguous KEY argument"));
    }

    [Test]
    public void DeriveRejectsManipulatedNonByteAlignedOutputLength()
    {
        RegisterIkm("ikm", Ikm, FormatConversions.HEX);
        ParameterVariableDeclaration parameter = new KDF_HKDF().GenerateParameters(
            "KDF-HKDF", new[] { "#HASH:HASH-SHA256", "#OUTLEN:256" });
        parameter.Id = "hkdf";
        parameter.SetParameter("OUTLEN", "257");
        VariableDictionary.Instance().Add(parameter);

        Action action = () => Execute("KEY okm=Derive(hkdf,ikm,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("positive multiple of 8"));
    }

    [TestCase("not-a-data-value")]
    [TestCase("b64(!)")]
    public void DeriveRejectsManipulatedInvalidSalt(string salt)
    {
        RegisterIkm("ikm", Ikm, FormatConversions.HEX);
        ParameterVariableDeclaration parameter = new KDF_HKDF().GenerateParameters(
            "KDF-HKDF", new[] { "#HASH:HASH-SHA256", "#OUTLEN:256" });
        parameter.Id = "hkdf";
        parameter.SetParameter("SALT", salt);
        VariableDictionary.Instance().Add(parameter);

        Action action = () => Execute("KEY okm=Derive(hkdf,ikm,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("HKDF #SALT"));
    }

    [Test]
    public void DeriveRejectsKeyWithoutAValidByteRepresentation()
    {
        VariableDictionary.Instance().Add(new KeyVariableDeclaration
        {
            Id = "ikm",
            Value = "invalid-key",
            KeyValue = "invalid-key",
            Type = new CryptoTypeKey()
        });

        Action action = () => Execute(
            "PARAM hkdf=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#OUTLEN:256) " +
            "KEY okm=Derive(hkdf,ikm,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("valid hex, Base64 or string"));
    }

    [Test]
    public void DeriveSupportsMaximumRfc5869OutputLength()
    {
        const int outputLengthBits = 255 * 32 * 8;
        CryptoScriptProgram result = Execute(
            KeyScript("HASH-SHA256", string.Empty, outputLengthBits) +
            "KEY okm=Derive(hkdf,ikm,\"\")");

        var okm = (KeyVariableDeclaration)result.Statements[2];
        FormatConversions.HexStringToByteArray(okm.Value).Should().HaveCount(255 * 32);
        AssertKeyMetadata(okm, outputLengthBits.ToString());
    }

    [TestCase("KEY okm=Derive(hkdf,ikm)")]
    [TestCase("KEY okm=Derive(hkdf,ikm,\"\",\"extra\")")]
    public void DeriveRejectsWrongArgumentCount(string derive)
    {
        Action action = () => Execute(KeyScript("HASH-SHA256", string.Empty, 256) + derive);

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("wrong number of arguments"));
    }

    [Test]
    public void DeriveRejectsMissingParameterInFirstArgument()
    {
        Action action = () => Execute(
            $"KEY ikm=GenerateKey(HMAC-SHA256,0x({Ikm})) VAR notParam=\"data\" " +
            "KEY okm=Derive(notParam,ikm,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("Missing argument of type PARAM"));
    }

    [Test]
    public void DeriveRejectsNonKeySecondArgument()
    {
        Action action = () => Execute(
            "VAR ikm=0x(00112233) " +
            "PARAM hkdf=Parameters(KDF-HKDF,#HASH:HASH-SHA256,#OUTLEN:256) " +
            "KEY okm=Derive(hkdf,ikm,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("wrong key argument"));
    }

    [Test]
    public void DeriveRejectsUnsupportedParameterMechanism()
    {
        Action action = () => Execute(
            "KEY key=GenerateKey(AES-CBC,128) PARAM p=Parameters(AES-CBC) " +
            "KEY okm=Derive(p,key,\"\")");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("does not support key derivation"));
    }

    private static CryptoScriptProgram RfcCase1(string info) => Execute(
        KeyScript("HASH-SHA256", $"#SALT:0x({Salt}),", 336) +
        $"KEY okm=Derive(hkdf,ikm,{info})");

    private static string KeyScript(string hash, string saltParameter, int outputLength) =>
        $"KEY ikm=GenerateKey(HMAC-SHA256,0x({Ikm})) " +
        $"PARAM hkdf=Parameters(KDF-HKDF,#HASH:{hash},{saltParameter}#OUTLEN:{outputLength}) ";

    private static void RegisterIkm(string id, string hex, string valueFormat)
    {
        string value = $"0x({hex})";
        VariableDictionary.Instance().Add(new KeyVariableDeclaration
        {
            Id = id,
            Value = value,
            KeyValue = value,
            ValueFormat = valueFormat,
            KeySize = (hex.Length * 4).ToString(),
            Type = new CryptoTypeKey()
        });
    }

    private static void AssertRfcCase1Result(KeyVariableDeclaration okm)
    {
        okm.Value.Should().BeEquivalentTo($"0x({RfcCase1Okm})", options => options.IgnoringCase());
        AssertKeyMetadata(okm, "336");
    }

    private static void AssertKeyMetadata(KeyVariableDeclaration okm, string keySize)
    {
        okm.Type.Should().BeOfType<CryptoTypeKey>();
        okm.Value.Should().Be(okm.KeyValue);
        okm.ValueFormat.Should().Be(FormatConversions.HEX);
        okm.KeySize.Should().Be(keySize);
        okm.Mechanism.Should().BeEmpty();
        okm.DerivationMechanism.Should().Be("KDF-HKDF");
        okm.KeyAttributes.Should().BeEmpty();
        VariableDictionary.Instance().Get("okm").Should().BeSameAs(okm);
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
