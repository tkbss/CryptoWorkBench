using System.Reflection;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class ConsoleSemanticDiagnosticTests
{
    private SyntaxError[] previousSyntaxErrors = null!;
    private bool previousSyntaxFlag;
    private System.Text.StringBuilder previousSyntaxMessage = null!;

    [SetUp]
    public void SetUp()
    {
        previousSyntaxErrors = SyntaxErrorList.Instance().ToArray();
        previousSyntaxFlag = SyntaxErrorListner.SyntaxErrorOccured;
        previousSyntaxMessage = SyntaxErrorListner.ErrorMessage;
        SyntaxErrorListner.SyntaxErrorOccured = false;
        SyntaxErrorListner.ErrorMessage = new();
    }

    [TearDown]
    public void TearDown()
    {
        SyntaxErrorList.Instance().Clear();
        SyntaxErrorList.Instance().AddRange(previousSyntaxErrors);
        SyntaxErrorListner.SyntaxErrorOccured = previousSyntaxFlag;
        SyntaxErrorListner.ErrorMessage = previousSyntaxMessage;
    }

    private static readonly Type ProgramType = Assembly.Load("CryptoWorkBenchConsole")
        .GetType("CryptoWorkBenchConsole.Program")!;

    private static void Execute(CryptoScriptRunner runner, string script, TextWriter output)
    {
        var context = ParserBuilder.StringBuild(script).program();
        ProgramType.GetMethod("ExecuteProgram", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { runner, context, output });
    }

    [Test]
    public void SuccessWithoutDiagnosticsWritesNothing()
    {
        using var output = new StringWriter();
        Execute(new CryptoScriptRunner(), "Compare(0x(AB),0x(AB))", output);
        Assert.That(output.ToString(), Is.Empty);
    }

    [TestCase(1)]
    [TestCase(2)]
    public void WarningsUseFormatterAndDoNotBlock(int count)
    {
        var warnings = Enumerable.Range(1, count).Select(i => new SemanticError
        {
            Severity = SemanticDiagnosticSeverity.Warning, Message = "warning " + i
        }).ToArray();
        var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) =>
        {
            foreach (var warning in warnings) sink.Report(warning);
        });
        using var output = new StringWriter();
        Assert.DoesNotThrow(() => Execute(runner, "Compare(0x(AB),0x(AB))", output));
        Assert.That(output.ToString(), Is.EqualTo(SemanticDiagnosticFormatter.FormatMany(warnings) + output.NewLine));
        output.GetStringBuilder().Clear();
        Execute(new CryptoScriptRunner(), "Compare(0x(AB),0x(AB))", output);
        Assert.That(output.ToString(), Is.Empty);
    }

    [Test]
    public void FailurePreservesExceptionIdentityAndDoesNotDuplicateItsDiagnostic()
    {
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning, Message = "warning" };
        var error = new SemanticError { Message = "failure", Type = "FunctionContract",
            ErrorCode = FunctionContractError.UnsupportedFunction };
        var exception = new SemanticErrorException { SemanticError = error };
        var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) =>
        {
            sink.Report(warning);
            sink.Report(error);
            throw exception;
        });
        using var output = new StringWriter();
        var thrown = Assert.Throws<TargetInvocationException>(() => Execute(runner, "Compare(0x(AB),0x(AB))", output));
        Assert.That(thrown!.InnerException, Is.SameAs(exception));
        Assert.That(output.ToString(), Is.EqualTo(SemanticDiagnosticFormatter.FormatMany(new[] { warning, error }) + output.NewLine));
    }

    [Test]
    public void ForeignExceptionDiagnosticIsAppendedByIdentityEvenWithEqualText()
    {
        var transported = new SemanticError { Message = "same" };
        var foreign = new SemanticError { Message = "same", Location = new DiagnosticLocation(Guid.NewGuid()),
            Type = "FunctionContract", ErrorCode = FunctionContractError.UnsupportedFunction };
        var exception = new SemanticErrorException { SemanticError = foreign };
        var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) =>
        {
            sink.Report(transported);
            throw exception;
        });
        using var output = new StringWriter();
        var thrown = Assert.Throws<TargetInvocationException>(() => Execute(runner, "Compare(0x(AB),0x(AB))", output));
        Assert.That(thrown!.InnerException, Is.SameAs(exception));
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(output.ToString(), Is.EqualTo(SemanticDiagnosticFormatter.FormatMany(new[] { transported, foreign }) + output.NewLine));
    }

    [Test]
    public void MainRunsSupportedV1ExampleOnStdoutWithoutDiagnostics()
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        var previousVariables = CryptoScript.Variables.VariableDictionary.Instance().GetVariables().ToArray();
        using var output = new StringWriter();
        using var errors = new StringWriter();
        try
        {
            CryptoScript.Variables.VariableDictionary.Instance().Clear();
            Console.SetOut(output);
            Console.SetError(errors);
            Assert.DoesNotThrow(() => ProgramType.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { Array.Empty<string>() }));
            Assert.That(output.ToString(), Does.StartWith("VAR key1=0x(1234)" + output.NewLine));
            Assert.That(output.ToString(), Does.Contain("Print(key1)" + output.NewLine + "out: 0x(1234)" + output.NewLine));
            Assert.That(output.ToString(), Does.Contain("VAR res = Compare(key1,key1)" + output.NewLine));
            Assert.That(output.ToString(), Does.Contain("Print(res)" + output.NewLine + "out: Values are equal" + output.NewLine));
            Assert.That(output.ToString(), Does.Not.Contain("Error ["));
            Assert.That(errors.ToString(), Is.Empty);
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
            CryptoScript.Variables.VariableDictionary.Instance().Clear();
            foreach (var variable in previousVariables) CryptoScript.Variables.VariableDictionary.Instance().Add(variable);
        }
    }

    [Test]
    public void ExplicitPrintRemainsOnStdoutWithoutDiagnosticOutput()
    {
        var previousOut = Console.Out;
        using var output = new StringWriter();
        using var diagnostics = new StringWriter();
        try
        {
            Console.SetOut(output);
            Execute(new CryptoScriptRunner(), "Print(0x(AB))", diagnostics);
            Assert.That(output.ToString(), Is.EqualTo("out: 0x(AB)" + output.NewLine));
            Assert.That(diagnostics.ToString(), Is.Empty);
        }
        finally { Console.SetOut(previousOut); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ScriptFailureSurvivesDiagnosticWriterFailure(bool disposedWriter)
    {
        var error = new SemanticError { Message = "script failure", Type = "FunctionContract",
            ErrorCode = FunctionContractError.UnsupportedFunction };
        var scriptException = new SemanticErrorException { SemanticError = error };
        int executedOperations = 0;
        var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) =>
        {
            ++executedOperations;
            sink.Report(error);
            throw scriptException;
        });
        using var writer = new FailingWriter(disposedWriter
            ? new ObjectDisposedException("controlled writer") : new IOException("controlled writer failure"));

        var thrown = Assert.Throws<TargetInvocationException>(() => Execute(runner,
            "Compare(0x(AB),0x(AB)) Compare(0x(CD),0x(CD))", writer));
        Assert.Multiple(() =>
        {
            Assert.That(thrown!.InnerException, Is.SameAs(scriptException));
            Assert.That(scriptException.StackTrace, Does.Contain(nameof(ScriptFailureSurvivesDiagnosticWriterFailure)));
            Assert.That(scriptException.StackTrace, Does.Contain("CryptoScriptRunner.Execute"));
            Assert.That(executedOperations, Is.EqualTo(1));
            Assert.That(writer.WriteAttempts, Is.EqualTo(1));
        });
    }

    [Test]
    public void SuccessfulScriptDoesNotSuppressDiagnosticWriterFailure()
    {
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning, Message = "warning" };
        var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) => sink.Report(warning));
        var writerException = new IOException("controlled writer failure");
        using var writer = new FailingWriter(writerException);
        var thrown = Assert.Throws<TargetInvocationException>(() => Execute(runner, "Compare(0x(AB),0x(AB))", writer));
        Assert.That(thrown!.InnerException, Is.SameAs(writerException));
        Assert.That(writer.WriteAttempts, Is.EqualTo(1));
    }

    private sealed class FailingWriter(Exception exception) : TextWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        public int WriteAttempts { get; private set; }
        public override void WriteLine(string? value)
        {
            ++WriteAttempts;
            throw exception;
        }
    }
}
