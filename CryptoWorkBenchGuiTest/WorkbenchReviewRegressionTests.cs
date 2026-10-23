using Avalonia.Headless;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;
using System.Collections.Specialized;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class WorkbenchReviewRegressionTests
{
    private readonly WorkbenchAutomationService service = WorkbenchAutomationService.Shared;
    private VariableDeclaration[] previous = null!;
    private VariableDictionary Dictionary => VariableDictionary.Instance();
    private static Task<PasswordResponse> NoPassword(object sender, PasswordRequestEventArgs request) =>
        Task.FromResult(PasswordResponse.UserCancelled(request.RequestId));

    [SetUp]
    public void SetUp()
    {
        previous = Dictionary.GetVariables().ToArray();
        Dictionary.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Assert.That(service.IsBusy, Is.False);
        Dictionary.Clear();
        foreach (var variable in previous) Dictionary.Add(variable);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RealSelectedMutationIsBusyUntilOuterMutationOrSetupPresentationCompletes(bool setup)
    {
        Dictionary.Add(new StringVariableDeclaration { Id = "expected", Value = "0x(01)" });
        var main = new VariableViewModel(); var other = new VariableViewModel();
        main.SetupVariables(); other.SetupVariables();
        main.SelectedVariable = main.DataVariables.Single();
        other.SelectedVariable = other.DataVariables.Single();
        main.SelectedVariable.Value = "0x(02)";
        other.SelectedVariable.Value = "0x(FF)";
        bool attempted = false;
        NotifyCollectionChangedEventHandler callback = (_, _) =>
        {
            if (attempted) return;
            attempted = true;
            Assert.That(service.IsBusy, Is.True);
            // This is the actual selected-entry command, not the null shortcut.
            other.ModifyValueCommand.Execute(other.SelectedVariable);
            Assert.Throws<ExecutionBusyException>(() => other.SetupVariables());
            Assert.Throws<ExecutionBusyException>(() => service.Mutate(() =>
                Dictionary.Get("expected").Value = "0x(EE)"));
        };
        main.DataVariables.CollectionChanged += callback;
        try
        {
            if (setup) main.SetupVariables();
            else main.ModifyValueCommand.Execute(main.SelectedVariable);
        }
        finally { main.DataVariables.CollectionChanged -= callback; }
        string expected = setup ? "0x(01)" : "0x(02)";
        Assert.That(attempted, Is.True);
        Assert.That(Dictionary.Get("expected").Value, Is.EqualTo(expected));
        Assert.That(main.DataVariables.Single().Value, Is.EqualTo(expected));
        Assert.That(main.IsSnapshotStale, Is.False);
        Assert.That(service.IsBusy, Is.False);
        // The same command is accepted after the outer presentation ends.
        other.ModifyValueCommand.Execute(other.SelectedVariable);
        Assert.That(Dictionary.Get("expected").Value, Is.EqualTo("0x(FF)"));
        Assert.That(other.DataVariables.Single().Value, Is.EqualTo("0x(FF)"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ThrowingMutationOrSetupPresentationPreservesExceptionAndReleasesAdmission(bool setup)
    {
        Dictionary.Add(new StringVariableDeclaration { Id = "expected", Value = "0x(01)" });
        var variables = new VariableViewModel(); variables.SetupVariables();
        variables.SelectedVariable = variables.DataVariables.Single();
        variables.SelectedVariable.Value = "0x(02)";
        var expected = new InvalidOperationException("controlled presentation failure");
        NotifyCollectionChangedEventHandler callback = (_, _) =>
        {
            Assert.That(service.IsBusy, Is.True);
            throw expected;
        };
        variables.DataVariables.CollectionChanged += callback;
        try
        {
            var observed = Assert.Throws<InvalidOperationException>(() =>
            {
                if (setup) variables.SetupVariables();
                else variables.ModifyValueCommand.Execute(variables.SelectedVariable);
            });
            Assert.That(observed, Is.SameAs(expected));
        }
        finally { variables.DataVariables.CollectionChanged -= callback; }
        Assert.That(service.IsBusy, Is.False);
        Assert.That(variables.IsSnapshotStale, Is.True);
        Assert.That(Dictionary.Get("expected").Value, Is.EqualTo(setup ? "0x(01)" : "0x(02)"));
        variables.SetupVariables();
        variables.SelectedVariable = variables.DataVariables.Single();
        variables.SelectedVariable.Value = "0x(03)";
        variables.ModifyValueCommand.Execute(variables.SelectedVariable);
        Assert.That(Dictionary.Get("expected").Value, Is.EqualTo("0x(03)"));
        Assert.That(variables.IsSnapshotStale, Is.False);
    }

    private sealed record Scenario(string Script, bool Succeeded, bool Effects, int Diagnostics,
        int Outputs, string Status, bool Syntax = false);

    private static Scenario Case(string scenario) => scenario switch
    {
        "success" => new("VAR applied=0x(02) Print(applied) Info(keymap)", true, true, 0, 2, "Line successfull parsed"),
        "continuing" => new("VAR applied=0x(02) KEY absent=Check(0x(AA)) Print(applied)", true, true, 1, 1, "Line successfull parsed"),
        "runtime" => new("VAR applied=0x(02) Print(applied) Print(missing) VAR after=0x(03)", false, true, 2, 1, "Script execution aborted (semantic error)."),
        "syntax" => new("VAR applied=0x(02) VAR =", false, false, 0, 0, "SYNTAX ERROR", true),
        "lexer" => new("VAR applied=0x(02) @", false, false, 0, 0, "SYNTAX ERROR", true),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario))
    };

    private sealed class CaptureFault : VariableDeclaration
    {
        internal bool Enabled = true;
        internal int Reads;
        public override string Value
        {
            get
            {
                ++Reads;
                if (Enabled) throw new InvalidOperationException("test-capture-detail");
                return "0x(00)";
            }
            set { }
        }
    }

    [TestCase("success")]
    [TestCase("continuing")]
    [TestCase("runtime")]
    [TestCase("syntax")]
    [TestCase("lexer")]
    public async Task CaptureFailurePreservesScriptOutcomeDiagnosticsOutputsAndEffects(string scenario)
    {
        var test = Case(scenario);
        var fault = new CaptureFault { Id = "captureFault" }; Dictionary.Add(fault);
        var result = await service.ExecuteSubmissionAsync(test.Script, NoPassword);
        Assert.That(result.Succeeded, Is.EqualTo(test.Succeeded), "The script outcome must survive capture failure.");
        Assert.That(result.HostSucceeded, Is.False);
        Assert.That(result.CaptureFailed, Is.True);
        Assert.That(result.EffectsPossible, Is.EqualTo(test.Effects));
        Assert.That(result.Status, Does.Contain(test.Status));
        Assert.That(result.Diagnostics, Has.Count.EqualTo(test.Diagnostics));
        Assert.That(result.Output, Has.Count.EqualTo(test.Outputs));
        if (test.Outputs != 0) Assert.That(result.Output[0], Is.EqualTo(new ExecutionOutput("print", "out: 0x(02)")));
        if (test.Outputs == 2) Assert.That(result.Output[1].Kind, Is.EqualTo("info"));
        Assert.That(result.SyntaxDiagnostics.Any(), Is.EqualTo(test.Syntax));
        Assert.That(result.Variables, Is.Empty); // Explicit failure; never a successful empty snapshot.
        Assert.That(Dictionary.Contains("applied"), Is.EqualTo(test.Effects));
        Assert.That(Dictionary.Contains("after"), Is.False);
        Assert.That(fault.Reads, Is.EqualTo(1), "No hidden second capture or script retry.");
        Assert.That(service.IsBusy, Is.False);
        if (scenario == "runtime") Assert.That(result.Error, Is.SameAs(result.Diagnostics.Last()));
        var syntaxCopy = result.SyntaxDiagnostics.ToArray();
        fault.Enabled = false;
        var recovered = await service.ExecuteSubmissionAsync("VAR recovered=0x(04)", NoPassword);
        Assert.That(recovered.HostSucceeded, Is.True);
        Assert.That(recovered.CaptureFailed, Is.False);
        Assert.That(recovered.Variables.Select(v => v.Identifier), Does.Contain("recovered"));
        Assert.That(result.SyntaxDiagnostics, Is.EqualTo(syntaxCopy));
    }

    [TestCase("success")]
    [TestCase("continuing")]
    [TestCase("runtime")]
    [TestCase("syntax")]
    [TestCase("lexer")]
    public async Task GuiKeepsOriginalStatusAndDiagnosticsAndMarksUnchangedVariablesStale(string scenario)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(AsyncPasswordGuiTests.TestApp));
        await session.Dispatch(async () =>
        {
            var test = Case(scenario);
            var status = new StatusViewModel(); var variables = new VariableViewModel();
            using var editor = new CryptoScriptEditViewModel(status, variables, new(), new(), null!, new HistoryService());
            await editor.ParseLineAsync("VAR previous=0x(01)");
            var oldVariable = variables.DataVariables.Single();
            int updates = 0;
            variables.DataVariables.CollectionChanged += (_, _) => ++updates;
            status.StatusString = "PREVIOUS-STATUS";
            var fault = new CaptureFault { Id = "captureFault" }; Dictionary.Add(fault);
            Assert.That(await editor.ExecuteLineAsync(test.Script), Is.False);
            Assert.That(status.StatusString, Does.Contain(test.Status).And.Contain("Variable state capture failed").And.Contain("stale"));
            Assert.That(status.StatusString, Does.Not.Contain("PREVIOUS-STATUS").And.Not.Contain("test-capture-detail"));
            Assert.That(status.Diagnostics, Has.Count.EqualTo(test.Diagnostics));
            Assert.That(variables.DataVariables.Single(), Is.SameAs(oldVariable));
            Assert.That(updates, Is.Zero);
            Assert.That(variables.IsSnapshotStale, Is.True);
            Assert.That(fault.Reads, Is.EqualTo(1));
            Assert.That(Dictionary.Contains("applied"), Is.EqualTo(test.Effects));
            if (test.Outputs != 0) Assert.That(editor.PrintMessage, Is.EqualTo("out: 0x(02)"));
            Assert.That(service.IsBusy, Is.False);
            fault.Enabled = false;
            await editor.ParseLineAsync("VAR recovered=0x(04)");
            Assert.That(status.StatusString, Is.EqualTo("Line successfull parsed"));
            Assert.That(variables.IsSnapshotStale, Is.False);
            Assert.That(variables.DataVariables.Select(v => v.Identifier), Does.Contain("recovered"));
            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task CaptureFailurePreservesEverySemanticFieldAndExceptionDiagnosticIdentity()
    {
        var warning = new SemanticError
        {
            Severity = SemanticDiagnosticSeverity.Warning, DiagnosticCode = SemanticDiagnosticCode.Unclassified,
            Message = "warning", Type = "type", Identifier = "id", FunctionName = "Compare",
            FunctionCall = "call", Value = "value", ErrorCode = FunctionContractError.UnsupportedFunction,
            Location = new DiagnosticLocation(Guid.NewGuid(), 7, 3, 2, "Compare", "mechanism", "id")
        };
        var error = new SemanticError { Message = "original runtime failure", Type = "FunctionContract", ErrorCode = FunctionContractError.UnsupportedFunction };
        var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) =>
        {
            sink.Report(warning); sink.Report(error);
            throw new SemanticErrorException { SemanticError = error };
        });
        Dictionary.Add(new CaptureFault { Id = "captureFault" });
        var result = await service.ExecuteSubmissionAsync("Compare(0x(01),0x(01))", NoPassword, testRunner: runner);
        Assert.That(result.CaptureFailed, Is.True);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Diagnostics, Has.Count.EqualTo(2));
        Assert.That(result.Error, Is.SameAs(result.Diagnostics[1]));
        var copy = result.Diagnostics[0];
        Assert.Multiple(() =>
        {
            Assert.That(copy, Is.Not.SameAs(warning));
            Assert.That(copy.Severity, Is.EqualTo(warning.Severity));
            Assert.That(copy.DiagnosticCode, Is.EqualTo(warning.DiagnosticCode));
            Assert.That(copy.Message, Is.EqualTo(warning.Message));
            Assert.That(copy.Type, Is.EqualTo(warning.Type));
            Assert.That(copy.Identifier, Is.EqualTo(warning.Identifier));
            Assert.That(copy.FunctionName, Is.EqualTo(warning.FunctionName));
            Assert.That(copy.FunctionCall, Is.EqualTo(warning.FunctionCall));
            Assert.That(copy.Value, Is.EqualTo(warning.Value));
            Assert.That(copy.ErrorCode, Is.EqualTo(warning.ErrorCode));
            Assert.That(copy.Location, Is.EqualTo(warning.Location).And.Not.SameAs(warning.Location));
        });
        warning.Message = "changed"; error.Message = "changed";
        Assert.That(copy.Message, Is.EqualTo("warning"));
        Assert.That(result.Error!.Message, Is.EqualTo("original runtime failure"));
    }

    [Test]
    public async Task BookDoesNotClaimCompleteSuccessOrRunNextLineAfterCaptureFailure()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(AsyncPasswordGuiTests.TestApp));
        await session.Dispatch(async () =>
        {
            var status = new StatusViewModel(); var variables = new VariableViewModel();
            using var editor = new CryptoScriptEditViewModel(status, variables, new(), new(), null!, new HistoryService());
            Dictionary.Add(new CaptureFault { Id = "captureFault" });
            var appended = new List<string>();
            await editor.ExecuteScriptBookAsync(new[] { "VAR applied=0x(02)", "VAR after=0x(03)" }, appended.Add);
            Assert.That(editor.LastScriptBookSucceeded, Is.False);
            Assert.That(appended, Is.EqualTo(new[] { "VAR applied=0x(02)" }));
            Assert.That(Dictionary.Contains("applied"), Is.True);
            Assert.That(Dictionary.Contains("after"), Is.False);
            Assert.That(status.StatusString, Does.StartWith("Line successfull parsed").And.Contain("capture failed"));
            Assert.That(variables.IsSnapshotStale, Is.True);
            return true;
        }, CancellationToken.None);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EarlierThrowingOutputSubscriberStillAbortsAndDoesNotHideRuntimeFailure(bool info)
    {
        var fault = new CaptureFault { Id = "captureFault" }; Dictionary.Add(fault);
        int calls = 0;
        Action<string> handler = _ => { ++calls; throw new InvalidOperationException("controlled event failure"); };
        if (info) OutputOperations.InfoEvent += handler;
        else OutputOperations.PrintEvent += handler;
        ExecutionPresentation result;
        try
        {
            result = await service.ExecuteSubmissionAsync("VAR before=0x(01) " +
                (info ? "Info(keymap)" : "Print(before)") + " VAR after=0x(02)", NoPassword);
        }
        finally
        {
            if (info) OutputOperations.InfoEvent -= handler;
            else OutputOperations.PrintEvent -= handler;
        }
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.HostSucceeded, Is.False);
        Assert.That(result.Status, Is.EqualTo("Script execution aborted (semantic error)."));
        Assert.That(result.CaptureFailed, Is.True);
        Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(result.Output, Is.Empty); // Existing multicast delivery stops at the earlier subscriber.
        Assert.That(Dictionary.Contains("before"), Is.True);
        Assert.That(Dictionary.Contains("after"), Is.False);
        Assert.That(service.IsBusy, Is.False);
        fault.Enabled = false;
        var recovered = await service.ExecuteSubmissionAsync("Print(0x(03))", NoPassword);
        Assert.That(recovered.HostSucceeded, Is.True);
        Assert.That(recovered.Output, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task KeyMapReportsCaptureFailureWithoutLosingItsScriptStatus()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(AsyncPasswordGuiTests.TestApp));
        await session.Dispatch(async () =>
        {
            Dictionary.Add(new CaptureFault { Id = "captureFault" });
            var status = new StatusViewModel();
            using var keyMap = new KeyMapViewModel(status);
            await keyMap.Initialization;
            Assert.That(status.StatusString, Does.StartWith("Line successfull parsed").And.Contain("capture failed"));
            Assert.That(service.IsBusy, Is.False);
            return true;
        }, CancellationToken.None);
    }
}
