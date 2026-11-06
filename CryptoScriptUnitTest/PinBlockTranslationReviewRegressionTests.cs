using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.PINBLOCK;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class PinBlockTranslationReviewRegressionTests
{
    private const string Call = "VAR result=PinBlockTranslate(source,sk,block,target,tk)";
    private const string SetupScript = """
        PARAM source=#MECH:WRAP-DES3-PINBLOCK-3 #PAN:0x(1234567890123456) #FILL:0x(AAAAAAAAA)
        PARAM target=#MECH:WRAP-AES-PINBLOCK-4 #PAN:0x(1234567890123456)
        KEY sk=0x(0123456789ABCDEFFEDCBA9876543210)
        KEY tk=0x(000102030405060708090A0B0C0D0E0F)
        VAR block=0x(6694CAF82B1C2994)
        """;

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.SyntaxErrorOccured = false;
        Run(new CryptoScriptRunner(), SetupScript);
    }

    private static VariableDeclaration Get(string id) => VariableDictionary.Instance().Get(id)!;
    private static void Run(CryptoScriptRunner runner, string script) => runner.Execute(ParserBuilder.StringBuild(script).program());
    private static string State(VariableDeclaration value) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType());
    private static Action Snapshot()
    {
        var originals = new[] { "source", "target", "sk", "tk" }.Select(Get).ToArray();
        var states = originals.Select(State).ToArray();
        var fields = originals.OfType<ParameterVariableDeclaration>().Select(p => p.GetParameters().ToArray()).ToArray();
        return () =>
        {
            for (int i = 0; i < originals.Length; i++)
            {
                Assert.That(Get(originals[i].Id), Is.SameAs(originals[i]));
                Assert.That(State(originals[i]), Is.EqualTo(states[i]));
            }
            Assert.That(((ParameterVariableDeclaration)Get("source")).GetParameters(), Is.EquivalentTo(fields[0]));
            Assert.That(((ParameterVariableDeclaration)Get("target")).GetParameters(), Is.EquivalentTo(fields[1]));
            Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
        };
    }

    [TestCase("VAR")]
    [TestCase("KEY")]
    [TestCase("PARAM")]
    [TestCase("PATH")]
    [TestCase("TR31H")]
    public void ResultDeclarationTypeIsCheckedBeforeTypeResolutionAndDispatch(string type)
    {
        var unchanged = Snapshot();
        int calls = 0;
        var runner = new CryptoScriptRunner((_, _) => calls++);
        var parser = ParserBuilder.StringBuild(Call.Replace("VAR result", type + " result"));
        var program = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        if (type == "VAR")
        {
            Assert.DoesNotThrow(() => runner.Execute(program));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(runner.Diagnostics, Is.Empty);
            Assert.That(Get("result").Type, Is.TypeOf<CryptoTypeVar>());
            return;
        }
        var exception = Assert.Throws<SemanticErrorException>(() => runner.Execute(program));
        unchanged();
        Assert.That(calls, Is.Zero, "No dispatch means no decrypt, encrypt or RNG.");
        var error = runner.Diagnostics.Single();
        Assert.That(error.Type, Is.EqualTo("Declaration"));
        Assert.That(error, Is.SameAs(exception!.SemanticError));
        Assert.That(error.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Error));
        Assert.That(error.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.InvalidDeclaration));
        Assert.That(error.Identifier, Is.EqualTo(type));
        Assert.That(error.Message, Is.EqualTo($"Declaration type mismatch. Expected type: VAR; actual type: {type}."));
        Assert.That(error.Location?.ExecutionId, Is.Not.Null);
        Assert.That(error.Location?.StatementIndex, Is.EqualTo(1));
        Assert.That(error.Location?.OperationId, Is.Null);
        Assert.That(error.Location?.ParentOperationId, Is.Null);
    }

    [Test]
    public void ValidResultStillPublishesBothPreparedStates()
    {
        Run(new CryptoScriptRunner(), Call);
        Assert.That(Get("result").Type, Is.TypeOf<CryptoTypeVar>());
        Assert.That(((ParameterVariableDeclaration)Get("source")).GetParameter("FILL"), Is.EqualTo("0x(ABCDEFABC)").IgnoreCase);
        Assert.That(((ParameterVariableDeclaration)Get("target")).GetParameter("RANDOM"), Is.Not.Empty);
    }

    [TestCase("source", false, false)] [TestCase("target", false, false)]
    [TestCase("source", true, false)] [TestCase("target", true, false)]
    [TestCase("source", false, true)] [TestCase("target", false, true)]
    [TestCase("source", true, true)] [TestCase("target", true, true)]
    public void ContractErrorsRetainCategoryAndExecutionContext(string side, bool missing, bool nested)
    {
        var parameter = (ParameterVariableDeclaration)Get(side);
        if (missing) parameter.GetParameters().Remove("#PAN");
        else parameter.SetParameter("IV", "0x(0000000000000000)");
        var unchanged = Snapshot();
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => Run(runner, nested ? "Print(" + Call[11..] + ")" : Call));
        unchanged();
        var error = runner.Diagnostics.Single();
        Assert.That(error.Type, Is.EqualTo("FunctionContract"));
        Assert.That(error.ErrorCode, Is.EqualTo(missing ? FunctionContractError.MissingRequiredParameter : FunctionContractError.ForbiddenAdditionalParameter));
        Assert.That(error.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.FunctionContractViolation));
        Assert.That(error.FunctionName, Is.EqualTo("PinBlockTranslate"));
        Assert.That(error.Location?.FunctionName, Is.EqualTo("PinBlockTranslate"));
        Assert.That(error.Location?.StatementIndex, Is.EqualTo(1));
        Assert.That(error.Location?.OperationId, Is.Not.Null);
        Assert.That(error.Location?.ParentOperationId, nested ? Is.Not.Null : Is.Null);
        Assert.That(error.Location?.Mechanism, Is.EqualTo(parameter.Mechanism));
        Assert.That(error.Message, Does.Contain(side + " PARAM").And.Contain(parameter.Mechanism).And.Contain(missing ? "#PAN" : "#IV"));
        Assert.That(error.Message, Does.Not.Contain("1234567890123456").And.Not.Contain("0123456789ABCDEF").And.Not.Contain("function Wrap").And.Not.Contain("function Unwrap"));
    }

    [Test]
    public void TranslationContractContextRetainsOriginalCause()
    {
        var cause = new FunctionContractException(FunctionContractError.ForbiddenAdditionalParameter,
            "WRAP-AES-PINBLOCK-4", CryptoScriptFunction.Wrap, "#IV");
        var contextual = new FunctionContractException(cause, "target");
        Assert.That(contextual.InnerException, Is.SameAs(cause));
        Assert.That(contextual.Error, Is.EqualTo(cause.Error));
        Assert.That(contextual.Mechanism, Is.EqualTo(cause.Mechanism));
        Assert.That(contextual.ParameterName, Is.EqualTo(cause.ParameterName));
        Assert.That(contextual.Function, Is.EqualTo(CryptoScriptFunction.PinBlockTranslate));
    }

    [TestCase("Wrap")] [TestCase("Unwrap")]
    public void OriginalContractDiagnosticsKeepTheirFunction(string function)
    {
        ((ParameterVariableDeclaration)Get("source")).SetParameter("IV", "0x(0000000000000000)");
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => Run(runner, $"VAR result={function}(source,sk,block)"));
        var error = runner.Diagnostics.Single();
        Assert.That(error.FunctionName, Is.EqualTo(function));
        Assert.That(error.Message, Does.Contain("function " + function).And.Not.Contain("PARAM:"));
    }

    [TestCase("target-encryption")] [TestCase("after-source-decode")] [TestCase("before-publish")]
    public void InjectedFailuresNeverPublishAnyOriginalState(string phase)
    {
        var unchanged = Snapshot();
        int failures = 0;
        var runner = new CryptoScriptRunner(null, mechanism => mechanism == "WRAP-AES-PINBLOCK-4"
            ? new PinBlockAlgorithm(mechanism, PinBlockCipherFamily.Aes)
            {
                TranslationCheckpoint = checkpoint =>
                {
                    if (checkpoint == phase) { failures++; throw new CryptographicException("Injected translation failure."); }
                },
                TranslationEncrypt = phase == "target-encryption" ? (_, _) =>
                { failures++; throw new CryptographicException("Injected translation failure."); } : null
            } : AlgorithmFactory.Create(mechanism));
        Assert.Throws<SemanticErrorException>(() => Run(runner, Call));
        Assert.That(failures, Is.EqualTo(1));
        unchanged();
        var error = runner.Diagnostics.Single();
        Assert.That(error.FunctionName, Is.EqualTo("PinBlockTranslate"));
        Assert.That(error.Message, Is.EqualTo("Injected translation failure."));
        Assert.That(error.FunctionCall, Is.EqualTo("PinBlockTranslate(<redacted>)"));
    }
}
