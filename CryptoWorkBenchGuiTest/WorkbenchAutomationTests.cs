using Avalonia.Headless;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class WorkbenchAutomationTests
{
    private readonly WorkbenchAutomationService service = WorkbenchAutomationService.Shared;
    private VariableDeclaration[] previous = null!;
    private static Task<PasswordResponse> NoPassword(object sender, PasswordRequestEventArgs request) =>
        Task.FromResult(PasswordResponse.UserCancelled(request.RequestId));
    private static TaskCompletionSource<T> Signal<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [SetUp]
    public void SetUp()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Assert.That(service.IsBusy, Is.False);
        VariableDictionary.Instance().Clear();
        foreach (var item in previous) VariableDictionary.Instance().Add(item);
    }

    [Test]
    public async Task SubmissionRunsMultipleStatementsAndFreshRunnersShareState()
    {
        var first = await service.ExecuteSubmissionAsync("VAR one=0x(01) VAR two=Compare(one,one)", NoPassword);
        var second = await service.ExecuteSubmissionAsync("VAR three=Compare(two,two)", NoPassword);
        Assert.That(first.Succeeded && second.Succeeded, Is.True);
        Assert.That(first.RunnerId, Is.Not.EqualTo(second.RunnerId));
        Assert.That(second.Variables.Select(v => v.Identifier), Is.EquivalentTo(new[] { "one", "two", "three" }));
    }

    [Test]
    public async Task SyntaxFailurePreventsWholeSubmissionAndNextParseResetsAllErrors()
    {
        var bad = await service.ExecuteSubmissionAsync("VAR blocked=0x(01) VAR =", NoPassword);
        Assert.That(bad.Succeeded, Is.False);
        Assert.That(bad.RunnerId, Is.Null);
        Assert.That(bad.Diagnostics, Is.Empty);
        Assert.That(VariableDictionary.Instance().Contains("blocked"), Is.False);
        Assert.That(SyntaxErrorList.Instance(), Is.Not.Empty);
        var good = await service.ExecuteSubmissionAsync("VAR good=0x(02)", NoPassword);
        Assert.That(good.Succeeded, Is.True);
        Assert.That(SyntaxErrorList.Instance(), Is.Empty);
        Assert.That(bad.SyntaxDiagnostics, Is.Not.Empty);
    }

    [Test]
    public async Task ContinuingErrorCompletesAndLaterThrownErrorPublishesPartialState()
    {
        var continued = await service.ExecuteSubmissionAsync("KEY missing=Check(0x(AA)) VAR continued=0x(01)", NoPassword);
        Assert.That(continued.Succeeded, Is.True);
        Assert.That(continued.Diagnostics.Any(d => d.Severity == SemanticDiagnosticSeverity.Error), Is.True);
        var failure = await service.ExecuteSubmissionAsync("VAR before=0x(02) Print(missing) VAR after=0x(03)", NoPassword);
        Assert.That(failure.Succeeded, Is.False);
        Assert.That(failure.Variables.Select(v => v.Identifier), Does.Contain("before").And.Contain("continued").And.Not.Contain("after"));
    }

    [TestCase("out: ignored", true)]
    [TestCase(" out: ignored", false)]
    [TestCase("OUT: ignored", false)]
    public async Task BookUsesPhysicalLinesAndExactOutputPrefix(string outputLine, bool success)
    {
        var appended = new List<string>();
        var results = new List<ExecutionPresentation>();
        string[] lines = ["VAR first=0x(01)", "", outputLine, "VAR last=0x(01) Print(first)"];
        bool completed = await service.RunAsync(p => service.ExecuteScriptBookAsync(p, lines, NoPassword,
            (_, _) => Task.CompletedTask, (line, _, result) =>
            {
                appended.Add(line);
                if (result is not null) results.Add(result);
                return Task.FromResult(true);
            }, CancellationToken.None), CancellationToken.None);
        Assert.That(completed, Is.EqualTo(success));
        Assert.That(appended, Is.EqualTo(success ? lines : lines.Take(3)));
        Assert.That(results.Select(r => r.RunnerId).Where(id => id is not null).Distinct().Count(), Is.EqualTo(success ? 3 : 2));
        Assert.That(VariableDictionary.Instance().Contains("first"), Is.True);
        Assert.That(VariableDictionary.Instance().Contains("last"), Is.EqualTo(success));
    }

    [Test]
    public async Task BookKeepsAdmissionBetweenRunnersAndClearCannotInterleave()
    {
        var between = Signal<bool>(); var release = Signal<bool>();
        var book = service.RunAsync(p => service.ExecuteScriptBookAsync(p,
            new[] { "VAR first=0x(01)", "VAR last=0x(01) Print(first)" }, NoPassword,
            (_, _) => Task.CompletedTask, async (_, number, _) =>
            {
                if (number == 1) { between.SetResult(true); await release.Task; }
                return true;
            }, CancellationToken.None), CancellationToken.None);
        await between.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await Assert.ThatAsync(() => service.ExecuteSubmissionAsync("VAR intrusion=0x(02)", NoPassword).WaitAsync(TimeSpan.FromSeconds(2)), Throws.TypeOf<ExecutionBusyException>());
            Assert.Throws<ExecutionBusyException>(() => service.Mutate(() => VariableDictionary.Instance().Clear()));
            Assert.Throws<ExecutionBusyException>(() => service.Mutate(() => VariableDictionary.Instance().Get("first").Value = "0x(FF)"));
            Assert.That(book.IsCompleted, Is.False);
        }
        finally { release.TrySetResult(true); await book; }
        Assert.That(VariableDictionary.Instance().Get("last").Value, Is.EqualTo("0x(01)"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CancelledPasswordKeepsAdmissionUntilLateResponseOrFaultIsDrained(bool fault)
    {
        using var cancellation = new CancellationTokenSource();
        var requested = Signal<PasswordRequestEventArgs>();
        var response = Signal<PasswordResponse>();
        var unwinding = Signal<bool>();
        var execution = service.ExecuteSubmissionAsync("", (_, request) =>
        { requested.SetResult(request); return response.Task; }, cancellation.Token,
            testStatements: new[] { new AsyncInfrastructureStatement(async context =>
            {
                try { using var answer = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad); }
                finally { unwinding.TrySetResult(true); }
            }) });
        var request = await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        PasswordSecret? secret = null;
        try
        {
            cancellation.Cancel();
            await unwinding.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(service.IsBusy, Is.True);
            Assert.That(execution.IsCompleted, Is.False);
            await Assert.ThatAsync(() => service.ExecuteSubmissionAsync("VAR intrusion=0x(01)", NoPassword).WaitAsync(TimeSpan.FromSeconds(2)), Throws.TypeOf<ExecutionBusyException>());
            Assert.Throws<ExecutionBusyException>(() => service.Mutate(() => VariableDictionary.Instance().Clear()));
        }
        finally
        {
            if (fault) response.TrySetException(new InvalidOperationException("late host failure"));
            else
            {
                secret = PasswordSecret.TakeOwnership("late-secret".ToCharArray());
                response.TrySetResult(PasswordResponse.Supplied(request.RequestId, secret));
            }
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
        }
        if (secret is not null) Assert.Throws<ObjectDisposedException>(() => _ = secret.Characters);
        Assert.That((await service.ExecuteSubmissionAsync("VAR recovered=0x(02)", NoPassword)).Succeeded, Is.True);
    }

    [Test]
    public async Task PasswordCallbackAndInheritedChildCannotAccessDictionaryOrReenterHost()
    {
        var execution = await service.ExecuteSubmissionAsync("", async (_, request) =>
        {
            Assert.Throws<ExecutionBusyException>(() => VariableDictionary.Instance().Clear());
            Assert.Throws<ExecutionBusyException>(() => ParserBuilder.StringBuild("VAR invalid=0x(01)"));
            await Task.Run(() => Assert.Throws<ExecutionBusyException>(() => VariableDictionary.Instance().GetVariables()));
            await Assert.ThatAsync(() => service.ExecuteSubmissionAsync("", NoPassword), Throws.TypeOf<ExecutionBusyException>());
            return PasswordResponse.UserCancelled(request.RequestId);
        }, testStatements: new[] { new AsyncInfrastructureStatement(async context =>
        { using var answer = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad); }) });
        Assert.That(execution.Succeeded, Is.False);
        Assert.That(execution.Status, Does.Contain("cancelled by user"));
    }

    [Test]
    public async Task OutputOriginOrderAndDiagnosticCopiesSurviveRunnerDisposal()
    {
        var warning = new SemanticError { Message = "original", Severity = SemanticDiagnosticSeverity.Warning };
        var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) => sink.Report(warning));
        var result = await service.ExecuteSubmissionAsync("Print(0x(01)) Info(keymap) Print(0x(02))", NoPassword, testRunner: runner);
        Assert.That(result.Output.Select(o => o.Kind), Is.EqualTo(new[] { "print", "info", "print" }));
        Assert.That(result.Output[0].Text, Is.EqualTo("out: 0x(01)"));
        Assert.That(result.Output[2].Text, Is.EqualTo("out: 0x(02)"));
        warning.Message = "changed";
        Assert.That(result.Diagnostics, Is.Not.Empty);
        Assert.That(result.Diagnostics.All(d => d.Message == "original"), Is.True);
        Assert.That(result.Diagnostics.All(d => !ReferenceEquals(d, warning)), Is.True);
    }

    [Test]
    public async Task CompetingGuiRequestsAndDetachedVariableEditAreImmediatelyBusy()
    {
        var session = GuiTestSession.Session;
        await session.Dispatch(async () =>
        {
            using var first = new CryptoScriptEditViewModel(new(), new(), new(), new(), null!, new HistoryService());
            var status = new StatusViewModel();
            using var second = new CryptoScriptEditViewModel(status, new(), new(), new(), null!, new HistoryService());
            var requested = Signal<PasswordRequestEventArgs>(); var response = Signal<PasswordResponse>();
            var execution = first.ExecuteLineAsync("", passwordHandler: (_, request) =>
            { requested.SetResult(request); return response.Task; },
                testStatements: new[] { new AsyncInfrastructureStatement(async context =>
                { using var answer = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad); }) });
            var request = await requested.Task;
            try
            {
                await second.ParseLineAsync("VAR blocked=0x(01)");
                Assert.That(status.StatusString, Does.Contain("busy"));
                await Assert.ThatAsync(() => second.OpenScriptBookAsync("must-not-be-read", true), Throws.TypeOf<ExecutionBusyException>());
                new VariableViewModel().ModifyValueCommand.Execute(null!);
                Assert.That(execution.IsCompleted, Is.False);
            }
            finally { response.TrySetResult(PasswordResponse.UserCancelled(request.RequestId)); await execution; }
            Assert.That(VariableDictionary.Instance().Contains("blocked"), Is.False);
            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task LexerOnlyFailureStopsSubmissionAndResetsForNextUnit()
    {
        var bad = await service.ExecuteSubmissionAsync("VAR blocked=0x(01) @", NoPassword);
        Assert.That(bad.Succeeded, Is.False);
        Assert.That(bad.SyntaxDiagnostics, Is.Not.Empty);
        Assert.That(VariableDictionary.Instance().Contains("blocked"), Is.False);
        var good = await service.ExecuteSubmissionAsync("VAR good=0x(02)", NoPassword);
        Assert.That(good.Succeeded, Is.True);
        Assert.That(good.SyntaxDiagnostics, Is.Empty);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OutputCallbacksCannotMutateParseOrReenterHost(bool info)
    {
        int calls = 0;
        Action<string> callback = _ =>
        {
            ++calls;
            Assert.Throws<ExecutionBusyException>(() => VariableDictionary.Instance().Clear());
            Assert.Throws<ExecutionBusyException>(() => ParserBuilder.StringBuild(""));
            Assert.Throws<ExecutionBusyException>(() => InterpreterExecution.Enter());
            Assert.Throws<ExecutionBusyException>(() => InterpreterExecution.AccessState(() => true));
        };
        if (info) OutputOperations.InfoEvent += callback;
        else OutputOperations.PrintEvent += callback;
        try
        {
            var result = await service.ExecuteSubmissionAsync(info ? "Info(keymap)" : "Print(0x(01))", NoPassword);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(calls, Is.EqualTo(1));
        }
        finally
        {
            if (info) OutputOperations.InfoEvent -= callback;
            else OutputOperations.PrintEvent -= callback;
        }
    }
}
