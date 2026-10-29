using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class WorkbenchRegistryCorrectionTests
{
    private WorkbenchAutomationService service = null!;
    private Clock clock = null!;
    private VariableDeclaration[] previous = null!;
    private static AutomationCaller Caller => WorkbenchAutomationService.LocalCaller;
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task<PasswordResponse> NoPassword(object _, PasswordRequestEventArgs r) => Task.FromResult(PasswordResponse.UserCancelled(r.RequestId));
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    [SetUp] public void Setup()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
        clock = new(); service = new(clock);
    }
    [TearDown] public async Task Teardown()
    {
        Assert.That(service.IsBusy, Is.False);
        await service.DisposeAsync();
        Assert.That(clock.Timers.All(t => t.Disposed), Is.True);
        VariableDictionary.Instance().Clear();
        foreach (var item in previous) VariableDictionary.Instance().Add(item);
    }
    private async Task<TextExecution> Request(string text = "VAR once=0x(01)") => new(service.InstanceId, Guid.NewGuid(),
        (await service.GetStatusAsync(Caller, default)).Data!.StateVersion, text, TextExecutionMode.Submission);
    private Task<Reply<ExecutionResult>> Execute(TextExecution request, CancellationToken ct = default) => service.ExecuteTextAsync(Caller, request, ct);
    private async Task<Guid> Active() => (await service.GetStatusAsync(Caller, default)).Data!.ActiveExecutionId!.Value;
    private async Task<ExecutionResult> Result(Guid id) => (await service.GetExecutionAsync(Caller, id, default)).Data!;
    private object Job(Guid id) => ((IDictionary)typeof(WorkbenchAutomationService).GetField("jobs", Fields)!.GetValue(service)!)[id]!;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference JobReference(Guid id) => new(Job(id));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<(WeakReference Payload, Guid Id)> RunPayload()
    {
        var payload = new byte[16 * 1024 * 1024];
        var weak = new WeakReference(payload);
        await service.RunAsync(_ => { GC.KeepAlive(payload); return Task.FromResult(true); }, default);
        return (weak, (await service.GetStatusAsync(Caller, default)).Data!.LastExecutionId!.Value);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<(WeakReference Request, Guid Id, string Version)> RunRequest()
    {
        var request = await Request();
        var weak = new WeakReference(request);
        await Execute(request);
        return (weak, request.ExecutionId, request.ExpectedStateVersion);
    }
    private static void Collect()
    {
        for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    }
    private void CheckTimerGraph()
    {
        foreach (var timer in clock.Timers.Where(t => !t.Disposed))
        {
            // Static C# lambdas can have a stateless compiler singleton Target.
            Assert.That(timer.Callback!.Target is null || timer.Callback.Target.GetType().GetFields(Fields).Length == 0, Is.True);
            var fields = timer.State!.GetType().GetFields(Fields);
            Assert.That(fields.Select(f => f.FieldType), Is.EquivalentTo(new[] { typeof(WorkbenchAutomationService), typeof(Guid), typeof(Guid) }));
            Assert.That(fields.Any(f => typeof(Delegate).IsAssignableFrom(f.FieldType)), Is.False);
        }
    }
    [Test] public async Task RetentionKeepsSafeResultButNotSixteenMiBGuiPayload()
    {
        var (weak, id) = await RunPayload();
        await Task.Yield(); Collect();
        Assert.That(weak.IsAlive, Is.False);
        Assert.That((await service.GetExecutionAsync(Caller, id, default)).Ok, Is.True);
        CheckTimerGraph();
        var job = JobReference(id);
        clock.Advance(TimeSpan.FromMinutes(15)); Collect();
        Assert.That(job.IsAlive, Is.False);
        Assert.That((await service.GetExecutionAsync(Caller, id, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired));
    }
    [Test] public async Task RealTextRequestIsCollectedDuringRetentionAndExpiredIdNeverReplays()
    {
        var (weak, id, version) = await RunRequest();
        await Task.Yield(); Collect();
        Assert.That(weak.IsAlive, Is.False);
        CheckTimerGraph();
        var retry = new TextExecution(service.InstanceId, id, version, "VAR once=0x(01)", TextExecutionMode.Submission);
        var retained = await Execute(retry);
        Assert.That(retained.Data!.StateVersion, Is.EqualTo("1"));
        clock.Advance(TimeSpan.FromMinutes(15));
        Assert.That((await Execute(retry)).Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired));
        Assert.That((await service.GetStatusAsync(Caller, default)).Data!.StateVersion, Is.EqualTo("1"));
    }
    [Test] public async Task DisposeExpiresRetainedResultsAndDisposesTimers()
    {
        var request = await Request(); await Execute(request);
        await service.DisposeAsync(); await service.DisposeAsync();
        Assert.That(clock.Timers.All(t => t.Disposed), Is.True);
        Assert.That((await Execute(request)).Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired));
    }

    [TestCase("timer", false)] [TestCase("registration", false)] [TestCase("worker", false)]
    [TestCase("timer", true)] [TestCase("registration", true)] [TestCase("worker", true)]
    public async Task FailedAcceptanceRollsBackAllOwnership(string stage, bool cancelRequest)
    {
        var request = await Request(); using var cts = new CancellationTokenSource();
        void Fail() { if (cancelRequest) cts.Cancel(); throw new InvalidOperationException("injected"); }
        clock.OnCreate = () => { if (stage == "timer") Fail(); };
        service.AdmissionCheckpoint = point => { if (point == stage) Fail(); };
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Execute(request, cts.Token));
        Assert.That(service.IsBusy, Is.False);
        Assert.That((await service.GetStatusAsync(Caller, default)).Data!.ActiveExecutionId, Is.Null);
        Assert.That((await service.GetExecutionAsync(Caller, request.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(clock.Timers.All(t => t.Disposed), Is.True);
        service.AdmissionCheckpoint = null; clock.OnCreate = null;
        Assert.That((await Execute(request)).Data!.State, Is.EqualTo(ExecutionState.Completed));
        Assert.That((await Execute(request)).Data!.StateVersion, Is.EqualTo("1"));
    }
    [TestCase("registration")] [TestCase("worker")]
    public async Task GuiAcceptanceFailureReleasesUserRegistration(string stage)
    {
        using var user = new CancellationTokenSource();
        service.AdmissionCheckpoint = point => { if (point == stage) throw new InvalidOperationException(); };
        Assert.Throws<InvalidOperationException>(() => service.RunAsync(_ => Task.FromResult(true), user.Token));
        await user.CancelAsync();
        Assert.That(service.IsBusy, Is.False);
        await service.DrainAsync();
        service.AdmissionCheckpoint = null;
        Assert.That(await service.RunAsync(_ => Task.FromResult(true), default), Is.True);
    }
    [Test] public async Task RequestCancellationAtHandoffPreventsAcceptance()
    {
        var request = await Request(); using var cts = new CancellationTokenSource();
        service.AdmissionCheckpoint = point => { if (point == "worker") cts.Cancel(); };
        Assert.ThrowsAsync<OperationCanceledException>(async () => await Execute(request, cts.Token));
        Assert.That(service.IsBusy, Is.False);
        Assert.That((await service.GetExecutionAsync(Caller, request.ExecutionId, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
        await service.DrainAsync(); service.AdmissionCheckpoint = null;
        Assert.That((await Execute(request)).Ok, Is.True);
    }
    [Test] public async Task CallbackFromRolledBackAttemptCannotCancelReusedId()
    {
        var request = await Request(); TimerCallback? stale = null; object? state = null;
        service.AdmissionCheckpoint = point => {
            if (point != "worker") return;
            stale = clock.Timers[0].Callback; state = clock.Timers[0].State;
            throw new InvalidOperationException();
        };
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Execute(request));
        service.AdmissionCheckpoint = null; clock.BlockDisposal = Signal();
        var retry = Execute(request);
        try
        {
            await clock.DisposalEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stale!(state);
            Assert.That((await Result(request.ExecutionId)).CancellationReason, Is.Null);
            clock.BlockDisposal.SetResult();
            Assert.That((await retry).Data!.State, Is.EqualTo(ExecutionState.Completed));
        }
        finally { clock.BlockDisposal.TrySetResult(); await retry; }
    }
    [Test] public async Task ExecutionCancelAtHandoffRemainsOwnedAndDoesNotExecute()
    {
        var request = await Request();
        service.AdmissionCheckpoint = point => { if (point == "worker") _ = service.CancelExecutionAsync(Caller, request.ExecutionId, default); };
        var result = (await Execute(request)).Data!;
        Assert.That(result.State, Is.EqualTo(ExecutionState.Cancelled));
        Assert.That(result.Effects, Is.EqualTo(Effects.None));
        Assert.That(result.StateVersion, Is.EqualTo("0"));
        Assert.That(VariableDictionary.Instance().Contains("once"), Is.False);
    }
    [Test] public async Task IdenticalRequestsRaceAcceptanceCompletionAndLookupWithoutReplay()
    {
        var request = await Request("Print(0x(01))");
        using var start = new Barrier(3); using var printed = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        int count = 0;
        void Print(string _) { Interlocked.Increment(ref count); printed.Set(); release.Wait(TimeSpan.FromSeconds(10)); }
        OutputOperations.PrintEvent += Print;
        Task<Reply<ExecutionResult>>? first = null, second = null;
        try
        {
            first = Task.Run(async () => { start.SignalAndWait(); return await Execute(request); });
            second = Task.Run(async () => { start.SignalAndWait(); return await Execute(request); });
            start.SignalAndWait(); Assert.That(printed.Wait(TimeSpan.FromSeconds(5)), Is.True);
            var lookup = Task.Run(async () => { for (int i = 0; i < 100; i++) Assert.That((await service.GetExecutionAsync(Caller, request.ExecutionId, default)).Ok, Is.True); });
            release.Set(); await Task.WhenAll(first, second, lookup);
            Assert.That(first.Result.Ok && second.Result.Ok, Is.True);
            Assert.That((await Execute(request)).Data!.State, Is.EqualTo(ExecutionState.Completed));
            Assert.That(count, Is.EqualTo(1));
        }
        finally { release.Set(); if (first is not null) await first; if (second is not null) await second; OutputOperations.PrintEvent -= Print; }
    }

    [TestCase("explicit", false)] [TestCase("timeout", false)] [TestCase("shutdown", false)]
    [TestCase("explicit", true)]
    public async Task TerminalPublicationWaitsForCallbacks(string trigger, bool throwCallback)
    {
        var registered = Signal(); var entered = Signal(); var releaseWork = Signal(); using var releaseCallback = new ManualResetEventSlim();
        var work = service.RunAsync(async p => {
            p.ExecutionToken.Register(() => { entered.TrySetResult(); releaseCallback.Wait(TimeSpan.FromSeconds(10)); if (throwCallback) throw new InvalidOperationException("PRIVATE_CALLBACK"); });
            registered.SetResult(); await releaseWork.Task; p.ExecutionToken.ThrowIfCancellationRequested(); return true;
        }, default);
        await registered.Task; var id = await Active();
        try
        {
            if (trigger == "timeout") clock.Advance(TimeSpan.FromSeconds(30));
            else if (trigger == "shutdown") service.BeginShutdown();
            else await service.CancelExecutionAsync(Caller, id, default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); releaseWork.SetResult();
            await clock.DisposalEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var wait = new CancellationTokenSource(); var drain = service.DrainAsync(wait.Token); wait.Cancel();
            Assert.ThrowsAsync<TaskCanceledException>(async () => await drain);
            var reason = (await Result(id)).CancellationReason;
            clock.Advance(TimeSpan.FromHours(1)); service.BeginShutdown();
            Assert.That((await service.CancelExecutionAsync(Caller, id, default)).Data!.Accepted, Is.False);
            var pending = await Result(id);
            Assert.That(pending.State, Is.EqualTo(ExecutionState.CancellationRequested));
            Assert.That(pending.FinishedAt, Is.Null);
            Assert.That(pending.CancellationReason, Is.EqualTo(reason));
            Assert.That((await service.GetDiagnosticsAsync(Caller, id, default)).Data!.IsFinal, Is.False);
            Assert.That(service.IsBusy, Is.True);
            Assert.That(service.DrainAsync().IsCompleted || work.IsCompleted, Is.False);
            releaseCallback.Set(); await work.WaitAsync(TimeSpan.FromSeconds(5));
            var final = await Result(id);
            Assert.That(final.State, Is.EqualTo(throwCallback ? ExecutionState.Failed : trigger == "timeout" ? ExecutionState.TimedOut : ExecutionState.Cancelled));
            Assert.That(final.FinishedAt, Is.EqualTo(clock.GetUtcNow()));
            Assert.That((await service.GetDiagnosticsAsync(Caller, id, default)).Data!.IsFinal, Is.True);
            Assert.That(AutomationJson.Serialize(final), Does.Not.Contain("PRIVATE_CALLBACK"));
            clock.Advance(TimeSpan.FromMinutes(14)); Assert.That((await service.GetExecutionAsync(Caller, id, default)).Ok, Is.True);
            clock.Advance(TimeSpan.FromMinutes(1)); Assert.That((await service.GetExecutionAsync(Caller, id, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired));
        }
        finally { releaseWork.TrySetResult(); releaseCallback.Set(); await work; }
    }
    [TestCase(false)] [TestCase(true)]
    public async Task DeadlineDuringResourceCleanupIsObservedAndCleanupFailureTerminates(bool throwCleanup)
    {
        clock.BlockDisposal = Signal(); clock.ThrowDisposal = throwCleanup;
        var request = await Request(); var work = Execute(request);
        try
        {
            await clock.DisposalEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That((await Result(request.ExecutionId)).FinishedAt, Is.Null);
            clock.Advance(TimeSpan.FromSeconds(30));
            Assert.That((await Result(request.ExecutionId)).CancellationReason, Is.EqualTo(CancellationReason.Timeout));
            Assert.That(service.IsBusy, Is.True);
            clock.BlockDisposal.SetResult(); var result = (await work).Data!;
            Assert.That(result.State, Is.EqualTo(throwCleanup ? ExecutionState.Failed : ExecutionState.Completed));
            Assert.That(result.Effects, Is.EqualTo(Effects.Possible));
            Assert.That(result.StateVersion, Is.EqualTo("1"));
            Assert.That(AutomationJson.Serialize(result), Does.Not.Contain("PRIVATE_CLEANUP"));
        }
        finally { clock.BlockDisposal.TrySetResult(); await work; }
    }

    [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
    public async Task CancelBeforeRunnerEntryHasNoEffects(bool deadline, bool afterCreation)
    {
        var entered = Signal(); var release = Signal();
        var work = service.RunAsync(async p => {
            if (afterCreation) p.BeforeRunnerStart = () => { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); };
            else { entered.SetResult(); await release.Task; }
            return await p.ExecuteAsync("VAR forbidden=0x(01)", NoPassword, default);
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); var id = await Active();
        try
        {
            if (deadline) clock.Advance(TimeSpan.FromSeconds(30)); else await service.CancelExecutionAsync(Caller, id, default);
            release.SetResult(); await work;
            var result = await Result(id);
            Assert.That(result.State, Is.EqualTo(deadline ? ExecutionState.TimedOut : ExecutionState.Cancelled));
            Assert.That(result.Effects, Is.EqualTo(Effects.None));
            Assert.That(result.StateVersion, Is.EqualTo("0"));
            Assert.That(VariableDictionary.Instance().Contains("forbidden"), Is.False);
        }
        finally { release.TrySetResult(); await work; }
    }
    [TestCase(false)] [TestCase(true)]
    public async Task BookCancellationCountsOnlyActuallyEnteredRunners(bool afterFirst)
    {
        var entered = Signal(); var release = Signal();
        var work = service.RunAsync(p => service.ExecuteScriptBookAsync(p,
            new[] { "VAR first=0x(01)", "VAR second=0x(02)" }, NoPassword,
            async (_, line) => { if (line == (afterFirst ? 2 : 1)) { entered.SetResult(); await release.Task; } },
            (_, _, _) => Task.FromResult(true), default), default);
        await entered.Task; var id = await Active();
        try
        {
            await service.CancelExecutionAsync(Caller, id, default); release.SetResult(); await work;
            var result = await Result(id);
            Assert.That(result.State, Is.EqualTo(ExecutionState.Cancelled));
            Assert.That(result.Effects, Is.EqualTo(afterFirst ? Effects.Possible : Effects.None));
            Assert.That(result.StateVersion, Is.EqualTo(afterFirst ? "1" : "0"));
            Assert.That(VariableDictionary.Instance().Contains("first"), Is.EqualTo(afterFirst));
            Assert.That(VariableDictionary.Instance().Contains("second"), Is.False);
        }
        finally { release.TrySetResult(); await work; }
    }
    [Test] public async Task RetentionExpiryConcurrentWithLookupReturnsOnlySafeResultOrTombstone()
    {
        var request = await Request(); await Execute(request);
        using var barrier = new Barrier(2);
        var expire = Task.Run(() => { barrier.SignalAndWait(); clock.Advance(TimeSpan.FromMinutes(15)); });
        barrier.SignalAndWait();
        for (int i = 0; i < 100; i++)
        {
            var reply = await service.GetExecutionAsync(Caller, request.ExecutionId, default);
            Assert.That(reply.Ok ? reply.Data!.State == ExecutionState.Completed : reply.Error!.Code == AutomationErrorCode.ResultExpired, Is.True);
        }
        await expire;
        Assert.That((await Execute(request)).Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired));
    }
    private sealed class CaptureFault : VariableDeclaration
    {
        public override string Value { get => throw new InvalidOperationException("PRIVATE_CAPTURE"); set { } }
    }
    [Test] public async Task ShutdownAfterLostResponseWaitDrainsCleanupWithoutReleasingAdmission()
    {
        clock.BlockDisposal = Signal();
        using var caller = new CancellationTokenSource();
        var request = await Request(); var response = Execute(request, caller.Token);
        try
        {
            await clock.DisposalEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            caller.Cancel();
            Assert.ThrowsAsync<TaskCanceledException>(async () => await response);
            service.BeginShutdown();
            var drain = service.DrainAsync();
            Assert.That(drain.IsCompleted, Is.False);
            Assert.That(service.IsBusy, Is.True);
            Assert.That((await Result(request.ExecutionId)).FinishedAt, Is.Null);
            clock.BlockDisposal.SetResult(); await drain.WaitAsync(TimeSpan.FromSeconds(5));
            var result = await Result(request.ExecutionId);
            Assert.That(result.State, Is.EqualTo(ExecutionState.Completed));
            Assert.That(result.CancellationReason, Is.EqualTo(CancellationReason.Shutdown));
            Assert.That((await Execute(request)).Data, Is.EqualTo(result));
        }
        finally { clock.BlockDisposal.TrySetResult(); await service.DrainAsync(); }
    }
    [Test] public async Task PartialEffectCaptureFailureStillWaitsForResourceCleanup()
    {
        service.Refresh(); VariableDictionary.Instance().Add(new CaptureFault { Id = "broken" });
        clock.BlockDisposal = Signal();
        var request = await Request(); var work = Execute(request);
        try
        {
            await clock.DisposalEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var pending = await Result(request.ExecutionId);
            Assert.That(pending.FinishedAt, Is.Null);
            clock.BlockDisposal.SetResult(); var result = (await work).Data!;
            Assert.That(VariableDictionary.Instance().Contains("once"), Is.True);
            Assert.That(result.State, Is.EqualTo(ExecutionState.Failed));
            Assert.That(result.Effects, Is.EqualTo(Effects.Possible));
            Assert.That(result.Variables.Stale, Is.True);
            Assert.That(result.StateVersion, Is.EqualTo("1"));
        }
        finally { clock.BlockDisposal.TrySetResult(); await work; }
    }

    private sealed class Clock : TimeProvider
    {
        private long ticks;
        internal readonly List<Timer> Timers = new();
        internal Action? OnCreate;
        internal TaskCompletionSource DisposalEntered = Signal();
        internal TaskCompletionSource? BlockDisposal;
        internal bool ThrowDisposal;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            OnCreate?.Invoke(); var timer = new Timer(this, callback, state, GetTimestamp() + dueTime.Ticks);
            lock (Timers) Timers.Add(timer); return timer;
        }
        internal void Advance(TimeSpan duration)
        {
            Interlocked.Add(ref ticks, duration.Ticks);
            Timer[] copy; lock (Timers) copy = Timers.ToArray();
            foreach (var timer in copy) timer.Fire();
        }
        internal sealed class Timer(Clock owner, TimerCallback callback, object? state, long due) : ITimer
        {
            internal TimerCallback? Callback = callback;
            internal object? State = state;
            internal bool Disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period) { due = owner.GetTimestamp() + dueTime.Ticks; return true; }
            internal void Fire() { if (due <= owner.GetTimestamp()) { due = long.MaxValue; Callback?.Invoke(State); } }
            public void Dispose() { Disposed = true; due = long.MaxValue; Callback = null; State = null; }
            public async ValueTask DisposeAsync()
            {
                owner.DisposalEntered.TrySetResult();
                if (owner.BlockDisposal is not null) await owner.BlockDisposal.Task;
                Dispose();
                if (owner.ThrowDisposal) throw new InvalidOperationException("PRIVATE_CLEANUP");
            }
        }
    }
}
