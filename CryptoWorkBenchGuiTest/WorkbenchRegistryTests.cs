using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class WorkbenchRegistryTests
{
    private WorkbenchAutomationService service = null!;
    private ManualClock clock = null!;
    private VariableDeclaration[] previous = null!;
    private static AutomationCaller Caller => WorkbenchAutomationService.LocalCaller;
    private static TaskCompletionSource<T> Signal<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task<PasswordResponse> NoPassword(object _, PasswordRequestEventArgs r) => Task.FromResult(PasswordResponse.UserCancelled(r.RequestId));
    private Task<Reply<ExecutionResult>> Execute(TextExecution request, CancellationToken ct = default) => service.ExecuteTextAsync(Caller, request, ct);
    private async Task<TextExecution> Request(string text, TextExecutionMode mode = TextExecutionMode.Submission) =>
        new(service.InstanceId, Guid.NewGuid(), (await service.GetStatusAsync(Caller, default)).Data!.StateVersion, text, mode);
    private async Task<ExecutionResult> Run(string text) => (await Execute(await Request(text))).Data!;
    private async Task<ExecutionResult> Last()
    {
        var status = (await service.GetStatusAsync(Caller, default)).Data!;
        return (await service.GetExecutionAsync(Caller, status.LastExecutionId!.Value, default)).Data!;
    }

    [SetUp]
    public void Setup()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
        clock = new(); service = new(clock);
    }
    [TearDown]
    public void Teardown()
    {
        Assert.That(service.IsBusy, Is.False);
        VariableDictionary.Instance().Clear();
        foreach (var item in previous) VariableDictionary.Instance().Add(item);
    }

    [Test]
    public async Task SnapshotsDetachAllGuiObjectsAndRedactEveryValueCategory()
    {
        var original = new StringVariableDeclaration { Id = "value", Value = "PRIVATE_VAR" };
        IReadOnlyList<VariableModel>? local = null;
        service.Mutate(() => {
            VariableDictionary.Instance().Add(original);
            VariableDictionary.Instance().Add(new PathVariableDeclaration { Id = "path", Value = "PRIVATE_PATH" });
            VariableDictionary.Instance().Add(new KeyVariableDeclaration { Id = "secret", Value = "PRIVATE_KEY" });
            VariableDictionary.Instance().Add(new ParameterVariableDeclaration { Id = "parameter", Value = "PRIVATE_PARAMETER" });
        }, models => local = models);
        var before = (await service.GetVariablesAsync(Caller, default)).Data!;
        original.Value = "CHANGED"; local![0].Identifier = "CHANGED"; local[0].Value = "PRIVATE_GUI";
        Assert.That(before.Items[0].Identifier, Is.EqualTo("value"));
        var result = await Run("Print(0x(DEADBEEF)) VAR = PRIVATE_SYNTAX");
        string json = AutomationJson.Serialize(result) + AutomationJson.Serialize(before);
        foreach (var secret in new[] { "PRIVATE_VAR", "PRIVATE_PATH", "PRIVATE_KEY", "PRIVATE_PARAMETER", "PRIVATE_GUI", "PRIVATE_SYNTAX", "DEADBEEF", "CHANGED" })
            Assert.That(json, Does.Not.Contain(secret));
        Assert.That(before.Items.All(v => !v.ValueIncluded), Is.True);
    }

    [Test]
    public async Task VersionTracksMutationsRuntimeAndSyntaxBoundaries()
    {
        service.Mutate(() => VariableDictionary.Instance().Clear());
        Assert.That((await service.GetStatusAsync(Caller, default)).Data!.StateVersion, Is.EqualTo("1"));
        service.Refresh();
        Assert.That((await service.GetStatusAsync(Caller, default)).Data!.StateVersion, Is.EqualTo("1"));
        Assert.That((await Run("VAR =")).StateVersion, Is.EqualTo("1"));
        var failed = await Run("VAR before=0x(01) Print(missing)");
        Assert.That(failed.State, Is.EqualTo(ExecutionState.Aborted));
        Assert.That(failed.StateVersion, Is.EqualTo("2"));
        Assert.That(failed.Variables.StateVersion, Is.EqualTo("2"));
        service.Mutate(() => VariableDictionary.Instance().Clear());
        Assert.That((await service.GetVariablesAsync(Caller, default)).Data!.StateVersion, Is.EqualTo("3"));
    }

    [Test]
    public async Task BookHasOneServiceIdentityAndMultipleRunnerDiagnosticIdentities()
    {
        var request = await Request("KEY one=Check(0x(AA))\r\nout: ignored\r\nKEY two=Check(0x(BB))", TextExecutionMode.ScriptBook);
        var result = (await Execute(request)).Data!;
        Assert.That(result.ExecutionId, Is.EqualTo(request.ExecutionId));
        Assert.That(result.State, Is.EqualTo(ExecutionState.Completed));
        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Diagnostics.Select(d => d.RunnerExecutionId).Distinct().Count(), Is.EqualTo(2));
        Assert.That(result.StateVersion, Is.EqualTo("1"));
        Assert.That((await service.GetDiagnosticsAsync(Caller, request.ExecutionId, default)).Data!.Items, Is.EqualTo(result.Diagnostics));
    }

    [Test]
    public async Task IntermediateExecutionSnapshotUsesLastCompletedVariablesUntilWholeBookFinishes()
    {
        var baseline = await Run("VAR baseline=0x(01)");
        var entered = Signal<bool>(); var release = Signal<bool>();
        var book = service.RunAsync(pipeline => service.ExecuteScriptBookAsync(pipeline,
            new[] { "VAR partial=0x(02)", "VAR final=0x(03)" }, NoPassword, (_, _) => Task.CompletedTask,
            async (_, line, _) => { if (line == 1) { entered.SetResult(true); await release.Task; } return true; }, default), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var status = (await service.GetStatusAsync(Caller, default)).Data!;
            var running = (await service.GetExecutionAsync(Caller, status.ActiveExecutionId!.Value, default)).Data!;
            Assert.That(running.Variables.Stale, Is.True);
            Assert.That(running.Variables.Items, Is.EqualTo(baseline.Variables.Items));
            Assert.That((await service.GetVariablesAsync(Caller, default)).Data!.Items, Is.EqualTo(baseline.Variables.Items));
        }
        finally { release.TrySetResult(true); await book; }
        Assert.That((await Last()).Variables.Items.Select(v => v.Identifier), Does.Contain("final").And.Contain("partial"));
    }

    [Test]
    public async Task BookSyntaxDiagnosticsPreservePhysicalLineNumbers()
    {
        var result = (await Execute(await Request("out: ignored\r\nVAR first=0x(01)\r\nVAR =", TextExecutionMode.ScriptBook))).Data!;
        Assert.That(result.State, Is.EqualTo(ExecutionState.SyntaxError));
        Assert.That(result.StateVersion, Is.EqualTo("1"));
        Assert.That(result.Diagnostics.Where(d => d.Kind == DiagnosticKind.Syntax).All(d => d.Line == 3), Is.True);
    }

    [Test]
    public async Task SimultaneousIdenticalIdsExecuteAndPrintExactlyOnce()
    {
        var request = await Request("Print(0x(01))");
        int prints = 0;
        var entered = Signal<bool>(); using var release = new ManualResetEventSlim();
        void Print(string _) { Interlocked.Increment(ref prints); entered.TrySetResult(true); release.Wait(TimeSpan.FromSeconds(10)); }
        OutputOperations.PrintEvent += Print;
        Task<Reply<ExecutionResult>>? first = null;
        try
        {
            first = Task.Run(() => Execute(request));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var duplicate = await Execute(request);
            Assert.That(duplicate.Ok, Is.True);
            Assert.That(duplicate.Data!.State, Is.EqualTo(ExecutionState.Running));
            Assert.That((await Execute(request with { Text = "Print(0x(02))" })).Error!.Code, Is.EqualTo(AutomationErrorCode.InvalidArgument));
            release.Set();
            var completed = await first;
            Assert.That((await Execute(request)).Data, Is.EqualTo(completed.Data));
            Assert.That(prints, Is.EqualTo(1));
            Assert.That(completed.Data!.Outputs, Has.Length.EqualTo(1));
            Assert.That(completed.Data.Outputs[0].Text, Is.Null);
        }
        finally { release.Set(); if (first is not null) await first; OutputOperations.PrintEvent -= Print; }
    }

    [Test]
    public async Task CancelledRequestBeforeAdmissionCreatesNoJob()
    {
        var request = await Request("VAR blocked=0x(01)");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () => await Execute(request, cancellation.Token));
        Assert.That((await service.GetExecutionAsync(Caller, request.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
        Assert.That(VariableDictionary.Instance().Contains("blocked"), Is.False);
    }

    [Test]
    public async Task LostResponseWaitDoesNotCancelExecutionAndAllowsResultRecovery()
    {
        var baseline = await Run("VAR initial=0x(01)");
        var request = await Request("Print(initial) VAR after=0x(02)");
        var entered = Signal<bool>(); using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        void Print(string _) { entered.TrySetResult(true); release.Wait(TimeSpan.FromSeconds(10)); }
        OutputOperations.PrintEvent += Print;
        try
        {
            var wait = Execute(request, cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var snapshot = (await service.GetVariablesAsync(Caller, default)).Data!;
            Assert.That(snapshot.Stale, Is.True);
            Assert.That(snapshot.Items, Is.EqualTo(baseline.Variables.Items));
            cancellation.Cancel();
            Assert.ThrowsAsync<TaskCanceledException>(async () => await wait);
            var running = (await service.GetExecutionAsync(Caller, request.ExecutionId, default)).Data!;
            Assert.That(running.CancellationReason, Is.Null);
            Assert.That(service.IsBusy, Is.True);
            release.Set(); await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var result = (await service.GetExecutionAsync(Caller, request.ExecutionId, default)).Data!;
            Assert.That(result.State, Is.EqualTo(ExecutionState.Completed));
            Assert.That(result.DeadlineAt, Is.EqualTo(running.DeadlineAt));
            Assert.That(result.Variables.Items.Select(v => v.Identifier), Does.Contain("after"));
        }
        finally { release.Set(); await service.DrainAsync(); OutputOperations.PrintEvent -= Print; }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task CancellationAndDeadlineWaitForSynchronousStatementAndPreserveLateCompletion(bool timeout, bool lastStatement)
    {
        var request = (await Request(lastStatement ? "Print(0x(01))" : "Print(0x(01)) VAR blocked=0x(02)")) with { TimeoutMs = 1000 };
        var entered = Signal<bool>(); using var release = new ManualResetEventSlim();
        void Print(string _) { entered.TrySetResult(true); release.Wait(TimeSpan.FromSeconds(10)); }
        OutputOperations.PrintEvent += Print;
        Task<Reply<ExecutionResult>>? task = null;
        try
        {
            task = Execute(request); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (timeout) clock.Advance(TimeSpan.FromSeconds(1));
            else Assert.That((await service.CancelExecutionAsync(Caller, request.ExecutionId, default)).Data!.Accepted, Is.True);
            var running = (await service.GetExecutionAsync(Caller, request.ExecutionId, default)).Data!;
            Assert.That(running.FinishedAt, Is.Null);
            Assert.That(running.State, Is.EqualTo(ExecutionState.CancellationRequested));
            Assert.That(task.IsCompleted, Is.False);
            clock.Advance(TimeSpan.FromMinutes(20));
            Assert.That((await service.GetExecutionAsync(Caller, request.ExecutionId, default)).Ok, Is.True, "Active jobs never expire.");
            release.Set(); var result = (await task).Data!;
            Assert.That(result.State, Is.EqualTo(lastStatement ? ExecutionState.Completed : timeout ? ExecutionState.TimedOut : ExecutionState.Cancelled));
            Assert.That(result.CancellationReason, Is.EqualTo(timeout ? CancellationReason.Timeout : CancellationReason.Explicit));
            Assert.That((await service.CancelExecutionAsync(Caller, request.ExecutionId, default)).Data!.Accepted, Is.False);
        }
        finally { release.Set(); if (task is not null) await task; OutputOperations.PrintEvent -= Print; }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PasswordDeadlineAndExplicitCancellationRetainAdmissionThroughDrain(bool timeout)
    {
        var requested = Signal<PasswordRequestEventArgs>(); var response = Signal<PasswordResponse>(); var unwound = Signal<bool>();
        var execution = service.ExecuteSubmissionAsync("", (_, request) => { requested.TrySetResult(request); return response.Task; },
            testStatements: new[] { new AsyncInfrastructureStatement(async context =>
            {
                try { using var answer = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad); }
                finally { unwound.TrySetResult(true); }
            }) });
        var request = await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var id = (await service.GetStatusAsync(Caller, default)).Data!.ActiveExecutionId!.Value;
        PasswordSecret? secret = null;
        try
        {
            Assert.That((await service.GetExecutionAsync(Caller, id, default)).Data!.State, Is.EqualTo(ExecutionState.WaitingForUser));
            if (timeout) clock.Advance(TimeSpan.FromSeconds(30)); else await service.CancelExecutionAsync(Caller, id, default);
            await unwound.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(service.IsBusy, Is.True);
            Assert.That(execution.IsCompleted, Is.False);
            var busy = await Execute(await Request("VAR blocked=0x(01)"));
            Assert.That(busy.Error!.Code, Is.EqualTo(AutomationErrorCode.Busy));
            secret = PasswordSecret.TakeOwnership("PRIVATE_PASSWORD".ToCharArray());
            response.SetResult(PasswordResponse.Supplied(request.RequestId, secret));
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Throws<ObjectDisposedException>(() => _ = secret.Characters);
            var result = await Last();
            Assert.That(result.State, Is.EqualTo(timeout ? ExecutionState.TimedOut : ExecutionState.Cancelled));
            Assert.That(AutomationJson.Serialize(result), Does.Not.Contain("PRIVATE_PASSWORD"));
        }
        finally { response.TrySetResult(PasswordResponse.UserCancelled(request.RequestId)); await execution; }
    }

    [TestCase("VAR applied=0x(01) Print(applied)", true)]
    [TestCase("VAR applied=0x(01) Print(missing)", true)]
    [TestCase("VAR =", false)]
    public async Task CaptureFailureKeepsOutcomeAndRecoveryNeverReplaysScript(string script, bool effects)
    {
        await Run("VAR previous=0x(02)");
        var fault = new CaptureFault { Id = "broken" }; VariableDictionary.Instance().Add(fault);
        var local = await service.ExecuteSubmissionAsync(script, NoPassword);
        var failed = await Last();
        Assert.That(local.CaptureFailed, Is.True);
        Assert.That(failed.State, Is.EqualTo(ExecutionState.Failed));
        Assert.That(failed.Effects, Is.EqualTo(effects ? Effects.Possible : Effects.None));
        Assert.That(failed.Variables.Stale, Is.True);
        Assert.That(failed.Outputs.Length, Is.EqualTo(local.Output.Count));
        Assert.That((await service.GetStatusAsync(Caller, default)).Data!.Lifecycle, Is.EqualTo(Lifecycle.Faulted));
        string json = AutomationJson.Serialize(failed);
        Assert.That(json, Does.Not.Contain("PRIVATE_EXCEPTION"));
        Assert.That(failed.Diagnostics.Any(d => d.Code == "CaptureFailed"), Is.True);
        fault.Enabled = false;
        var recovered = service.Refresh();
        Assert.That(recovered.Select(v => v.Identifier).Contains("applied"), Is.EqualTo(effects));
        Assert.That((await service.GetVariablesAsync(Caller, default)).Data!.Stale, Is.False);
        Assert.That((await service.GetStatusAsync(Caller, default)).Data!.LastExecutionId, Is.EqualTo(failed.ExecutionId));
    }

    [Test]
    public async Task InvalidIdsArgumentsAndUnsafeExternalPathsAreRejectedWithoutJobs()
    {
        var request = await Request("VAR blocked=0x(01)");
        foreach (var invalid in new[] { request with { ExecutionId = Guid.Empty }, request with { TimeoutMs = 999 },
            request with { TimeoutMs = 120001 }, request with { ExpectedStateVersion = "NaN" }, request with { Mode = (TextExecutionMode)999 } })
            Assert.That((await Execute(invalid)).Error!.Code, Is.EqualTo(AutomationErrorCode.InvalidArgument));
        Assert.That((await Execute(request with { InstanceId = Guid.NewGuid() })).Error!.Code, Is.EqualTo(AutomationErrorCode.InstanceMismatch));
        Assert.That((await Execute(request with { ExpectedStateVersion = "99" })).Error!.Code, Is.EqualTo(AutomationErrorCode.StateConflict));
        Assert.That((await Execute(request with { OutputPolicy = OutputPolicy.IncludeText })).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        var epoch = Guid.NewGuid(); service.ActivateMcp(epoch);
        var external = Caller with { Source = CallerSource.Mcp, ActivationEpoch = epoch };
        Assert.That((await service.ExecuteTextAsync(external, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        Assert.That((await service.ExecuteEditorAsync(external, new(service.InstanceId, Guid.NewGuid(), Guid.NewGuid(), "0", "0"), default)).Error!.Code,
            Is.EqualTo(AutomationErrorCode.EditorUnavailable));
        Assert.That((await service.GetExecutionAsync(Caller, request.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
    }

    [Test]
    public async Task OwnershipPermissionAndActivationRevocationAreCheckedOnEveryRead()
    {
        var result = await Run("VAR value=0x(01)");
        Assert.That((await service.GetExecutionAsync(Caller with { PrincipalId = "other" }, result.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
        Assert.That((await service.CancelExecutionAsync(Caller with { PrincipalId = "other" }, result.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
        Assert.That((await service.GetExecutionAsync(Caller with { Permissions = ImmutableHashSet<string>.Empty }, result.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        var epoch = Guid.NewGuid(); service.ActivateMcp(epoch);
        var external = Caller with { Source = CallerSource.Mcp, ActivationEpoch = epoch };
        Assert.That((await service.GetVariablesAsync(external, default)).Ok, Is.True);
        Assert.That((await service.GetExecutionAsync(external, result.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
        service.DisableMcp();
        Assert.That((await service.GetVariablesAsync(external, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        service.ActivateMcp(Guid.NewGuid());
        Assert.That((await service.GetStatusAsync(external, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        Assert.Throws<ArgumentException>(() => service.ActivateMcp(epoch));
    }

    [Test]
    public async Task RetentionEvictsOldestAndExpiredIdsNeverReplay()
    {
        var first = await Request(""); await Execute(first);
        for (int i = 0; i < 64; ++i) await Run("");
        Assert.That((await service.GetExecutionAsync(Caller, first.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired));
        var last = await Last();
        clock.Advance(TimeSpan.FromMinutes(15));
        Assert.That((await service.GetExecutionAsync(Caller, last.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired));
        Assert.That((await Execute(first)).Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired));
        Assert.That((await service.GetStatusAsync(Caller, default)).Data!.StateVersion, Is.EqualTo("65"));
    }

    [Test]
    public async Task TombstoneCapacityRejectsNewIdsButAllowsExistingResultReads()
    {
        var result = await Run("");
        // Fill minimal tombstones directly: retention transition itself is covered above.
        var tombstones = (Dictionary<Guid, AutomationCaller>)typeof(WorkbenchAutomationService).GetField("tombstones", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        for (int i = 1; i < AutomationContracts.MaxExecutionIds; ++i) tombstones.Add(Guid.NewGuid(), Caller);
        Assert.That((await Execute(await Request(""))).Error!.Code, Is.EqualTo(AutomationErrorCode.LimitExceeded));
        Assert.That((await service.GetExecutionAsync(Caller, result.ExecutionId, default)).Ok, Is.True);
    }

    [Test]
    public async Task ProjectionLimitsDoNotTruncateLocalOutputOrMetadata()
    {
        for (int i = 0; i < 1005; ++i) VariableDictionary.Instance().Add(new StringVariableDeclaration { Id = "v" + i, Value = "PRIVATE_VALUE" });
        var local = await service.ExecuteSubmissionAsync(string.Concat(Enumerable.Repeat("Print(0x(01)) ", 1005)), NoPassword);
        var result = await Last();
        Assert.That(local.Output, Has.Count.EqualTo(1005));
        Assert.That(local.Variables, Has.Count.EqualTo(1005));
        Assert.That(result.Outputs, Has.Length.EqualTo(1000));
        Assert.That(result.Variables.Items, Has.Length.EqualTo(1000));
        Assert.That(result.Truncated && result.Variables.Truncated, Is.True);
        Assert.That(result.Outputs.All(o => o.Text is null && o.Redacted), Is.True);
        Assert.That(result.Outputs.Select(o => o.Sequence), Is.EqualTo(Enumerable.Range(1, 1000).Select(i => (long)i)));
    }

    [Test]
    public async Task DiagnosticLimitsKeepErrorFlagAndNeverExposeFreeText()
    {
        var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) =>
        {
            for (int i = 0; i < 1005; ++i) sink.Report(new SemanticError { Message = "PRIVATE_MESSAGE", Value = "PRIVATE_VALUE", FunctionCall = "PRIVATE_CALL" });
        });
        await service.ExecuteSubmissionAsync("Print(0x(01))", NoPassword, testRunner: runner);
        var result = await Last();
        Assert.That(result.Diagnostics, Has.Length.EqualTo(1000));
        Assert.That(result.HasErrors && result.Truncated, Is.True);
        Assert.That(AutomationJson.Serialize(result), Does.Not.Contain("PRIVATE_"));
    }

    [Test]
    public async Task FinalPresentationRetainsAdmissionAndShutdownDrainsOwnedTask()
    {
        var entered = Signal<bool>(); var release = Signal<bool>();
        var operation = service.RunAsync(async pipeline =>
        {
            await pipeline.ExecuteAsync("VAR done=0x(01)", NoPassword, default);
            entered.SetResult(true); await release.Task; return true;
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var id = (await service.GetStatusAsync(Caller, default)).Data!.ActiveExecutionId!.Value;
            service.BeginShutdown();
            var drain = service.DrainAsync();
            Assert.That(drain.IsCompleted, Is.False);
            Assert.That(service.IsBusy, Is.True);
            Assert.That((await service.GetExecutionAsync(Caller, id, default)).Data!.FinishedAt, Is.Null);
            Assert.That((await Execute(await Request(""))).Error!.Code, Is.EqualTo(AutomationErrorCode.ShuttingDown));
            release.SetResult(true); await drain;
            Assert.That((await service.GetExecutionAsync(Caller, id, default)).Data!.State, Is.EqualTo(ExecutionState.Completed));
        }
        finally { release.TrySetResult(true); await operation; }
    }

    [Test]
    public async Task ByteBudgetEvictsResultsBeforeCountLimitAndKeepsCurrentSnapshot()
    {
        service.Mutate(() => {
            for (int i = 0; i < 1000; ++i) VariableDictionary.Instance().Add(new StringVariableDeclaration
                { Id = new string('\u4E00', 250) + i, Value = "PRIVATE_VALUE" });
        });
        var ids = new List<Guid>();
        for (int i = 0; i < 8; ++i) ids.Add((await Run("")).ExecutionId);
        int bytes = Encoding.UTF8.GetByteCount(AutomationJson.Serialize((await service.GetVariablesAsync(Caller, default)).Data!));
        int expired = 0;
        foreach (var id in ids)
        {
            var reply = await service.GetExecutionAsync(Caller, id, default);
            if (reply.Ok) bytes += Encoding.UTF8.GetByteCount(AutomationJson.Serialize(reply.Data!));
            else { Assert.That(reply.Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired)); ++expired; }
        }
        Assert.That(expired, Is.GreaterThan(0));
        Assert.That(bytes, Is.LessThanOrEqualTo(AutomationContracts.MaxResultBytes));
        Assert.That((await service.GetVariablesAsync(Caller, default)).Data!.Items, Has.Length.EqualTo(1000));
    }

    [Test]
    public async Task FailedMutationPublishesPartialStateAndAdvancesVersion()
    {
        Assert.Throws<InvalidOperationException>(() => service.Mutate(() =>
        {
            VariableDictionary.Instance().Add(new StringVariableDeclaration { Id = "partial", Value = "PRIVATE" });
            throw new InvalidOperationException("PRIVATE");
        }));
        var snapshot = (await service.GetVariablesAsync(Caller, default)).Data!;
        Assert.That(snapshot.StateVersion, Is.EqualTo("1"));
        Assert.That(snapshot.Stale, Is.False);
        Assert.That(snapshot.Items.Select(v => v.Identifier), Does.Contain("partial"));
    }

    [Test]
    public async Task CancelRequestAlreadyCancelledDoesNotChangeRunningJobAndFirstReasonWins()
    {
        var entered = Signal<bool>(); var release = Signal<bool>();
        var operation = service.RunAsync(async _ => { entered.SetResult(true); await release.Task; return true; }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var id = (await service.GetStatusAsync(Caller, default)).Data!.ActiveExecutionId!.Value;
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => service.CancelExecutionAsync(Caller, id, cancelled.Token));
            Assert.That((await service.GetExecutionAsync(Caller, id, default)).Data!.CancellationReason, Is.Null);
            Assert.That((await service.CancelExecutionAsync(Caller, id, default)).Data!.Accepted, Is.True);
            clock.Advance(TimeSpan.FromSeconds(40)); service.BeginShutdown();
            Assert.That((await service.GetExecutionAsync(Caller, id, default)).Data!.CancellationReason, Is.EqualTo(CancellationReason.Explicit));
        }
        finally { release.TrySetResult(true); await operation; }
    }

    [Test]
    public async Task BusyRejectionLeavesIdReusableAndNoResultRecord()
    {
        var entered = Signal<bool>(); var release = Signal<bool>();
        var operation = service.RunAsync(async _ => { entered.SetResult(true); await release.Task; return true; }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var request = await Request("VAR once=0x(01)");
        try
        {
            Assert.That((await Execute(request)).Error!.Code, Is.EqualTo(AutomationErrorCode.Busy));
            Assert.That((await service.GetExecutionAsync(Caller, request.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
        }
        finally { release.TrySetResult(true); await operation; }
        Assert.That((await Execute(request)).Data!.State, Is.EqualTo(ExecutionState.Completed));
    }

    [Test]
    public async Task DiagnosticsStayImmutableAfterMutableSourceChanges()
    {
        var error = new SemanticError { Message = "PRIVATE", DiagnosticCode = SemanticDiagnosticCode.Unclassified };
        var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) => sink.Report(error));
        var local = await service.ExecuteSubmissionAsync("Print(0x(01))", NoPassword, testRunner: runner);
        var before = await Last(); var json = AutomationJson.Serialize(before);
        error.Message = "CHANGED"; error.Location = null;
        foreach (var diagnostic in local.Diagnostics) { diagnostic.Message = "CHANGED_LOCAL"; diagnostic.Location = null; }
        Assert.That(AutomationJson.Serialize(await Last()), Is.EqualTo(json));
    }

    [Test]
    public async Task HostFailureIsObservedAndResultUsesOnlyCatalogMessages()
    {
        Assert.ThrowsAsync<InvalidOperationException>(async () => await service.RunAsync<bool>(_ => throw new InvalidOperationException("PRIVATE_HOST"), default));
        await service.DrainAsync();
        var result = await Last();
        Assert.That(result.State, Is.EqualTo(ExecutionState.Failed));
        Assert.That(result.Error!.Code, Is.EqualTo(AutomationErrorCode.InternalError));
        Assert.That(AutomationJson.Serialize(result), Does.Not.Contain("PRIVATE_HOST"));
    }

    [Test]
    public async Task ClearUsesSharedVersionBoundaryAndServiceInstanceIdsDiffer()
    {
        var shared = WorkbenchAutomationService.Shared;
        var before = (await shared.GetStatusAsync(Caller, default)).Data!;
        await shared.RunAsync(_ => Task.FromResult(WorkbenchAutomationService.ClearAdmitted()), default);
        var after = (await shared.GetVariablesAsync(Caller, default)).Data!;
        Assert.That(long.Parse(after.StateVersion), Is.EqualTo(long.Parse(before.StateVersion) + 1));
        Assert.That(after.Stale, Is.False);
        Assert.That(after.Items, Is.Empty);
        Assert.That(shared.InstanceId, Is.Not.EqualTo(service.InstanceId));
    }

    private sealed class CaptureFault : VariableDeclaration
    {
        internal bool Enabled = true;
        public override string Value { get => Enabled ? throw new InvalidOperationException("PRIVATE_EXCEPTION") : ""; set { } }
    }

    [Test]
    public async Task RecoveryFailureBlocksScriptAndMutationUntilReliableMetadataCanBeCaptured()
    {
        var fault = new MetadataFault { Id = "broken" };
        VariableDictionary.Instance().Add(fault);
        var rejected = await Run("VAR forbidden=0x(01)");
        Assert.That(rejected.State, Is.EqualTo(ExecutionState.Failed));
        Assert.That(rejected.Effects, Is.EqualTo(Effects.None));
        Assert.That((await service.GetStatusAsync(Caller, default)).Data!.Lifecycle, Is.EqualTo(Lifecycle.Faulted));
        bool mutated = false;
        Assert.Throws<InvalidOperationException>(() => service.Mutate(() => mutated = true));
        Assert.That(mutated, Is.False);
        Assert.That(VariableDictionary.Instance().Contains("forbidden"), Is.False);
        fault.Enabled = false; service.Refresh();
        Assert.That((await service.GetStatusAsync(Caller, default)).Data!.Lifecycle, Is.EqualTo(Lifecycle.Ready));
        Assert.That(VariableDictionary.Instance().Contains("forbidden"), Is.False);
    }

    [Test]
    public async Task McpDisableCancelsOwnedWorkRevokesAccessAndDiscardsResultAfterDrain()
    {
        var activation = Guid.NewGuid(); service.ActivateMcp(activation);
        var external = Caller with { Source = CallerSource.Mcp, ActivationEpoch = activation };
        var entered = Signal<bool>(); var release = Signal<bool>();
        var operation = service.RunAsync(async _ => { entered.SetResult(true); await release.Task; return true; }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var id = (await service.GetStatusAsync(Caller, default)).Data!.ActiveExecutionId!.Value;
        // Simulate the future authenticated host binding without adding a production MCP execute bypass.
        var registry = (System.Collections.IDictionary)typeof(WorkbenchAutomationService).GetField("jobs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        var job = registry[id]!;
        job.GetType().GetField("Caller", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(job, external);
        try
        {
            Assert.That((await service.GetExecutionAsync(external, id, default)).Ok, Is.True);
            service.DisableMcp();
            Assert.That((await service.GetExecutionAsync(external, id, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
            Assert.That(service.IsBusy, Is.True);
            Assert.That(operation.IsCompleted, Is.False);
            var result = (ExecutionResult)job.GetType().GetField("Result", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(job)!;
            Assert.That(result.CancellationReason, Is.EqualTo(CancellationReason.McpDisabled));
        }
        finally { release.TrySetResult(true); await operation; }
        Assert.That(registry.Contains(id), Is.False);
    }

    [Test]
    public async Task CompletedRegistryNeverRetainsTypedGuiTaskResults()
    {
        await service.ExecuteSubmissionAsync("VAR secret=0x(DEADBEEF)", NoPassword);
        var id = (await Last()).ExecutionId;
        var registry = (System.Collections.IDictionary)typeof(WorkbenchAutomationService).GetField("jobs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        var job = registry[id]!;
        var task = (Task)job.GetType().GetProperty("Task", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(job)!;
        Assert.That(task, Is.Not.InstanceOf<Task<ExecutionPresentation>>());
        Assert.That(task.IsCompletedSuccessfully, Is.True);
        Assert.That(AutomationJson.Serialize(await Last()), Does.Not.Contain("DEADBEEF"));
    }

    [Test]
    public async Task AcceptedWorkerDoesNotInheritRequestScopedAmbientObjects()
    {
        var ambient = new AsyncLocal<object?> { Value = new object() };
        var seen = await service.RunAsync(_ => Task.FromResult(ambient.Value), default);
        Assert.That(seen, Is.Null);
        Assert.That(ambient.Value, Is.Not.Null);
    }

    private sealed class MetadataFault : VariableDeclaration
    {
        internal bool Enabled = true;
        public override CryptoType? Type { get => Enabled ? throw new InvalidOperationException("PRIVATE_METADATA") : null; set { } }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long ticks;
        private readonly List<ManualTimer> timers = new();
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state); timer.Change(dueTime, period);
            lock (timers) timers.Add(timer); return timer;
        }
        internal void Advance(TimeSpan duration)
        {
            Interlocked.Add(ref ticks, duration.Ticks);
            ManualTimer[] snapshot; lock (timers) snapshot = timers.ToArray();
            foreach (var timer in snapshot) timer.Fire();
        }
        private sealed class ManualTimer(ManualClock owner, TimerCallback callback, object? state) : ITimer
        {
            private long due = long.MaxValue;
            public bool Change(TimeSpan dueTime, TimeSpan period) { due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.GetTimestamp() + dueTime.Ticks; return true; }
            internal void Fire() { if (Interlocked.Read(ref due) <= owner.GetTimestamp()) { Interlocked.Exchange(ref due, long.MaxValue); callback(state); } }
            public void Dispose() => Interlocked.Exchange(ref due, long.MaxValue);
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
