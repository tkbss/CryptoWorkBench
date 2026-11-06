using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using Newtonsoft.Json;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class AssignmentCorrectionTests
{
    private const string Header = "{KBVID:D;KEYU:D0;ALGO:A;MODEU:B;EXP:E;}";
    private const string HeaderWithMetadata =
        "{KBVID:D;KEYU:D0;ALGO:A;MODEU:B;EXP:E;OPTID:LB;OPTBD:\"private-marker\";}";
    private VariableDeclaration[] previous = null!;

    [SetUp]
    public void Setup()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }

    [TearDown]
    public void Cleanup()
    {
        VariableDictionary.Instance().Clear();
        foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
    }

    private static VariableDeclaration Get(string id = "target") => VariableDictionary.Instance().Get(id);

    private static Task<CryptoScriptProgram> Run(string script, bool asynchronous, CryptoScriptRunner? runner = null)
    {
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(SyntaxErrorList.Instance(), Is.Empty);
        runner ??= new();
        return asynchronous ? runner.ExecuteAsync(context) : Task.FromResult(runner.Execute(context));
    }

    // Include PARAM metadata that is intentionally not part of its public JSON representation.
    private static string Snapshot(VariableDeclaration variable) => JsonConvert.SerializeObject(new
    {
        State = variable,
        Parameters = (variable as ParameterVariableDeclaration)?.GetParameters(),
        ExplicitParameters = (variable as ParameterVariableDeclaration)?.ExplicitParameterNames,
        Padding = (variable as ParameterVariableDeclaration)?.OriginalExplicitPadding
    });

    public static IEnumerable<TestCaseData> RejectedInitializers()
    {
        const string parameterSetup = "TR31H header=" + HeaderWithMetadata +
            " PARAM target=#MECH:WRAP-AES-TR31 #BLKH:header";
        var cases = new (string Name, string Setup, string Initializer, SemanticDiagnosticCode Code)[]
        {
            ("HeaderPlaceholder", "TR31H target=" + HeaderWithMetadata,
                "BlockHeader(WRAP-AES-TR31,0x(01))", SemanticDiagnosticCode.FunctionContractViolation),
            ("LegacyDisplayHeader", "TR31H target=" + HeaderWithMetadata,
                "BlockHeader(WRAP-AES-TR31)", SemanticDiagnosticCode.FunctionContractViolation),
            ("MechInIv", parameterSetup, "#IV:\"MECH\"", SemanticDiagnosticCode.InvalidAssignmentInitializer),
            ("MechInPadding", parameterSetup, "#PAD:\"MECH\"", SemanticDiagnosticCode.InvalidAssignmentInitializer),
            ("MechInPrivateValue", parameterSetup, "#IV:\"private-MECH-marker\"", SemanticDiagnosticCode.InvalidAssignmentInitializer),
            ("Int32Overflow", "VAR target=\"private-marker\"", "2147483648", SemanticDiagnosticCode.InvalidAssignmentInitializer),
            ("LongInteger", "VAR target=\"private-marker\"", new string('9', 1024), SemanticDiagnosticCode.InvalidAssignmentInitializer)
        };
        foreach (var item in cases)
            foreach (bool asynchronous in new[] { false, true })
                yield return new TestCaseData(item.Setup, item.Initializer, item.Code, asynchronous)
                    .SetName($"RejectedInitializer_{item.Name}_{(asynchronous ? "Async" : "Sync")}");
    }

    [TestCaseSource(nameof(RejectedInitializers))]
    public async Task RejectedInitializerPreservesCompleteTarget(string setup, string initializer,
        SemanticDiagnosticCode code, bool asynchronous)
    {
        await Run(setup, asynchronous);
        var original = Get();
        var originalType = original.Type;
        string originalValue = original.Value;
        string metadata = Snapshot(original);
        var definition = (original as BlockHeaderVariableDeclaration)?.Definition;
        var headerReference = (original as ParameterVariableDeclaration)?.HeaderReference;
        var runner = new CryptoScriptRunner();

        await Assert.ThatAsync(async () => await Run("VAR before=0 target=" + initializer + " VAR after=1",
            asynchronous, runner), Throws.TypeOf<SemanticErrorException>());

        Assert.That(Get(), Is.SameAs(original));
        Assert.That(Get().Type, Is.SameAs(originalType));
        Assert.That(Get().Value, Is.EqualTo(originalValue));
        Assert.That(Snapshot(Get()), Is.EqualTo(metadata));
        if (definition is not null)
            Assert.That(((BlockHeaderVariableDeclaration)Get()).Definition, Is.SameAs(definition));
        if (headerReference is not null)
            Assert.That(((ParameterVariableDeclaration)Get()).HeaderReference, Is.SameAs(headerReference));
        Assert.That(Get("before").Value, Is.EqualTo("0"));
        Assert.That(Get("after"), Is.Null);
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
        var diagnostic = runner.Diagnostics.Single();
        Assert.That(diagnostic.DiagnosticCode, Is.EqualTo(code));
        Assert.That(diagnostic.Type, Is.EqualTo("Assignment"));
        Assert.That(diagnostic.Identifier, Is.EqualTo("target"));
        Assert.That(diagnostic.Location!.StatementIndex, Is.EqualTo(2));
        Assert.That(diagnostic.Location.ExecutionId, Is.EqualTo(runner.ExecutionId));
        string diagnostics = JsonConvert.SerializeObject(runner.Diagnostics);
        Assert.That(diagnostics, Does.Not.Contain("private-marker").And.Not.Contain("private-MECH-marker"));
        Assert.That(diagnostics, Does.Not.Contain(initializer));
    }

    [TestCase(false, false)] [TestCase(false, true)]
    [TestCase(true, false)] [TestCase(true, true)]
    public async Task StructuredHeaderResultsRemainUsable(bool literal, bool asynchronous)
    {
        await Run("TR31H target=" + HeaderWithMetadata, asynchronous);
        var original = Get();
        var metadata = Snapshot(original);
        var initializer = literal ? "{KBVID:D;KEYU:K0;ALGO:A;MODEU:B;EXP:E;}" :
            "CreateHeader(KBVID:D,KEYU:K0,ALGO:A,MODEU:B,EXP:E)";
        var program = await Run("target=" + initializer + " PARAM consumer=#MECH:WRAP-AES-TR31 #BLKH:target", asynchronous);
        Assert.That(Get(), Is.Not.SameAs(original));
        Assert.That(Get().Id, Is.EqualTo(original.Id));
        Assert.That(Get().Type, Is.SameAs(original.Type));
        Assert.That(Snapshot(original), Is.EqualTo(metadata));
        var header = (BlockHeaderVariableDeclaration)Get();
        Assert.That(header.Definition, Is.Not.Null);
        Assert.That(header.Definition!.Attributes["KEYU"], Is.EqualTo("K0"));
        Assert.That(((ParameterVariableDeclaration)Get("consumer")).HeaderReference, Is.SameAs(header));
        Assert.That(program.Diagnostics, Is.Empty);
    }

    [TestCase(false, false)] [TestCase(false, true)]
    [TestCase(true, false)] [TestCase(true, true)]
    public async Task ActualMechanismIsUsedEvenAfterMisleadingValue(bool misleadingFirst, bool asynchronous)
    {
        await Run("PARAM target=#MECH:AES-ECB", asynchronous);
        var original = Get();
        string metadata = Snapshot(original);
        string initializer = misleadingFirst ? "#IV:\"MECH\" #MECH:AES-CBC" :
            "#MECH:AES-CBC #IV:0x(000102030405060708090A0B0C0D0E0F)";
        var program = await Run("target=" + initializer, asynchronous);
        var parameter = (ParameterVariableDeclaration)Get();
        Assert.That(parameter, Is.Not.SameAs(original));
        Assert.That(parameter.Type, Is.SameAs(original.Type));
        Assert.That(parameter.Id, Is.EqualTo(original.Id));
        Assert.That(parameter.Mechanism, Is.EqualTo("AES-CBC"));
        Assert.That(parameter.GetParameter("MECH"), Is.EqualTo("AES-CBC"));
        Assert.That(parameter.GetParameter("IV"), Is.EqualTo(misleadingFirst ? "\"MECH\"" : "0x(000102030405060708090A0B0C0D0E0F)"));
        Assert.That(parameter.ExplicitParameterNames, Is.EquivalentTo(new[] { "#MECH", "#IV" }));
        Assert.That(Snapshot(original), Is.EqualTo(metadata));
        Assert.That(program.Diagnostics, Is.Empty);
    }

    [TestCase("0", false)] [TestCase("0", true)]
    [TestCase("2147483647", false)] [TestCase("2147483647", true)]
    [TestCase("0002147483647", false)] [TestCase("0002147483647", true)]
    [TestCase("\"2147483648\"", false)] [TestCase("\"2147483648\"", true)]
    public async Task SupportedNumbersAndQuotedTextPreserveTheirValue(string literal, bool asynchronous)
    {
        await Run("VAR target=\"old\"", asynchronous);
        var original = Get();
        string metadata = Snapshot(original);
        var program = await Run("target=" + literal + " VAR comparison=Compare(target," + literal + ")", asynchronous);
        Assert.That(Get(), Is.Not.SameAs(original));
        Assert.That(Get().Id, Is.EqualTo(original.Id));
        Assert.That(Get().Type, Is.SameAs(original.Type));
        Assert.That(Get().Value, Is.EqualTo(literal));
        Assert.That(Snapshot(original), Is.EqualTo(metadata));
        Assert.That(Get("comparison").Value, Is.EqualTo("Values are equal"));
        Assert.That(program.Diagnostics, Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public async Task LegacyDeclarationBehaviorRemainsUnchanged(bool asynchronous)
    {
        var program = await Run("TR31H placeholder=BlockHeader(WRAP-AES-TR31,0x(01)) " +
            "TR31H display=BlockHeader(WRAP-AES-TR31) PARAM legacy=#IV:\"MECH\" " +
            "VAR overflow=2147483648 VAR verylong=" + new string('9', 1024), asynchronous);
        Assert.That(((BlockHeaderVariableDeclaration)Get("placeholder")).Definition, Is.Null);
        Assert.That(Get("placeholder").Value, Is.Empty);
        Assert.That(((BlockHeaderVariableDeclaration)Get("display")).Definition, Is.Null);
        Assert.That(Get("display").Value, Is.EqualTo(
            "{KBVID:D,KBLEN:0144,KEYU:D0,ALGO:A,MODEU:B,KEYVN:00,EXP:S,KEYCTX:0,NUMOPTB:00,RSV:0}"));
        Assert.That(((ParameterVariableDeclaration)Get("legacy")).Mechanism, Is.EqualTo("\"MECH\""));
        Assert.That(Get("legacy").Value, Is.EqualTo("#MECH:\"MECH\"#IV:\"MECH\""));
        Assert.That(Get("overflow").Value, Is.EqualTo("\"\""));
        Assert.That(Get("verylong").Value, Is.EqualTo("\"\""));
        Assert.That(program.Diagnostics, Is.Empty);
    }
}
