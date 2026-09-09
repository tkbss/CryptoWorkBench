using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class ParameterEvaluatorTests
{
    private VariableDeclaration[] previous = null!;

    [SetUp]
    public void SetUp()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }

    [TearDown]
    public void TearDown()
    {
        VariableDictionary.Instance().Clear();
        foreach (var variable in previous)
            VariableDictionary.Instance().Add(variable);
    }

    [TestCase("#IV", "0x(AbCd)")]
    [TestCase("#IV", "b64(YWJj)")]
    [TestCase("#ADATA", "\"raw value\"")]
    [TestCase("#MECH", "AES-CBC")]
    [TestCase("#PAD", "PKCS-7")]
    public void PreservesRecognizedValues(string type, string value)
    {
        var result = ParameterEvaluator.Evaluate(type, value);
        Assert.That(result.Type, Is.EqualTo(type));
        Assert.That(result.Value, Is.EqualTo(value));
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
    }

    [Test]
    public void ResolvesVariableWithoutChangingIt()
    {
        var variable = new StringVariableDeclaration { Id = "input", Value = "0x(Ab)" };
        VariableDictionary.Instance().Add(variable);
        var result = ParameterEvaluator.Evaluate("#IV", "input");
        Assert.That(result.Value, Is.EqualTo("0x(Ab)"));
        Assert.That(VariableDictionary.Instance().Get("input"), Is.SameAs(variable));
        Assert.That(variable.Value, Is.EqualTo("0x(Ab)"));
        Assert.That(VariableDictionary.Instance().GetVariables().Count(), Is.EqualTo(1));
    }

    private static FunctionCallInitializerNode Call(string value) =>
        new("Compare", "Compare(#IV:" + value + ",#IV:0x(Ab))", new FunctionCallArgumentNode[]
        {
            new ParameterArgumentNode("#IV", value), new ParameterArgumentNode("#IV", "0x(Ab)")
        });

    private static VariableDeclarationNode Declaration(string? value) =>
        new("result", "PARAM", null, null, new[]
        {
            new ParameterInitializerNode("#MECH", "AES-CBC", "#MECH:AES-CBC"),
            new ParameterInitializerNode("#IV", value, "#IV:" + value)
        }, null);

    [Test]
    public void DeclarationStandardPathResolvesParameterAndRegistersResult()
    {
        VariableDictionary.Instance().Add(new StringVariableDeclaration { Id = "input", Value = "0x(Ab)" });
        var errors = new List<SemanticError>();
        var result = (ParameterVariableDeclaration)VariableDeclarationEvaluator.Evaluate(
            Declaration("input"), errors, _ => throw new AssertionException("Unexpected call"));
        Assert.That(result.GetParameter("IV"), Is.EqualTo("0x(Ab)"));
        Assert.That(VariableDictionary.Instance().Get("result"), Is.SameAs(result));
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void FunctionStandardPathResolvesParameters()
    {
        VariableDictionary.Instance().Add(new StringVariableDeclaration { Id = "input", Value = "0x(Ab)" });
        var errors = new List<SemanticError>();
        var result = (FunctionCall)FunctionCallEvaluator.EvaluateFunctionCall(Call("input"), errors);
        Assert.That(result.Arguments.Cast<ArgumentParameter>().Select(p => p.Value),
            Is.EqualTo(new[] { "0x(Ab)", "0x(Ab)" }));
        Assert.That(result.ReturnVariable!.Value, Is.EqualTo("Values are equal"));
        Assert.That(errors, Is.Empty);
    }

    [TestCase("missing")]
    [TestCase(null)]
    public void DeclarationStandardPathKeepsErrorBoundary(string? value)
    {
        var errors = new List<SemanticError>();
        var error = Assert.Throws<SemanticErrorException>(() => VariableDeclarationEvaluator.Evaluate(
            Declaration(value), errors, _ => throw new AssertionException("Unexpected call")));
        Assert.That(errors.Single(), Is.SameAs(error!.SemanticError));
        Assert.That(error.SemanticError!.Type, Is.EqualTo("Declaration"));
        Assert.That(error.SemanticError.Message, Is.EqualTo("Error in  parameter declaration : #IV:" + value));
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
    }

    [Test]
    public void ArgumentPropagatesFailureAndFunctionWrapsIt()
    {
        var errors = new List<SemanticError>();
        var direct = Assert.Throws<ArgumentException>(() => FunctionCallEvaluator.EvaluateArgument(
            new ParameterArgumentNode("#IV", "missing"), "Compare", errors));
        Assert.That(direct!.Message, Is.EqualTo("Unknown parameter value : missing"));
        Assert.That(errors, Is.Empty);
        var wrapped = Assert.Throws<SemanticErrorException>(() =>
            FunctionCallEvaluator.EvaluateFunctionCall(Call("missing"), errors));
        Assert.That(errors.Single(), Is.SameAs(wrapped!.SemanticError));
        Assert.That(wrapped.SemanticError!.Type, Is.EqualTo("FunctionCall"));
        Assert.That(wrapped.SemanticError.Message, Is.EqualTo(direct.Message));
    }

    [TestCase("0x(Ab)")]
    [TestCase("missing")]
    public void DeclareparamVisitorPreservesEvaluationAndErrors(string value)
    {
        var parser = ParserBuilder.StringBuild("#IV:" + value);
        var context = parser.declareparam();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        var visitor = new AntlrToStatement();
        if (value == "missing")
        {
            var error = Assert.Throws<ArgumentException>(() => visitor.VisitDeclareparam(context));
            Assert.That(error!.Message, Is.EqualTo("Unknown parameter value : missing"));
        }
        else
        {
            var result = (ArgumentParameter)visitor.VisitDeclareparam(context);
            Assert.That(result.Type, Is.EqualTo("#IV"));
            Assert.That(result.Value, Is.EqualTo(value));
        }
        Assert.That(visitor.SemanticErrors, Is.Empty);
    }
}
