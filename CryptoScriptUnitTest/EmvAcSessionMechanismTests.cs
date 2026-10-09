using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvAcSessionMechanismTests
{
    private const string Mechanism = "KDF-EMV-AC-SESSION";
    private const string TdeaMasterKey = "4F5276A285D36175DC4F516BBF80CB1A";
    private const string Aes128MasterKey = "2EF6E07ECBA86BCF3C3CFF7BBEBE6F38";
    private const string Aes192MasterKey = "7481B0525D05D393D6DCBADD333C6BC514087C6BF5FA10C4";
    private const string Aes256MasterKey = "14D63F23982740AC65B482BAF5913092D8132BAA4143A24D3CF437232711A507";
    private const string MacData = "000102030405060708090A0B0C0D0E0F";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void RegistryFactoryAndParametersExposeTheMechanism()
    {
        AlgorithmFactory.Create(Mechanism).Should().BeOfType<KDF_EMV_AC_SESSION>();
        MechanismList.Instance.Mechanisms.Count(value => value == Mechanism).Should().Be(1);
        MechanismRegistry.TryGet(Mechanism, out MechanismRegistryEntry? entry).Should().BeTrue();
        entry!.SupportedFunctions.Should().BeEquivalentTo(new[]
        {
            CryptoScriptFunction.Parameters,
            CryptoScriptFunction.Derive
        });

        ParameterVariableDeclaration parameters = Execute(
                $"PARAM sessionParameters=Parameters({Mechanism})")
            .Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;

        parameters.Mechanism.Should().Be(Mechanism);
        parameters.GetParameters().Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string>("#MECH", Mechanism));
    }

    [TestCase("DES3-ECB", TdeaMasterKey, KeyAlgorithm.Tdea, 128,
        "5B10C70AFEE94975A345C69888048FF9")]
    [TestCase("AES-ECB", Aes128MasterKey, KeyAlgorithm.Aes, 128,
        "89F7B697A028A93345BE7A409665B9A4")]
    [TestCase("AES-ECB", Aes192MasterKey, KeyAlgorithm.Aes, 192,
        "78429FD2061D24B1F8830B2C91D3ED95ED9D4B069A7C2707")]
    [TestCase("AES-ECB", Aes256MasterKey, KeyAlgorithm.Aes, 256,
        "A99C5840A0CBFBF093DEA740FFFFF5A241BFE9472968CC6E0B9EC76BA280CE0F")]
    public void CryptoScriptPathMatchesEmvCoKnownAnswerVectorsAndMetadata(
        string keyMechanism,
        string masterKey,
        KeyAlgorithm expectedAlgorithm,
        int expectedSize,
        string expectedSessionKey)
    {
        KeyVariableDeclaration result = Derive(keyMechanism, masterKey, "0x(0001)");

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo($"0x({expectedSessionKey})").IgnoreCase);
            Assert.That(result.KeyValue, Is.EqualTo(result.Value));
            Assert.That(result.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(result.KeyType, Is.EqualTo(KeyType.Secret(expectedAlgorithm)));
            Assert.That(result.KeySizeInBits, Is.EqualTo(new KeySize(expectedSize)));
            Assert.That(result.KeySize, Is.EqualTo(expectedSize.ToString()));
            Assert.That(result.Usage, Is.EqualTo(KeyUsagePolicy.Unspecified));
            Assert.That(result.DerivationMechanism, Is.EqualTo(Mechanism));
        });
    }

    [Test]
    public void HexVarAtcIsAcceptedWithoutTextOrIntegerInterpretation()
    {
        CryptoScriptProgram program = Execute(
            $"KEY master=GenerateKey(AES-ECB,0x({Aes128MasterKey})) " +
            $"PARAM p=Parameters({Mechanism}) VAR atc=0x(0001) " +
            "KEY output=Derive(p,master,atc)");

        var result = (KeyVariableDeclaration)program.Statements[3];
        result.Value.Should().BeEquivalentTo(
            "0x(89F7B697A028A93345BE7A409665B9A4)",
            options => options.IgnoringCase());
    }

    [Test]
    public void NestedFunctionResultIsRejectedEvenWhenItReturnsTwoHexBytes()
    {
        const string decryptKey = "000102030405060708090A0B0C0D0E0F";
        const string ciphertext = "A72846C84C7F9B9BB4104ADFBBCAFFF5";
        string declarations =
            $"KEY decryptKey=GenerateKey(AES-CBC,0x({decryptKey})) " +
            "PARAM decryptParameters=Parameters(#MECH:AES-CBC," +
            "#IV:0x(00000000000000000000000000000000),#PAD:PKCS-7) " +
            $"KEY master=GenerateKey(AES-ECB,0x({Aes128MasterKey})) " +
            $"PARAM p=Parameters({Mechanism}) ";

        CryptoScriptProgram reference = Execute(
            declarations + $"VAR atc=Decrypt(decryptParameters,decryptKey,0x({ciphertext}))");
        ((StringVariableDeclaration)reference.Statements[4]).Value.Should().Be("0x(0001)");

        Action action = () => Execute(
            declarations +
            $"KEY output=Derive(p,master,Decrypt(decryptParameters,decryptKey,0x({ciphertext})))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                "hexadecimal literal or a VAR containing hexadecimal binary data"));
    }

    [Test]
    public void EqualKeyValuesRemainDistinguishableBySourceVariableIdentity()
    {
        CryptoScriptProgram program = Execute(
            $"KEY tdea=GenerateKey(DES3-ECB,0x({Aes128MasterKey})) " +
            $"KEY aes=GenerateKey(AES-ECB,0x({Aes128MasterKey})) " +
            $"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,aes,0x(0001))");

        var result = (KeyVariableDeclaration)program.Statements[3];
        Assert.Multiple(() =>
        {
            Assert.That(result.Value,
                Is.EqualTo("0x(89F7B697A028A93345BE7A409665B9A4)").IgnoreCase);
            Assert.That(result.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Aes)));
        });
    }

    [Test]
    public void LegacyDeriveStringArrayStillResolvesTheRegisteredKeyByValue()
    {
        var algorithm = new KDF_EMV_AC_SESSION();
        ParameterVariableDeclaration parameters = algorithm.GenerateParameters(Mechanism);
        KeyVariableDeclaration master = CreateKey(
            "master", KeyAlgorithm.Aes, Convert.FromHexString(Aes128MasterKey));
        VariableDictionary.Instance().Add(master);

        KeyVariableDeclaration result = algorithm.Derive(new[]
        {
            parameters.Value,
            master.Value,
            "0x(0001)"
        });

        Assert.That(result.Value,
            Is.EqualTo("0x(89F7B697A028A93345BE7A409665B9A4)").IgnoreCase);
    }

    [TestCase("0x(00112233445566778899AABBCCDDEEFF)")]
    [TestCase("b64(AAAAAAAAAAAAAAAAAAAAAA==)")]
    [TestCase("\"0123456789abcdef\"")]
    [TestCase("rawKey")]
    [TestCase("p")]
    public void RejectsMasterKeyArgumentsThatAreNotKeyVariables(string argument)
    {
        string declarations = argument == "rawKey"
            ? $"VAR rawKey=0x({Aes128MasterKey}) "
            : string.Empty;
        Action action = () => Execute(
            $"{declarations}PARAM p=Parameters({Mechanism}) " +
            $"KEY output=Derive(p,{argument},0x(0001))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("wrong key argument"));
    }

    [TestCase("0x(00)")]
    [TestCase("0x(000102)")]
    [TestCase("b64(AAE=)")]
    [TestCase("\"0001\"")]
    [TestCase("1")]
    [TestCase("master")]
    [TestCase("p")]
    public void RejectsInvalidAtcLengthsAndInputForms(string atc)
    {
        Action action = () => Execute(
            $"KEY master=GenerateKey(AES-ECB,0x({Aes128MasterKey})) " +
            $"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,master,{atc})");

        action.Should().Throw<SemanticErrorException>();
    }

    [TestCase("b64(AAE=)")]
    [TestCase("\"0001\"")]
    [TestCase("1")]
    public void RejectsVarAtcThatIsNotHexadecimalBinaryData(string value)
    {
        Action action = () => Execute(
            $"KEY master=GenerateKey(AES-ECB,0x({Aes128MasterKey})) " +
            $"PARAM p=Parameters({Mechanism}) VAR atc={value} " +
            "KEY output=Derive(p,master,atc)");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                "hexadecimal literal or a VAR containing hexadecimal binary data"));
    }

    [TestCase(KeyAlgorithm.Aes, 15)]
    [TestCase(KeyAlgorithm.Aes, 20)]
    [TestCase(KeyAlgorithm.Aes, 31)]
    [TestCase(KeyAlgorithm.Tdea, 15)]
    [TestCase(KeyAlgorithm.Tdea, 24)]
    public void RejectsInvalidMasterKeyLengths(KeyAlgorithm algorithm, int length)
    {
        RegisterKey("master", algorithm, new byte[length]);

        Action action = () => Execute(
            $"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,master,0x(0001))");

        action.Should().Throw<SemanticErrorException>();
    }

    [TestCase(KeyAlgorithm.Unknown)]
    [TestCase(KeyAlgorithm.Hmac)]
    [TestCase(KeyAlgorithm.Rsa)]
    [TestCase(KeyAlgorithm.Ec)]
    public void RejectsUnsupportedMasterKeyAlgorithms(KeyAlgorithm algorithm)
    {
        RegisterKey("master", algorithm, Convert.FromHexString(Aes128MasterKey));

        Action action = () => Execute(
            $"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,master,0x(0001))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                "requires an AES or TDEA master KEY"));
    }

    [Test]
    public void RejectsKeySizeMetadataThatDoesNotMatchTheMasterKeyBytes()
    {
        string value = $"0x({Aes128MasterKey})";
        VariableDictionary.Instance().Add(new KeyVariableDeclaration
        {
            Id = "master",
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            KeyType = KeyType.Secret(KeyAlgorithm.Aes),
            KeySizeInBits = new KeySize(192),
            Type = new CryptoTypeKey()
        });

        Action action = () => Execute(
            $"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,master,0x(0001))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                "size metadata does not match"));
    }

    [TestCase("#HASH:HASH-SHA256")]
    [TestCase("#SALT:0x(0001)")]
    [TestCase("#OUTLEN:128")]
    [TestCase("#PAD:NONE")]
    [TestCase("#IV:0x(00000000000000000000000000000000)")]
    [TestCase("#KEYTYPE:AES-128")]
    public void RejectsKnownAdditionalParameters(string parameter)
    {
        Action action = () => Execute(
            $"PARAM p=Parameters({Mechanism},{parameter})");

        action.Should().Throw<SemanticErrorException>();
    }

    [Test]
    public void RejectsDuplicateMechanismParameter()
    {
        Action action = () => Execute(
            $"PARAM p=Parameters({Mechanism},#MECH:{Mechanism})");

        action.Should().Throw<SemanticErrorException>();
    }

    [Test]
    public void DirectParameterDeclarationIsAccepted()
    {
        CryptoScriptProgram program = Execute(
            $"KEY master=GenerateKey(AES-ECB,0x({Aes128MasterKey})) " +
            $"PARAM p=#MECH:{Mechanism} " +
            "KEY output=Derive(p,master,0x(0001))");

        ((KeyVariableDeclaration)program.Statements[2]).Value.Should().BeEquivalentTo(
            "0x(89F7B697A028A93345BE7A409665B9A4)",
            options => options.IgnoringCase());
    }

    [Test]
    public void DirectParameterDeclarationRejectsForbiddenAdditionalParameter()
    {
        Action action = () => Execute(
            $"KEY master=GenerateKey(AES-ECB,0x({Aes128MasterKey})) " +
            $"PARAM p=#MECH:{Mechanism} #HASH:HASH-SHA256 " +
            "KEY output=Derive(p,master,0x(0001))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.ErrorCode ==
                FunctionContractError.ForbiddenAdditionalParameter);
    }

    [Test]
    public void DirectParameterDeclarationRejectsIdenticalDuplicateMechanism()
    {
        Action action = () => Execute(
            $"KEY master=GenerateKey(AES-ECB,0x({Aes128MasterKey})) " +
            $"PARAM p=#MECH:{Mechanism} #MECH:{Mechanism} " +
            "KEY output=Derive(p,master,0x(0001))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("duplicate #MECH"));
    }

    [Test]
    public void DirectParameterDeclarationRejectsContradictoryMechanism()
    {
        Action action = () => Execute(
            $"KEY master=GenerateKey(AES-ECB,0x({Aes128MasterKey})) " +
            $"PARAM p=#MECH:{Mechanism} #MECH:AES-CBC " +
            "KEY output=Derive(p,master,0x(0001))");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("consistent #MECH"));
    }

    [Test]
    public void ParametersFunctionRejectsContradictoryMechanism()
    {
        Action action = () => Execute(
            $"PARAM p=Parameters({Mechanism},#MECH:AES-CBC)");

        action.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("consistent #MECH"));
    }

    [Test]
    public void ExistingMechanismStillUsesLegacyDuplicateMechanismSemantics()
    {
        CryptoScriptProgram program = Execute(
            "PARAM p=#MECH:AES-CBC #MECH:AES-ECB");

        var parameters = (ParameterVariableDeclaration)program.Statements[0];
        Assert.Multiple(() =>
        {
            Assert.That(parameters.Mechanism, Is.EqualTo("AES-CBC"));
            Assert.That(parameters.GetParameter("MECH"), Is.EqualTo("AES-ECB"));
        });
    }

    [Test]
    public void DerivedAesSessionKeyCanBeUsedImmediatelyForAesCmac()
    {
        CryptoScriptProgram program = Execute(
            $"KEY master=GenerateKey(AES-ECB,0x({Aes128MasterKey})) " +
            $"PARAM kdf=Parameters({Mechanism}) " +
            "KEY session=Derive(kdf,master,0x(0001)) " +
            "PARAM macParameters=Parameters(AES-CMAC) " +
            $"VAR mac=Mac(macParameters,session,0x({MacData}))");

        var session = (KeyVariableDeclaration)program.Statements[2];
        var mac = (StringVariableDeclaration)program.Statements[4];
        AssertSessionMetadata(session, KeyAlgorithm.Aes, 128);
        // Fixed OpenSSL 3.6.1 AES-CMAC reference for the documented session key and MacData.
        mac.Value.Should().BeEquivalentTo(
            "0x(D3A78E1517C22F7040A3556A23B8A2A7)",
            options => options.IgnoringCase());
    }

    [Test]
    public void DerivedTdeaSessionKeyCanBeUsedImmediatelyForRetailMac()
    {
        CryptoScriptProgram program = Execute(
            $"KEY master=GenerateKey(DES3-ECB,0x({TdeaMasterKey})) " +
            $"PARAM kdf=Parameters({Mechanism}) " +
            "KEY session=Derive(kdf,master,0x(0001)) " +
            "PARAM macParameters=Parameters(DES3-RETAIL) " +
            $"VAR mac=Mac(macParameters,session,0x({MacData}))");

        var session = (KeyVariableDeclaration)program.Statements[2];
        var mac = (StringVariableDeclaration)program.Statements[4];
        AssertSessionMetadata(session, KeyAlgorithm.Tdea, 128);
        // Fixed independent ANSI X9.19 calculation using .NET DES-ECB for the session key and MacData.
        mac.Value.Should().BeEquivalentTo(
            "0x(613D21599AAC13F5)",
            options => options.IgnoringCase());
    }

    [Test]
    public void UnknownNamedParameterIsRejectedByTheLanguage()
    {
        _ = ParserBuilder.StringBuild(
            $"PARAM p=Parameters({Mechanism},#UNKNOWN:1)").program();

        Assert.That(
            SyntaxErrorListner.SyntaxErrorOccured || LexerErrorListener.LexerErrorOccured,
            Is.True);
    }

    [Test]
    public void RejectsParameterSetForAnotherMechanism()
    {
        var parameters = new ParameterVariableDeclaration { Mechanism = "KDF-HKDF" };
        var key = CreateKey("master", KeyAlgorithm.Aes, Convert.FromHexString(Aes128MasterKey));
        var arguments = new AlgorithmCallArguments(new[]
        {
            new AlgorithmCallArgument(parameters.Value, parameters),
            new AlgorithmCallArgument(key.Value, key),
            new AlgorithmCallArgument("0x(0001)")
        });

        Action action = () => new KDF_EMV_AC_SESSION().Derive(arguments);

        action.Should().Throw<ArgumentException>()
            .WithMessage($"*requires {Mechanism} parameters*");
    }

    [Test]
    public void ValidationErrorsDoNotContainMasterKeyMaterial()
    {
        const string secret = "00112233445566778899AABBCCDDEE";
        RegisterKey("master", KeyAlgorithm.Aes, Convert.FromHexString(secret));

        SemanticErrorException exception = Assert.Throws<SemanticErrorException>(() => Execute(
            $"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,master,0x(0001))"))!;

        exception.ToString().ToUpperInvariant().Should().NotContain(secret);
    }

    private static KeyVariableDeclaration Derive(
        string keyMechanism,
        string masterKey,
        string atc) =>
        Execute(
                $"KEY master=GenerateKey({keyMechanism},0x({masterKey})) " +
                $"PARAM p=Parameters({Mechanism}) KEY output=Derive(p,master,{atc})")
            .Statements[2].Should().BeOfType<KeyVariableDeclaration>().Subject;

    private static void RegisterKey(string id, KeyAlgorithm algorithm, byte[] bytes) =>
        VariableDictionary.Instance().Add(CreateKey(id, algorithm, bytes));

    private static KeyVariableDeclaration CreateKey(
        string id,
        KeyAlgorithm algorithm,
        byte[] bytes)
    {
        string value = $"0x({Convert.ToHexString(bytes)})";
        KeyMaterialKind materialKind = algorithm is KeyAlgorithm.Rsa or KeyAlgorithm.Ec
            ? KeyMaterialKind.Private
            : KeyMaterialKind.Secret;
        return new KeyVariableDeclaration
        {
            Id = id,
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            KeyType = new KeyType(algorithm, materialKind),
            KeySizeInBits = new KeySize(bytes.Length * 8),
            Type = new CryptoTypeKey()
        };
    }

    private static void AssertSessionMetadata(
        KeyVariableDeclaration session,
        KeyAlgorithm algorithm,
        int size)
    {
        Assert.Multiple(() =>
        {
            Assert.That(session.KeyType, Is.EqualTo(KeyType.Secret(algorithm)));
            Assert.That(session.KeySizeInBits, Is.EqualTo(new KeySize(size)));
            Assert.That(session.Usage, Is.EqualTo(KeyUsagePolicy.Unspecified));
            Assert.That(session.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(session.KeyValue, Is.EqualTo(session.Value));
            Assert.That(session.DerivationMechanism, Is.EqualTo(Mechanism));
        });
    }

    private static CryptoScriptProgram Execute(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
