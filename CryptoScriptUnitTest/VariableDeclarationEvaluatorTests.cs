using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class VariableDeclarationEvaluatorTests
{
    private VariableDeclaration[] previous = null!;
    private readonly List<SemanticError> errors = new();

    [SetUp]
    public void SetUp()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
        errors.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        VariableDictionary.Instance().Clear();
        foreach (var variable in previous)
            VariableDictionary.Instance().Add(variable);
    }

    private static VariableDeclarationNode Node(string type = "VAR") =>
        new("result", type, null, null, Array.Empty<ParameterInitializerNode>(), null);

    private static FunctionCallInitializerNode Call() =>
        new("Unknown", "Unknown()", Array.Empty<FunctionCallArgumentNode>());

    private static ArgumentParameter Parameter(string type, string value)
    {
        var parameter = new ArgumentParameter();
        parameter.SetParameter(type, value);
        return parameter;
    }

    private Statement Evaluate(VariableDeclarationNode node,
        Func<FunctionCallInitializerNode, Statement>? call = null,
        Func<string, string, ArgumentParameter>? parameter = null) =>
        VariableDeclarationEvaluator.Evaluate(node, errors,
            call ?? (_ => throw new AssertionException("Unexpected function evaluation")),
            parameter ?? Parameter);

    [Test]
    public void EvaluatesLiteralWithoutParserAndRegistersResult()
    {
        var result = (VariableDeclaration)Evaluate(Node() with
        {
            Expression = new LiteralInitializerNode("0x(AbCd)")
        });
        Assert.That(result.Value, Is.EqualTo("0x(AbCd)"));
        Assert.That(result.Type, Is.TypeOf<CryptoTypeVar>());
        Assert.That(result.ValueFormat, Is.EqualTo(FormatConversions.HEX));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.SameAs(result));
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void EvaluatesParametersInOrderAndRegistersResult()
    {
        var seen = new List<(string, string)>();
        var result = (ParameterVariableDeclaration)Evaluate(Node("PARAM") with
        {
            Parameters = new[]
            {
                new ParameterInitializerNode("#MECH", "AES-CBC", "#MECH:AES-CBC"),
                new ParameterInitializerNode("#IV", "0x(Ab)", "#IV:0x(Ab)"),
                new ParameterInitializerNode("#IV", "0x(cd)", "#IV:0x(cd)")
            }
        }, parameter: (type, value) =>
        {
            seen.Add((type, value));
            return Parameter(type, value);
        });
        Assert.That(seen, Is.EqualTo(new[]
        {
            ("#MECH", "AES-CBC"), ("#IV", "0x(Ab)"), ("#IV", "0x(cd)")
        }));
        Assert.That(result.GetParameter("IV"), Is.EqualTo("0x(cd)"));
        Assert.That(result.GetParameter("MECH"), Is.EqualTo("AES-CBC"));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.SameAs(result));
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void PassesFunctionNodeOnceAndRegistersSameReturnVariable()
    {
        var initializer = Call();
        var returned = new StringVariableDeclaration { Id = "old", Type = new CryptoTypeVar() };
        var count = 0;
        var result = Evaluate(Node() with { FunctionCall = initializer }, call =>
        {
            count++;
            Assert.That(call, Is.SameAs(initializer));
            return new FunctionCall { ReturnVariable = returned };
        });
        Assert.That(count, Is.EqualTo(1));
        Assert.That(result, Is.SameAs(returned));
        Assert.That(returned.Id, Is.EqualTo("result"));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.SameAs(returned));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PropagatesFunctionExceptionWithoutWrapping(bool semantic)
    {
        var existing = new SemanticError { Type = "FunctionCall", Message = "failure" };
        Exception expected = semantic
            ? new SemanticErrorException { SemanticError = existing }
            : new InvalidOperationException("failure");
        var actual = Assert.Catch(() => Evaluate(Node() with { FunctionCall = Call() }, _ =>
        {
            errors.Add(existing);
            throw expected;
        }));
        Assert.That(actual, Is.SameAs(expected));
        Assert.That(errors, Is.EqualTo(new[] { existing }));
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
    }

    [TestCase("VAR", "#MECH", "AES-CBC", "#MECH:AES-CBC", "Declaration type mismatch. Expected type : PARAM")]
    [TestCase("PARAM", "#IV", "0x(12)", "#IV:0x(12)", "Type PARAM does not contain element #MECH")]
    [TestCase("PARAM", null, null, "MECH", "Error in  parameter declaration : MECH")]
    [TestCase("PARAM", "#MECH", null, "MECH", "Error in  parameter declaration : MECH")]
    public void PreservesDeclarationErrors(string type, string? parameterType,
        string? value, string raw, string message)
    {
        var error = Assert.Throws<SemanticErrorException>(() => Evaluate(Node(type) with
        {
            Parameters = new[] { new ParameterInitializerNode(parameterType, value, raw) }
        }));
        Assert.That(error!.SemanticError!.Type, Is.EqualTo("Declaration"));
        Assert.That(error.SemanticError.Identifier, Is.EqualTo(type));
        Assert.That(error.SemanticError.Message, Is.EqualTo(message));
        Assert.That(errors.Single(), Is.SameAs(error.SemanticError));
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
    }

    [Test]
    public void WrapsParameterDelegateExceptionAtExistingBoundary()
    {
        var error = Assert.Throws<SemanticErrorException>(() => Evaluate(Node("PARAM") with
        {
            Parameters = new[] { new ParameterInitializerNode("#MECH", "AES-CBC", "#MECH:AES-CBC") }
        }, parameter: (_, _) => throw new InvalidOperationException("inner")));
        Assert.That(error!.SemanticError!.Message,
            Is.EqualTo("Error in  parameter declaration : #MECH:AES-CBC"));
        Assert.That(errors.Single(), Is.SameAs(error.SemanticError));
    }

    [Test]
    public void RejectsReturnTypeAfterFunctionSideEffects()
    {
        var returned = new StringVariableDeclaration { Id = "before", Type = new CryptoTypeKey() };
        var error = Assert.Throws<SemanticErrorException>(() => Evaluate(
            Node() with { FunctionCall = Call() }, _ =>
            {
                VariableDictionary.Instance().Add(returned);
                return new FunctionCall { ReturnVariable = returned };
            }));
        Assert.That(error!.SemanticError!.Message,
            Is.EqualTo("Declaration type mismatch. Expected type : KEY"));
        Assert.That(errors.Single(), Is.SameAs(error.SemanticError));
        Assert.That(returned.Id, Is.EqualTo("before"));
        Assert.That(VariableDictionary.Instance().Get("before"), Is.SameAs(returned));
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void PreservesNullResults(int kind)
    {
        var node = Node();
        if (kind != 0)
            node = node with { FunctionCall = Call() };
        Assert.That(Evaluate(node, _ => kind == 1 ? new FunctionCall() : null!), Is.Null);
        Assert.That(errors, Is.Empty);
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
    }

    [Test]
    public void Tr31TypeResolutionStillFailsBeforeFunctionEvaluation()
    {
        var expected = Assert.Throws<Exception>(() => CryptoType.Parse("TR31H"));
        var actual = Assert.Throws<Exception>(() => Evaluate(Node("TR31H") with
        {
            FunctionCall = Call(), Tr31Header = new Tr31HeaderInitializerNode("{KBVID:D;}")
        }));
        Assert.That(actual!.Message, Is.EqualTo(expected!.Message));
        Assert.That(errors, Is.Empty);
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
    }

    [TestCase("VAR result = #MECH:AES-CBC", "Declaration")]
    [TestCase("VAR result = Unknown()", "FunctionCall")]
    public void VisitorUsesReassignedErrorList(string source, string expectedType)
    {
        var visitor = new AntlrToStatement();
        var original = visitor.SemanticErrors;
        var existing = new SemanticError { Message = "existing" };
        errors.Add(existing);
        visitor.SemanticErrors = errors;
        var parser = ParserBuilder.StringBuild(source);
        var declaration = parser.program().statement(0).declaration();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        var error = Assert.Throws<SemanticErrorException>(() => visitor.VisitDeclaration(declaration));
        Assert.That(original, Is.Empty);
        Assert.That(errors, Has.Count.EqualTo(2));
        Assert.That(errors[0], Is.SameAs(existing));
        Assert.That(errors[1], Is.SameAs(error!.SemanticError));
        Assert.That(errors[1].Type, Is.EqualTo(expectedType));
    }
}
