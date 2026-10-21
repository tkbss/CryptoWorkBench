using CryptoScript.Documentation;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Operations;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class SaveFunctionTests
{
    private string directory = null!;
    private VariableDeclaration[] previous = null!;
    private readonly VariableSnapshotJsonSerializer serializer = new();
    private readonly VariableSnapshotFileStore store = new();
    private readonly List<string> output = new();
    private static VariableDictionary Variables => VariableDictionary.Instance();
    private string Destination => Path.Combine(directory, "保存-ä.json");
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    [SetUp]
    public void SetUp()
    {
        previous = Variables.GetVariables().ToArray();
        Variables.Clear();
        directory = Path.Combine(Path.GetTempPath(), "CryptoScript-S03-" + Guid.NewGuid().ToString("N"));
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
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        return context;
    }

    private static CryptoScriptProgram Run(string script) => new CryptoScriptRunner().Execute(Parse(script));

    private void Prepare() => Run($"PATH path = {Quote(Destination)} VAR data = \"Grüezi 世界\" VAR other = \"Grüezi 世界\"");

    private SemanticError Reject(string script, SemanticDiagnosticCode? code = null)
    {
        var before = Variables.GetVariables().ToArray();
        var runner = new CryptoScriptRunner();
        var exception = Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse(script)))!;
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(runner.Diagnostics[0], Is.SameAs(exception.SemanticError));
        if (code is not null) Assert.That(exception.SemanticError!.DiagnosticCode, Is.EqualTo(code));
        Assert.That(Variables.GetVariables().ToArray(), Is.EqualTo(before));
        Assert.That(Directory.GetFiles(directory, ".cryptoscript-*.tmp"), Is.Empty);
        return exception.SemanticError!;
    }

    [TestCase("data", 1)]
    [TestCase("other, data", 2)]
    [TestCase("path, data", 2)]
    public void ExplicitSelectionPreservesObjectsOrderAndContents(string selection, int count)
    {
        Prepare();
        var before = Variables.GetVariables().ToArray();
        string snapshot = serializer.Serialize(before);
        var selected = selection.Split(',').Select(n => Variables.Get(n.Trim())).ToArray();
        var program = Run($"Save(path, {selection})");
        var call = (FunctionCall)program.Statements.Single();
        Assert.That(call.ReturnVariable, Is.Null);
        Assert.That(call.CallText, Is.EqualTo("Save(<redacted>)"));
        Assert.That(call.Invocation!.Arguments.Skip(1).Select(a => a.SourceVariable), Is.EqualTo(selected));
        var read = store.Read((PathVariableDeclaration)Variables.Get("path"));
        Assert.That(read, Has.Count.EqualTo(count));
        Assert.That(serializer.Serialize(read), Is.EqualTo(serializer.Serialize(selected)));
        Assert.That(Variables.GetVariables().ToArray(), Is.EqualTo(before));
        Assert.That(serializer.Serialize(before), Is.EqualTo(snapshot));
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { Destination }));
        Assert.That(output, Is.Empty);
    }

    private void PrepareAllTypes()
    {
        Prepare();
        Run("""
            KEY key = GenerateKey(AES-ECB, 0x(000102030405060708090A0B0C0D0E0F))
            TR31H header = CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E)
            PARAM first = #MECH:WRAP-AES-TR31 #BLKH:header
            PARAM second = #MECH:WRAP-AES-TR31 #BLKH:header
            PARAM shares = Parameters(SECRET_SPLIT, #PARTS:2)
            SECRET secret = Share(shares, key)
            """);
    }

    [TestCase("ALL")]
    [TestCase("secret, second, key, path, data, first, header")]
    [TestCase("first, second")]
    public void AllTypesAndSharedOrEmbeddedHeaderReferencesRoundTrip(string selection)
    {
        PrepareAllTypes();
        var before = Variables.GetVariables().ToArray();
        string snapshot = serializer.Serialize(before);
        var selected = selection == "ALL" ? before : selection.Split(',').Select(n => Variables.Get(n.Trim())).ToArray();
        var result = Run($"Save(path, {selection})");
        Assert.That(((FunctionCall)result.Statements.Single()).Invocation!.Arguments.Skip(1)
            .Select(a => a.SourceVariable), Is.EqualTo(selected));
        var read = store.Read((PathVariableDeclaration)Variables.Get("path"));
        Assert.That(serializer.Serialize(read), Is.EqualTo(serializer.Serialize(selected)));
        var first = (ParameterVariableDeclaration)read.Single(v => v.Id == "first");
        var second = (ParameterVariableDeclaration)read.Single(v => v.Id == "second");
        Assert.That(first.HeaderReference, Is.SameAs(second.HeaderReference));
        if (read.Any(v => v.Id == "header"))
            Assert.That(first.HeaderReference, Is.SameAs(read.Single(v => v.Id == "header")));
        Assert.That(first.HeaderReference, Is.Not.SameAs(Variables.Get("header")));
        Assert.That(Variables.GetVariables().ToArray(), Is.EqualTo(before));
        Assert.That(serializer.Serialize(before), Is.EqualTo(snapshot));
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { Destination }));
    }

    [Test]
    public void AllIsContextualAndIncludesDestinationOnce()
    {
        Prepare();
        Run("VAR ALL = \"ordinary\" VAR all = \"lowercase\" Print(ALL)");
        Assert.That(output, Has.Count.EqualTo(1));
        var before = Variables.GetVariables().ToArray();
        Run("Save(path, ALL)");
        var read = store.Read((PathVariableDeclaration)Variables.Get("path"));
        Assert.That(read.Select(v => v.Id), Is.EqualTo(before.Select(v => v.Id)));
        Assert.That(read.Count(v => v.Id == "path"), Is.EqualTo(1));
        Assert.That(read.Single(v => v.Id == "ALL").Value, Is.EqualTo("\"ordinary\""));
    }

    [Test]
    public void AllCanBeAPathIdentifierAndLowercaseAllIsAnOrdinarySelection()
    {
        Run($"PATH ALL = {Quote(Destination)} VAR all = 123 Save(ALL, all)");
        Assert.That(store.Read((PathVariableDeclaration)Variables.Get("ALL")).Select(v => v.Id),
            Is.EqualTo(new[] { "all" }));
    }

    [TestCase("quoted")]
    [TestCase("bare")]
    [TestCase("relative")]
    public void PathDeclarationFormsWriteInsideTestDirectory(string form)
    {
        string value = form == "relative" ? Path.GetRelativePath(Environment.CurrentDirectory, Destination) : Destination;
        string literal = form == "bare" ? value : Quote(value);
        Run($"PATH path = {literal}\nVAR data = \"日本語\" Save(path, data)");
        Assert.That(Variables.Get("path"), Is.TypeOf<PathVariableDeclaration>());
        Assert.That(Variables.Get("path").Value, Is.EqualTo(value));
        Assert.That(store.Read((PathVariableDeclaration)Variables.Get("path")).Single().Value,
            Is.EqualTo("\"日本語\""));
        Assert.That(File.Exists(Destination), Is.True);
    }

    [TestCase(@"C:\folder\file.json")]
    [TestCase(@"\\server\share\file.json")]
    [TestCase("/tmp/file.json")]
    public void AbsolutePathSyntaxMapsWithoutAccessingThosePaths(string value)
    {
        Run($"PATH bare = {value}\nPATH quoted = {Quote(value)}");
        foreach (var name in new[] { "bare", "quoted" })
        {
            Assert.That(Variables.Get(name), Is.TypeOf<PathVariableDeclaration>());
            Assert.That(Variables.Get(name).Value, Is.EqualTo(value));
        }
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [TestCase("Save()")]
    [TestCase("Save(path)")]
    [TestCase("Save(data, other)")]
    [TestCase("Save(path, data, data)")]
    [TestCase("Save(path, path, path)")]
    [TestCase("Save(path, ALL, data)")]
    [TestCase("Save(path, data, ALL)")]
    [TestCase("Save(path, ALL, ALL)")]
    [TestCase("Save(path, 0x(AABBCCDD))")]
    [TestCase("Save(path, \"password-sensitive\")")]
    [TestCase("Save(path, AES-ECB)")]
    [TestCase("Save(path, #IV:0x(AABBCCDD))")]
    [TestCase("Save(path, Print(\"password-sensitive\"))")]
    [TestCase("Save(Print(\"password-sensitive\"), data)")]
    [TestCase("Save(path, Save(path, data))")]
    public void InvalidArgumentsAreRejectedBeforeSideEffects(string script)
    {
        Prepare();
        string before = serializer.Serialize(Variables.GetVariables());
        var error = Reject(script, SemanticDiagnosticCode.FunctionContractViolation);
        Assert.That(error.FunctionCall, Is.EqualTo("Save(<redacted>)"));
        Assert.That(error.Message, Does.Not.Contain("AABBCCDD").And.Not.Contain("password-sensitive"));
        Assert.That(serializer.Serialize(Variables.GetVariables()), Is.EqualTo(before));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
        Assert.That(output, Is.Empty);
    }

    [TestCase("Save(missing, data)")]
    [TestCase("Save(path, data, missing)")]
    public void UnknownReferencesAreControlledAndDoNotWrite(string script)
    {
        Prepare();
        Reject(script, SemanticDiagnosticCode.UnknownVariable);
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [TestCase("VAR result = Save(path, data)")]
    [TestCase("KEY result = Save(path, data)")]
    [TestCase("PATH result = Save(path, data)")]
    [TestCase("PARAM result = Save(path, data)")]
    [TestCase("SECRET result = Save(path, data)")]
    [TestCase("TR31H result = Save(path, data)")]
    [TestCase("VAR data = Save(path, data)")]
    public void SaveCannotInitializeAnyDeclaration(string script)
    {
        Prepare();
        Reject(script, SemanticDiagnosticCode.InvalidDeclaration);
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [TestCase("Print(Save(path, data))")]
    [TestCase("VAR result = Compare(Save(path, data), \"password-sensitive\")")]
    [TestCase("Print(Compare(Save(path, data), \"password-sensitive\"))")]
    public void SaveCannotBeUsedAsANestedValueAndDiagnosticsRemainRedacted(string script)
    {
        Prepare();
        var error = Reject(script, SemanticDiagnosticCode.FunctionContractViolation);
        Assert.That(error.FunctionCall, Is.EqualTo("Save(<redacted>)"));
        Assert.That(error.Message, Does.Not.Contain("password-sensitive"));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
        Assert.That(output, Is.Empty);
    }

    [Test]
    public void ExistingDestinationIsNeverOverwritten()
    {
        Prepare();
        File.WriteAllText(Destination, "existing");
        var error = Reject("Save(path, data)", SemanticDiagnosticCode.FunctionExecutionFailed);
        Assert.That(error.Message, Does.Contain("AlreadyExists"));
        Assert.That(File.ReadAllText(Destination), Is.EqualTo("existing"));
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { Destination }));
    }

    [Test]
    public void InvalidArgumentsAlsoPreserveAnExistingDestination()
    {
        Prepare();
        File.WriteAllText(Destination, "existing");
        Reject("Save(path, Print(\"password-sensitive\"))", SemanticDiagnosticCode.FunctionContractViolation);
        Assert.That(File.ReadAllText(Destination), Is.EqualTo("existing"));
        Assert.That(output, Is.Empty);
    }

    [Test]
    public void UnexpectedExecutionErrorsCannotLeakExceptionPayloads()
    {
        Prepare();
        var runner = new CryptoScriptRunner((call, _) =>
        {
            Assert.That(call.CallText, Is.EqualTo("Save(<redacted>)"));
            throw new InvalidOperationException("password-sensitive " + Destination);
        });
        var error = Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse("Save(path, data)")))!;
        Assert.That(error.SemanticError!.Message, Is.EqualTo("Save failed."));
        Assert.That(error.SemanticError.FunctionCall, Is.EqualTo("Save(<redacted>)"));
        Assert.That(error.InnerException, Is.Null);
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [Test]
    public void AllWithOnlyTheDestinationExportsThePathItself()
    {
        Run($"PATH path = {Quote(Destination)} Save(path, ALL)");
        var read = store.Read((PathVariableDeclaration)Variables.Get("path"));
        Assert.That(read, Has.Count.EqualTo(1));
        Assert.That(read.Single(), Is.TypeOf<PathVariableDeclaration>());
        Assert.That(read.Single().Value, Is.EqualTo(Destination));
    }

    [TestCase("missing-directory")]
    [TestCase("directory-target")]
    [TestCase("invalid-path")]
    public void FilesystemFailuresHaveSafeDiagnostics(string scenario)
    {
        Prepare();
        var path = (PathVariableDeclaration)Variables.Get("path");
        path.Value = scenario switch
        {
            "missing-directory" => Path.Combine(directory, "missing-sensitive", "file.json"),
            "directory-target" => directory,
            _ => "\0password-sensitive"
        };
        var error = Reject("Save(path, data)", SemanticDiagnosticCode.FunctionExecutionFailed);
        Assert.That(error.Message, Does.StartWith("Snapshot file operation failed:"));
        Assert.That(error.Message, Does.Not.Contain(directory).And.Not.Contain("sensitive"));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SerializationFailureDoesNotChangeVariablesOrPublishAFile(bool all)
    {
        Prepare();
        Variables.Get("data").ValueFormat = "password-sensitive";
        var error = Reject(all ? "Save(path, ALL)" : "Save(path, data)",
            SemanticDiagnosticCode.FunctionExecutionFailed);
        Assert.That(error.Message, Does.Contain("InvalidSnapshot").And.Not.Contain("password-sensitive"));
        Assert.That(Variables.Get("data").ValueFormat, Is.EqualTo("password-sensitive"));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [Test]
    public void LiteralKeyIsTypedAndCanBeSaved()
    {
        Prepare();
        Run("KEY legacy = 0x(AABBCCDD)");
        Run("Save(path, legacy)");
        Assert.That(Variables.Get("legacy"), Is.TypeOf<KeyVariableDeclaration>());
        var restored = store.Read((PathVariableDeclaration)Variables.Get("path")).Single();
        Assert.That(restored, Is.TypeOf<KeyVariableDeclaration>());
        Assert.That(serializer.Serialize([restored]), Is.EqualTo(serializer.Serialize([Variables.Get("legacy")])));
        Assert.That(((KeyVariableDeclaration)restored).KeyType.Algorithm, Is.EqualTo(KeyAlgorithm.Unknown));
        Assert.That(((KeyVariableDeclaration)restored).KeySizeInBits.Bits, Is.EqualTo(32UL));
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { Destination }));
    }

    [TestCase("123")]
    [TestCase("0x(ABCD)")]
    public void NonPathLiteralDeclarationsAreControlled(string literal)
    {
        Reject($"PATH path = {literal}", SemanticDiagnosticCode.InvalidDeclaration);
        Assert.That(Variables.GetVariables(), Is.Empty);
    }

    [Test]
    public void SaveDocumentationIsRegisteredCopiedAndReachableThroughInfo()
    {
        Assert.That(OperationList.Operations, Does.Contain("Save"));
        var id = InfoDocumentId.CreateFunction("Save");
        Assert.That(InfoDocumentCatalog.TryGet(id, out var entry), Is.True);
        Assert.That(entry!.MarkdownFileName, Is.EqualTo("Info.Func.Save.md"));
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocumentation("Save", out var markdown), Is.True);
        Assert.That(markdown, Does.Contain("CreateNew").And.Contain("ALL").And.Contain("not encrypted"));
        Run("Info(Save)");
        Assert.That(output, Has.Count.EqualTo(1));
        Assert.That(output[0], Does.Contain("Save"));
        Assert.That(Variables.GetVariables(), Is.Empty);
    }
}
