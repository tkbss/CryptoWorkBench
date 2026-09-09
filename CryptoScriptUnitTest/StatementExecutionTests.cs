using Antlr4.Runtime;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class StatementExecutionTests
{
    private VariableDeclaration[] previous = null!;
    private readonly List<string> output = new();

    [SetUp]
    public void SetUp()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
        output.Clear();
        OutputOperations.PrintEvent += Record;
    }

    [TearDown]
    public void TearDown()
    {
        OutputOperations.PrintEvent -= Record;
        VariableDictionary.Instance().Clear();
        foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
    }

    private void Record(string value) => output.Add(value);

    private static CryptoScriptParser.ProgramContext Parse(string source)
    {
        var parser = ParserBuilder.StringBuild(source);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        return context;
    }

    [Test]
    public void PreservesStatementTextCallTextNullsAndOrder()
    {
        var context = Parse("VAR value = \"raw value\" Compare(value, value) VAR result = Compare(value,value) VAR empty =");
        var result = new CryptoScriptRunner().Execute(context);
        Assert.That(result.Statements, Has.Count.EqualTo(4));
        Assert.That(result.Statements[0].Text, Is.EqualTo("VARvalue=\"raw value\""));
        Assert.That(result.Statements[3], Is.Null);
        var call = (FunctionCall)result.Statements[1];
        Assert.That(call.Text, Is.Empty);
        Assert.That(call.CallText, Is.EqualTo("Compare(value,value)"));
        Assert.That(result.Statements[2].Text, Is.EqualTo("VARresult=Compare(value,value)"));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.SameAs(result.Statements[2]));
    }

    [Test]
    public void ExecutesEarlierSideEffectsOnceAndStopsAtRuntimeFailure()
    {
        var context = Parse("VAR value = \"one\" Print(value) Unknown() Print(\"later\")");
        var runner = new CryptoScriptRunner();
        var original = runner.SemanticErrors;
        var existing = new SemanticError { Message = "existing" };
        runner.SemanticErrors = new() { existing };
        Assert.Throws<SemanticErrorException>(() => runner.Execute(context));
        Assert.That(output, Is.EqualTo(new[] { "out: \"one\"" }));
        Assert.That(VariableDictionary.Instance().Contains("value"), Is.True);
        Assert.That(original, Is.Empty);
        Assert.That(runner.SemanticErrors, Has.Count.EqualTo(2));
        Assert.That(runner.SemanticErrors[0], Is.SameAs(existing));
    }

    [Test]
    public void ExecutesBeforeMappingTheNextStatement()
    {
        var context = Parse("Print(\"first\")");
        // A malformed later declaration must fail only after the earlier call has executed.
        var malformed = new CryptoScriptParser.StatementContext(context, -1);
        malformed.AddChild(new CryptoScriptParser.DeclarationContext(malformed, -1));
        context.children.Insert(context.ChildCount - 1, malformed);
        Assert.Throws<NullReferenceException>(() => new CryptoScriptRunner().Execute(context));
        Assert.That(output, Is.EqualTo(new[] { "out: \"first\"" }));
    }

    [Test]
    public void PreservesNonStatementRecoveryChildrenAndSkipsLastChild()
    {
        var context = Parse("VAR value = 1");
        context.children.Insert(0, new Antlr4.Runtime.Tree.ErrorNodeImpl(new CommonToken(0, "bad")));
        var result = new CryptoScriptRunner().Execute(context);
        Assert.That(result.Statements, Has.Count.EqualTo(2));
        Assert.That(result.Statements[0], Is.Null);
        Assert.That(result.Statements[1].Text, Is.EqualTo("VARvalue=1"));
    }

    [TestCase("VAR value = Unknown(missing)")]
    [TestCase("TR31H value = 0x(AB)")]
    [TestCase("VAR value = #IV:missing")]
    [TestCase("Print(\"untouched\")")]
    [TestCase("Unknown(Nested(missing))")]
    public void StatementMappingDoesNotExecuteOrResolve(string source)
    {
        var context = Parse(source).statement(0);
        var node = AntlrToStatement.Map(context);
        if (context.declaration() is { } declaration)
        {
            var statement = (VariableDeclarationStatementNode)node!;
            Assert.That(statement.RawText, Is.EqualTo(context.GetText()));
            Assert.That(statement.Declaration.Identifier, Is.EqualTo(declaration.ID().GetText()));
        }
        else
        {
            var statement = (FunctionCallStatementNode)node!;
            Assert.That(statement.Call.CallText, Is.EqualTo(context.GetText()));
        }
        Assert.That(output, Is.Empty);
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
    }

    [Test]
    public void EvaluatesOwnAstWithoutParserAndPreservesRawTexts()
    {
        var errors = new List<SemanticError>();
        var declaration = new VariableDeclarationNode("value", "VAR",
            new LiteralInitializerNode("\"a\\n b\""), null, Array.Empty<ParameterInitializerNode>(), null);
        var result = (VariableDeclaration)StatementEvaluator.Evaluate(
            new VariableDeclarationStatementNode(declaration, "declaration raw text"), errors)!;
        Assert.That(result.Text, Is.EqualTo("declaration raw text"));
        Assert.That(result.Value, Is.EqualTo("\"a\\n b\""));
        var call = new FunctionCallInitializerNode("Print", "call raw text",
            new FunctionCallArgumentNode[] { new VariableArgumentNode("value") });
        var executed = (FunctionCall)StatementEvaluator.Evaluate(new FunctionCallStatementNode(call), errors)!;
        Assert.That(executed.Text, Is.Empty);
        Assert.That(executed.CallText, Is.EqualTo("call raw text"));
        Assert.That(output, Is.EqualTo(new[] { "out: \"a\\n b\"" }));
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void EmptyRecoveryStatementRemainsNull()
    {
        var context = Parse("");
        context.children.Insert(0, new CryptoScriptParser.StatementContext(context, -1));
        Assert.That(AntlrToStatement.Map(context.statement(0)), Is.Null);
        Assert.That(new CryptoScriptRunner().Execute(context).Statements, Is.EqualTo(new Statement?[] { null }));
    }
}
