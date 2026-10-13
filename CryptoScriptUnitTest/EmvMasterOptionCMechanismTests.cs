using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvMasterOptionCMechanismTests
{
    private const string Mechanism = "KDF-EMV-MASTER-C";
    private const string Imk128 = "2EF6E07ECBA86BCF3C3CFF7BBEBE6F38";
    private const string Imk192 = "7481B0525D05D393D6DCBADD333C6BC514087C6BF5FA10C4";
    private const string Imk256 = "14D63F23982740AC65B482BAF5913092D8132BAA4143A24D3CF437232711A507";
    private const string Pan = "99012345678901234";
    private const string Icc128 = "35C006583E2B417207AE5B166C0EA6EF";
    private const string Icc192 = "62566BB70C70FC5C90E42F516AA7C4C5C791E8450BFE7018";
    private const string Icc256 = "8FF3C2A25D2AA18F0DE61855EE142340ADB1A69B33939F5DF93E03AF9DE93357";
    private const string Prefix = "KEY IMK=GenerateKey(AES-ECB,0x(" + Imk128 + ")) ";

    // Fixed AP5.1 characterizations, independently rechecked with Python cryptography /
    // OpenSSL in AP5.2. No expected value is calculated with either production core.
    public static IEnumerable<TestCaseData> HierarchyCases()
    {
        var vectors = new[]
        {
            (128, Imk128, Icc128, "1E955EB21AFFA73F01DBED4D41BD25F4"),
            (192, Imk192, Icc192, "3006A6041278FCD5AB0DFC5D679777E8F34EF83E90188E85"),
            (256, Imk256, Icc256, "8BAB29FA43EB767878BD3EA707193E5C1628F21558791FFF78FE275AB845A1D6")
        };
        foreach (var (bits, imk, icc, session) in vectors)
            foreach (bool directParam in new[] { false, true })
                foreach (bool panVar in new[] { false, true })
                    yield return new TestCaseData(bits, imk, icc, session, directParam, panVar);
    }

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCaseSource(nameof(HierarchyCases))]
    public void FullKeyHierarchyMatchesFixedValuesAndMetadata(
        int bits, string imk, string icc, string session, bool directParam, bool panVar)
    {
        string parameters = directParam ? $"PARAM p=#MECH:{Mechanism} #PSN:\"45\" " :
            $"PARAM p=Parameters({Mechanism},#PSN:\"45\") ";
        Execute($"KEY IMK=GenerateKey(AES-ECB,0x({imk})) " + parameters +
            (panVar ? $"VAR pan=\"{Pan}\" " : "") +
            $"KEY ICC=Derive(p,IMK,{(panVar ? "pan" : "\"" + Pan + "\"")}) " +
            "PARAM s=Parameters(KDF-EMV-AC-SESSION) KEY session=Derive(s,ICC,0x(0001))");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), icc, bits, Mechanism);
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("session"), session, bits, "KDF-EMV-AC-SESSION");
        var original = (KeyVariableDeclaration)VariableDictionary.Instance().Get("IMK");
        Assert.That(original.KeyValue, Is.EqualTo("0x(" + imk + ")").IgnoreCase);
    }

    [TestCase("PARAM p=Parameters(KDF-EMV-MASTER-C)")]
    [TestCase("PARAM p=#MECH:KDF-EMV-MASTER-C")]
    public void DefaultPsnHasIndependentFixedExpectation(string parameters)
    {
        Execute(Prefix + parameters + $" KEY ICC=Derive(p,IMK,\"{Pan}\")");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"),
            "CDDADAB206289E67A9BE8C323C30AE9C", 128, Mechanism);
    }

    [Test]
    public void M1DirectMechanismArgumentIsRejectedWithoutCreatingAKey()
    {
        Execute(Prefix);
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("IMK");
        string originalKey = key.Serialize();
        var error = Assert.Throws<SemanticErrorException>(() =>
            Execute($"KEY ICC=Derive(#MECH:{Mechanism},IMK,\"{Pan}\")"));
        Assert.Multiple(() =>
        {
            Assert.That(error!.SemanticError!.Type, Is.EqualTo("FunctionCall"));
            Assert.That(error.SemanticError.FunctionName, Is.EqualTo("Derive"));
            Assert.That(error.SemanticError.Message, Does.Contain("genuine PARAM variable reference"));
            Assert.That(error.SemanticError.Message, Does.Not.Contain(Imk128).And.Not.Contain(Pan));
            Assert.That(VariableDictionary.Instance().Contains("ICC"), Is.False);
            Assert.That(key.Serialize(), Is.EqualTo(originalKey));
        });
    }

    [Test]
    public void M1ReconstructedParameterFromANonParamVariableIsRejected()
    {
        // A host-supplied VAR carrying serialized parameters retains Kind=Variable.
        // Kind alone cannot distinguish the dispatch's temporary PARAM from a true reference.
        var source = new StringVariableDeclaration
        {
            Id = "serializedParameters", Value = $"#MECH:{Mechanism}#PSN:\"45\"",
            ValueFormat = FormatConversions.PAR
        };
        VariableDictionary.Instance().Add(source);
        string originalValue = source.Value;
        RejectScript(Prefix + $"KEY ICC=Derive(serializedParameters,IMK,\"{Pan}\")",
            "genuine PARAM variable reference");
        Assert.That(VariableDictionary.Instance().Get(source.Id), Is.SameAs(source));
        Assert.That(source.Value, Is.EqualTo(originalValue));
        Assert.That(source.Type, Is.TypeOf<CryptoTypeVar>());
    }

    [TestCase("PARAM p=Parameters(KDF-EMV-MASTER-C,#PSN:\"45\")")]
    [TestCase("PARAM p=#MECH:KDF-EMV-MASTER-C #PSN:\"45\"")]
    public void M1GenuineParameterReferencesPreserveInputsAndKeyMetadata(string declaration)
    {
        Execute(Prefix + declaration + $" VAR pan=\"{Pan}\"");
        var parameters = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("IMK");
        var pan = (StringVariableDeclaration)VariableDictionary.Instance().Get("pan");
        string originalParameters = parameters.Value;
        string originalKey = key.Serialize();
        string originalPan = pan.Value;
        Execute("KEY ICC=Derive(p,IMK,pan)");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), Icc128, 128, Mechanism);
        Assert.Multiple(() =>
        {
            Assert.That(VariableDictionary.Instance().Get("p"), Is.SameAs(parameters));
            Assert.That(parameters.Value, Is.EqualTo(originalParameters));
            Assert.That(VariableDictionary.Instance().Get("IMK"), Is.SameAs(key));
            Assert.That(key.Serialize(), Is.EqualTo(originalKey));
            Assert.That(pan.Value, Is.EqualTo(originalPan));
        });
    }

    [Test]
    public void M1ReconstructionMarksOnlyTheCopiedArgumentAndPreservesItsValueAndKind()
    {
        Execute(Prefix + $"PARAM p=Parameters({Mechanism},#PSN:\"45\")");
        var parameters = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("IMK");
        AlgorithmCallArguments original = Structured(parameters, key);
        var temporary = new ParameterVariableDeclaration();
        temporary.SetInstance(parameters.Value);
        AlgorithmCallArguments reconstructed = original.WithSourceVariable(0, temporary);
        Assert.Multiple(() =>
        {
            Assert.That(original.Arguments[0].HasReconstructedSourceVariable, Is.False);
            Assert.That(original.Arguments[0].SourceVariable, Is.SameAs(parameters));
            Assert.That(reconstructed.Arguments[0].HasReconstructedSourceVariable, Is.True);
            Assert.That(reconstructed.Arguments[0].SourceVariable, Is.SameAs(temporary));
            Assert.That(reconstructed.Arguments[0].Kind, Is.EqualTo(original.Arguments[0].Kind));
            Assert.That(reconstructed.Values, Is.EqualTo(original.Values));
            Assert.That(reconstructed.Arguments[1], Is.SameAs(original.Arguments[1]));
            Assert.That(reconstructed.Arguments[2], Is.SameAs(original.Arguments[2]));
        });
        var error = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_C().Derive(reconstructed));
        Assert.That(error!.Message, Does.Contain("genuine PARAM variable reference"));
        Execute($"KEY ICC=Derive(p,IMK,\"{Pan}\")");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), Icc128, 128, Mechanism);
    }

    [TestCase("KDF-EMV-MASTER-A", "DES3-ECB", "0123456789ABCDEFFEDCBA9876543210", "\"123456789012\"",
        "FE890EC1B3FBF7EC32A880C8D6169461", KeyAlgorithm.Tdea)]
    [TestCase("KDF-EMV-MASTER-B", "DES3-ECB", "0123456789ABCDEFFEDCBA9876543210", "\"123456789012\"",
        "FE890EC1B3FBF7EC32A880C8D6169461", KeyAlgorithm.Tdea)]
    [TestCase("KDF-EMV-AC-SESSION", "AES-ECB", Imk128, "0x(0001)",
        "89F7B697A028A93345BE7A409665B9A4", KeyAlgorithm.Aes)]
    public void M1OtherEmvMechanismsRetainDirectMechanismArguments(
        string mechanism, string generator, string imk, string data, string expected, KeyAlgorithm algorithm)
    {
        Execute($"KEY IMK=GenerateKey({generator},0x({imk})) KEY ICC=Derive(#MECH:{mechanism},IMK,{data})");
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC");
        Assert.Multiple(() =>
        {
            Assert.That(key.Value, Is.EqualTo("0x(" + expected + ")").IgnoreCase);
            Assert.That(key.KeyValue, Is.EqualTo(key.Value));
            Assert.That(key.KeyType, Is.EqualTo(KeyType.Secret(algorithm)));
            Assert.That(key.KeySizeInBits, Is.EqualTo(new KeySize(128)));
            Assert.That(key.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(key.Usage, Is.EqualTo(KeyUsagePolicy.Unspecified));
            Assert.That(key.DerivationMechanism, Is.EqualTo(mechanism));
        });
    }

    [Test]
    public void M1ExplicitLegacyOperationsAdapterRetainsSerializedParametersAndInputs()
    {
        Execute(Prefix + $"PARAM p=Parameters({Mechanism},#PSN:\"45\") VAR pan=\"{Pan}\" KEY ICC=Derive(p,IMK,pan)");
        var parameters = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("IMK");
        string originalKey = key.Serialize();
        string[] values = { parameters.Value, key.Value, VariableDictionary.Instance().Get("pan").Value };
        string[] originalValues = (string[])values.Clone();
        var legacy = (KeyVariableDeclaration)new CryptoOperations().Derive(values);
        AssertKey(legacy, Icc128, 128, Mechanism);
        Assert.That(legacy.Value, Is.EqualTo(VariableDictionary.Instance().Get("ICC").Value));
        Assert.That(values, Is.EqualTo(originalValues));
        Assert.That(parameters.Value, Is.EqualTo(originalValues[0]));
        Assert.That(key.Serialize(), Is.EqualTo(originalKey));
    }

    [TestCase("1", "00", "0C0A4EE137B64B36D85E13A8EDBDF100")]
    [TestCase("123456789012345", "00", "3EC1431AA67BFBC7D39623B1A94AB007")]
    [TestCase("1234567890123456", "00", "D16D4CA4ACEC451576CC36F3A9EF354E")]
    [TestCase("12345678901234567", "00", "04F3C48D63035391BB55B26F2615AA0B")]
    [TestCase("123456789012345678", "00", "E1EECAAD96EDB144E3A9F72CC7045B28")]
    [TestCase("1234567890123456789", "00", "F755500B70BF36ED0ED01ADB4A0005EC")]
    [TestCase("00123456789012", "01", "0490252CF520C76E5AEA5EE593364612")]
    [TestCase(Pan, "99", "BE8EEACA6001F25ACA29CC456F491F0B")]
    public void FullScriptPanBoundariesAndPsnUseFixedCharacterizations(string pan, string psn, string expected)
    {
        Execute(Prefix + $"VAR pan=\"{pan}\" PARAM p=Parameters({Mechanism},#PSN:\"{psn}\") KEY ICC=Derive(p,IMK,pan)");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), expected, 128, Mechanism);
    }

    [TestCase("\"\"", "ASCII decimal digits")]
    [TestCase("\"12345678901234567890\"", "ASCII decimal digits")]
    [TestCase("\"123A\"", "ASCII decimal digits")]
    [TestCase("\"１２３\"", "ASCII decimal digits")]
    [TestCase("\"١٢٣\"", "ASCII decimal digits")]
    [TestCase("\"123 4\"", "ASCII decimal digits")]
    [TestCase("\"123-4\"", "ASCII decimal digits")]
    [TestCase("0x(1234)", "direct normal string literal")]
    [TestCase("b64(MTIzNA==)", "direct normal string literal")]
    [TestCase("1234", "direct normal string literal")]
    [TestCase("IMK", "direct normal string literal")]
    [TestCase("p", "direct normal string literal")]
    [TestCase("hexPan", "direct normal string literal")]
    [TestCase("basePan", "direct normal string literal")]
    [TestCase("intPan", "direct normal string literal")]
    public void RejectsInvalidPanWithSpecificSemanticError(string pan, string message)
    {
        RejectScript(Prefix + "VAR hexPan=0x(1234) VAR basePan=b64(MTIzNA==) VAR intPan=1234 " +
            $"PARAM p=Parameters({Mechanism}) KEY ICC=Derive(p,IMK,{pan})", message);
    }

    [Test]
    public void SuccessfulNestedStringFunctionIsRejectedByItsOrigin()
    {
        Execute("VAR comparison=Compare(\"1234\",\"1234\")");
        Assert.That(((StringVariableDeclaration)VariableDictionary.Instance().Get("comparison")).ValueFormat,
            Is.EqualTo(FormatConversions.STR));
        RejectScript(Prefix + $"PARAM p=Parameters({Mechanism}) KEY ICC=Derive(p,IMK,Compare(\"1234\",\"1234\"))",
            "direct normal string literal");
    }

    [TestCase("0x(" + Imk128 + ")")]
    [TestCase("b64(Lvbgfsuoa88PPP97vr5vOA==)")]
    [TestCase("\"2EF6E07ECBA86BCF3C3CFF7BBEBE6F38\"")]
    [TestCase("rawKey")]
    [TestCase("p")]
    public void NonKeyArgumentsNeverFallBackToRegisteredValues(string key)
    {
        RejectScript(Prefix + $"VAR rawKey=0x({Imk128}) PARAM p=Parameters({Mechanism}) KEY ICC=Derive(p,{key},\"1234\")",
            "wrong key argument");
    }

    [TestCase(KeyAlgorithm.Tdea)]
    [TestCase(KeyAlgorithm.Hmac)]
    [TestCase(KeyAlgorithm.Rsa)]
    [TestCase(KeyAlgorithm.Unknown)]
    public void RejectsWrongKeyAlgorithmsFromTheReferencedKey(KeyAlgorithm algorithm)
    {
        AddKey("bad", Imk128, algorithm == KeyAlgorithm.Rsa ? KeyType.Public(algorithm) : KeyType.Secret(algorithm), 128);
        RejectScript($"PARAM p=Parameters({Mechanism}) KEY ICC=Derive(p,bad,\"1234\")", "Secret AES KEY");
    }

    [TestCase(15)]
    [TestCase(17)]
    [TestCase(23)]
    [TestCase(25)]
    [TestCase(31)]
    [TestCase(33)]
    public void RejectsInvalidAesKeyDataLengths(int length)
    {
        AddKey("bad", new string('A', length * 2), KeyType.Secret(KeyAlgorithm.Aes), length * 8);
        RejectScript($"PARAM p=Parameters({Mechanism}) KEY ICC=Derive(p,bad,\"1234\")", "AES key must contain 16, 24 or 32 bytes");
    }

    [Test]
    public void SameValuesKeepKeyIdentityAndRejectContradictorySizeMetadata()
    {
        Execute(Prefix + $"PARAM p=Parameters({Mechanism},#PSN:\"45\")");
        AddKey("wrongType", Imk128, KeyType.Secret(KeyAlgorithm.Tdea), 128);
        AddKey("wrongSize", Imk128, KeyType.Secret(KeyAlgorithm.Aes), 192);
        Execute($"KEY ICC=Derive(p,IMK,\"{Pan}\")");
        AssertKey((KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC"), Icc128, 128, Mechanism);
        RejectScript($"KEY rejected=Derive(p,wrongType,\"{Pan}\")", "Secret AES KEY", "rejected");
        RejectScript($"KEY rejected=Derive(p,wrongSize,\"{Pan}\")", "size metadata", "rejected");
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
    public void RejectsInvalidPsnInBothDeclarationStyles(string psn)
    {
        string prefix = Prefix + "VAR psnVar=\"45\" ";
        RejectScript(prefix + $"PARAM p=Parameters({Mechanism},#PSN:{psn}) KEY ICC=Derive(p,IMK,\"1234\")", "PSN");
        SetUp();
        RejectScript(prefix + $"PARAM p=#MECH:{Mechanism} #PSN:{psn} KEY ICC=Derive(p,IMK,\"1234\")", "PSN");
    }

    [TestCase("#PSN:\"45\",#PSN:\"45\"", "duplicate")]
    [TestCase("#PSN:\"45\",#PSN:\"01\"", "duplicate")]
    [TestCase("#MECH:KDF-EMV-MASTER-C", "duplicate")]
    [TestCase("#MECH:KDF-EMV-MASTER-A", "consistent")]
    [TestCase("#IV:0x(0000000000000000)", "does not allow parameter #IV")]
    [TestCase("#PAD:NONE", "does not allow parameter #PAD")]
    public void FunctionParametersRejectDuplicatesContradictionsAndForbiddenNames(string extra, string message) =>
        RejectScript(Prefix + $"PARAM p=Parameters({Mechanism},{extra}) KEY ICC=Derive(p,IMK,\"1234\")", message);

    [TestCase("#PSN:\"45\" #PSN:\"45\"", "duplicate")]
    [TestCase("#PSN:\"45\" #PSN:\"01\"", "duplicate")]
    [TestCase("#MECH:KDF-EMV-MASTER-C", "duplicate")]
    [TestCase("#MECH:KDF-EMV-MASTER-A", "consistent")]
    [TestCase("#IV:0x(0000000000000000)", "does not allow parameter #IV")]
    public void DirectParametersRejectDuplicatesContradictionsAndForbiddenNames(string extra, string message) =>
        RejectScript(Prefix + $"PARAM p=#MECH:{Mechanism} {extra} KEY ICC=Derive(p,IMK,\"1234\")", message);

    [TestCase("p")]
    [TestCase("p,IMK")]
    [TestCase("p,IMK,\"1234\",\"45\"")]
    public void RejectsMissingAndExtraDeriveArguments(string arguments) =>
        RejectScript(Prefix + $"PARAM p=Parameters({Mechanism}) KEY ICC=Derive({arguments})", "argument");

    [Test]
    public void UnknownNamedParametersAndInconsistentStoredMechanismAreRejected()
    {
        var parameters = new KDF_EMV_MASTER_C().GenerateParameters(Mechanism);
        parameters.GetParameters().Add("#UNKNOWN", "value");
        var key = AddKey("IMK", Imk128, KeyType.Secret(KeyAlgorithm.Aes), 128);
        var error = Assert.Throws<FunctionContractException>(() => new KDF_EMV_MASTER_C().Derive(Structured(parameters, key)));
        Assert.That(error!.Error, Is.EqualTo(FunctionContractError.UnknownParameter));
        Assert.That(error.ParameterName, Is.EqualTo("#UNKNOWN"));
        parameters = new KDF_EMV_MASTER_C().GenerateParameters(Mechanism);
        parameters.SetParameter("#MECH", "KDF-EMV-MASTER-A");
        var contradiction = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_C().Derive(Structured(parameters, key)));
        Assert.That(contradiction!.Message, Does.Contain("consistent #MECH"));
    }

    [Test]
    public void WrongSelectedMechanismIsRejectedAtTheWrapperBoundary()
    {
        var key = AddKey("IMK", Imk128, KeyType.Secret(KeyAlgorithm.Aes), 128);
        var wrong = new KDF_EMV_MASTER_A().GenerateParameters("KDF-EMV-MASTER-A");
        var error = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_C().Derive(Structured(wrong, key)));
        Assert.That(error!.Message, Does.Contain("requires KDF-EMV-MASTER-C parameters"));
        error = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_C().GenerateParameters("KDF-EMV-MASTER-A"));
        Assert.That(error!.Message, Does.Contain("requires KDF-EMV-MASTER-C parameters"));
    }

    [TestCase("#PSN", "malformed")]
    [TestCase("#UNKNOWN", "malformed")]
    [TestCase("#PSN:\"45\"#PSN", "malformed")]
    [TestCase("#UNKNOWN:value", "only MECH and PSN")]
    [TestCase("#IV:0x(0000000000000000)", "only MECH and PSN")]
    [TestCase("#MECH:KDF-EMV-MASTER-C", "duplicate")]
    [TestCase("#MECH:KDF-EMV-MASTER-A", "duplicate")]
    [TestCase("#PSN:\"45\"#PSN:\"45\"", "duplicate")]
    [TestCase("#PSN:\"45\"#PSN:\"01\"", "duplicate")]
    [TestCase("#PSN:", "malformed")]
    [TestCase("#PSN:45", "normal string literal")]
    [TestCase("#", "malformed")]
    public void LegacyDeriveRejectsMalformedParametersBeforeLossyParsing(string suffix, string message)
    {
        Execute(Prefix);
        string key = VariableDictionary.Instance().Get("IMK").Value;
        var error = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_C().Derive(new[]
            { "#MECH:" + Mechanism + suffix, key, "\"" + Pan + "\"" }));
        Assert.That(error!.Message, Does.Contain(message));
        Assert.That(error.Message, Does.Not.Contain(Imk128).And.Not.Contain(Pan));
        Assert.That(VariableDictionary.Instance().Contains("ICC"), Is.False);
    }

    [Test]
    public void LegacyDeriveRetainsKeyMetadataQuotedPanAndParameterOrder()
    {
        Execute(Prefix);
        string key = VariableDictionary.Instance().Get("IMK").Value;
        AssertKey(new KDF_EMV_MASTER_C().Derive(new[] { "#PSN:\"45\"#MECH:" + Mechanism, key, "\"" + Pan + "\"" }),
            Icc128, 128, Mechanism);
        var error = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_C().Derive(new[]
            { "#MECH:" + Mechanism, key, Pan }));
        Assert.That(error!.Message, Does.Contain("direct normal string literal"));
        AddKey("sameValueWrongType", Imk128, KeyType.Secret(KeyAlgorithm.Tdea), 128);
        error = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_C().Derive(new[]
            { "#MECH:" + Mechanism, key, "\"" + Pan + "\"" }));
        Assert.That(error!.Message, Does.Contain("Ambiguous KEY argument"));
    }

    [Test]
    public void ExistingLegacyParameterSemanticsRemainUnchanged()
    {
        var parameters = new ParameterVariableDeclaration();
        parameters.SetInstance("#MECH:DES3-ECB#UNKNOWN#PAD:NONE#PAD:PKCS-7");
        Assert.That(parameters.GetParameter("PAD"), Is.EqualTo("PKCS-7"));
        Assert.That(parameters.GetParameters().ContainsKey("#UNKNOWN"), Is.False);
        parameters.SetInstance("#MECH:KDF-EMV-MASTER-C#MECH:DES3-ECB");
        Assert.That(parameters.Mechanism, Is.EqualTo("DES3-ECB"));
    }

    [Test]
    public void StructuredValuesCannotActivateLegacyLookup()
    {
        Execute(Prefix);
        string key = VariableDictionary.Instance().Get("IMK").Value;
        var error = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_C().Derive(
            AlgorithmCallArguments.FromValues(new[] { "#MECH:" + Mechanism, key, "\"" + Pan + "\"" })));
        Assert.That(error!.Message, Does.Contain("genuine PARAM variable reference"));
        var parameters = new KDF_EMV_MASTER_C().GenerateParameters(Mechanism);
        error = Assert.Throws<ArgumentException>(() => new KDF_EMV_MASTER_C().Derive(
            new AlgorithmCallArguments(new[]
            {
                new AlgorithmCallArgument(parameters.Value, parameters, ResolvedCallArgumentKind.Variable),
                new AlgorithmCallArgument(key),
                new AlgorithmCallArgument("\"" + Pan + "\"", kind: ResolvedCallArgumentKind.OtherLiteral)
            })));
        Assert.That(error!.Message, Is.EqualTo("wrong key argument"));
    }

    private static AlgorithmCallArguments Structured(ParameterVariableDeclaration parameters, KeyVariableDeclaration key) =>
        new(new[]
        {
            new AlgorithmCallArgument(parameters.Value, parameters, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(key.Value, key, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument("\"" + Pan + "\"", kind: ResolvedCallArgumentKind.OtherLiteral)
        });

    private static KeyVariableDeclaration AddKey(string id, string hex, KeyType type, int bits)
    {
        var key = new KeyVariableDeclaration
        {
            Id = id, Value = "0x(" + hex + ")", KeyValue = "0x(" + hex + ")",
            ValueFormat = FormatConversions.HEX, KeyType = type, KeySizeInBits = new KeySize(bits),
            Type = new CryptoTypeKey()
        };
        VariableDictionary.Instance().Add(key);
        return key;
    }

    private static void AssertKey(KeyVariableDeclaration key, string expected, int bits, string mechanism)
    {
        Assert.Multiple(() =>
        {
            Assert.That(key.Value, Is.EqualTo("0x(" + expected + ")").IgnoreCase);
            Assert.That(key.KeyValue, Is.EqualTo(key.Value));
            Assert.That(key.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(key.KeySizeInBits, Is.EqualTo(new KeySize(bits)));
            Assert.That(key.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Aes)));
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

    private static void RejectScript(string script, string message, string resultId = "ICC")
    {
        var error = Assert.Throws<SemanticErrorException>(() => Execute(script));
        Assert.That(error!.SemanticError!.Message, Does.Contain(message).IgnoreCase);
        Assert.That(error.SemanticError.Message, Does.Not.Contain(Imk128).And.Not.Contain(Pan));
        Assert.That(VariableDictionary.Instance().Contains(resultId), Is.False);
    }
}
