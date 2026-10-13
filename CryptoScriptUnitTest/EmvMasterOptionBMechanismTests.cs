using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.Documentation;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvMasterOptionBMechanismTests
{
    private const string Mechanism = "KDF-EMV-MASTER-B";
    private const string Imk = "0123456789ABCDEFFEDCBA9876543210";
    private const string Prefix = "KEY IMK=GenerateKey(DES3-ECB,0x(" + Imk + ")) ";

    // Independently calculated on 2026-10-13 with Python hashlib SHA-1 and
    // cryptography 50.0.2 / OpenSSL 4.0.3 TDEA-ECB (explicit K1||K2||K1), not CryptoScript.
    // Inputs: Imk above, PANs in the boundary TestCases, PSN 00 (also the omitted-PSN default).
    // EMV Book 2 v4.4 A1.4.2 preparation followed by E(Y)||E(~Y), then odd parity on bit 0.
    // PAN 1234567890123456789: packed input 0123456789012345678900;
    // SHA-1 FBBFA1E09EB192CE450C61C05297AB9CDB65854E; Y 1091924506105297;
    // raw ciphertext 2E9275306B4FCB2A53A1CB1710DDAEF1 (before parity).
    private const string Pan1Icc = "F77F048F6D1F3E9D9BE53D6BC7328086";
    private const string Pan15Icc = "2F4AEF9837CE89AB670710D6CEA7026D";
    private const string Pan16Icc = "154F349D8585CB7F6B0798E9839B10C1";
    private const string Pan17Icc = "1C13AE23B54AF7BA49FBE09192C87983";
    private const string Pan18Icc = "C43446BAC7C71C267A3768EF4F8A5DAB";
    private const string Pan19Icc = "2F9275316B4FCB2A52A1CB1610DCAEF1";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCase(Imk, "99012345678901234", "45", "985EC4FD3EDF6162E31AF1C7D0543416")]
    [TestCase(Imk, "12345678901234567", "01", "AD406D7F6D7570916D75E5DCAB8CF737")]
    [TestCase(Imk, "123456789012345678", "00", "C43446BAC7C71C267A3768EF4F8A5DAB")]
    [TestCase(Imk, "00123456789012", "01", "7C2CF1495458DC3B62913BA87AF7F44A")]
    public void FullScriptVectorsAndMetadata(string key, string pan, string psn, string expected)
    {
        Execute($"KEY IMK=GenerateKey(DES3-ECB,0x({key})) PARAM p=Parameters({Mechanism},#PSN:\"{psn}\") KEY ICC=Derive(p,IMK,\"{pan}\")");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), expected, Mechanism);
    }

    [TestCase("PARAM p=Parameters(KDF-EMV-MASTER-B)")]
    [TestCase("PARAM p=#MECH:KDF-EMV-MASTER-B")]
    public void DefaultPsnAndNormalStringVar(string parameters)
    {
        Execute(Prefix + parameters + " VAR pan=\"123456789012\" KEY ICC=Derive(p,IMK,pan)");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), "FE890EC1B3FBF7EC32A880C8D6169461", Mechanism);
    }

    [Test]
    public void DirectParamAndSessionKeyChaining()
    {
        // Independently computed with DesEdeEngine, without session-key parity correction.
        Execute(Prefix + "PARAM p=#MECH:KDF-EMV-MASTER-B #PSN:\"45\" KEY ICC=Derive(p,IMK,\"99012345678901234\") " +
            "PARAM s=Parameters(KDF-EMV-AC-SESSION) KEY session=Derive(s,ICC,0x(0001))");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("session"), "43A79915202AD2C8504F42A91438B152", "KDF-EMV-AC-SESSION");
    }

    [TestCase("\"\"")]
    [TestCase("\"12345678901234567890\"")]
    [TestCase("\"123A\"")]
    [TestCase("\"１２３\"")]
    [TestCase("\"123 4\"")]
    [TestCase("0x(1234)")]
    [TestCase("b64(MTIzNA==)")]
    [TestCase("1234")]
    [TestCase("IMK")]
    [TestCase("p")]
    [TestCase("b64(ASNFZ4mrze/+3LqYdlQyEA==)")]
    [TestCase("\"0123456789ABCDEFFEDCBA9876543210\"")]
    [TestCase("hexPan")]
    [TestCase("basePan")]
    public void RejectsPanForms(string pan)
    {
        RejectScript(Prefix +
            "VAR hexPan=0x(1234) VAR basePan=b64(MTIzNA==) PARAM p=Parameters(KDF-EMV-MASTER-B) " + $"KEY ICC=Derive(p,IMK,{pan})", "pan");
    }

    [Test]
    public void RejectsSuccessfulNestedStringFunctionByOrigin()
    {
        Execute("VAR comparison=Compare(\"1234\",\"1234\")");
        Assert.That(((StringVariableDeclaration)VariableDictionary.Instance().Get("comparison")).ValueFormat,
            Is.EqualTo(FormatConversions.STR));
        var error = Assert.Throws<SemanticErrorException>(() => Execute(Prefix +
            "PARAM p=Parameters(KDF-EMV-MASTER-B) KEY ICC=Derive(p,IMK,Compare(\"1234\",\"1234\"))"));
        Assert.That(error!.SemanticError!.Message, Does.Contain("direct normal string literal"));
        Assert.That(VariableDictionary.Instance().Contains("ICC"), Is.False);
    }

    [TestCase("\"\"")]
    [TestCase("\"1\"")]
    [TestCase("\"001\"")]
    [TestCase("\"A1\"")]
    [TestCase("\"１２\"")]
    [TestCase("45")]
    [TestCase("0x(45)")]
    [TestCase("b64(NDU=)")]
    [TestCase("psnVar")]
    public void RejectsPsnFormsInBothDeclarationStyles(string psn)
    {
        string prefix = Prefix + "VAR psnVar=\"45\" ";
        RejectScript(prefix + $"PARAM p=Parameters({Mechanism},#PSN:{psn}) KEY ICC=Derive(p,IMK,\"1234\")", "PSN");
        SetUp();
        RejectScript(prefix + $"PARAM p=#MECH:{Mechanism} #PSN:{psn} KEY ICC=Derive(p,IMK,\"1234\")", "PSN");
    }

    [TestCase("#PSN:\"45\",#PSN:\"45\"")]
    [TestCase("#PSN:\"45\",#PSN:\"01\"")]
    [TestCase("#MECH:KDF-EMV-MASTER-B")]
    [TestCase("#MECH:KDF-EMV-AC-SESSION")]
    [TestCase("#IV:0x(0000000000000000)")]
    [TestCase("#PAD:NONE")]
    public void RejectsExtraAndDuplicateFunctionParameters(string extra) =>
        RejectScript(Prefix + $"PARAM p=Parameters({Mechanism},{extra}) KEY ICC=Derive(p,IMK,\"1234\")", "parameter");

    [TestCase("#PSN:\"45\" #PSN:\"45\"")]
    [TestCase("#PSN:\"45\" #PSN:\"01\"")]
    [TestCase("#MECH:KDF-EMV-MASTER-B")]
    [TestCase("#MECH:KDF-EMV-AC-SESSION")]
    [TestCase("#IV:0x(0000000000000000)")]
    public void RejectsExtraAndDuplicateDirectParameters(string extra) =>
        RejectScript(Prefix + $"PARAM p=#MECH:{Mechanism} {extra} KEY ICC=Derive(p,IMK,\"1234\")", "parameter");

    [TestCase("0x(" + Imk + ")")]
    [TestCase("rawKey")]
    [TestCase("p")]
    [TestCase("b64(ASNFZ4mrze/+3LqYdlQyEA==)")]
    [TestCase("\"0123456789ABCDEFFEDCBA9876543210\"")]
    public void RejectsNonKeySources(string key) =>
        RejectScript($"VAR rawKey=0x({Imk}) PARAM p=Parameters({Mechanism}) KEY ICC=Derive(p,{key},\"1234\")", "KEY");

    [Test]
    public void RejectsAdditionalArgumentsAndContradictoryKeySize()
    {
        Execute(Prefix + "PARAM p=Parameters(KDF-EMV-MASTER-B)");
        RejectScript("KEY ICC=Derive(p,IMK,\"1234\",\"45\")", "argument");
        VariableDictionary.Instance().Add(new KeyVariableDeclaration
        {
            Id = "inconsistent", Value = "0x(" + Imk + ")", KeyValue = "0x(" + Imk + ")",
            ValueFormat = FormatConversions.HEX, KeyType = KeyType.Secret(KeyAlgorithm.Tdea),
            KeySizeInBits = new KeySize(192), Type = new CryptoTypeKey()
        });
        RejectScript("KEY ICC=Derive(p,inconsistent,\"1234\")", "size metadata");
    }

    [Test]
    public void ExistingMechanismRetainsLastWriteWinsAndUnknownParametersAreRejected()
    {
        Execute("PARAM old=#MECH:DES3-ECB #PAD:NONE #PAD:PKCS-7");
        Assert.That(((ParameterVariableDeclaration)VariableDictionary.Instance().Get("old")).GetParameter("PAD"), Is.EqualTo("PKCS-7"));
        var parameters = new KDF_EMV_MASTER_B().GenerateParameters(Mechanism);
        parameters.GetParameters().Add("#UNKNOWN", "value");
        var error = Assert.Throws<FunctionContractException>(() => MechanismParameterContractValidator.Validate(
            Mechanism, CryptoScriptFunction.Derive, parameters));
        Assert.That(error!.Error, Is.EqualTo(FunctionContractError.UnknownParameter));
        Assert.That(error.ParameterName, Is.EqualTo("#UNKNOWN"));
    }

    [TestCase("AES-ECB", Imk)]
    [TestCase("DES3-ECB", "0123456789ABCDEFFEDCBA98765432100011223344556677")]
    public void RejectsWrongKeyTypeOrLength(string algorithm, string value) =>
        RejectScript($"KEY IMK=GenerateKey({algorithm},0x({value})) PARAM p=Parameters({Mechanism}) KEY ICC=Derive(p,IMK,\"1234\")", "requires");

    [Test]
    public void EqualKeyValuesUseReferencedMetadata()
    {
        Execute(Prefix + $"KEY aes=GenerateKey(AES-ECB,0x({Imk})) PARAM p=Parameters({Mechanism},#PSN:\"45\") KEY ICC=Derive(p,IMK,\"99012345678901234\")");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), "985EC4FD3EDF6162E31AF1C7D0543416", Mechanism);
        var error = Assert.Throws<SemanticErrorException>(() => Execute("KEY rejected=Derive(p,aes,\"99012345678901234\")"));
        Assert.That(error!.SemanticError!.Message, Does.Contain("Secret TDEA KEY"));
        Assert.That(VariableDictionary.Instance().Contains("rejected"), Is.False);
    }

    [TestCase("#PSN:\"45\"", true)]
    [TestCase("#PSN:45", false)]
    [TestCase("#PSN:\"45\"#PSN:\"45\"", false)]
    [TestCase("#IV:0x(0000000000000000)", false)]
    [TestCase("#MECH:KDF-EMV-AC-SESSION", false)]
    public void SerializedLegacyParameterContracts(string suffix, bool valid)
    {
        var parameter = new ParameterVariableDeclaration();
        Action validate = () =>
        {
            parameter.SetInstance("#MECH:" + Mechanism + suffix);
            MechanismParameterContractValidator.Validate(Mechanism, CryptoScriptFunction.Derive, parameter);
        };
        if (valid) Assert.DoesNotThrow(validate);
        else Assert.That(Assert.Catch(validate), Is.InstanceOf<ArgumentException>().Or.InstanceOf<FunctionContractException>());
    }

    [Test]
    public void LegacyDeriveUsesQuotedPanAndStrictSerializedParameters()
    {
        Execute(Prefix);
        var algorithm = new KDF_EMV_MASTER_B();
        string key = VariableDictionary.Instance().Get("IMK").Value;
        var result = algorithm.Derive(new[] { "#MECH:" + Mechanism + "#PSN:\"45\"", key, "\"99012345678901234\"" });
        AssertKey(result, "985EC4FD3EDF6162E31AF1C7D0543416", Mechanism);
        Assert.Throws<ArgumentException>(() => algorithm.Derive(new[]
            { "#MECH:" + Mechanism + "#PSN:\"45\"#PSN:\"45\"", key, "\"99012345678901234\"" }));
        Assert.Throws<ArgumentException>(() => algorithm.Derive(new[]
            { "#MECH:" + Mechanism, key, "99012345678901234" }));
    }

    [Test]
    public void RegistryFactoryAndDeployedDocumentation()
    {
        Assert.That(AlgorithmFactory.Create(Mechanism), Is.TypeOf<KDF_EMV_MASTER_B>());
        Assert.That(MechanismRegistry.TryGet(Mechanism, out _), Is.True);
        Assert.That(MechanismList.Instance.Mechanisms, Does.Contain(Mechanism));
        Assert.That(InfoDocumentCatalog.TryGet(InfoDocumentId.CreateMechanism(Mechanism), out var catalogEntry), Is.True);
        Assert.That(catalogEntry!.MarkdownFileName, Is.EqualTo("Info.Mech.KDF-EMV-MASTER-B.md"));
        string path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Mech.KDF-EMV-MASTER-B.md");
        Assert.That(File.Exists(path), Is.True);
        string document = File.ReadAllText(path);
        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("normative implementation review"));
            Assert.That(document, Does.Contain("13 October 2026"));
            Assert.That(document, Does.Contain("881ec9219ca7f558603e98acb3696b7e5219201e"));
            Assert.That(document, Does.Contain("A1.3.1").And.Contain("A1.4.1").And.Contain("A1.4.2"));
            Assert.That(document, Does.Contain("not EMV product certification"));
            Assert.That(document, Does.Contain("Secure Messaging").And.Contain("Linux/Unix").And.Contain("FIPS"));
            Assert.That(document, Does.Not.Contain("verification pending").And.Not.Contain("release gate"));
        });
        string example = document.Split("```text")[1].Split("```")[0];
        Execute(example);
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("sessionKey"), "43A79915202AD2C8504F42A91438B152", "KDF-EMV-AC-SESSION");
        string? displayed = null;
        void Capture(string text) => displayed = text;
        OutputOperations.InfoEvent += Capture;
        try { Execute("Info(KDF-EMV-MASTER-B)"); }
        finally { OutputOperations.InfoEvent -= Capture; }
        Assert.That(displayed, Is.EqualTo(document));
    }

    [TestCase("#PSN", "malformed")]
    [TestCase("#UNKNOWN", "malformed")]
    [TestCase("#PSN:\"45\"#PSN", "malformed")]
    [TestCase("#UNKNOWN:value", "only MECH and PSN")]
    [TestCase("#IV:0x(0000000000000000)", "only MECH and PSN")]
    [TestCase("#MECH:KDF-EMV-MASTER-B", "duplicate")]
    [TestCase("#MECH:KDF-EMV-AC-SESSION", "duplicate")]
    [TestCase("#PSN:\"45\"#PSN:\"45\"", "duplicate")]
    [TestCase("#PSN:\"45\"#PSN:\"01\"", "duplicate")]
    [TestCase("#PSN:", "malformed")]
    [TestCase("#", "malformed")]
    public void LegacyDeriveRejectsCompleteMalformedRepresentation(string suffix, string message)
    {
        Execute(Prefix);
        string key = VariableDictionary.Instance().Get("IMK").Value;
        var error = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_B().Derive(new[]
            { "#MECH:" + Mechanism + suffix, key, "\"99012345678901234\"" }));
        Assert.That(error!.Message, Does.Contain(message));
        Assert.That(error.Message, Does.Not.Contain(Imk).And.Not.Contain("99012345678901234"));
        Assert.That(VariableDictionary.Instance().Contains("ICC"), Is.False);
    }

    [TestCase("AES-CBC")]
    [TestCase("DES3-ECB")]
    [TestCase("KDF-EMV-AC-SESSION")]
    public void PsnIsRejectedForOtherMechanismsInEveryParameterForm(string mechanism)
    {
        RejectScript($"PARAM p=Parameters({mechanism},#PSN:\"45\")", "does not allow parameter #PSN");
        SetUp();
        RejectScript($"PARAM p=#MECH:{mechanism} #PSN:\"45\"", "does not allow parameter #PSN");
        var parameter = new ParameterVariableDeclaration();
        var error = Assert.Throws<FunctionContractException>(() =>
            parameter.SetInstance($"#MECH:{mechanism}#PSN:\"45\""));
        Assert.That(error!.Error, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
        Assert.That(error.ParameterName, Is.EqualTo("#PSN"));

        // Also cover already-materialized parameters, independently of SetInstance.
        parameter = new ParameterVariableDeclaration { Mechanism = mechanism };
        parameter.SetParameter("#PSN", "\"45\"");
        error = Assert.Throws<FunctionContractException>(() =>
            MechanismParameterContractValidator.Validate(mechanism, CryptoScriptFunction.Parameters, parameter));
        Assert.That(error!.Error, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
    }

    [Test]
    public void AllOtherRegistryMechanismsRejectPsn()
    {
        foreach (var mechanism in MechanismRegistry.Entries.Where(entry =>
                     entry.CanonicalName != Mechanism && entry.CanonicalName != "KDF-EMV-MASTER-A" &&
                     entry.CanonicalName != "KDF-EMV-MASTER-C"))
        {
            var parameter = new ParameterVariableDeclaration { Mechanism = mechanism.CanonicalName };
            parameter.SetParameter("#PSN", "\"45\"");
            var error = Assert.Throws<FunctionContractException>(() =>
                MechanismParameterContractValidator.Validate(mechanism.CanonicalName, CryptoScriptFunction.Parameters, parameter));
            Assert.That(error!.Error, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter), mechanism.CanonicalName);
        }
    }

    [Test]
    public void ExistingLegacyParserStillIgnoresMalformedSegmentsAndUsesLastWriteWins()
    {
        var parameter = new ParameterVariableDeclaration();
        parameter.SetInstance("#MECH:DES3-ECB#UNKNOWN#PAD:NONE#PAD:PKCS-7");
        Assert.That(parameter.GetParameter("#PAD"), Is.EqualTo("PKCS-7"));
        Assert.That(parameter.GetParameters().ContainsKey("#UNKNOWN"), Is.False);
        parameter.SetInstance("#MECH:KDF-EMV-MASTER-B#MECH:DES3-ECB");
        Assert.That(parameter.Mechanism, Is.EqualTo("DES3-ECB"));
        Execute("PARAM p=Parameters(AES-CBC,#PAD:NONE,#PAD:PKCS-7,#LABEL:\"retained\")");
        parameter = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        Assert.That(parameter.GetParameter("#PAD"), Is.EqualTo("PKCS-7"));
        Assert.That(parameter.GetParameter("#LABEL"), Is.EqualTo("\"retained\""));
    }

    [Test]
    public void ValidSerializedPsnBeforeMechanismRetainsItsValue()
    {
        Execute(Prefix);
        string key = VariableDictionary.Instance().Get("IMK").Value;
        var result = new KDF_EMV_MASTER_B().Derive(new[]
            { "#PSN:\"45\"#MECH:" + Mechanism, key, "\"99012345678901234\"" });
        AssertKey(result, "985EC4FD3EDF6162E31AF1C7D0543416", Mechanism);
    }

    [TestCase("99012345678901234", "45", "985EC4FD3EDF6162E31AF1C7D0543416", "43A79915202AD2C8504F42A91438B152")]
    [TestCase("12345678901234567", "01", "AD406D7F6D7570916D75E5DCAB8CF737", "5CD81C0194FA97A27DD15DC79EDC5CBC")]
    [TestCase("123456789012345678", "00", "C43446BAC7C71C267A3768EF4F8A5DAB", "7024D3A822B63E3E60B3311BBF8E90B0")]
    public void AllFixedVectorsChainThroughStringVarToSession(string pan, string psn, string icc, string session)
    {
        Execute(Prefix + $"VAR pan=\"{pan}\" PARAM p=Parameters({Mechanism},#PSN:\"{psn}\") " +
            "KEY ICC=Derive(p,IMK,pan) PARAM s=Parameters(KDF-EMV-AC-SESSION) KEY session=Derive(s,ICC,0x(0001))");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), icc, Mechanism);
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("session"), session, "KDF-EMV-AC-SESSION");
    }

    [TestCase("1", Pan1Icc)]
    [TestCase("123456789012345", Pan15Icc)]
    [TestCase("1234567890123456", Pan16Icc)]
    [TestCase("12345678901234567", Pan17Icc)]
    [TestCase("123456789012345678", Pan18Icc)]
    [TestCase("1234567890123456789", Pan19Icc)]
    public void PanBoundariesMatchIndependentKnownAnswersAndKeepMetadata(string pan, string expectedIcc)
    {
        Execute(Prefix + $"PARAM p=Parameters({Mechanism}) KEY ICC=Derive(p,IMK,\"{pan}\") " +
            $"PARAM a=Parameters(KDF-EMV-MASTER-A) KEY optionA=Derive(a,IMK,\"{pan}\")");
        var icc = (KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC");
        var optionA = (KeyVariableDeclaration)VariableDictionary.Instance().Get("optionA");
        AssertKey(icc, expectedIcc, Mechanism);
        if (pan.Length <= 16) Assert.That(icc.Value, Is.EqualTo(optionA.Value));
        else Assert.That(icc.Value, Is.Not.EqualTo(optionA.Value));
        Assert.That(optionA.DerivationMechanism, Is.EqualTo("KDF-EMV-MASTER-A"));
    }

    [Test]
    public void WrongSelectedMechanismAndStoredMechanismAreRejected()
    {
        Execute(Prefix);
        string key = VariableDictionary.Instance().Get("IMK").Value;
        Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_B().Derive(new[]
            { "#MECH:KDF-EMV-MASTER-A#PSN:\"45\"", key, "\"99012345678901234\"" }));
        var parameter = new KDF_EMV_MASTER_B().GenerateParameters(Mechanism);
        parameter.SetParameter("#MECH", "KDF-EMV-MASTER-A");
        Assert.Throws<ArgumentException>(() => MechanismParameterContractValidator.Validate(
            Mechanism, CryptoScriptFunction.Derive, parameter));
        Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_B().GenerateParameters("KDF-EMV-MASTER-A"));
    }

    private static void AssertKey(KeyVariableDeclaration key, string expected, string mechanism)
    {
        Assert.Multiple(() =>
        {
            Assert.That(key.Value, Is.EqualTo("0x(" + expected + ")").IgnoreCase);
            Assert.That(key.KeyValue, Is.EqualTo(key.Value));
            Assert.That(key.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(key.KeySizeInBits, Is.EqualTo(new KeySize(128)));
            Assert.That(key.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Tdea)));
            Assert.That(key.Usage, Is.EqualTo(KeyUsagePolicy.Unspecified));
            Assert.That(key.DerivationMechanism, Is.EqualTo(mechanism));
        });
    }

    private static void Execute(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        new CryptoScriptRunner().Execute(context);
    }

    private static void RejectScript(string script, string messageFragment)
    {
        var error = Assert.Throws<SemanticErrorException>(() => Execute(script));
        Assert.That(error!.SemanticError!.Message, Does.Contain(messageFragment).IgnoreCase);
        Assert.That(VariableDictionary.Instance().Contains("ICC"), Is.False);
    }
}
