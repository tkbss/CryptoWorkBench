using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvMasterOptionAMechanismTests
{
    private const string Mechanism = "KDF-EMV-MASTER-A";
    private const string Imk = "0123456789ABCDEFFEDCBA9876543210";
    private const string Prefix = "KEY IMK=GenerateKey(DES3-ECB,0x(" + Imk + ")) ";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCase(Imk, "99012345678901234", "45", "67F8292358083E5EA7AB7FDA58D53B6B")]
    [TestCase(Imk, "12345678901234567", "01", "73AD54688CEF2934B0979857E3C719F1")]
    [TestCase(Imk, "123456789012", "00", "FE890EC1B3FBF7EC32A880C8D6169461")]
    [TestCase("00112233445566778899AABBCCDDEEFF", "99012345678901234", "45", "7FABA85B199461BFA83DBAF461010E2F")]
    [TestCase(Imk, "00123456789012", "01", "7C2CF1495458DC3B62913BA87AF7F44A")]
    public void FullScriptVectorsAndMetadata(string key, string pan, string psn, string expected)
    {
        Execute($"KEY IMK=GenerateKey(DES3-ECB,0x({key})) PARAM p=Parameters({Mechanism},#PSN:\"{psn}\") KEY ICC=Derive(p,IMK,\"{pan}\")");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), expected, Mechanism);
    }

    [TestCase("PARAM p=Parameters(KDF-EMV-MASTER-A)")]
    [TestCase("PARAM p=#MECH:KDF-EMV-MASTER-A")]
    public void DefaultPsnAndNormalStringVar(string parameters)
    {
        Execute(Prefix + parameters + " VAR pan=\"123456789012\" KEY ICC=Derive(p,IMK,pan)");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), "FE890EC1B3FBF7EC32A880C8D6169461", Mechanism);
    }

    [Test]
    public void DirectParamAndSessionKeyChaining()
    {
        // Independently computed with DesEdeEngine, without session-key parity correction.
        Execute(Prefix + "PARAM p=#MECH:KDF-EMV-MASTER-A #PSN:\"45\" KEY ICC=Derive(p,IMK,\"99012345678901234\") " +
            "PARAM s=Parameters(KDF-EMV-AC-SESSION) KEY session=Derive(s,ICC,0x(0001))");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("session"), "68533955B11DEC088A36B4871DEAF9CF", "KDF-EMV-AC-SESSION");
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
            "VAR hexPan=0x(1234) VAR basePan=b64(MTIzNA==) PARAM p=Parameters(KDF-EMV-MASTER-A) " + $"KEY ICC=Derive(p,IMK,{pan})", "pan");
    }

    [Test]
    public void RejectsSuccessfulNestedStringFunctionByOrigin()
    {
        Execute("VAR comparison=Compare(\"1234\",\"1234\")");
        Assert.That(((StringVariableDeclaration)VariableDictionary.Instance().Get("comparison")).ValueFormat,
            Is.EqualTo(FormatConversions.STR));
        var error = Assert.Throws<SemanticErrorException>(() => Execute(Prefix +
            "PARAM p=Parameters(KDF-EMV-MASTER-A) KEY ICC=Derive(p,IMK,Compare(\"1234\",\"1234\"))"));
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
    [TestCase("#MECH:KDF-EMV-MASTER-A")]
    [TestCase("#MECH:KDF-EMV-AC-SESSION")]
    [TestCase("#IV:0x(0000000000000000)")]
    [TestCase("#PAD:NONE")]
    public void RejectsExtraAndDuplicateFunctionParameters(string extra) =>
        RejectScript(Prefix + $"PARAM p=Parameters({Mechanism},{extra}) KEY ICC=Derive(p,IMK,\"1234\")", "parameter");

    [TestCase("#PSN:\"45\" #PSN:\"45\"")]
    [TestCase("#PSN:\"45\" #PSN:\"01\"")]
    [TestCase("#MECH:KDF-EMV-MASTER-A")]
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
        Execute(Prefix + "PARAM p=Parameters(KDF-EMV-MASTER-A)");
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
        var parameters = new KDF_EMV_MASTER_A().GenerateParameters(Mechanism);
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
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), "67F8292358083E5EA7AB7FDA58D53B6B", Mechanism);
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
        var algorithm = new KDF_EMV_MASTER_A();
        string key = VariableDictionary.Instance().Get("IMK").Value;
        var result = algorithm.Derive(new[] { "#MECH:" + Mechanism + "#PSN:\"45\"", key, "\"99012345678901234\"" });
        AssertKey(result, "67F8292358083E5EA7AB7FDA58D53B6B", Mechanism);
        Assert.Throws<ArgumentException>(() => algorithm.Derive(new[]
            { "#MECH:" + Mechanism + "#PSN:\"45\"#PSN:\"45\"", key, "\"99012345678901234\"" }));
        Assert.Throws<ArgumentException>(() => algorithm.Derive(new[]
            { "#MECH:" + Mechanism, key, "99012345678901234" }));
    }

    [Test]
    public void RegistryFactoryAndDeployedDocumentation()
    {
        Assert.That(AlgorithmFactory.Create(Mechanism), Is.TypeOf<KDF_EMV_MASTER_A>());
        Assert.That(MechanismRegistry.TryGet(Mechanism, out _), Is.True);
        Assert.That(MechanismList.Instance.Mechanisms, Does.Contain(Mechanism));
        string path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Mech.KDF-EMV-MASTER-A.md");
        Assert.That(File.Exists(path), Is.True);
        string document = File.ReadAllText(path);
        Assert.That(document, Does.Contain("release gate"));
        string example = document.Split("```text")[1].Split("```")[0];
        Execute(example);
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("sessionKey"), "68533955B11DEC088A36B4871DEAF9CF", "KDF-EMV-AC-SESSION");
        string? displayed = null;
        void Capture(string text) => displayed = text;
        OutputOperations.InfoEvent += Capture;
        try { Execute("Info(KDF-EMV-MASTER-A)"); }
        finally { OutputOperations.InfoEvent -= Capture; }
        Assert.That(displayed, Is.EqualTo(document));
    }

    [TestCase("#PSN", "malformed")]
    [TestCase("#UNKNOWN", "malformed")]
    [TestCase("#PSN:\"45\"#PSN", "malformed")]
    [TestCase("#UNKNOWN:value", "only MECH and PSN")]
    [TestCase("#IV:0x(0000000000000000)", "only MECH and PSN")]
    [TestCase("#MECH:KDF-EMV-MASTER-A", "duplicate")]
    [TestCase("#MECH:KDF-EMV-AC-SESSION", "duplicate")]
    [TestCase("#PSN:\"45\"#PSN:\"45\"", "duplicate")]
    [TestCase("#PSN:\"45\"#PSN:\"01\"", "duplicate")]
    [TestCase("#PSN:", "malformed")]
    [TestCase("#", "malformed")]
    public void LegacyDeriveRejectsCompleteMalformedRepresentation(string suffix, string message)
    {
        Execute(Prefix);
        string key = VariableDictionary.Instance().Get("IMK").Value;
        var error = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_A().Derive(new[]
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
        foreach (var mechanism in MechanismRegistry.Entries.Where(entry => entry.CanonicalName != Mechanism))
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
        parameter.SetInstance("#MECH:KDF-EMV-MASTER-A#MECH:DES3-ECB");
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
        var result = new KDF_EMV_MASTER_A().Derive(new[]
            { "#PSN:\"45\"#MECH:" + Mechanism, key, "\"99012345678901234\"" });
        AssertKey(result, "67F8292358083E5EA7AB7FDA58D53B6B", Mechanism);
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
