using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class AssignmentTests
{
    private VariableDeclaration[] previous = null!;
    private readonly List<string> output = new();
    private const string Header = "{KBVID:D;KEYU:D0;ALGO:A;MODEU:B;EXP:E;}";
    private const string Key = "0x(000102030405060708090A0B0C0D0E0F)";

    [SetUp] public void Setup()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
        output.Clear();
        OutputOperations.PrintEvent += Record;
    }

    [TearDown] public void Cleanup()
    {
        OutputOperations.PrintEvent -= Record;
        VariableDictionary.Instance().Clear();
        foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
    }

    private void Record(string value) => output.Add(value);
    private static VariableDeclaration Get(string id = "target") => VariableDictionary.Instance().Get(id);
    private static CryptoScriptParser.ProgramContext Parse(string source)
    {
        var parser = ParserBuilder.StringBuild(source);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero, source);
        Assert.That(SyntaxErrorList.Instance(), Is.Empty, source);
        return context;
    }
    private static Task<CryptoScriptProgram> Run(string source, bool asynchronous = false,
        CryptoScriptRunner? runner = null)
    {
        runner ??= new();
        var context = Parse(source);
        return asynchronous ? runner.ExecuteAsync(context) : Task.FromResult(runner.Execute(context));
    }

    [TestCase("target = \"new\"")]
    [TestCase("target = Unknown(missing)")]
    [TestCase("target = #MECH:AES-CBC #IV:missing")]
    [TestCase("target = " + Header)]
    public void MappingIsSyntaxOnlyAndPreservesInitializer(string source)
    {
        var context = Parse(source).statement(0);
        var node = (AssignmentStatementNode)AntlrToStatement.Map(context)!;
        Assert.That(node.Identifier, Is.EqualTo("target"));
        Assert.That(node.RawText, Is.EqualTo(context.GetText()));
        Assert.That(Get(), Is.Null);
        Assert.That(output, Is.Empty);
        if (context.assignment().functionCall() is not null)
            Assert.That(node.Initializer, Is.TypeOf<FunctionCallExpressionNode>());
        if (context.assignment().declareparam().Length != 0)
            Assert.That(node.Parameters.Items.Select(p => p.TypeName), Is.EqualTo(new[] { "#MECH", "#IV" }));
        if (context.assignment().tr31Header() is not null)
            Assert.That(node.Tr31Header!.Fields, Has.Count.EqualTo(5));
    }

    [TestCase(false)] [TestCase(true)]
    public async Task RepeatedAssignmentsAndFollowingStatementsUseNewValue(bool asynchronous)
    {
        var program = await Run("VAR d=\"Original\" d=\"Neuer Wert\" Print(d) d=0x(1234) " +
            "VAR copy=Compare(d,0x(1234)) d=Compare(d,d) Print(d)", asynchronous);
        Assert.That(Get("d").Value, Is.EqualTo("Values are equal"));
        Assert.That(Get("copy").Value, Is.EqualTo("Values are equal"));
        Assert.That(output, Is.EqualTo(new[] { "out: \"Neuer Wert\"", "out: Values are equal" }));
        Assert.That(program.Statements[0], Is.Not.SameAs(program.Statements[1]));
        Assert.That(((VariableDeclaration)program.Statements[0]).Value, Is.EqualTo("\"Original\""));
        Assert.That(program.Statements[1].Text, Is.EqualTo("d=\"Neuer Wert\""));
        Assert.That(program.Diagnostics, Is.Empty);
    }

    [TestCase("0x(AABB)", "HEX")]
    [TestCase("b64(AQI=)", "B64")]
    [TestCase("42", "INT")]
    [TestCase("\"new\"", "STR")]
    public async Task VarLiteralsKeepDeclarationSemantics(string literal, string unusedFormat)
    {
        await Run("VAR target=\"old\" VAR expected=" + literal + " target=" + literal);
        Assert.That(Get().Value, Is.EqualTo(Get("expected").Value));
        Assert.That(Get().ValueFormat, Is.EqualTo(Get("expected").ValueFormat));
        Assert.That(Get().Type!.Name, Is.EqualTo("VAR"));
    }

    [TestCase(false)] [TestCase(true)]
    public async Task KeyLiteralAndFunctionPreserveCompleteResultMetadata(bool asynchronous)
    {
        await Run("KEY target=0x(AA) target=b64(AQI=)", asynchronous);
        var literal = (KeyVariableDeclaration)Get();
        Assert.That(literal.KeySizeInBits.Bits, Is.EqualTo(16));
        Assert.That(literal.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Unknown)));
        await Run("target=GenerateKey(AES-ECB," + Key + ")", asynchronous);
        var key = (KeyVariableDeclaration)Get();
        Assert.That(key.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Aes)));
        Assert.That(key.KeySizeInBits.Bits, Is.EqualTo(128));
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt);
        key.DerivationMechanism = "assignment-test";
        key.KeyAttributes.Add(new() { ID = "KS", Length = 8, Data = "1234" });
        var attributes = key.KeyAttributes.ToArray();
        await Run("KEY found=0x(FF) found=Check(" + key.Kcv + ")", asynchronous);
        var found = (KeyVariableDeclaration)Get("found");
        Assert.That(found.Value, Is.EqualTo(key.Value));
        Assert.That(found.KeyType, Is.EqualTo(key.KeyType));
        Assert.That(found.Usage, Is.EqualTo(key.Usage));
        Assert.That(found.DerivationMechanism, Is.EqualTo(key.DerivationMechanism));
        Assert.That(found.KeyAttributes.Select(a => (a.ID, a.Length, a.Data)),
            Is.EqualTo(attributes.Select(a => (a.ID, a.Length, a.Data))));
        Assert.That(found.KeyAttributes[0], Is.Not.SameAs(key.KeyAttributes[0]));
        Assert.That(key.Id, Is.EqualTo("target"));
    }

    [TestCase(false)] [TestCase(true)]
    public async Task ParameterHeaderPathAndSecretAssignments(bool asynchronous)
    {
        await Run("TR31H header=" + Header + " TR31H target=" + Header +
            " target=CreateHeader(KBVID:D,KEYU:K0,ALGO:A,MODEU:B,EXP:E)" +
            " PARAM p=#MECH:WRAP-AES-TR31 #BLKH:header p=#MECH:WRAP-AES-TR31 #BLKH:target" +
            " PATH path=\"old\" path=\"new\\\\file.bin\"" +
            " KEY key=" + Key + " PARAM shares=Parameters(SECRET_SPLIT,#PARTS:2)" +
            " SECRET secret=Share(shares,key) secret=Share(shares,key) KEY restored=Restore(secret)", asynchronous);
        Assert.That(((BlockHeaderVariableDeclaration)Get()).Definition, Is.Not.Null);
        Assert.That(((ParameterVariableDeclaration)Get("p")).HeaderReference, Is.SameAs(Get()));
        Assert.That(((ParameterVariableDeclaration)Get("p")).ExplicitParameterNames, Does.Contain("#BLKH"));
        Assert.That(Get("path"), Is.TypeOf<PathVariableDeclaration>());
        Assert.That(Get("path").Value, Is.EqualTo("new\\file.bin"));
        Assert.That(Get("secret"), Is.TypeOf<SecretVariableDeclaration>());
        Assert.That(Get("restored").Value, Is.EqualTo(Key).IgnoreCase);
        await Run("target=" + Header + " shares=Parameters(SECRET_SPLIT,#PARTS:3)", asynchronous);
        Assert.That(((ParameterVariableDeclaration)Get("shares")).GetParameter("PARTS"), Is.EqualTo("3"));
    }

    public static IEnumerable<TestCaseData> Failures()
    {
        var cases = new (string Setup, string Assignment, SemanticDiagnosticCode Code)[]
        {
            ("VAR target=\"old\"", "target=GenerateKey(AES-ECB," + Key + ")", SemanticDiagnosticCode.AssignmentTypeMismatch),
            ("KEY target=GenerateKey(AES-ECB," + Key + ")", "target=Compare(0x(01),0x(01))", SemanticDiagnosticCode.AssignmentTypeMismatch),
            ("KEY target=" + Key, "target=0x(A)", SemanticDiagnosticCode.InvalidAssignmentInitializer),
            ("KEY target=" + Key, "target=\"secret-marker\"", SemanticDiagnosticCode.InvalidAssignmentInitializer),
            ("PATH target=\"old\"", "target=17", SemanticDiagnosticCode.InvalidAssignmentInitializer),
            ("TR31H target=" + Header, "target=0x(11)", SemanticDiagnosticCode.InvalidAssignmentInitializer),
            ("PARAM target=#MECH:AES-CBC #IV:0x(000102030405060708090A0B0C0D0E0F)", "target=#IV:0x(11)", SemanticDiagnosticCode.InvalidAssignmentInitializer),
            ("PARAM target=#MECH:SECRET_SPLIT #PARTS:2", "target=#MECH:SECRET_SPLIT #PARTS:1", SemanticDiagnosticCode.InvalidAssignmentInitializer),
            ("PARAM target=#MECH:AES-CBC", "target=Wrap(target,missing,missing)", SemanticDiagnosticCode.AssignmentTypeMismatch),
            ("VAR target=\"old\"", "target=#MECH:AES-CBC", SemanticDiagnosticCode.AssignmentTypeMismatch),
            ("VAR target=\"old\"", "target=Compare(\"secret-marker\")", SemanticDiagnosticCode.FunctionExecutionFailed),
            ("VAR target=\"old\"", "target=Hash(HASH-SHA256,missing)", SemanticDiagnosticCode.UnknownVariable),
            ("VAR target=\"old\"", "target=Unknown()", SemanticDiagnosticCode.UnknownFunction)
        };
        foreach (var item in cases)
            foreach (bool asynchronous in new[] { false, true })
                yield return new TestCaseData(item.Setup, item.Assignment, item.Code, asynchronous);
    }

    [TestCaseSource(nameof(Failures))]
    public async Task FailuresPreserveTargetAndAttachDiagnostic(string setup, string assignment,
        SemanticDiagnosticCode code, bool asynchronous)
    {
        await Run(setup, asynchronous);
        var original = Get();
        string value = original.Value;
        string metadata = Newtonsoft.Json.JsonConvert.SerializeObject(original);
        var runner = new CryptoScriptRunner();
        await Assert.ThatAsync(async () => await Run("VAR before=0 " + assignment + " VAR after=1", asynchronous, runner),
            Throws.TypeOf<SemanticErrorException>());
        Assert.That(Get(), Is.SameAs(original));
        Assert.That(Get().Value, Is.EqualTo(value));
        Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(Get()), Is.EqualTo(metadata));
        Assert.That(Get("after"), Is.Null);
        var diagnostic = runner.Diagnostics.Last(d => d.DiagnosticCode == code);
        Assert.That(diagnostic.DiagnosticCode, Is.EqualTo(code));
        Assert.That(diagnostic.Location!.StatementIndex, Is.EqualTo(2));
        Assert.That(diagnostic.Location.ExecutionId, Is.EqualTo(runner.ExecutionId));
        Assert.That(diagnostic.Message, Does.Not.Contain("secret-marker"));
    }

    [TestCase("Print(\"secret-marker\")")]
    [TestCase("Info(types)")]
    [TestCase("Load(missing)")]
    [TestCase("Save(missing,target)")]
    [TestCase("SecureLoad(missing)")]
    [TestCase("SecureSave(missing,target)")]
    [TestCase("Delete(target)")]
    [TestCase("Compare(Hash(HASH-SHA256,0x(11)),Print(\"secret-marker\"))")]
    public async Task VoidFunctionsAreRejectedBeforeAnyOperation(string initializer)
    {
        await Run("VAR target=\"old\"");
        var original = Get();
        int operations = 0;
        var runner = new CryptoScriptRunner((_, _) => operations++);
        await Assert.ThatAsync(async () => await Run("target=" + initializer, true, runner), Throws.TypeOf<SemanticErrorException>());
        Assert.That(runner.Diagnostics.Single().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.MissingReturnValue));
        Assert.That(Get(), Is.SameAs(original));
        Assert.That(operations, Is.Zero);
        Assert.That(output, Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public async Task UnknownTargetIsCheckedBeforeRhsAndNeverCreated(bool asynchronous)
    {
        int operations = 0;
        var runner = new CryptoScriptRunner((_, _) => operations++);
        await Assert.ThatAsync(async () => await Run("target=Compare(0x(11),0x(11))", asynchronous, runner),
            Throws.TypeOf<SemanticErrorException>());
        Assert.That(runner.Diagnostics.Single().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.UnknownVariable));
        Assert.That(Get(), Is.Null);
        Assert.That(operations, Is.Zero);
    }

    private sealed class ResultAlgorithm(Func<VariableDeclaration> result) : CryptoAlgorithm
    {
        public override VariableDeclaration Unwrap(string[] arguments) => result();
    }

    [TestCase(false)] [TestCase(true)]
    public async Task FailedAssignmentDoesNotClaimToRollbackForeignParameterEffects(bool asynchronous)
    {
        await Run("VAR target=0x(11) VAR pin=0x(1234) PARAM p=#MECH:WRAP-DES3-PINBLOCK-1 " +
            "KEY key=GenerateKey(DES3-ECB,0x(0123456789ABCDEFFEDCBA9876543210))");
        var original = Get();
        var parameter = (ParameterVariableDeclaration)Get("p");
        string before = parameter.Value;
        await Assert.ThatAsync(async () => await Run("target=Compare(Wrap(p,key,pin))", asynchronous),
            Throws.TypeOf<SemanticErrorException>());
        Assert.That(Get(), Is.SameAs(original));
        Assert.That(Get().Value, Is.EqualTo("0x(11)"));
        Assert.That(parameter.Value, Is.Not.EqualTo(before), "The foreign Wrap operation completed before Compare failed.");
    }

    [Test]
    public void EmptyAssignmentIsSyntaxErrorWhileEmptyDeclarationStaysCompatible()
    {
        var parser = ParserBuilder.StringBuild("target=");
        parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Positive);
        Assert.That(new CryptoScriptRunner().Execute(Parse("VAR target=")).Statements.Single(), Is.Null);
    }

    [TestCase(false)] [TestCase(true)]
    public async Task CheckMissKeepsContinuingDiagnosticBehaviorAndOriginalKey(bool asynchronous)
    {
        await Run("KEY target=" + Key);
        var original = Get();
        var runner = new CryptoScriptRunner();
        await Run("target=Check(0x(0000000000000000000000000000000000000000)) VAR after=1", asynchronous, runner);
        Assert.That(Get(), Is.SameAs(original));
        Assert.That(Get("after"), Is.Not.Null);
        Assert.That(runner.Diagnostics.Single().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyCheckValueNotFound));
        Assert.That(runner.Diagnostics.Single().Location!.StatementIndex, Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public async Task RuntimeRepresentationMayChangeWithoutChangingScriptType(bool asynchronous)
    {
        await Run("VAR target=0x(11) PARAM p=#MECH:WRAP-AES-TR31");
        var type = Get().Type;
        var runner = new CryptoScriptRunner(null, _ => new ResultAlgorithm(() =>
            new VariableDeclaration { Type = new CryptoTypeVar(), Value = "0x(22)", ValueFormat = FormatConversions.HEX }));
        await Run("target=Unwrap(p,0x(11),target)", asynchronous, runner);
        Assert.That(Get(), Is.TypeOf<VariableDeclaration>());
        Assert.That(Get().Value, Is.EqualTo("0x(22)"));
        Assert.That(Get().Type, Is.SameAs(type));
    }

    [TestCase("null", SemanticDiagnosticCode.MissingReturnValue)]
    [TestCase("untyped", SemanticDiagnosticCode.MissingReturnValue)]
    [TestCase("throw", SemanticDiagnosticCode.FunctionExecutionFailed)]
    [TestCase("alias", SemanticDiagnosticCode.FunctionContractViolation)]
    public async Task InvalidFunctionResultsDoNotPublish(string kind, SemanticDiagnosticCode code)
    {
        await Run("VAR target=0x(11) PARAM p=#MECH:WRAP-AES-TR31");
        var original = Get();
        var runner = new CryptoScriptRunner(null, _ => new ResultAlgorithm(() => kind switch
        {
            "null" => null!, "untyped" => new VariableDeclaration(), "alias" => original,
            _ => throw new InvalidOperationException("secret-marker")
        }));
        await Assert.ThatAsync(async () => await Run("target=Unwrap(p,0x(11),target)", true, runner),
            Throws.TypeOf<SemanticErrorException>());
        Assert.That(Get(), Is.SameAs(original));
        Assert.That(Get().Value, Is.EqualTo("0x(11)"));
        Assert.That(runner.Diagnostics.Last().DiagnosticCode, Is.EqualTo(code));
        Assert.That(runner.Diagnostics.Last().Message, Does.Not.Contain("secret-marker"));
    }
}
