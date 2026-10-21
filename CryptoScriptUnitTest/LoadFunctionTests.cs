using CryptoScript.Documentation;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Operations;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class LoadFunctionTests
{
    private string directory = null!;
    private VariableDeclaration[] previous = null!;
    private readonly VariableSnapshotJsonSerializer serializer = new();
    private readonly List<string> output = new();
    private static VariableDictionary Variables => VariableDictionary.Instance();
    private string Source => Path.Combine(directory, "保存-ä.json");
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    [SetUp]
    public void SetUp()
    {
        previous = Variables.GetVariables().ToArray();
        Variables.Clear();
        directory = Path.Combine(Path.GetTempPath(), "CryptoScript-S04-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        output.Clear();
        OutputOperations.PrintEvent += Record;
        OutputOperations.InfoEvent += Record;
    }

    [TearDown]
    public void TearDown()
    {
        OutputOperations.PrintEvent -= Record;
        OutputOperations.InfoEvent -= Record;
        Variables.Clear();
        foreach (var variable in previous) Variables.Add(variable);
        Directory.Delete(directory, recursive: true);
    }

    private void Record(string value) => output.Add(value);
    private static CryptoScriptParser.ProgramContext Parse(string script)
    {
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.SyntaxErrorOccured = false;
        SyntaxErrorListner.ErrorMessage.Clear();
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero, script);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False, script);
        return context;
    }
    private static CryptoScriptProgram Run(string script) => new CryptoScriptRunner().Execute(Parse(script));
    private void Reset(string pathName = "path")
    {
        Variables.Clear();
        Run($"PATH {pathName} = {Quote(Source)}");
    }
    private void Prepare(string selection = "data, other")
    {
        Reset();
        Run("VAR data = \"Grüezi 世界\" VAR other = 0x(AABBCCDD)");
        Run($"Save(path, {selection})");
        Reset();
    }
    private void PrepareGraph(string selection = "header, first, second")
    {
        Reset();
        Run("""
            TR31H header = CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E)
            PARAM first = #MECH:WRAP-AES-TR31 #BLKH:header
            PARAM second = #MECH:WRAP-AES-TR31 #BLKH:header
            """);
        Run($"Save(path, {selection})");
    }
    private SemanticError Reject(string script, SemanticDiagnosticCode code = SemanticDiagnosticCode.FunctionContractViolation)
    {
        var before = Variables.GetVariables().ToArray();
        string state = serializer.Serialize(before);
        byte[]? file = File.Exists(Source) ? File.ReadAllBytes(Source) : null;
        var runner = new CryptoScriptRunner();
        var exception = Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse(script)))!;
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(runner.Diagnostics[0], Is.SameAs(exception.SemanticError));
        Assert.That(exception.SemanticError!.DiagnosticCode, Is.EqualTo(code));
        var after = Variables.GetVariables().ToArray();
        Assert.That(after, Has.Length.EqualTo(before.Length));
        for (int i = 0; i < before.Length; i++) Assert.That(after[i], Is.SameAs(before[i]));
        Assert.That(serializer.Serialize(after), Is.EqualTo(state));
        Assert.That(File.Exists(Source) ? File.ReadAllBytes(Source) : null, Is.EqualTo(file));
        Assert.That(exception.InnerException, Is.Null);
        Assert.That(exception.SemanticError.FunctionCall, Does.EndWith("(<redacted>)"));
        Assert.That(exception.SemanticError.Message, Does.Not.Contain("AABBCCDD").And.Not.Contain("password-sensitive"));
        Assert.That(output, Is.Empty);
        return exception.SemanticError;
    }

    [TestCase("")]
    [TestCase(", ALL")]
    [TestCase(", #ONCONFLICT:REJECT")]
    [TestCase(", ALL, #ONCONFLICT:REJECT")]
    public void AllSixTypesRoundTripWithMetadataAndSharedIdentity(string selection)
    {
        Reset();
        Run("""
            VAR data = "Grüezi 世界"
            KEY key = GenerateKey(AES-ECB, 0x(000102030405060708090A0B0C0D0E0F))
            TR31H header = CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E)
            PARAM first = #MECH:WRAP-AES-TR31 #BLKH:header
            PARAM second = #MECH:WRAP-AES-TR31 #BLKH:header
            PARAM shares = Parameters(SECRET_SPLIT, #PARTS:2)
            SECRET secret = Share(shares, key)
            Save(path, ALL)
            """);
        var originals = Variables.GetVariables().ToArray();
        string expected = serializer.Serialize(originals);
        Reset("input");
        var input = Variables.Get("input");
        var call = (FunctionCall)Run($"Load(input{selection})").Statements.Single();
        Assert.That(call.ReturnVariable, Is.Null);
        Assert.That(call.CallText, Is.EqualTo("Load(<redacted>)"));
        Assert.That(serializer.Serialize(Variables.GetVariables().Where(v => v.Id != "input")), Is.EqualTo(expected));
        Assert.That(Variables.Get("input"), Is.SameAs(input));
        foreach (var original in originals) Assert.That(Variables.Get(original.Id), Is.Not.SameAs(original));
        Assert.That(((ParameterVariableDeclaration)Variables.Get("first")).HeaderReference, Is.SameAs(Variables.Get("header")));
        Assert.That(((ParameterVariableDeclaration)Variables.Get("second")).HeaderReference, Is.SameAs(Variables.Get("header")));
        Run($"PATH destination = {Quote(Path.Combine(directory, "roundtrip.json"))}");
        Run("Save(destination, path, data, key, header, first, second, shares, secret)");
        Assert.That(File.ReadAllText(Path.Combine(directory, "roundtrip.json")), Is.EqualTo(File.ReadAllText(Source)));
        Assert.That(output, Is.Empty);
    }

    [TestCase("data", "data")]
    [TestCase("other, data", "other,data")]
    [TestCase("data, #ONCONFLICT:REJECT", "data")]
    public void SelectsUndeclaredSnapshotNamesInArgumentOrder(string selection, string expected)
    {
        Prepare();
        Run($"Load(path, {selection})");
        Assert.That(Variables.GetVariables().Select(v => v.Id), Is.EqualTo(new[] { "path" }.Concat(expected.Split(','))));
    }

    [Test]
    public void FunctionShapedSnapshotIdentifiersCanBeSelectedWithoutExecutingThem()
    {
        Prepare();
        var json = JObject.Parse(File.ReadAllText(Source));
        json["variables"]![0]!["identifier"] = "MasterKey";
        json["variables"]![1]!["identifier"] = "SessionKey";
        File.WriteAllText(Source, json.ToString());
        Run("Load(path, MasterKey, SessionKey)");
        Assert.That(Variables.Get("MasterKey").Value, Is.EqualTo("\"Grüezi 世界\""));
        Assert.That(Variables.Get("SessionKey").Value, Is.EqualTo("0x(AABBCCDD)"));
        Assert.That(output, Is.Empty);
    }

    [TestCase("Load(path)")]
    [TestCase("Load(path, ALL)")]
    [TestCase("Load(path, #ONCONFLICT:REJECT)")]
    [TestCase("Load(path, other, data)")]
    [TestCase("Load(path, other, data, #ONCONFLICT:REJECT)")]
    public void RejectsAnyConflictWithoutInsertingEarlierNewNames(string script)
    {
        Prepare();
        Run("VAR data = \"existing\"");
        Reject(script);
        Assert.That(Variables.Contains("other"), Is.False);
    }

    [TestCase("Load(path, #ONCONFLICT:REPLACE)")]
    [TestCase("Load(path, ALL, #ONCONFLICT:REPLACE)")]
    [TestCase("Load(path, other, data, #ONCONFLICT:REPLACE)")]
    public void ReplacePublishesWholeObjectsAndKeepsUnselectedObjects(string script)
    {
        Prepare();
        Run("KEY data = 0x(000102030405060708090A0B0C0D0E0F) VAR keep = \"unchanged\"");
        var old = Variables.Get("data");
        var keep = Variables.Get("keep");
        var path = Variables.Get("path");
        Run(script);
        Assert.That(Variables.Get("data"), Is.TypeOf<StringVariableDeclaration>().And.Not.SameAs(old));
        Assert.That(Variables.Get("data").Value, Is.EqualTo("\"Grüezi 世界\""));
        Assert.That(old.Value, Is.EqualTo("0x(000102030405060708090A0B0C0D0E0F)"));
        Assert.That(Variables.Get("other").Value, Is.EqualTo("0x(AABBCCDD)"));
        Assert.That(Variables.Get("keep"), Is.SameAs(keep));
        Assert.That(Variables.Get("path"), Is.SameAs(path));
    }

    [TestCase("Load()")]
    [TestCase("Load(data)")]
    [TestCase("Load(\"password-sensitive\")")]
    [TestCase("Load(path, data, data)")]
    [TestCase("Load(path, ALL, ALL)")]
    [TestCase("Load(path, ALL, data)")]
    [TestCase("Load(path, data, ALL)")]
    [TestCase("Load(path, #ONCONFLICT:REPLACE, data)")]
    [TestCase("Load(path, #ONCONFLICT:REJECT, #ONCONFLICT:REPLACE)")]
    [TestCase("Load(path, #ONCONFLICT:REPLACE, #ONCONFLICT:REPLACE)")]
    [TestCase("Load(path, #ONCONFLICT:UNKNOWN)")]
    [TestCase("Load(path, #ONCONFLICT:replace)")]
    [TestCase("Load(path, #ONCONFLICT:\"password-sensitive\")")]
    [TestCase("Load(path, #ONCONFLICT:0x(AABBCCDD))")]
    [TestCase("Load(path, #ONCONFLICT:b64(AABBCCDD))")]
    [TestCase("Load(path, #ONCONFLICT:PKCS-7)")]
    [TestCase("Load(path, #ONCONFLICT:MAC-SEND)")]
    [TestCase("Load(path, #ONCONFLICT:123)")]
    [TestCase("Load(path, #IV:0x(AABBCCDD))")]
    [TestCase("Load(path, 0x(AABBCCDD))")]
    [TestCase("Load(path, AES-ECB)")]
    [TestCase("Load(path, Print(\"password-sensitive\"))")]
    [TestCase("Load(Print(\"password-sensitive\"), data)")]
    [TestCase("Load(path, Load(path))")]
    public void InvalidArgumentsHaveNoSideEffects(string script)
    {
        Prepare();
        Run("VAR data = \"existing\" VAR UNKNOWN = \"REPLACE\"");
        Reject(script);
    }

    [TestCase("Load(missing)")]
    [TestCase("Load(path, missing)")]
    [TestCase("Load(path, data, missing, #ONCONFLICT:REPLACE)")]
    public void UnknownPathOrSelectionIsControlled(string script)
    {
        Prepare();
        Run("VAR data = \"existing\"");
        Reject(script, SemanticDiagnosticCode.UnknownVariable);
    }

    [TestCase("VAR")][TestCase("KEY")][TestCase("PATH")]
    [TestCase("PARAM")][TestCase("SECRET")][TestCase("TR31H")]
    public void VoidLoadCannotInitializeAnyTypeBeforeFileAccess(string type)
    {
        Reset(); // File deliberately absent: contract must win over NotFound.
        Reject($"{type} result = Load(path)", SemanticDiagnosticCode.InvalidDeclaration);
    }

    [TestCase("Print(Load(path))")]
    [TestCase("Print(Compare(Print(\"password-sensitive\"), Load(path)))")]
    [TestCase("VAR result = Compare(Print(\"password-sensitive\"), Load(path))")]
    [TestCase("Compare(Save(path, ALL), Load(path))")]
    public void NestedVoidCallsAreRejectedBeforeAnySiblingSideEffect(string script)
    {
        Reset();
        Reject(script);
        Assert.That(File.Exists(Source), Is.False);
    }

    [TestCase("Print(#ONCONFLICT:REPLACE)")]
    [TestCase("Print(Print(\"password-sensitive\"), #ONCONFLICT:REPLACE)")]
    [TestCase("Parameters(AES-ECB, #ONCONFLICT:REPLACE)")]
    [TestCase("Save(path, data, #ONCONFLICT:REPLACE)")]
    [TestCase("Encrypt(AES-ECB, #ONCONFLICT:REPLACE)")]
    [TestCase("Info(Load, #ONCONFLICT:REPLACE)")]
    public void ConflictParameterIsNotAcceptedByOtherFunctions(string script)
    {
        Prepare();
        Reject(script);
    }

    [Test]
    public void ConflictOptionIsNotAMechanismParameter()
    {
        Assert.That(ParameterRegistry.TryGet("#ONCONFLICT", out var definition), Is.True);
        Assert.That(definition!.Description, Does.Contain("Load-only"));
        Assert.That(ParameterTypeList.Instance.ParameterTypes, Does.Not.Contain("#ONCONFLICT"));
        Assert.Throws<ArgumentException>(() => new ParameterVariableDeclaration().SetParameter("#ONCONFLICT", "REPLACE"));
    }

    [TestCase("Load(path, #ONCONFLICT:0x(AABBCCDDZZ))")]
    [TestCase("Load(path, #ONCONFLICT:b64(AABBCCDD!))")]
    [TestCase("Load(path, #ONCONFLICT:Print(\"password-sensitive\"))")]
    [TestCase("Load(path, \"password-sensitive\" 0x(AABBCCDD))")]
    [TestCase("Load(path, #ONCONFLICT:\"password-sensitive")]
    public void MalformedLoadSyntaxRedactsAllPublishedDiagnostics(string script)
    {
        Reset();
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.SyntaxErrorOccured = false;
        SyntaxErrorListner.ErrorMessage.Clear();
        ParserBuilder.StringBuild(script).program();
        Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.True);
        Assert.That(SyntaxErrorList.Instance().ToArray(), Is.Not.Empty);
        foreach (var error in SyntaxErrorList.Instance())
        {
            Assert.That(error.Symbol, Is.EqualTo("<redacted>"));
            Assert.That(error.Message, Does.Contain("Load").And.Not.Contain("AABBCCDD").And.Not.Contain("password-sensitive"));
        }
        Assert.That(SyntaxErrorListner.ErrorMessage.ToString(),
            Does.Not.Contain("AABBCCDD").And.Not.Contain("password-sensitive"));
        Assert.That(File.Exists(Source), Is.False);
        Assert.That(Variables.GetVariables().Select(v => v.Id), Is.EqualTo(new[] { "path" }));
        Assert.That(output, Is.Empty);
    }

    [TestCase("Load(path, #ONCONFLICT:0x(AABBCCDDZZ))")]
    [TestCase("Load(path, #ONCONFLICT:b64(AABBCCDD!))")]
    [TestCase("Load(path, \"password-sensitive\" 0x(AABBCCDD))")]
    public void FileParserAlsoRedactsMalformedLoadSyntax(string script)
    {
        string file = Path.Combine(directory, "invalid-script.txt");
        File.WriteAllText(file, script);
        var previousError = Console.Error;
        using var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            ParserBuilder.FileBuild(file).program();
        }
        finally { Console.SetError(previousError); }
        Assert.That(captured.ToString(), Does.Contain("Load").And.Not.Contain("AABBCCDD").And.Not.Contain("password-sensitive"));
    }

    [TestCase(false)] [TestCase(true)]
    public void PathHasNoConflictExemption(bool all)
    {
        Prepare("path, data");
        var oldPath = Variables.Get("path");
        Reject(all ? "Load(path, ALL)" : "Load(path)");
        Run(all ? "Load(path, ALL, #ONCONFLICT:REPLACE)" : "Load(path, #ONCONFLICT:REPLACE)");
        Assert.That(Variables.Get("path"), Is.Not.SameAs(oldPath));
        Assert.That(Variables.Get("path").Value, Is.EqualTo(Source));
        Assert.That(Variables.Contains("data"), Is.True);
    }

    [TestCase("header, first, second")]
    [TestCase("first, second")]
    public void SelectiveParamsKeepSharedDependenciesWithoutPublishingOrRelinkingHeader(string snapshotSelection)
    {
        PrepareGraph(snapshotSelection);
        Reset();
        Run("TR31H header = CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E)");
        var liveHeader = Variables.Get("header");
        Run("Load(path, first, second)");
        var first = (ParameterVariableDeclaration)Variables.Get("first");
        var second = (ParameterVariableDeclaration)Variables.Get("second");
        Assert.That(first.HeaderReference, Is.SameAs(second.HeaderReference).And.Not.SameAs(liveHeader));
        Assert.That(Variables.Get("header"), Is.SameAs(liveHeader));
        Reset();
        Run("Load(path, first)");
        Assert.That(Variables.GetVariables().Select(v => v.Id), Is.EqualTo(new[] { "path", "first" }));
        Assert.That(((ParameterVariableDeclaration)Variables.Get("first")).HeaderReference, Is.Not.Null);
    }

    [TestCase("header")]
    [TestCase("first, header")]
    public void ReplaceRejectsReferencesFromUnchangedParams(string selection)
    {
        PrepareGraph();
        Reject($"Load(path, {selection}, #ONCONFLICT:REPLACE)");
    }

    [Test]
    public void ReplacingAllReferrersAndHeaderPublishesImportedGraph()
    {
        PrepareGraph();
        var oldHeader = Variables.Get("header");
        Run("Load(path, first, second, header, #ONCONFLICT:REPLACE)");
        Assert.That(Variables.Get("header"), Is.Not.SameAs(oldHeader));
        foreach (string name in new[] { "first", "second" })
            Assert.That(((ParameterVariableDeclaration)Variables.Get(name)).HeaderReference, Is.SameAs(Variables.Get("header")));
    }

    [Test]
    public void EqualIdentifiersOnDifferentHeaderObjectsDoNotBlockReplacement()
    {
        PrepareGraph();
        // Existing PARAM objects keep the old header, now detached from the root dictionary.
        Run("TR31H header = CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E)");
        var first = (ParameterVariableDeclaration)Variables.Get("first");
        var oldDependency = first.HeaderReference;
        Assert.That(oldDependency, Is.Not.SameAs(Variables.Get("header")));
        Run("Load(path, header, #ONCONFLICT:REPLACE)");
        Assert.That(Variables.Get("first"), Is.SameAs(first));
        Assert.That(first.HeaderReference, Is.SameAs(oldDependency).And.Not.SameAs(Variables.Get("header")));
    }

    [TestCase("{password-sensitive", "InvalidSnapshot")]
    [TestCase("Print(\"password-sensitive\")", "InvalidSnapshot")]
    [TestCase("{\"format\":\"CryptoScriptVariables\",\"version\":2}", "UnsupportedFormat")]
    [TestCase("{\"format\":\"password-sensitive\",\"version\":1}", "UnsupportedFormat")]
    public void BadFilesRetainSanitizedS02Categories(string content, string category)
    {
        Reset();
        File.WriteAllText(Source, content);
        var error = Reject("Load(path)", SemanticDiagnosticCode.FunctionExecutionFailed);
        Assert.That(error.Message, Is.EqualTo($"Snapshot file operation failed: {category}."));
    }

    [TestCase("type")][TestCase("format")][TestCase("duplicate")]
    public void EntireFileIsValidatedEvenWhenOnlyFirstRootIsSelected(string corruption)
    {
        Prepare();
        var json = JObject.Parse(File.ReadAllText(Source));
        if (corruption == "duplicate") json["variables"]![1]!["identifier"] = "data";
        else json["variables"]![1]!["metadata"]![corruption] = "password-sensitive";
        File.WriteAllText(Source, json.ToString());
        var error = Reject("Load(path, data, #ONCONFLICT:REPLACE)", SemanticDiagnosticCode.FunctionExecutionFailed);
        Assert.That(error.Message, Does.Contain("InvalidSnapshot"));
    }

    [Test]
    public void BrokenUnselectedHeaderReferenceAbortsWholeImport()
    {
        PrepareGraph();
        var json = JObject.Parse(File.ReadAllText(Source));
        json["variables"]![2]!["value"]!["headerReference"] = "o999";
        File.WriteAllText(Source, json.ToString());
        Reset();
        Assert.That(Reject("Load(path, header)", SemanticDiagnosticCode.FunctionExecutionFailed).Message,
            Does.Contain("InvalidSnapshot"));
    }

    [Test]
    public void LoadOptionCannotBeSmuggledIntoSnapshotParamMetadata()
    {
        PrepareGraph();
        var json = JObject.Parse(File.ReadAllText(Source));
        ((JArray)json["variables"]![1]!["value"]!["parameters"]!).Add(
            new JObject { ["name"] = "#ONCONFLICT", ["value"] = "REPLACE" });
        File.WriteAllText(Source, json.ToString());
        Reset();
        Assert.That(Reject("Load(path, header)", SemanticDiagnosticCode.FunctionExecutionFailed).Message,
            Does.Contain("InvalidSnapshot"));
    }

    [TestCase("missing", "NotFound")]
    [TestCase("directory", "InvalidPath")]
    [TestCase("oversized", "SizeLimitExceeded")]
    [TestCase("utf8", "InvalidSnapshot")]
    public void FileFailuresPreserveDictionaryAndDoNotExposePaths(string scenario, string category)
    {
        Reset();
        if (scenario == "directory") Variables.Get("path").Value = directory;
        if (scenario == "oversized")
        {
            using var file = File.Create(Source);
            file.SetLength(VariableSnapshotFileStore.DefaultMaxFileSizeBytes + 1);
        }
        if (scenario == "utf8") File.WriteAllBytes(Source, [0xc3, 0x28]);
        var error = Reject("Load(path)", SemanticDiagnosticCode.FunctionExecutionFailed);
        Assert.That(error.Message, Does.Contain(category).And.Not.Contain(directory));
    }

    [Test]
    public void DeserializedTextIsDataAndNeverExecuted()
    {
        Reset();
        const string script = "Print(\"password-sensitive\") VAR injected = 123";
        Run($"VAR data = {Quote(script)} Save(path, data)");
        Reset();
        Run("Load(path)");
        Assert.That(Variables.Get("data").Value, Is.EqualTo(Quote(script)));
        Assert.That(Variables.Contains("injected"), Is.False);
        Assert.That(output, Is.Empty);
    }

    [Test]
    public void UnknownAlgorithmKeyIsUsableByAesAfterLoad()
    {
        Reset();
        Run("KEY key = 0x(000102030405060708090A0B0C0D0E0F) Save(path, key)");
        string expected = serializer.Serialize([Variables.Get("key")]);
        Reset();
        Run("Load(path, key) PARAM p = Parameters(AES-ECB, #PAD:NONE)");
        var key = (KeyVariableDeclaration)Variables.Get("key");
        Assert.That(key.KeyType.Algorithm, Is.EqualTo(KeyAlgorithm.Unknown));
        Run("VAR cipher = Encrypt(p, key, 0x(00112233445566778899AABBCCDDEEFF)) VAR plain = Decrypt(p, key, cipher)");
        Assert.That(Variables.Get("cipher").Value, Is.EqualTo("0x(69C4E0D86A7B0430D8CDB78070B4C55A)").IgnoreCase);
        Assert.That(Variables.Get("plain").Value, Is.EqualTo("0x(00112233445566778899AABBCCDDEEFF)").IgnoreCase);
        Assert.That(serializer.Serialize([key]), Is.EqualTo(expected));
    }

    [Test]
    public void AllIsContextualAndLowercaseAllIsAnOrdinarySelector()
    {
        Reset("ALL");
        Run("VAR all = 123 Save(ALL, all)");
        Reset("ALL");
        Run("Load(ALL, all)");
        Assert.That(Variables.Get("all").Value, Is.EqualTo("123"));
        Run("Print(ALL)");
        Assert.That(output, Has.Count.EqualTo(1));
    }

    [Test]
    public void EmptySnapshotIsAnAtomicNoOp()
    {
        Reset();
        new VariableSnapshotFileStore().Write((PathVariableDeclaration)Variables.Get("path"), []);
        var path = Variables.Get("path");
        Run("Load(path)");
        Assert.That(Variables.GetVariables().Single(), Is.SameAs(path));
    }

    [Test]
    public void UnexpectedExceptionsAreRedactedAndDoNotPublish()
    {
        Prepare();
        var before = Variables.GetVariables().ToArray();
        var runner = new CryptoScriptRunner((call, _) =>
        {
            Assert.That(call.CallText, Is.EqualTo("Load(<redacted>)"));
            throw new InvalidOperationException("password-sensitive " + Source);
        });
        var exception = Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse("Load(path)")))!;
        Assert.That(exception.SemanticError!.Message, Is.EqualTo("Load failed."));
        Assert.That(exception.SemanticError.FunctionCall, Is.EqualTo("Load(<redacted>)"));
        Assert.That(exception.InnerException, Is.Null);
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(Variables.GetVariables().ToArray(), Is.EqualTo(before));
    }

    [Test]
    public void LoadDocumentationIsRegisteredAndReachable()
    {
        Assert.That(OperationList.Operations, Does.Contain("Load"));
        var id = InfoDocumentId.CreateFunction("Load");
        Assert.That(InfoDocumentCatalog.TryGet(id, out var entry), Is.True);
        Assert.That(entry!.MarkdownFileName, Is.EqualTo("Info.Func.Load.md"));
        Assert.That(InfoDocumentUri.TryParse("cryptoscript-info://function/Load", out var parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(id));
        Assert.That(new FileInfoDocumentationProvider().TryGetDocument(id, out string markdown), Is.True);
        Assert.That(markdown, Does.Contain("REJECT").And.Contain("REPLACE").And.Contain("not encrypted or authenticated"));
        Run("Info(Load)");
        Assert.That(output.Single(), Does.Contain("Load"));
        Assert.That(Variables.GetVariables(), Is.Empty);
    }
}
