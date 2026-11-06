using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using Newtonsoft.Json;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class ParameterAssignmentSerializationTests
{
    private const string Header = "{KBVID:D;KEYU:D0;ALGO:A;MODEU:B;EXP:E;OPTID:LB;OPTBD:\"private-header\";}";
    private VariableDeclaration[] previous = null!;

    [SetUp] public void Setup()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }

    [TearDown] public void Cleanup()
    {
        VariableDictionary.Instance().Clear();
        foreach (var item in previous) VariableDictionary.Instance().Add(item);
    }

    private static VariableDeclaration Get(string id = "target") => VariableDictionary.Instance().Get(id);

    private static Task<CryptoScriptProgram> Run(string source, bool asynchronous, CryptoScriptRunner? runner = null)
    {
        var parser = ParserBuilder.StringBuild(source);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(SyntaxErrorList.Instance(), Is.Empty);
        runner ??= new();
        return asynchronous ? runner.ExecuteAsync(context) : Task.FromResult(runner.Execute(context));
    }

    private static string Snapshot(ParameterVariableDeclaration value) => JsonConvert.SerializeObject(new
    {
        State = value, Parameters = value.GetParameters(),
        value.ExplicitParameterNames, value.OriginalExplicitPadding
    });

    public static IEnumerable<TestCaseData> InvalidCases()
    {
        var cases = new List<(string Name, string Payload, string Rhs)>();
        foreach (var (name, payload) in new[]
        {
            ("Mech", "private-marker#MECH:HASH-SHA512"),
            ("Iv", "private-marker#IV:0x(FF)"),
            ("Pad", "private-marker#PAD:NONE"),
            ("Label", "private-marker#LABEL:injected"),
            ("Unknown", "private-marker#UNKNOWN:injected"),
            ("Bare", "private-marker#discarded"),
            ("Trailing", "private-marker#"),
            ("Leading", "#private-marker")
        })
        {
            cases.Add((name + "Reference", payload, "#MECH:HASH-SHA256 #IV:payload"));
            cases.Add((name + "Literal", payload, "#MECH:HASH-SHA256 #IV:\"" + payload + "\""));
        }
        cases.AddRange(new[]
        {
            ("IvBefore", "0x(00)#MECH:HASH-SHA512", "#IV:payload #MECH:HASH-SHA256"),
            ("RepeatedMech", "0x(00)#MECH:HASH-SHA512", "#MECH:HASH-SHA256 #IV:payload #MECH:HASH-SHA256"),
            ("LabelCarrier", "0x(00)#MECH:HASH-SHA512", "#MECH:HASH-SHA256 #LABEL:payload"),
            ("PadCarrier", "NONE#MECH:HASH-SHA512", "#MECH:HASH-SHA256 #PAD:payload"),
            ("OverwrittenValue", "private-marker#discarded", "#MECH:HASH-SHA256 #IV:payload #IV:0x(00)"),
            ("SelectorExtraSegment", "#MECH:HASH-SHA256#IV:0x(00)", "#MECH:payload"),
            ("SelectorBare", "#MECH:HASH-SHA256#discarded", "#MECH:payload"),
            ("UnknownSelectorBare", "future#discarded", "#MECH:payload"),
            ("FunctionSaltReference", "private-marker#discarded", "Parameters(KDF-HKDF,#HASH:HASH-SHA256,#SALT:payload,#OUTLEN:128)"),
            ("FunctionSaltLiteral", "private-marker#discarded", "Parameters(KDF-HKDF,#HASH:HASH-SHA256,#SALT:\"private-marker#discarded\",#OUTLEN:128)")
        });
        foreach (var item in cases)
            foreach (bool asynchronous in new[] { false, true })
                yield return new TestCaseData(item.Payload, item.Rhs, asynchronous)
                    .SetName($"SerializationReject_{item.Name}_{asynchronous}");
    }

    [TestCaseSource(nameof(InvalidCases))]
    public async Task AmbiguousResolvedValuesNeverReplaceTarget(string payload, string rhs, bool asynchronous)
    {
        // VAR retains the normal string encoding; PATH supplies its unquoted value.
        await Run("TR31H header=" + Header + " PARAM target=#MECH:WRAP-AES-TR31 #BLKH:header " +
            (rhs.StartsWith("Parameters(") ? "VAR" : "PATH") + " payload=\"" + payload + "\"", asynchronous);
        var original = (ParameterVariableDeclaration)Get();
        var type = original.Type;
        var header = original.HeaderReference;
        string before = Snapshot(original);
        var source = Get("payload");
        string sourceState = JsonConvert.SerializeObject(source);
        var runner = new CryptoScriptRunner();

        await Assert.ThatAsync(async () => await Run("VAR before=0 target=" + rhs +
            " VAR digest=Hash(target,0x(01)) VAR after=1", asynchronous, runner),
            Throws.TypeOf<SemanticErrorException>());

        Assert.That(Get(), Is.SameAs(original));
        Assert.That(Get().Type, Is.SameAs(type));
        Assert.That(Snapshot((ParameterVariableDeclaration)Get()), Is.EqualTo(before));
        Assert.That(original.HeaderReference, Is.SameAs(header));
        Assert.That(Get("payload"), Is.SameAs(source));
        Assert.That(JsonConvert.SerializeObject(source), Is.EqualTo(sourceState));
        Assert.That(Get("before").Value, Is.EqualTo("0"));
        Assert.That(Get("digest"), Is.Null);
        Assert.That(Get("after"), Is.Null);
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
        var diagnostic = runner.Diagnostics.Single();
        Assert.That(diagnostic.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.InvalidAssignmentInitializer));
        Assert.That((int)diagnostic.DiagnosticCode, Is.EqualTo(19));
        Assert.That(diagnostic.Type, Is.EqualTo("Assignment"));
        Assert.That(diagnostic.Identifier, Is.EqualTo("target"));
        Assert.That(diagnostic.Location!.ExecutionId, Is.EqualTo(runner.ExecutionId));
        Assert.That(diagnostic.Location.StatementIndex, Is.EqualTo(2));
        string diagnostics = JsonConvert.SerializeObject(runner.Diagnostics);
        Assert.That(diagnostics, Does.Not.Contain("private-marker").And.Not.Contain("private-header"));
        Assert.That(diagnostics, Does.Not.Contain(payload).And.Not.Contain(rhs));
    }

    [TestCase(false)] [TestCase(true)]
    public async Task OriginalReproductionStopsBeforeHash(bool asynchronous)
    {
        await Run("PATH payload=\"0x(00)#MECH:HASH-SHA512\" PARAM target=#MECH:HASH-SHA256", asynchronous);
        var old = (ParameterVariableDeclaration)Get();
        string before = Snapshot(old);
        var runner = new CryptoScriptRunner();
        await Assert.ThatAsync(async () => await Run(
            "target=#MECH:HASH-SHA256 #IV:payload VAR digest=Hash(target,0x(01))", asynchronous, runner),
            Throws.TypeOf<SemanticErrorException>());
        Assert.That(Get(), Is.SameAs(old));
        Assert.That(Snapshot(old), Is.EqualTo(before));
        Assert.That(Get("digest"), Is.Null);
        Assert.That(runner.Diagnostics.Single().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.InvalidAssignmentInitializer));
        Assert.That(runner.Diagnostics.Single().Location!.StatementIndex, Is.EqualTo(1));
        Assert.That(runner.Diagnostics.Single().Location!.ExecutionId, Is.EqualTo(runner.ExecutionId));
    }

    private static void AssertConsumedState(ParameterVariableDeclaration parameter)
    {
        var consumed = new ParameterVariableDeclaration();
        consumed.SetInstance(parameter.Value);
        Assert.That(consumed.Mechanism, Is.EqualTo(parameter.Mechanism));
        Assert.That(consumed.GetParameter("MECH"), Is.EqualTo(parameter.GetParameter("MECH")));
        Assert.That(consumed.GetParameters(), Is.EquivalentTo(parameter.GetParameters()));
        Assert.That(consumed.Value, Is.EqualTo(parameter.Value));
    }

    [TestCase("HASH-SHA256", false, false)] [TestCase("HASH-SHA256", false, true)]
    [TestCase("HASH-SHA256", true, false)] [TestCase("HASH-SHA256", true, true)]
    [TestCase("HASH-SHA512", false, false)] [TestCase("HASH-SHA512", false, true)]
    [TestCase("HASH-SHA512", true, false)] [TestCase("HASH-SHA512", true, true)]
    public async Task SafeValuesAndNormalizedSelectorsMatchActualHash(string mechanism, bool reference, bool asynchronous)
    {
        await Run("TR31H header=" + Header + " PARAM target=#MECH:WRAP-AES-TR31 #BLKH:header " +
            "PATH selector=\"#mech:" + mechanism.ToLowerInvariant() + "\" PATH empty=\"\"", asynchronous);
        var old = (ParameterVariableDeclaration)Get();
        string before = Snapshot(old);
        // Colons, spaces, empty values and Base64-encoded '#' are lossless in this format.
        var program = await Run("target=#MECH:" + (reference ? "selector" : mechanism) +
            " #LABEL:\"a:b c\" #IV:b64(Iw==) #NONCE:empty #MECH:" + mechanism +
            " VAR digest=Hash(target,0x(01))", asynchronous);
        var current = (ParameterVariableDeclaration)Get();
        Assert.That(current, Is.Not.SameAs(old));
        Assert.That(current.Type, Is.SameAs(old.Type));
        Assert.That(Snapshot(old), Is.EqualTo(before));
        Assert.That(current.Mechanism, Is.EqualTo(mechanism));
        Assert.That(current.GetParameter("LABEL"), Is.EqualTo("\"a:b c\""));
        Assert.That(current.GetParameter("IV"), Is.EqualTo("b64(Iw==)"));
        Assert.That(current.GetParameter("NONCE"), Is.Empty);
        Assert.That(current.ExplicitParameterNames.Count(n => n == "#MECH"), Is.EqualTo(1));
        AssertConsumedState(current);
        byte[] expected = mechanism == "HASH-SHA256" ? SHA256.HashData(new byte[] { 1 }) : SHA512.HashData(new byte[] { 1 });
        Assert.That(Get("digest").Value, Is.EqualTo("0x(" + Convert.ToHexString(expected) + ")").IgnoreCase);
        Assert.That(program.Diagnostics, Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public async Task FunctionResultStillSupportsHash(bool asynchronous)
    {
        await Run("PARAM target=#MECH:AES-ECB", asynchronous);
        var program = await Run("target=Parameters(HASH-SHA512) VAR digest=Hash(target,0x(01))", asynchronous);
        AssertConsumedState((ParameterVariableDeclaration)Get());
        Assert.That(Get("digest").Value, Is.EqualTo("0x(" + Convert.ToHexString(SHA512.HashData(new byte[] { 1 })) + ")").IgnoreCase);
        Assert.That(program.Diagnostics, Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public async Task StructuredHeaderReferenceSurvivesRoundtripValidation(bool asynchronous)
    {
        await Run("TR31H header=" + Header + " PARAM target=#MECH:AES-ECB", asynchronous);
        var program = await Run("target=#MECH:WRAP-AES-TR31 #BLKH:header", asynchronous);
        var current = (ParameterVariableDeclaration)Get();
        AssertConsumedState(current);
        Assert.That(current.HeaderReference, Is.SameAs(Get("header")));
        Assert.That(program.Diagnostics, Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public async Task LegacyDeclarationSerializationRemainsUnchanged(bool asynchronous)
    {
        var program = await Run("PATH payload=\"0x(00)#MECH:HASH-SHA512\" " +
            "PARAM target=#MECH:HASH-SHA256 #IV:payload VAR digest=Hash(target,0x(01))", asynchronous);
        Assert.That(((ParameterVariableDeclaration)Get()).Mechanism, Is.EqualTo("HASH-SHA256"));
        Assert.That(Get("digest").Value, Is.EqualTo("0x(" + Convert.ToHexString(SHA512.HashData(new byte[] { 1 })) + ")").IgnoreCase);
        Assert.That(program.Diagnostics, Is.Empty);
    }
}

