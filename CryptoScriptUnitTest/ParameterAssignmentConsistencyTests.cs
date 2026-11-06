using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using Newtonsoft.Json;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class ParameterAssignmentConsistencyTests
{
    private const string Header = "{KBVID:D;KEYU:D0;ALGO:A;MODEU:B;EXP:E;OPTID:LB;OPTBD:\"private-marker\";}";
    private const string Iv = "0x(000102030405060708090A0B0C0D0E0F)";
    private VariableDeclaration[] previous = null!;

    [SetUp] public void Setup()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }

    [TearDown] public void Cleanup()
    {
        VariableDictionary.Instance().Clear();
        foreach (var value in previous) VariableDictionary.Instance().Add(value);
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

    private static Task<CryptoScriptProgram> Prepare(bool asynchronous) => Run(
        "TR31H header=" + Header + " PARAM target=#MECH:WRAP-AES-TR31 #BLKH:header " +
        "PARAM selector=#MECH:HASH-SHA256 PATH lower=\"hash-sha256\" KEY key=" + Iv, asynchronous);

    public static IEnumerable<TestCaseData> InvalidCases()
    {
        var cases = new (string Name, string Rhs)[]
        {
            ("AesCbcEcb", "#MECH:AES-CBC #MECH:AES-ECB"),
            ("AesEcbCbc", "#MECH:AES-ECB #MECH:AES-CBC"),
            ("AesSecret", "#MECH:AES-CBC #MECH:SECRET_SPLIT #PARTS:1"),
            ("SecretAes", "#MECH:SECRET_SPLIT #MECH:AES-CBC #PARTS:1"),
            ("Hash256512", "#MECH:HASH-SHA256 #MECH:HASH-SHA512"),
            ("Hash512256", "#MECH:HASH-SHA512 #MECH:HASH-SHA256"),
            ("InterleavedIv", "#MECH:AES-CBC #IV:\"private-marker\" #MECH:AES-ECB"),
            ("InterleavedPad", "#MECH:AES-ECB #PAD:NONE #MECH:AES-CBC"),
            ("ResolvedConflict", "#MECH:selector #MECH:HASH-SHA512"),
            ("ResolvedConflictReverse", "#MECH:HASH-SHA512 #MECH:selector"),
            ("SingleInvalidSecret", "#MECH:SECRET_SPLIT #PARTS:1"),
            ("RepeatedInvalidSecret", "#MECH:SECRET_SPLIT #PARTS:1 #MECH:SECRET_SPLIT"),
            ("RepeatedOtherParameter", "#MECH:SECRET_SPLIT #PARTS:2 #MECH:SECRET_SPLIT #PARTS:3")
        };
        foreach (var item in cases)
            foreach (bool asynchronous in new[] { false, true })
                yield return new TestCaseData(item.Rhs, asynchronous).SetName($"MechanismReject_{item.Name}_{asynchronous}");
    }

    [TestCaseSource(nameof(InvalidCases))]
    public async Task InvalidMechanismsKeepCompleteTarget(string rhs, bool asynchronous)
    {
        await Prepare(asynchronous);
        var original = (ParameterVariableDeclaration)Get();
        var type = original.Type;
        var header = original.HeaderReference;
        string value = original.Value, metadata = Snapshot(original);
        var runner = new CryptoScriptRunner();
        await Assert.ThatAsync(async () => await Run("VAR before=0 target=" + rhs + " VAR after=1", asynchronous, runner),
            Throws.TypeOf<SemanticErrorException>());
        Assert.That(Get(), Is.SameAs(original));
        Assert.That(Get().Type, Is.SameAs(type));
        Assert.That(Get().Value, Is.EqualTo(value));
        Assert.That(Snapshot((ParameterVariableDeclaration)Get()), Is.EqualTo(metadata));
        Assert.That(((ParameterVariableDeclaration)Get()).HeaderReference, Is.SameAs(header));
        Assert.That(Get("before").Value, Is.EqualTo("0"));
        Assert.That(Get("after"), Is.Null);
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
        var diagnostic = runner.Diagnostics.Single();
        Assert.That(diagnostic.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.InvalidAssignmentInitializer));
        Assert.That(diagnostic.Type, Is.EqualTo("Assignment"));
        Assert.That(diagnostic.Identifier, Is.EqualTo("target"));
        Assert.That(diagnostic.Location!.ExecutionId, Is.EqualTo(runner.ExecutionId));
        Assert.That(diagnostic.Location.StatementIndex, Is.EqualTo(2));
        Assert.That(JsonConvert.SerializeObject(runner.Diagnostics), Does.Not.Contain("private-marker").And.Not.Contain(rhs));
    }

    public static IEnumerable<TestCaseData> ValidCases()
    {
        var cases = new (string Name, string Rhs, string Mechanism)[]
        {
            ("Single", "#MECH:AES-CBC #IV:" + Iv, "AES-CBC"),
            ("Identical", "#MECH:AES-CBC #MECH:AES-CBC", "AES-CBC"),
            ("InterleavedBefore", "#IV:" + Iv + " #MECH:AES-CBC #PAD:NONE #MECH:AES-CBC", "AES-CBC"),
            ("InterleavedAfter", "#MECH:AES-CBC #PAD:NONE #MECH:AES-CBC #IV:" + Iv, "AES-CBC"),
            ("Hash", "#MECH:HASH-SHA256 #MECH:HASH-SHA256", "HASH-SHA256"),
            ("ResolvedPrefix", "#MECH:selector #MECH:HASH-SHA256", "HASH-SHA256"),
            ("ResolvedCase", "#MECH:lower #MECH:HASH-SHA256", "HASH-SHA256"),
            ("Secret", "#MECH:SECRET_SPLIT #PARTS:2 #MECH:SECRET_SPLIT", "SECRET_SPLIT"),
            ("SingleSecret", "#MECH:SECRET_SPLIT #PARTS:2", "SECRET_SPLIT"),
            ("HeaderReference", "#MECH:WRAP-AES-TR31 #BLKH:header #MECH:WRAP-AES-TR31", "WRAP-AES-TR31")
        };
        foreach (var item in cases)
            foreach (bool asynchronous in new[] { false, true })
                yield return new TestCaseData(item.Rhs, item.Mechanism, asynchronous).SetName($"MechanismAccept_{item.Name}_{asynchronous}");
    }

    [TestCaseSource(nameof(ValidCases))]
    public async Task EquivalentMechanismsPublishOneConsistentValue(string rhs, string mechanism, bool asynchronous)
    {
        await Prepare(asynchronous);
        var original = (ParameterVariableDeclaration)Get();
        string originalMetadata = Snapshot(original);
        var program = await Run("target=" + rhs, asynchronous);
        var current = (ParameterVariableDeclaration)Get();
        Assert.That(current, Is.Not.SameAs(original));
        Assert.That(current.Type, Is.SameAs(original.Type));
        Assert.That(current.Id, Is.EqualTo(original.Id));
        Assert.That(Snapshot(original), Is.EqualTo(originalMetadata));
        Assert.That(current.Mechanism, Is.EqualTo(mechanism));
        Assert.That(current.GetParameter("MECH"), Is.EqualTo(mechanism));
        var serializedMechanisms = current.Value.Split('#').Where(part => part.StartsWith("MECH:")).ToArray();
        Assert.That(serializedMechanisms, Is.EqualTo(new[] { "MECH:" + mechanism }));
        Assert.That(current.ExplicitParameterNames.Count(name => name == "#MECH"), Is.EqualTo(1));
        Assert.That(program.Diagnostics, Is.Empty);
        if (mechanism == "HASH-SHA256")
        {
            var next = await Run("VAR digest=Hash(target,0x(01))", asynchronous);
            Assert.That(Get("digest").Value, Is.EqualTo("0x(" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 1 })) + ")").IgnoreCase);
            Assert.That(next.Diagnostics, Is.Empty);
        }
        if (mechanism == "SECRET_SPLIT")
        {
            var next = await Run("SECRET shares=Share(target,key) KEY restored=Restore(shares)", asynchronous);
            Assert.That(Get("restored").Value, Is.EqualTo(Get("key").Value).IgnoreCase);
            Assert.That(next.Diagnostics, Is.Empty);
        }
        if (mechanism == "WRAP-AES-TR31")
            Assert.That(current.HeaderReference, Is.SameAs(Get("header")));
    }

    [TestCase(false)] [TestCase(true)]
    public async Task HistoricalDuplicateDeclarationsRemainUnchanged(bool asynchronous)
    {
        var program = await Run("PARAM first=#MECH:AES-CBC #MECH:AES-ECB " +
            "PARAM second=#MECH:AES-ECB #MECH:AES-CBC", asynchronous);
        Assert.That(((ParameterVariableDeclaration)Get("first")).Mechanism, Is.EqualTo("AES-CBC"));
        Assert.That(((ParameterVariableDeclaration)Get("first")).GetParameter("MECH"), Is.EqualTo("AES-ECB"));
        Assert.That(((ParameterVariableDeclaration)Get("second")).Mechanism, Is.EqualTo("AES-ECB"));
        Assert.That(((ParameterVariableDeclaration)Get("second")).GetParameter("MECH"), Is.EqualTo("AES-CBC"));
        Assert.That(((ParameterVariableDeclaration)Get("first")).ExplicitParameterNames.Count(name => name == "#MECH"), Is.EqualTo(2));
        Assert.That(program.Diagnostics, Is.Empty);
    }
}
