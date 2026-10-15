using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.EMV;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

internal static class EmvAcFixtures
{
    internal const string Data = "000102030405060708090A0B0C0D0E0F";
    internal const string AesKey = "89F7B697A028A93345BE7A409665B9A4";
    internal const string AesAc = "D3A78E1517C22F70";

    // Fixed independent AP6.2 Python cryptography/OpenSSL characterizations.
    // Expected values are never calculated by the tested core or wrappers.
    internal static readonly (string Cipher, string Key, string Ac)[] Vectors =
    {
        ("DES3-ECB", "5B10C70AFEE94975A345C69888048FF9", "613D21599AAC13F5"),
        ("AES-ECB", AesKey, AesAc),
        ("AES-ECB", "78429FD2061D24B1F8830B2C91D3ED95ED9D4B069A7C2707", "497A0F0552EDFE73"),
        ("AES-ECB", "A99C5840A0CBFBF093DEA740FFFFF5A241BFE9472968CC6E0B9EC76BA280CE0F", "3AFC4EF4D46F8F27")
    };

    public static IEnumerable<TestCaseData> Matrix()
    {
        foreach (var v in Vectors)
            foreach (bool direct in new[] { false, true })
                foreach (bool literal in new[] { false, true })
                    yield return new TestCaseData(v.Cipher, v.Key, v.Ac, direct, literal);
    }

    internal static string Prefix(string cipher = "AES-ECB", string key = AesKey) =>
        $"KEY sk=GenerateKey({cipher},0x({key})) VAR data=0x({Data}) ";

    internal static CryptoScriptRunner Execute(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        var runner = new CryptoScriptRunner();
        runner.Execute(context);
        return runner;
    }

    internal static AlgorithmCallArguments Structured(string parameterName = "p") => new(new[]
    {
        new AlgorithmCallArgument(VariableDictionary.Instance().Get(parameterName).Value,
            VariableDictionary.Instance().Get(parameterName), ResolvedCallArgumentKind.Variable),
        new AlgorithmCallArgument(VariableDictionary.Instance().Get("sk").Value,
            VariableDictionary.Instance().Get("sk"), ResolvedCallArgumentKind.Variable),
        new AlgorithmCallArgument(VariableDictionary.Instance().Get("data").Value,
            VariableDictionary.Instance().Get("data"), ResolvedCallArgumentKind.Variable)
    });

    internal static void Reset()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }
}

[NonParallelizable]
public class EmvSpecificParameterBindingTests
{
    private static readonly (string Name, string Value, string Owner)[] Restricted =
    {
        ("#EXPECTED", "0x(D3A78E1517C22F70)", "MAC-EMV-AC-VERIFY"),
        ("#ARC", "0x(3030)", "MAC-EMV-ARPC-METHOD-1"),
        ("#CSU", "0x(00000000)", "MAC-EMV-ARPC-METHOD-2")
    };

    [SetUp]
    public void SetUp() => EmvAcFixtures.Reset();

    public static IEnumerable<TestCaseData> ForbiddenDeclarations()
    {
        foreach (string mechanism in new[] { "AES-CMAC", "DES3-RETAIL", "KDF-HKDF", "AES-CBC",
                     "MAC-EMV-AC", "MAC-EMV-AC-VERIFY", "MAC-EMV-ARPC-METHOD-1", "MAC-EMV-ARPC-METHOD-2" })
            foreach (var p in Restricted.Where(p => p.Owner != mechanism))
                foreach (bool direct in new[] { false, true })
                    yield return new TestCaseData(mechanism, p.Name, p.Value, direct);
    }

    [TestCaseSource(nameof(ForbiddenDeclarations))]
    [Category("EmvParameterBinding")]
    public void PublicRunnerRejectsOnlyOwnerRestrictedParametersBeforeOperations(
        string mechanism, string name, string value, bool direct)
    {
        string operation = mechanism == "KDF-HKDF" ? "Derive" : mechanism == "AES-CBC" ? "Encrypt" : "Mac";
        string script = Prefix(mechanism) + Declaration(mechanism, name + ":" + value, direct) +
            $"VAR result={operation}(p,sk,data) VAR after=0x(AABB)";
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        var entered = new List<string>();
        var runner = new CryptoScriptRunner((call, _) => entered.Add(call.Name));
        var error = Assert.Throws<SemanticErrorException>(() => runner.Execute(context));
        Assert.That(error!.SemanticError!.Message, Does.Contain("does not allow parameter " + name));
        Assert.That(error.SemanticError.Type, Is.EqualTo(direct ? "Declaration" : "FunctionContract"));
        if (!direct)
            Assert.That(error.SemanticError.ErrorCode, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
        Assert.That(entered, Does.Not.Contain(operation));
        Assert.That(VariableDictionary.Instance().Contains("p"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
    }

    public static IEnumerable<TestCaseData> GenericRestrictedParameters() =>
        new[] { "AES-CMAC", "DES3-RETAIL" }.SelectMany(m => Restricted.Select(p =>
            new TestCaseData(m, p.Name, p.Value)));

    [TestCaseSource(nameof(GenericRestrictedParameters))]
    [Category("EmvParameterBinding")]
    public void ActualMacUseRejectsLaterChangesBeforeKeyDecoding(string mechanism, string name, string value)
    {
        EmvAcFixtures.Execute(Prefix(mechanism) + Declaration(mechanism, "", false));
        var p = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        p.SetParameter(name, value);
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("sk");
        key.Value = key.KeyValue = "0x(INVALID)"; // A primitive/decoder error must not win over the contract error.
        string before = key.Serialize(), parametersBefore = p.Value;
        var error = Assert.Throws<SemanticErrorException>(() =>
            EmvAcFixtures.Execute("VAR result=Mac(p,sk,data) VAR after=0x(AABB)"));
        Assert.That(error!.SemanticError!.ErrorCode, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
        Assert.That(error.SemanticError.Identifier, Is.EqualTo(name));
        Assert.That(key.Serialize(), Is.EqualTo(before));
        Assert.That(p.Value, Is.EqualTo(parametersBefore));
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);

        // Both the public value-only facade and direct algorithm legacy calls must reject it too.
        string[] identifiers = { "p", "sk", "data" };
        Assert.Throws<FunctionContractException>(() => new CryptoOperations().Mac(identifiers));
        Assert.Throws<FunctionContractException>(() => AlgorithmFactory.Create(mechanism).Mac(identifiers));
        Assert.Throws<FunctionContractException>(() => AlgorithmFactory.Create(mechanism).Mac(new[]
            { p.Value, key.Value, VariableDictionary.Instance().Get("data").Value }));
    }

    [TestCaseSource(nameof(GenericRestrictedParameters))]
    [Category("EmvParameterBinding")]
    public void SerializedAndReconstructedParametersCannotBypassBinding(string mechanism, string name, string value)
    {
        EmvAcFixtures.Execute(Prefix(mechanism));
        string serialized = "#MECH:" + mechanism + "#PAD:ISO-9797-M2#MACLEN:8" + name + ":" + value;
        var error = Assert.Throws<FunctionContractException>(() => new ParameterVariableDeclaration().SetInstance(serialized));
        Assert.That(error!.Error, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
        Assert.That(error.ParameterName, Is.EqualTo(name));
        VariableDictionary.Instance().Add(new StringVariableDeclaration
            { Id = "fake", Value = serialized, ValueFormat = FormatConversions.PAR });
        var semantic = Assert.Throws<SemanticErrorException>(() =>
            EmvAcFixtures.Execute("VAR result=Mac(fake,sk,data) VAR after=0x(AABB)"));
        Assert.That(semantic!.SemanticError!.ErrorCode, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        Assert.Throws<FunctionContractException>(() => new CryptoOperations().GenerateParameters(
            new[] { mechanism, name + ":" + value }));
    }

    [Test]
    [Category("EmvParameterBinding")]
    public void AllRegisteredMechanismsKeepEachNewNameBoundToItsOnlyOwner()
    {
        foreach (var entry in MechanismRegistry.Entries)
            foreach (var p in Restricted)
            {
                var parameters = new ParameterVariableDeclaration { Mechanism = entry.CanonicalName };
                parameters.SetParameter(p.Name, p.Value);
                if (entry.CanonicalName == p.Owner)
                    Assert.DoesNotThrow(() => MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(
                        entry.CanonicalName, parameters));
                else
                    Assert.That(Assert.Throws<FunctionContractException>(() =>
                        MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(entry.CanonicalName, parameters))!
                        .Error, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
                parameters.GetParameters()["#MECH"] = "AES-CMAC";
                Assert.Throws<FunctionContractException>(() =>
                    MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(p.Owner, parameters));
                parameters.GetParameters()["#MECH"] = p.Owner;
                if (entry.CanonicalName != p.Owner)
                    Assert.Throws<FunctionContractException>(() =>
                        MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(p.Owner, parameters));
            }
    }

    [TestCase("AES-CMAC", false, "D3A78E1517C22F7040A3556A23B8A2A7")]
    [TestCase("AES-CMAC", true, "D3A78E1517C22F7040A3556A23B8A2A7")]
    [TestCase("DES3-RETAIL", false, "613D21599AAC13F5")]
    [TestCase("DES3-RETAIL", true, "613D21599AAC13F5")]
    [Category("EmvParameterBinding")]
    public void HistoricalParametersDuplicatesAndLegacyMacValuesRemainValid(string mechanism, bool direct, string expected)
    {
        string historical = mechanism == "AES-CMAC" ? "#IV:0x(01020304),#PAD:PKCS-7,#MACLEN:4" : "#MACLEN:4,#MACLEN:8";
        EmvAcFixtures.Execute(Prefix(mechanism) + Declaration(mechanism, historical, direct) + "VAR result=Mac(p,sk,data)");
        var p = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        var key = VariableDictionary.Instance().Get("sk");
        var data = VariableDictionary.Instance().Get("data");
        Assert.That(VariableDictionary.Instance().Get("result").Value, Is.EqualTo("0x(" + expected + ")").IgnoreCase);
        foreach (string[] values in new[] { new[] { "p", "sk", "data" }, new[] { p.Value, key.Value, data.Value } })
            Assert.That(new CryptoOperations().Mac(values).Value, Is.EqualTo("0x(" + expected + ")").IgnoreCase);
    }

    private static string Prefix(string mechanism) => mechanism == "DES3-RETAIL"
        ? EmvAcFixtures.Prefix("DES3-ECB", EmvAcFixtures.Vectors[0].Key) : EmvAcFixtures.Prefix();

    private static string Declaration(string mechanism, string additional, bool direct)
    {
        string required = mechanism switch
        {
            "DES3-RETAIL" => "#PAD:ISO-9797-M2,#MACLEN:8",
            "KDF-HKDF" => "#HASH:HASH-SHA256,#OUTLEN:128",
            "AES-CBC" => "#IV:0x(00000000000000000000000000000000),#PAD:NONE",
            "MAC-EMV-AC-VERIFY" => "#EXPECTED:0x(D3A78E1517C22F70)",
            "MAC-EMV-ARPC-METHOD-1" => "#ARC:0x(3030)",
            "MAC-EMV-ARPC-METHOD-2" => "#CSU:0x(00000000)",
            _ => ""
        };
        string named = string.Join(',', new[] { required, additional }.Where(value => value.Length > 0));
        return direct ? $"PARAM p=#MECH:{mechanism} {named.Replace(',', ' ')} " :
            $"PARAM p=Parameters({mechanism}{(named.Length == 0 ? "" : "," + named)}) ";
    }
}

[NonParallelizable]
public class EmvParameterLossAndDiagnosticRegressionTests
{
    private static readonly string[] Names = { "#EXPECTED", "#ARC", "#CSU" };
    private static readonly string[] LegacyWrapMechanisms =
    {
        "WRAP-AES-PINBLOCK-4", "WRAP-AES-TR31", "WRAP-DES3-PINBLOCK-0",
        "WRAP-DES3-PINBLOCK-1", "WRAP-DES3-PINBLOCK-2", "WRAP-DES3-PINBLOCK-3", "WRAP-DES3-TR31"
    };

    [SetUp]
    public void SetUp() => EmvAcFixtures.Reset();

    public static IEnumerable<TestCaseData> SerializedCases() =>
        new[] { "AES-CMAC", "DES3-RETAIL" }.SelectMany(mechanism => Names.Select(name =>
            new TestCaseData(mechanism, name)));

    [TestCaseSource(nameof(SerializedCases))]
    [Category("EmvParameterInformationLoss")]
    public void ValuelessSegmentsAreRejectedBeforeLegacyMacOrRunnerCanDiscardThem(string mechanism, string name)
    {
        EmvAcFixtures.Execute(mechanism == "AES-CMAC" ? EmvAcFixtures.Prefix() :
            EmvAcFixtures.Prefix("DES3-ECB", EmvAcFixtures.Vectors[0].Key));
        foreach (string spelling in new[] { name, name.ToLowerInvariant() })
        {
            string serialized = "#MECH:" + mechanism + "#PAD:ISO-9797-M2#MACLEN:8" + spelling;
            var error = Assert.Throws<FunctionContractException>(() =>
                new ParameterVariableDeclaration().SetInstance(serialized));
            Assert.That(error!.ParameterName, Is.EqualTo(name));
            Assert.That(error.Error, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
            Assert.Throws<FunctionContractException>(() => new CryptoOperations().Mac(new[]
                { serialized, VariableDictionary.Instance().Get("sk").Value, "0x(" + EmvAcFixtures.Data + ")" }));
            VariableDictionary.Instance().Add(new StringVariableDeclaration
                { Id = "fake", Value = serialized, ValueFormat = FormatConversions.PAR });
            var runner = new CryptoScriptRunner();
            var semantic = Assert.Throws<SemanticErrorException>(() =>
                Execute(runner, "VAR result=Mac(fake,sk,data) VAR after=0x(AABB)"));
            Assert.That(semantic!.SemanticError!.ErrorCode, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
            Assert.That(VariableDictionary.Instance().Get("fake").Value, Is.EqualTo(serialized));
            AssertNoResults();
        }
    }

    [TestCase("#EXPECTED")]
    [TestCase("#ARC")]
    [TestCase("#CSU")]
    [Category("EmvParameterInformationLoss")]
    public void StructuredHashValidatesTheCurrentParamBeforeItsStaleValueProjection(string name)
    {
        EmvAcFixtures.Execute("PARAM p=Parameters(HASH-SHA256) VAR data=0x(" + EmvAcFixtures.Data + ")");
        var p = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        string staleValue = p.Value;
        p.GetParameters().Add(name.ToLowerInvariant(), "0x(3030)");
        var snapshot = p.GetParameters().ToArray();
        var runner = new CryptoScriptRunner();
        var error = Assert.Throws<SemanticErrorException>(() =>
            Execute(runner, "VAR result=Hash(p,data) VAR after=0x(AABB)"));
        Assert.That(error!.SemanticError!.ErrorCode, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
        Assert.That(error.SemanticError.FunctionName, Is.EqualTo("Hash"));
        Assert.That(error.SemanticError.Identifier, Is.EqualTo(name));
        Assert.That(p.Value, Is.EqualTo(staleValue));
        Assert.That(p.GetParameters().ToArray(), Is.EqualTo(snapshot));
        Assert.That(VariableDictionary.Instance().Get("p"), Is.SameAs(p));
        AssertNoResults();
    }

    public static IEnumerable<TestCaseData> WrapCases() => LegacyWrapMechanisms.SelectMany(mechanism =>
        Names.Select(name => new TestCaseData(mechanism, name)));

    [TestCaseSource(nameof(WrapCases))]
    [Category("EmvParameterInformationLoss")]
    public void LegacyWrapGeneratorsCannotDiscardRestrictedNames(string mechanism, string name)
    {
        foreach (string supplied in new[] { name + ":0x(3030)", name.TrimStart('#').ToLowerInvariant() })
        {
            var error = Assert.Throws<FunctionContractException>(() =>
                new CryptoOperations().GenerateParameters(mechanism, supplied));
            Assert.That(error!.Error, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
            Assert.That(error.ParameterName, Is.EqualTo(name));
            Assert.That(error.Mechanism, Is.EqualTo(mechanism));
        }
    }

    [TestCaseSource(nameof(LegacyWrapMechanisms))]
    [Category("EmvParameterInformationLoss")]
    public void LegacyWrapFallbackStillToleratesUnrelatedNames(string mechanism)
    {
        foreach (string[] args in new[] { new[] { mechanism },
                     new[] { mechanism, "#IV:0x(01020304)", "#ARC_SUFFIX:ignored", "#BROKEN" },
                     new[] { mechanism, "#IV:ARC#EXPECTED#ARC#CSU" } })
        {
            var p = (ParameterVariableDeclaration)new CryptoOperations().GenerateParameters(args);
            Assert.That(p.Mechanism, Is.Empty);
            Assert.That(p.GetParameters(), Is.Empty);
        }
    }

    [TestCase("AES-CMAC", "D3A78E1517C22F7040A3556A23B8A2A7")]
    [TestCase("DES3-RETAIL", "613D21599AAC13F5")]
    [Category("EmvParameterInformationLoss")]
    public void OtherValuelessSegmentsAndNonMatchingNamesKeepHistoricalMacResults(string mechanism, string expected)
    {
        string serialized = "#MECH:" + mechanism + "#PAD:ISO-9797-M2#MACLEN:8" +
            "#BROKEN#ARC_SUFFIX#EXPECTED_SUFFIX#CSU_SUFFIX";
        var p = new ParameterVariableDeclaration();
        // The segment scanner must not inspect words within a parameter value.
        p.SetInstance(serialized + "#IV:ARC CSU EXPECTED");
        Assert.That(p.GetParameter("#IV"), Is.EqualTo("ARC CSU EXPECTED"));
        EmvAcFixtures.Execute(mechanism == "AES-CMAC" ? EmvAcFixtures.Prefix() :
            EmvAcFixtures.Prefix("DES3-ECB", EmvAcFixtures.Vectors[0].Key));
        Assert.That(new CryptoOperations().Mac(new[]
            { serialized, VariableDictionary.Instance().Get("sk").Value, "0x(" + EmvAcFixtures.Data + ")" }).Value,
            Is.EqualTo("0x(" + expected + ")").IgnoreCase);
    }

    [Test]
    [Category("EmvParameterInformationLoss")]
    public void HashStillUsesItsHistoricalInputsAndIgnoresUnrelatedLiveParameters()
    {
        EmvAcFixtures.Execute("PARAM p=Parameters(HASH-SHA256) VAR data=0x(" + EmvAcFixtures.Data + ")");
        var p = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        p.GetParameters()["#IV"] = "ARC CSU EXPECTED";
        EmvAcFixtures.Execute("VAR result=Hash(p,data) VAR after=0x(AABB)");
        Assert.That(VariableDictionary.Instance().Get("result").Value,
            Is.EqualTo("0x(BE45CB2605BF36BEBDE684841A28F0FD43C69850A3DCE5FEDBA69928EE3A8991)").IgnoreCase);
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.True);
    }

    [Test]
    [Category("EmvParameterInformationLoss")]
    public void LegacyNamedArgumentValuesAreNotReparsedAsSerializedSegments()
    {
        const string value = "ARC#EXPECTED#ARC#CSU";
        var p = (ParameterVariableDeclaration)new CryptoOperations().GenerateParameters("AES-CMAC", "#IV:" + value);
        // AES-CMAC historically ignores IV arguments; the preflight must not turn
        // words inside that ignored value into supplied parameter names.
        Assert.That(p.Mechanism, Is.EqualTo("AES-CMAC"));
        Assert.That(p.GetParameter("#IV"), Is.Empty);
        Assert.That(p.GetParameters().Keys, Does.Not.Contain("#EXPECTED"));
        Assert.That(p.GetParameters().Keys, Does.Not.Contain("#ARC"));
        Assert.That(p.GetParameters().Keys, Does.Not.Contain("#CSU"));
    }

    [TestCase("MAC-EMV-AC-VERIFY", "#EXPECTED:0x(D3A78E1517C22F70)")]
    [TestCase("MAC-EMV-ARPC-METHOD-1", "#ARC:0x(3030)")]
    [TestCase("MAC-EMV-ARPC-METHOD-2", "#CSU:0x(00000000)")]
    [Category("EmvParameterInformationLoss")]
    public void LegacyOwnersStillAcceptTheirRestrictedParameter(string mechanism, string named)
    {
        var p = (ParameterVariableDeclaration)new CryptoOperations().GenerateParameters(mechanism, named);
        Assert.That(p.Mechanism, Is.EqualTo(mechanism));
        Assert.That(p.GetParameter(named.Split(':', 2)[0]), Is.EqualTo(named.Split(':', 2)[1]));
    }

    [TestCase("#EXPECTED", true)]
    [TestCase("#ARC", true)]
    [TestCase("#CSU", true)]
    [TestCase("#EXPECTED", false)]
    [TestCase("#ARC", false)]
    [TestCase("#CSU", false)]
    [Category("EmvParameterDiagnosticSafety")]
    public void UnknownMechanismNeverAppearsInAnyPublicDiagnostic(string name, bool direct)
    {
        // Synthetic non-production sentinel; deliberately invalid as a mechanism.
        const string sentinel = "DEADBEEF00112233445566778899AABB";
        string unknown = "0x(" + sentinel + ")";
        string serialized = "#MECH:" + unknown + name + ":0x(3030)";
        var contract = Assert.Throws<FunctionContractException>(() =>
            new ParameterVariableDeclaration().SetInstance(serialized));
        Assert.That(contract!.Mechanism, Is.EqualTo("<unknown mechanism>"));
        Assert.That(contract.Message, Does.Not.Contain(sentinel));
        Assert.That(contract.ToString(), Does.Not.Contain(unknown));
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix());
        if (!direct)
            VariableDictionary.Instance().Add(new StringVariableDeclaration
                { Id = "fake", Value = serialized, ValueFormat = FormatConversions.PAR });
        var runner = new CryptoScriptRunner();
        string script = direct ? "PARAM p=#MECH:" + unknown + " " + name + ":0x(3030) " : "";
        script += "VAR result=Mac(" + (direct ? "p" : "fake") + ",sk,data) VAR after=0x(AABB)";
        var error = Assert.Throws<SemanticErrorException>(() => Execute(runner, script));
        Assert.That(error!.SemanticError!.Type, Is.EqualTo(direct ? "Declaration" : "FunctionContract"));
        Assert.That(error.SemanticError.DiagnosticCode, Is.EqualTo(direct
            ? SemanticDiagnosticCode.InvalidDeclaration : SemanticDiagnosticCode.FunctionContractViolation));
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(runner.Diagnostics[0], Is.SameAs(error.SemanticError));
        string[] visible = { error.Message, error.ToString(), error.SemanticError.Message,
            error.SemanticError.FunctionCall, error.SemanticError.Identifier, error.SemanticError.Value,
            error.SemanticError.Location?.Mechanism ?? "", SemanticDiagnosticFormatter.Format(error.SemanticError),
            SemanticDiagnosticFormatter.FormatMany(runner.Diagnostics),
            SemanticDiagnosticFormatter.FormatMany(runner.SemanticErrors) };
        foreach (string output in visible)
        {
            Assert.That(output, Does.Not.Contain(sentinel));
            Assert.That(output, Does.Not.Contain(unknown));
            Assert.That(output, Does.Not.Contain(EmvAcFixtures.AesKey));
        }
        Assert.That(error.SemanticError.Message, Does.Contain("<unknown mechanism>"));
        Assert.That(VariableDictionary.Instance().Contains("p"), Is.False);
        AssertNoResults();
    }

    public static IEnumerable<TestCaseData> LiveMacCases() => Names.SelectMany(name =>
        new[] { false, true }.Select(lower => new TestCaseData(name, lower)));

    [TestCaseSource(nameof(LiveMacCases))]
    [Category("EmvLiveParameterDiagnosticSafety")]
    public void GenuineMutatedMacParametersFailSafelyBeforeTheFactory(string name, bool lower)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + "PARAM p=Parameters(AES-CMAC)");
        var p = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        p.Mechanism = "0x(" + LiveMechanismSentinel + ")";
        p.SetParameter(lower ? name.ToLowerInvariant() : name, "0x(3030)");
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("sk");
        string valueBefore = p.Value, keyBefore = key.Serialize();
        string dataBefore = VariableDictionary.Instance().Get("data").Value;
        var dictionaryBefore = p.GetParameters().ToArray();

        var contract = Assert.Throws<FunctionContractException>(() =>
            new CryptoOperations().Mac(Invocation("p", "sk", "data")));
        Assert.That(contract!.Error, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
        Assert.That(contract.Function, Is.EqualTo(CryptoScriptFunction.Mac));
        Assert.That(contract.Mechanism, Is.EqualTo("<unknown mechanism>"));
        Assert.That(contract.ParameterName, Is.EqualTo(name));
        AssertSafeException(contract);

        var runner = new CryptoScriptRunner();
        var error = Assert.Throws<SemanticErrorException>(() =>
            Execute(runner, "VAR result=Mac(p,sk,data) VAR after=0x(AABB)"));
        AssertSafeContractDiagnostic(runner, error!, "Mac");
        Assert.That(error!.SemanticError!.Identifier, Is.EqualTo(name));
        Assert.That(p.Value, Is.EqualTo(valueBefore));
        Assert.That(p.GetParameters().ToArray(), Is.EqualTo(dictionaryBefore));
        Assert.That(VariableDictionary.Instance().Get("p"), Is.SameAs(p));
        Assert.That(VariableDictionary.Instance().Get("sk"), Is.SameAs(key));
        Assert.That(key.Serialize(), Is.EqualTo(keyBefore));
        Assert.That(VariableDictionary.Instance().Get("data").Value, Is.EqualTo(dataBefore));
        AssertNoResults();
    }

    public static IEnumerable<TestCaseData> OtherFactoryCases() =>
        new[] { "Derive", "Encrypt", "Decrypt", "Wrap", "Unwrap" }.SelectMany(operation =>
            Names.Select(name => new TestCaseData(operation, name)));

    [TestCaseSource(nameof(OtherFactoryCases))]
    [Category("EmvLiveParameterDiagnosticSafety")]
    public void SharedStructuredFactoryEntryChecksLiveParametersBeforeDispatch(string operation, string name)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + "PARAM p=Parameters(AES-CMAC)");
        var p = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        p.Mechanism = "0x(" + LiveMechanismSentinel + ")";
        p.SetParameter(name, "0x(3030)");
        var runner = new CryptoScriptRunner();
        var error = Assert.Throws<SemanticErrorException>(() =>
            Execute(runner, $"VAR result={operation}(p,sk,data) VAR after=0x(AABB)"));
        AssertSafeContractDiagnostic(runner, error!, operation);
        AssertNoResults();
    }

    public static IEnumerable<TestCaseData> ProjectedMechanismCases() =>
        new[] { "GenerateKey", "Parameters" }.SelectMany(operation =>
            Names.Select(name => new TestCaseData(operation, name)));

    [TestCaseSource(nameof(ProjectedMechanismCases))]
    [Category("EmvLiveParameterDiagnosticSafety")]
    public void InvalidLiveParamInAMechanismSlotCannotLeakThroughValueProjection(string operation, string name)
    {
        EmvAcFixtures.Execute("PARAM p=Parameters(AES-CMAC)");
        var p = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        p.Mechanism = "0x(" + LiveMechanismSentinel + ")";
        p.SetParameter(name, "0x(3030)");
        string before = p.Value;
        var arguments = new List<ResolvedCallArgument>(Invocation("p").Arguments);
        if (operation == "GenerateKey")
            arguments.Add(new ResolvedCallArgument("128", ResolvedCallArgumentKind.OtherLiteral));
        var invocation = new OperationInvocation(arguments);
        var contract = Assert.Throws<FunctionContractException>(() =>
        {
            _ = operation == "GenerateKey" ? new CryptoOperations().GenerateKey(invocation)
                : new CryptoOperations().GenerateParameters(invocation);
        });
        AssertSafeException(contract!);
        Assert.That(contract!.Function.ToString(), Is.EqualTo(operation));
        var runner = new CryptoScriptRunner();
        var error = Assert.Throws<SemanticErrorException>(() =>
            Execute(runner, operation + "(p" + (operation == "GenerateKey" ? ",128" : "") + ") VAR after=0x(AABB)"));
        AssertSafeContractDiagnostic(runner, error!, operation);
        Assert.That(p.Value, Is.EqualTo(before));
        AssertNoResults();
    }

    private const string LiveMechanismSentinel = "F102030405060708091A2B3C4D5E6F70";

    private static OperationInvocation Invocation(params string[] names) => new(names.Select(name =>
        new ResolvedCallArgument(VariableDictionary.Instance().Get(name).Value,
            ResolvedCallArgumentKind.Variable, VariableDictionary.Instance().Get(name))));

    private static void AssertSafeException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            foreach (string text in new[] { current.Message, current.ToString() })
            {
                Assert.That(text, Does.Not.Contain(LiveMechanismSentinel));
                Assert.That(text, Does.Not.Contain(EmvAcFixtures.AesKey));
                Assert.That(text, Does.Not.Contain(EmvAcFixtures.Data));
            }
    }

    private static void AssertSafeContractDiagnostic(CryptoScriptRunner runner,
        SemanticErrorException error, string function)
    {
        AssertSafeException(error);
        Assert.That(error.SemanticError!.Type, Is.EqualTo("FunctionContract"));
        Assert.That(error.SemanticError.FunctionName, Is.EqualTo(function));
        Assert.That(error.SemanticError.Location!.FunctionName, Is.EqualTo(function));
        Assert.That(error.SemanticError.ErrorCode, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
        Assert.That(error.SemanticError.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.FunctionContractViolation));
        Assert.That(error.SemanticError.Message, Does.Contain("<unknown mechanism>"));
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(runner.Diagnostics[0], Is.SameAs(error.SemanticError));
        foreach (var diagnostic in runner.Diagnostics.Concat(runner.SemanticErrors))
            foreach (string text in new[] { diagnostic.Message, diagnostic.FunctionCall, diagnostic.Value,
                         diagnostic.Identifier, diagnostic.Location?.ToString() ?? "",
                         SemanticDiagnosticFormatter.Format(diagnostic) })
            {
                Assert.That(text, Does.Not.Contain(LiveMechanismSentinel));
                Assert.That(text, Does.Not.Contain(EmvAcFixtures.AesKey));
                Assert.That(text, Does.Not.Contain(EmvAcFixtures.Data));
            }
        Assert.That(SemanticDiagnosticFormatter.FormatMany(runner.Diagnostics), Does.Not.Contain(LiveMechanismSentinel));
    }

    private static void Execute(CryptoScriptRunner runner, string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        runner.Execute(context);
    }

    private static void AssertNoResults()
    {
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
    }
}

[NonParallelizable]
public class EmvAcMechanismTests
{
    private const string Mechanism = "MAC-EMV-AC";

    [SetUp]
    public void SetUp() => EmvAcFixtures.Reset();

    [TestCaseSource(typeof(EmvAcFixtures), nameof(EmvAcFixtures.Matrix))]
    public void AllFixedAcValuesThroughRunner(string cipher, string key, string ac, bool direct, bool literal)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix(cipher, key) +
            (direct ? $"PARAM p=#MECH:{Mechanism} " : $"PARAM p=Parameters({Mechanism}) ") +
            $"VAR ac=Mac(p,sk,{(literal ? "0x(" + EmvAcFixtures.Data + ")" : "data")})");
        var result = VariableDictionary.Instance().Get("ac");
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<StringVariableDeclaration>());
            Assert.That(result.Type, Is.TypeOf<CryptoTypeVar>());
            Assert.That(result.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(result.Value, Is.EqualTo("0x(" + ac + ")").IgnoreCase);
            Assert.That(FormatConversions.ToByteArray(result.Value, result.ValueFormat), Has.Length.EqualTo(8));
        });
    }

    [TestCase("\"000102\"")]
    [TestCase("1")]
    [TestCase("b64(AAEC)")]
    [TestCase("sk")]
    [TestCase("p")]
    [TestCase("Hash(HASH-SHA1,0x(0001))")]
    public void InvalidDataArgumentsAreRejected(string argument)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + $"PARAM p=Parameters({Mechanism})");
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute($"VAR ac=Mac(p,sk,{argument})"));
        Assert.That(VariableDictionary.Instance().Contains("ac"), Is.False);
    }

    [TestCase("\"000102\"")]
    [TestCase("b64(AAEC)")]
    [TestCase("1")]
    public void NonHexVarsAreRejected(string value)
    {
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
            $"PARAM p=Parameters({Mechanism}) VAR bad={value} VAR ac=Mac(p,sk,bad)"));
        Assert.That(VariableDictionary.Instance().Contains("ac"), Is.False);
    }

    [TestCase("0x(89F7B697A028A93345BE7A409665B9A4)")]
    [TestCase("\"89F7B697A028A93345BE7A409665B9A4\"")]
    [TestCase("b64(ife2l6AoqTNFvnpAlmW5pA==)")]
    [TestCase("data")]
    [TestCase("p")]
    [TestCase("GenerateKey(AES-ECB,0x(89F7B697A028A93345BE7A409665B9A4))")]
    public void NonKeyReferencesAreRejected(string argument)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + $"PARAM p=Parameters({Mechanism})");
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute($"VAR ac=Mac(p,{argument},data)"));
        Assert.That(VariableDictionary.Instance().Contains("ac"), Is.False);
    }

    [TestCase("#MECH:MAC-EMV-AC", "genuine PARAM variable reference")]
    [TestCase("Parameters(MAC-EMV-AC)", "Missing argument of type PARAM")]
    public void NonReferencedParametersAreRejected(string argument, string message)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix());
        var error = Assert.Throws<SemanticErrorException>(() =>
            EmvAcFixtures.Execute($"VAR ac=Mac({argument},sk,data)"));
        Assert.That(error!.SemanticError!.Message, Does.Contain(message));
    }

    [Test]
    public void ReconstructedParamVarIsRejected()
    {
        VariableDictionary.Instance().Add(new StringVariableDeclaration
        {
            Id = "fake", Value = "#MECH:MAC-EMV-AC", ValueFormat = FormatConversions.PAR
        });
        var error = Assert.Throws<SemanticErrorException>(() =>
            EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + "VAR ac=Mac(fake,sk,data)"));
        Assert.That(error!.SemanticError!.Message, Does.Contain("genuine PARAM variable reference"));
    }

    [TestCase("#IV:0x(0000000000000000)")]
    [TestCase("#PAD:NONE")]
    [TestCase("#MACLEN:8")]
    [TestCase("#EXPECTED:0x(D3A78E1517C22F70)")]
    [TestCase("#MECH:MAC-EMV-AC")]
    [TestCase("#MECH:AES-CMAC")]
    public void AdditionalAndDuplicateParametersAreRejected(string additional)
    {
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
            $"PARAM p=Parameters({Mechanism},{additional}) VAR ac=Mac(p,sk,data)"));
        EmvAcFixtures.Reset();
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
            $"PARAM p=#MECH:{Mechanism} {additional} VAR ac=Mac(p,sk,data)"));
    }

    [TestCase("Mac(p,sk)")]
    [TestCase("Mac(p,sk,data,data)")]
    public void WrongArgumentCountIsRejected(string call)
    {
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
            $"PARAM p=Parameters({Mechanism}) VAR ac={call}"));
    }

    [TestCase("p, data, sk")]
    [TestCase("sk, p, data")]
    public void WrongArgumentOrderIsRejected(string arguments)
    {
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
            $"PARAM p=Parameters({Mechanism}) VAR ac=Mac({arguments})"));
    }

    [Test]
    public void KeyAndParameterIdentityAndInputValuesArePreserved()
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + $"PARAM p=Parameters({Mechanism})");
        var dictionary = VariableDictionary.Instance();
        var key = (KeyVariableDeclaration)dictionary.Get("sk");
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.MacVerify);
        key.DerivationMechanism = "Imported";
        var otherKey = new KeyVariableDeclaration
        {
            Id = "other", KeyValue = key.KeyValue, Value = key.Value,
            KeyType = KeyType.Secret(KeyAlgorithm.Hmac), KeySizeInBits = new KeySize(128)
        };
        dictionary.Add(otherKey);
        var parameters = dictionary.Get("p");
        var data = dictionary.Get("data");
        string keyBefore = key.Serialize(), parametersBefore = parameters.Value, dataBefore = data.Value;
        EmvAcFixtures.Execute("VAR ac=Mac(p,sk,data)");
        Assert.Multiple(() =>
        {
            Assert.That(dictionary.Get("sk"), Is.SameAs(key));
            Assert.That(key.Serialize(), Is.EqualTo(keyBefore));
            Assert.That(dictionary.Get("p"), Is.SameAs(parameters));
            Assert.That(parameters.Value, Is.EqualTo(parametersBefore));
            Assert.That(dictionary.Get("data"), Is.SameAs(data));
            Assert.That(data.Value, Is.EqualTo(dataBefore));
            Assert.That(dictionary.Get("ac").Value, Is.EqualTo("0x(" + EmvAcFixtures.AesAc + ")").IgnoreCase);
        });
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR bad=Mac(p,other,data)"));
        Assert.That(dictionary.Contains("bad"), Is.False);
    }

    [TestCase(KeyAlgorithm.Hmac)]
    [TestCase(KeyAlgorithm.Unknown)]
    [TestCase(KeyAlgorithm.Rsa)]
    public void InvalidAlgorithmMetadataIsRejected(KeyAlgorithm algorithm)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + $"PARAM p=Parameters({Mechanism})");
        ((KeyVariableDeclaration)VariableDictionary.Instance().Get("sk")).KeyType = algorithm == KeyAlgorithm.Rsa
            ? KeyType.Public(algorithm) : KeyType.Secret(algorithm);
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR ac=Mac(p,sk,data)"));
    }

    [Test]
    public void InconsistentKnownSizeAndUnknownParametersAreRejectedBeforeCore()
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + $"PARAM p=Parameters({Mechanism})");
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("sk");
        key.KeyValue = "0x(" + new string('A', 48) + ")";
        var error = Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR ac=Mac(p,sk,data)"));
        Assert.That(error!.SemanticError!.Message, Does.Contain("size metadata"));
        key.KeyValue = "0x(" + EmvAcFixtures.AesKey + ")";
        ((ParameterVariableDeclaration)VariableDictionary.Instance().Get("p")).GetParameters().Add("#UNKNOWN", "secret");
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR ac=Mac(p,sk,data)"));
    }

    [TestCase(0)]
    [TestCase(15)]
    [TestCase(17)]
    [TestCase(33)]
    public void InvalidKeyLengthIsRejected(int length)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + $"PARAM p=Parameters({Mechanism})");
        ((KeyVariableDeclaration)VariableDictionary.Instance().Get("sk")).KeyValue = "0x(" + new string('A', length * 2) + ")";
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR ac=Mac(p,sk,data)"));
    }

    [TestCase("0x(0)")]
    [TestCase("0x(000G)")]
    [TestCase("0x(0001)junk")]
    [TestCase("0x()")]
    public void MalformedHostHexVarsAreRejected(string value)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + $"PARAM p=Parameters({Mechanism})");
        VariableDictionary.Instance().Get("data").Value = value;
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR ac=Mac(p,sk,data)"));
    }

    [Test]
    public void DirectWrapperAlsoEnforcesContractsAndRejectsValueOnlyNewCalls()
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + $"PARAM p=Parameters({Mechanism})");
        var wrapper = new MAC_EMV_AC();
        var arguments = EmvAcFixtures.Structured();
        Assert.That(wrapper.Mac(arguments).Value, Is.EqualTo("0x(" + EmvAcFixtures.AesAc + ")").IgnoreCase);
        Assert.Throws<ArgumentException>(() => wrapper.Mac(arguments.Values));
        Assert.Throws<ArgumentException>(() => wrapper.Mac(new AlgorithmCallArguments(arguments.Arguments.Take(2))));
        Assert.Throws<ArgumentException>(() => wrapper.Mac(arguments.WithSourceVariable(0,
            (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p"))));
    }

    [Test]
    public void ExistingValueOnlyMacContractsStillWork()
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + "PARAM p=Parameters(AES-CMAC)");
        string[] values = EmvAcFixtures.Structured().Values;
        Assert.That(new CryptoOperations().Mac(values).Value,
            Is.EqualTo("0x(D3A78E1517C22F7040A3556A23B8A2A7)").IgnoreCase);
        EmvAcFixtures.Reset();
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix("DES3-ECB", EmvAcFixtures.Vectors[0].Key) +
            "PARAM p=Parameters(DES3-RETAIL,#PAD:ISO-9797-M2,#MACLEN:8)");
        Assert.That(new CryptoOperations().Mac(EmvAcFixtures.Structured().Values).Value,
            Is.EqualTo("0x(613D21599AAC13F5)").IgnoreCase);
    }

    [Test]
    public void ExistingMacParameterDuplicateSemanticsRemainUnchanged()
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
            "PARAM p=#MECH:AES-CMAC #MECH:AES-CMAC VAR ac=Mac(p,sk,data)");
        Assert.That(VariableDictionary.Instance().Get("ac").Value,
            Is.EqualTo("0x(D3A78E1517C22F7040A3556A23B8A2A7)").IgnoreCase);
    }
}
