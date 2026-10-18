using CryptoScript.CryptoAlgorithm.PINBLOCK;
using CryptoScript.Documentation;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Text.RegularExpressions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class PinBlockTranslationTests
{
    private static readonly string[] Vectors = ["2D8F0FF09AFDCCD0", "429B597B94CB18A5", "978BB4AC07BBD2EB", "6694CAF82B1C2994", "6685B016B0CEC63CB7E0F2663C9551EC"];
    private static string Key(int f) => f == 4 ? "000102030405060708090A0B0C0D0E0F" : "0123456789ABCDEFFEDCBA9876543210";
    private static string Parameters(int f, bool fields = true) => "#MECH:" + PinBlockTranslationRules.Mechanism(f) +
        (PinBlockTranslationRules.UsesPan(f) ? " #PAN:0x(1234567890123456)" : "") +
        (!fields ? "" : f switch { 1 => " #TRANSACTION:0x(012345678)", 3 => " #FILL:0x(ABCDEFABC)", 4 => " #RANDOM:0x(0011223344556677)", _ => "" });
    private static string Setup(int s, int t) => $"PARAM source={Parameters(s)}\nPARAM target={Parameters(t)}\nKEY sk=0x({Key(s)})\nKEY tk=0x({Key(t)})\nVAR block=0x({Vectors[s]})\n";
    private const string Call = "VAR result=PinBlockTranslate(source,sk,block,target,tk)";

    private static IEnumerable<TestCaseData> KeySizes() =>
        from s in Enumerable.Range(0, 5)
        from t in Enumerable.Range(0, 5)
        where t == 2 || s != 2 && (t != 1 || s == 1)
        from sourceSize in s == 4 ? new[] { 16, 24, 32 } : new[] { 16, 24 }
        from targetSize in t == 4 ? new[] { 16, 24, 32 } : new[] { 16, 24 }
        select new TestCaseData(s, t, sourceSize, targetSize);

    [TestCaseSource(nameof(KeySizes))]
    public void DifferentKeysAllSupportedSizesAndPinBoundariesRoundTrip(int s, int t, int sourceSize, int targetSize)
    {
        foreach (string pin in new[] { "0000", "01234", "000012345678" })
        {
            Reset();
            string sourceKey = Convert.ToHexString(Enumerable.Range(1, sourceSize).Select(i => (byte)i).ToArray());
            string targetKey = Convert.ToHexString(Enumerable.Range(91, targetSize).Select(i => (byte)i).ToArray());
            Execute($"PARAM source={Parameters(s, false)}\nPARAM target={Parameters(t, false)}\n" +
                $"KEY sk=0x({sourceKey})\nKEY tk=0x({targetKey})\nVAR pin=0x({pin})\nVAR block=Wrap(source,sk,pin)\n" +
                Call + "\nVAR recovered=Unwrap(target,tk,result)");
            Assert.That(Get("recovered").Value, Is.EqualTo($"0x({pin})"));
        }
    }

    [TestCase("0012345678")] [TestCase("00123456789")] [TestCase("0012345678901234567")]
    public void CompletePanBoundariesPreserveLeadingZeros(string pan)
    {
        Execute($"PARAM source=#MECH:WRAP-DES3-PINBLOCK-0 #PAN:0x({pan})\n" +
            $"PARAM target=#MECH:WRAP-AES-PINBLOCK-4 #PAN:0x({pan})\n" +
            $"KEY sk=0x({Key(0)})\nKEY tk=0x({Key(4)})\nVAR pin=0x(01234)\nVAR block=Wrap(source,sk,pin)\n" + Call +
            "\nVAR recovered=Unwrap(target,tk,result)");
        Assert.That(Get("recovered").Value, Is.EqualTo("0x(01234)"));
        Assert.That(((ParameterVariableDeclaration)Get("target")).GetParameter("PAN"), Is.EqualTo($"0x({pan})"));
    }

    [TestCase(1, 0, "source")] [TestCase(0, 2, "target")] [TestCase(2, 2, "source")]
    public void UnusedPanIsRejected(int s, int t, string side)
    {
        Execute(Setup(s,t));
        ((ParameterVariableDeclaration)Get(side)).SetParameter("PAN", "0x(1234567890123456)");
        var runner = Execute(Call, true);
        Assert.That(runner.Diagnostics.Last().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.InvalidParameter));
    }

    [Test]
    public void DeclaredMetadataWarningsKeepOriginalKeyAndExecutionLocation()
    {
        Execute(Setup(0,4));
        var key = new KeyVariableDeclaration {
            Id = "sk", Type = new CryptoTypeKey(), Value = $"0x({Key(0)})", ValueFormat = FormatConversions.HEX,
            KeyType = KeyType.Secret(KeyAlgorithm.Aes), Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt)
        };
        VariableDictionary.Instance().Add(key);
        var runner = Execute(Call);
        Assert.That(Get("sk"), Is.SameAs(key));
        Assert.That(key.KeyType.Algorithm, Is.EqualTo(KeyAlgorithm.Aes));
        Assert.That(key.Usage.Allows(KeyUsage.PinEncrypt), Is.False);
        foreach (var code in new[] { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict })
        {
            var diagnostic = runner.Diagnostics.Single(d => d.DiagnosticCode == code);
            Assert.That(diagnostic.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
            Assert.That(diagnostic.FunctionName, Is.EqualTo("PinBlockTranslate"));
            Assert.That(diagnostic.Identifier, Is.EqualTo("sk"));
            Assert.That(diagnostic.Location?.ExecutionId, Is.Not.Null);
        }
    }

    [Test]
    public void MalformedStructureHasOneRedactedDiagnosticAndNoResult()
    {
        Execute(Setup(0,4));
        byte[] cipher = CryptoScript.CryptoAlgorithm.DES3.DES3_ECB.EncryptPinBlockForAnalysis(
            Convert.FromHexString(Key(0)), new byte[8]);
        Get("block").Value = "0x(" + Convert.ToHexString(cipher) + ")";
        var runner = Execute(Call, true);
        var error = runner.Diagnostics.Single(d => d.Severity == SemanticDiagnosticSeverity.Error);
        Assert.That(error.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.InvalidPinBlock));
        Assert.That(error.FunctionCall, Is.EqualTo("PinBlockTranslate(<redacted>)"));
        Assert.That(error.Message, Does.Not.Contain(Key(0)).And.Not.Contain("01234"));
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
    }

    [SetUp]
    public void Reset()
    {
        VariableDictionary.Instance().Clear();
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.SyntaxErrorOccured = false;
    }

    private static CryptoScriptRunner Execute(string text, bool fails = false)
    {
        var parser = ParserBuilder.StringBuild(text);
        var program = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        var runner = new CryptoScriptRunner();
        if (fails) Assert.Throws<SemanticErrorException>(() => runner.Execute(program));
        else
        {
            try { runner.Execute(program); }
            catch (SemanticErrorException e) { Assert.Fail(e.SemanticError?.Message ?? "Missing semantic diagnostic"); }
        }
        return runner;
    }

    private static VariableDeclaration Get(string name) => VariableDictionary.Instance().Get(name)!;
    private static IEnumerable<TestCaseData> Matrix() =>
        from s in Enumerable.Range(0, 5) from t in Enumerable.Range(0, 5) select new TestCaseData(s, t);

    [TestCaseSource(nameof(Matrix))]
    public void EveryTransitionMatchesFixedVectorsOrBlocksWithoutMutation(int s, int t)
    {
        Execute(Setup(s, t));
        var source = Get("source"); var target = Get("target");
        var sk = Get("sk"); var tk = Get("tk");
        string beforeSource = source.Value, beforeTarget = target.Value;
        // Independent explicit matrix expectation, not derived from production rules.
        bool forbidden = t != 2 && (s == 2 || t == 1 && s != 1);
        var runner = Execute(Call, forbidden);
        Assert.That(Get("source"), Is.SameAs(source));
        Assert.That(Get("target"), Is.SameAs(target));
        Assert.That(Get("sk"), Is.SameAs(sk));
        Assert.That(Get("tk"), Is.SameAs(tk));
        if (forbidden)
        {
            Assert.That(runner.Diagnostics.Count(d => d.Severity == SemanticDiagnosticSeverity.Error), Is.EqualTo(1));
            Assert.That(runner.Diagnostics.Last().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.PinBlockTranslationForbidden));
            Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
            Assert.That(source.Value, Is.EqualTo(beforeSource));
            Assert.That(target.Value, Is.EqualTo(beforeTarget));
        }
        else Assert.That(Get("result").Value, Is.EqualTo($"0x({Vectors[t]})").IgnoreCase);
    }

    [TestCase(0, 0)] [TestCase(0, 3)] [TestCase(0, 4)]
    [TestCase(3, 0)] [TestCase(3, 3)] [TestCase(3, 4)]
    [TestCase(4, 0)] [TestCase(4, 3)] [TestCase(4, 4)]
    public void FullPanMismatchPrecedesEvenInvalidCiphertext(int s, int t)
    {
        Execute(Setup(s,t));
        // Same selected ISO-0 digits, different prefix/check digit.
        ((ParameterVariableDeclaration)Get("target")).SetParameter("PAN", "0x(9234567890123457)");
        Get("block").Value = "0x(00)";
        string source = Get("source").Value, target = Get("target").Value;
        var runner = Execute(Call, true);
        Assert.That(runner.Diagnostics.Last().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.PinBlockPanMismatch));
        Assert.That(Get("source").Value, Is.EqualTo(source));
        Assert.That(Get("target").Value, Is.EqualTo(target));
    }

    [TestCase("")]
    [TestCase("source,sk,block,target")]
    [TestCase("source,sk,block,target,tk,tk")]
    [TestCase("source,sk,block,source,tk")]
    [TestCase("source,0x(0123456789ABCDEFFEDCBA9876543210),block,target,tk")]
    [TestCase("source,sk,0x(2D8F0FF09AFDCCD0),target,tk")]
    [TestCase("source,block,sk,target,tk")]
    public void RequiresFiveGenuineTypedReferencesAndDistinctParams(string args)
    {
        Execute(Setup(0,0));
        var runner = Execute($"VAR result=PinBlockTranslate({args})", true);
        Assert.That(runner.Diagnostics.Last().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.FunctionContractViolation));
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
    }

    [TestCase(1, "TRANSACTION", "0x(012345678)")]
    [TestCase(3, "FILL", "0x(ABCDEFABC)")]
    [TestCase(4, "RANDOM", "0x(0011223344556677)")]
    public void SourceStaleFieldsAreIgnoredAndBothUpdatesAreCommitted(int f, string field, string expected)
    {
        Execute(Setup(f,4));
        var source = (ParameterVariableDeclaration)Get("source");
        source.SetParameter(field, "\"stale\"");
        var target = (ParameterVariableDeclaration)Get("target");
        target.GetParameters().Remove("#RANDOM");
        Execute(Call);
        Assert.That(source.GetParameter(field), Is.EqualTo(expected));
        Assert.That(target.GetParameter("RANDOM"), Does.Match(@"^0x\([0-9A-F]{16}\)$"));
        Execute("VAR recovered=Unwrap(target,tk,result)");
        Assert.That(Get("recovered").Value, Is.EqualTo("0x(01234)"));
    }

    [TestCase("target-field")] [TestCase("target-length")] [TestCase("source-block")]
    [TestCase("target-key")] [TestCase("source-key")] [TestCase("extra-param")]
    [TestCase("missing-pan")] [TestCase("invalid-pan")] [TestCase("mechanism")]
    public void FailureAtEachStageLeavesBothParamsUnchanged(string failure)
    {
        Execute(Setup(3,4));
        var source = (ParameterVariableDeclaration)Get("source");
        var target = (ParameterVariableDeclaration)Get("target");
        source.SetParameter("FILL", "\"stale\"");
        switch (failure)
        {
            case "target-field": target.SetParameter("RANDOM", "0x(00)"); break;
            case "target-length": target.Mechanism = PinBlockTranslationRules.Mechanism(3); target.GetParameters().Remove("#RANDOM"); target.SetParameter("FILL", "0x(AA)"); break;
            case "source-block": Get("block").Value = "0x(00)"; break;
            case "target-key": Get("tk").Value = "0x(00)"; break;
            case "source-key": Get("sk").Value = "0x(00)"; break;
            case "extra-param": target.SetParameter("IV", "0x(00)"); break;
            case "missing-pan": target.GetParameters().Remove("#PAN"); break;
            case "invalid-pan": target.SetParameter("PAN", "0x(123456789A)"); break;
            case "mechanism": target.Mechanism = "AES-ECB"; break;
        }
        string beforeSource = source.Value, beforeTarget = target.Value;
        var sourceFields = source.GetParameters().ToArray(); var targetFields = target.GetParameters().ToArray();
        Execute(Call, true);
        Assert.That(source.Value, Is.EqualTo(beforeSource));
        Assert.That(target.Value, Is.EqualTo(beforeTarget));
        Assert.That(source.GetParameters(), Is.EquivalentTo(sourceFields));
        Assert.That(target.GetParameters(), Is.EquivalentTo(targetFields));
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
    }

    [Test]
    public void SameKeyReferenceIsSupported()
    {
        Execute(Setup(0,3));
        Execute("VAR result=PinBlockTranslate(source,sk,block,target,sk)");
        Assert.That(Get("result").Value, Is.EqualTo("0x(" + Vectors[3] + ")").IgnoreCase);
    }

    [Test]
    public void DocumentationMatchesCentralRulesAndAllExamplesExecute()
    {
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocumentation("pintranslations", out var overview), Is.True);
        Assert.That(PinBlockTranslationRules.Entries.Count(r => r.Status == PinBlockTranslationStatus.Allowed), Is.EqualTo(13));
        Assert.That(PinBlockTranslationRules.Entries.Count(r => r.Status == PinBlockTranslationStatus.IccOnly), Is.EqualTo(5));
        Assert.That(PinBlockTranslationRules.Entries.Count(r => r.Status == PinBlockTranslationStatus.Forbidden), Is.EqualTo(7));
        Assert.That(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "InfoDocs"), "Info.PinTranslation.*.md"), Has.Length.EqualTo(25));
        foreach (var rule in PinBlockTranslationRules.Entries)
        {
            var id = InfoDocumentId.CreatePinTranslation(rule.SourceFormat, rule.TargetFormat);
            string uri = InfoDocumentUri.ToCanonicalString(id);
            Assert.That(InfoDocumentUri.TryParse(uri, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(id));
            Assert.That(provider.TryGetDocument(id, out var document), Is.True);
            string status = rule.Status == PinBlockTranslationStatus.IccOnly ? "ICC-only" : rule.Status.ToString();
            Assert.That(overview, Does.Contain($"[{status}]({uri})"));
            Assert.That(document, Does.Contain($"PCI classification: {status}"));
            foreach (Match link in Regex.Matches(document + overview, @"cryptoscript-info://[^)\s]+"))
            {
                Assert.That(InfoDocumentUri.TryParse(link.Value, out var linked), Is.True, link.Value);
                Assert.That(provider.HasDocument(linked!), Is.True, link.Value);
            }
            if (rule.Status == PinBlockTranslationStatus.Forbidden) continue;
            Reset();
            string example = Regex.Match(document, @"```cryptoscript\s+(.*?)```", RegexOptions.Singleline).Groups[1].Value;
            Execute(example);
            Assert.That(Get("result").Value, Is.EqualTo($"0x({Vectors[rule.TargetFormat]})").IgnoreCase);
        }
        Execute("Info(pintranslations)");
    }
}
