using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class FunctionCallEvaluatorTests
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

    private static FunctionCallInitializerNode Call(string name, params FunctionCallArgumentNode[] arguments) =>
        new(name, name + "(raw text)", arguments);

    private static ArgumentParameter Parameter(string type, string value)
    {
        var parameter = new ArgumentParameter();
        parameter.SetParameter(type, value);
        return parameter;
    }

    private FunctionCall Evaluate(FunctionCallInitializerNode call,
        Func<string, string, ArgumentParameter>? parameter = null) =>
        (FunctionCall)FunctionCallEvaluator.EvaluateFunctionCall(call, errors, parameter ?? Parameter);

    private Statement Argument(FunctionCallArgumentNode argument,
        Func<string, string, ArgumentParameter>? parameter = null) =>
        FunctionCallEvaluator.EvaluateArgument(argument, "Outer", errors, parameter ?? Parameter);

    [Test]
    public void ExecutesCompareAndPreservesCallMetadataAndArgumentOrder()
    {
        var call = Call("Compare", new LiteralArgumentNode("0x(AB)"), new LiteralArgumentNode("0x(ab)"));
        var result = Evaluate(call);
        Assert.That(result.Name, Is.EqualTo(call.Name));
        Assert.That(result.CallText, Is.EqualTo(call.CallText));
        Assert.That(result.Arguments.Cast<ArgumentExpression>().Select(a => a.Expr!.Value()),
            Is.EqualTo(new[] { "0x(AB)", "0x(ab)" }));
        Assert.That(result.ReturnVariable, Is.TypeOf<StringVariableDeclaration>());
        Assert.That(result.ReturnVariable!.Value, Is.EqualTo("Values are equal"));
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void EvaluatesMechanismLiteralInfoAndEmptyArguments()
    {
        Assert.That(((ArgumentMechanism)Argument(new MechanismArgumentNode("AES-CBC"))).Mechanism!.Value,
            Is.EqualTo("AES-CBC"));
        Assert.That(((ArgumentExpression)Argument(new LiteralArgumentNode("000123"))).Expr!.Value(),
            Is.EqualTo("000123"));
        Assert.That(((ArgumentInfo)Argument(new InfoArgumentNode("functions"))).InfoType, Is.EqualTo("functions"));
        Assert.That(Argument(new EmptyArgumentNode()), Is.TypeOf<Argument>());
        Assert.That(Argument(null!), Is.TypeOf<Argument>());
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ResolvesSameVariableObject()
    {
        var variable = new StringVariableDeclaration { Id = "input", Value = "0x(AB)" };
        VariableDictionary.Instance().Add(variable);
        Assert.That(((ArgumentVariable)Argument(new VariableArgumentNode("input"))).Id, Is.SameAs(variable));
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ReturnsParameterDelegateResultUnchanged()
    {
        var expected = new ArgumentParameter();
        var seen = new List<(string, string)>();
        var result = Argument(new ParameterArgumentNode("#IV", "missing"), (type, value) =>
        {
            seen.Add((type, value));
            return expected;
        });
        Assert.That(result, Is.SameAs(expected));
        Assert.That(seen, Is.EqualTo(new[] { ("#IV", "missing") }));
        Assert.That(Argument(new ParameterArgumentNode("#IV", "missing"), (_, _) => null!), Is.Null);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void NestedCallsUseSameDelegateInDepthFirstLeftToRightOrder()
    {
        var seen = new List<string>();
        var result = Evaluate(Call("Compare",
            new NestedCallArgumentNode(Call("Compare",
                new ParameterArgumentNode("first", "one"), new ParameterArgumentNode("second", "two"))),
            new NestedCallArgumentNode(Call("Compare",
                new ParameterArgumentNode("third", "three"), new ParameterArgumentNode("fourth", "four")))),
            (type, value) =>
            {
                seen.Add(type + ":" + value);
                return new ArgumentParameter { Type = "same", Value = "same" };
            });
        Assert.That(seen, Is.EqualTo(new[] { "first:one", "second:two", "third:three", "fourth:four" }));
        // Unquoted nested text currently becomes an empty string literal; preserve that behavior.
        Assert.That(result.Arguments.Cast<ArgumentExpression>().Select(a => a.Expr!.Value()),
            Is.All.EqualTo("\"\""));
        Assert.That(result.ReturnVariable!.Value, Is.EqualTo("Values are equal"));
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void UnknownFunctionFailsBeforeEvaluatingArguments()
    {
        var call = Call("Unknown", new ParameterArgumentNode("#IV", "missing"));
        var error = Assert.Throws<SemanticErrorException>(() => Evaluate(call,
            (_, _) => throw new AssertionException("Must not evaluate arguments")));
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(error!.SemanticError));
        Assert.That(errors[0].Type, Is.EqualTo("FunctionCall"));
        Assert.That(errors[0].FunctionName, Is.EqualTo("Unknown"));
        Assert.That(errors[0].FunctionCall, Is.EqualTo(call.CallText));
        Assert.That(errors[0].Message, Is.EqualTo("Unknown function: Unknown"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WrapsOperationFailuresWithAndWithoutArguments(bool withArgument)
    {
        var call = withArgument ? Call("Compare", new LiteralArgumentNode("0x(AB)")) : Call("Compare");
        var error = Assert.Throws<SemanticErrorException>(() => Evaluate(call));
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(error!.SemanticError));
        Assert.That(errors[0].Type, Is.EqualTo("FunctionCall"));
        Assert.That(errors[0].FunctionName, Is.EqualTo("Compare"));
        Assert.That(errors[0].FunctionCall, Is.EqualTo(call.CallText));
        Assert.That(errors[0].Message, Is.EqualTo("wrong number of arguments"));
    }

    [Test]
    public void MissingVariableStopsRemainingArgumentsAndPreservesNestedErrorOrder()
    {
        var existing = new SemanticError { Message = "existing" };
        errors.Add(existing);
        var inner = Call("Compare", new VariableArgumentNode("missing"), new ParameterArgumentNode("unused", "unused"));
        var outer = Call("Print", new NestedCallArgumentNode(inner));
        var error = Assert.Throws<SemanticErrorException>(() => Evaluate(outer,
            (_, _) => throw new AssertionException("Must stop at missing variable")));
        Assert.That(errors.Select(e => e.Type), Is.EqualTo(new[] { existing.Type, "Variable", "FunctionCall", "FunctionCall" }));
        Assert.That(errors[0], Is.SameAs(existing));
        Assert.That(errors[1].Identifier, Is.EqualTo("missing"));
        Assert.That(errors[1].Message, Is.EqualTo("Error  variable : missing is not declared"));
        Assert.That(errors[2].FunctionName, Is.EqualTo("Compare"));
        Assert.That(errors[2].FunctionCall, Is.EqualTo(inner.CallText));
        Assert.That(errors[3].FunctionName, Is.EqualTo("Print"));
        Assert.That(errors[3].FunctionCall, Is.EqualTo(outer.CallText));
        Assert.That(errors.Skip(2).Select(e => e.Message), Is.All.EqualTo(new SemanticErrorException().Message));
        Assert.That(errors[3], Is.SameAs(error!.SemanticError));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PreservesMechanismErrorBoundary(bool insideCall)
    {
        var argument = new MechanismArgumentNode("invalid-mechanism");
        var error = Assert.Throws<SemanticErrorException>(() =>
        {
            if (insideCall) Evaluate(Call("Compare", argument));
            else Argument(argument);
        });
        Assert.That(errors, Has.Count.EqualTo(insideCall ? 2 : 1));
        Assert.That(errors[0].Type, Is.EqualTo("Argument:Mechanism"));
        Assert.That(errors[0].FunctionName, Is.EqualTo(insideCall ? "Compare" : "Outer"));
        Assert.That(errors[0].Value, Is.EqualTo("invalid-mechanism"));
        Assert.That(errors[0].Message, Is.EqualTo("Unknown mechanism invalid-mechanism"));
        Assert.That(errors[^1], Is.SameAs(error!.SemanticError));
        if (insideCall)
        {
            Assert.That(errors[1].Type, Is.EqualTo("FunctionCall"));
            Assert.That(errors[1].Message, Is.EqualTo(new SemanticErrorException().Message));
        }
    }

    [Test]
    public void DirectParameterExceptionPropagatesButCallWrapsIt()
    {
        var expected = new InvalidOperationException("parameter failure");
        var argument = new ParameterArgumentNode("#IV", "missing");
        Assert.That(Assert.Throws<InvalidOperationException>(() => Argument(argument, (_, _) => throw expected)),
            Is.SameAs(expected));
        Assert.That(errors, Is.Empty);
        var error = Assert.Throws<SemanticErrorException>(() => Evaluate(Call("Compare", argument), (_, _) => throw expected));
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(error!.SemanticError));
        Assert.That(errors[0].Type, Is.EqualTo("FunctionCall"));
        Assert.That(errors[0].Message, Is.EqualTo(expected.Message));
    }

    [Test]
    public void EachEvaluationUsesItsExplicitErrorList()
    {
        var other = new List<SemanticError>();
        Assert.Throws<SemanticErrorException>(() => Evaluate(Call("Unknown")));
        var error = Assert.Throws<SemanticErrorException>(() =>
            FunctionCallEvaluator.EvaluateFunctionCall(Call("OtherUnknown"), other, Parameter));
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0].FunctionName, Is.EqualTo("Unknown"));
        Assert.That(other.Single(), Is.SameAs(error!.SemanticError));
        Assert.That(other[0].FunctionName, Is.EqualTo("OtherUnknown"));
    }

    [TestCase("function")]
    [TestCase("argument")]
    [TestCase("declaration")]
    public void VisitorUsesReassignedErrorList(string entry)
    {
        var visitor = new AntlrToStatement();
        var original = visitor.SemanticErrors;
        visitor.SemanticErrors = errors;
        var parser = ParserBuilder.StringBuild("VAR result = Compare(missing)");
        var declaration = parser.program().statement(0).declaration();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        var error = Assert.Throws<SemanticErrorException>(() =>
        {
            if (entry == "function") visitor.VisitFunctionCall(declaration.functionCall());
            else if (entry == "argument") visitor.VisitArgument(declaration.functionCall().arguments().argument(0));
            else visitor.VisitDeclaration(declaration);
        });
        Assert.That(original, Is.Empty);
        Assert.That(errors.Select(e => e.Type), Is.EqualTo(entry == "argument"
            ? new[] { "Variable" } : new[] { "Variable", "FunctionCall" }));
        Assert.That(errors[^1], Is.SameAs(error!.SemanticError));
    }
}
