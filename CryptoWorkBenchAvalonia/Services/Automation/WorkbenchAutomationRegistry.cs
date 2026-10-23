using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Models;

namespace CryptoWorkBenchAvalonia.Services.Automation;

public sealed partial class WorkbenchAutomationService : IWorkbenchAutomationService, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly TimeProvider clock;
    private readonly Dictionary<Guid, Job> jobs = new();
    private readonly Dictionary<Guid, AutomationCaller> tombstones = new();
    private readonly AsyncLocal<Job?> current = new();
    private readonly HashSet<Guid> revokedEpochs = new();
    private Guid? epoch;
    private Job? active;
    private Guid? last;
    private long version;
    private long completionSequence;
    private bool stopping;
    private bool faulted;
    private VariableSnapshot variables;
    private const int SnapshotBudget = 2 * 1024 * 1024;
    // Instance-local test seam, never exposed through the automation contract.
    internal Action<string>? AdmissionCheckpoint { get; set; }
    public Guid InstanceId { get; } = Guid.NewGuid();

    public WorkbenchAutomationService() : this(TimeProvider.System) { }
    internal WorkbenchAutomationService(TimeProvider timeProvider)
    {
        clock = timeProvider;
        variables = new("0", clock.GetUtcNow(), true, [], false);
    }

    internal static AutomationCaller LocalCaller { get; } = new("local-gui", CallerSource.Gui,
        ImmutableHashSet.Create(AutomationPermissions.StatusRead, AutomationPermissions.VariablesMetadataRead,
            AutomationPermissions.ExecutionRead, AutomationPermissions.ExecutionRun), Guid.Empty);

    private sealed class Job
    {
        internal required Guid Id;
        internal readonly Guid RegistrationId = Guid.NewGuid();
        internal required AutomationCaller Caller;
        internal required string Fingerprint;
        internal required ExecutionResult Result;
        internal VariableSnapshot? PendingVariables;
        internal CancellationTokenSource Cancellation = new();
        // A result-free completion signal prevents the register retaining GUI Task<T>
        // results, operation closures or the original script after completion.
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Task => Completion.Task;
        internal Task CancellationCallbacks = Task.CompletedTask;
        internal ITimer? Timer;
        internal ITimer? RetentionTimer;
        internal CancellationTokenRegistration UserCancellation;
        internal CancellationReason? Reason;
        internal ExecutionState Outcome = ExecutionState.Completed;
        internal bool Effects;
        internal bool CaptureFailed;
        internal bool Revoked;
        internal bool Expired;
        internal bool ResourcesReleased;
        internal bool CancellationClosed;
        internal bool CleanupFailed;
        internal long FinishedTimestamp;
        internal long AcceptedTimestamp;
        internal long CompletionSequence;
        internal int Bytes;
        internal int SourceLineOffset;
    }

    private string Version => version.ToString(CultureInfo.InvariantCulture);
    private Reply<T> Ok<T>(T data) where T : class => Reply<T>.Success(InstanceId, data);
    private Reply<T> Fail<T>(AutomationErrorCode code, Guid? id = null) where T : class =>
        Reply<T>.Failure(InstanceId, AutomationErrors.Create(code, id));

    private bool Authorized(AutomationCaller caller, string permission) =>
        !string.IsNullOrEmpty(caller.PrincipalId) && caller.Permissions.Contains(permission) &&
        (caller.Source == CallerSource.Gui || (caller.Source == CallerSource.Mcp && !stopping &&
            epoch == caller.ActivationEpoch && !revokedEpochs.Contains(caller.ActivationEpoch)));

    private bool Owns(AutomationCaller caller, Job job) => !job.Revoked &&
        SameOwner(caller, job.Caller);
    private static bool SameOwner(AutomationCaller caller, AutomationCaller owner) =>
        caller.Source == owner.Source && caller.PrincipalId == owner.PrincipalId && caller.ActivationEpoch == owner.ActivationEpoch;

    public Task<Reply<WorkbenchStatus>> GetStatusAsync(AutomationCaller caller, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (!Authorized(caller, AutomationPermissions.StatusRead)) return Task.FromResult(Fail<WorkbenchStatus>(AutomationErrorCode.Forbidden));
            Prune();
            var state = active is null ? WorkbenchExecutionState.Idle : active.Reason is not null
                ? WorkbenchExecutionState.CancellationRequested : active.Result.State == ExecutionState.WaitingForUser
                ? WorkbenchExecutionState.WaitingForUser : WorkbenchExecutionState.Running;
            return Task.FromResult(Ok(new WorkbenchStatus(stopping ? Lifecycle.Stopping : faulted ? Lifecycle.Faulted : Lifecycle.Ready,
                epoch.HasValue, false, state, active?.Id, IsBusy, Version, last,
                [AutomationPermissions.StatusRead, AutomationPermissions.VariablesMetadataRead, AutomationPermissions.ExecutionRead])));
        }
    }

    public Task<Reply<VariableSnapshot>> GetVariablesAsync(AutomationCaller caller, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (sync) return Task.FromResult(Authorized(caller, AutomationPermissions.VariablesMetadataRead)
            ? Ok(variables with { Stale = variables.Stale || active is not null || IsBusy || faulted })
            : Fail<VariableSnapshot>(AutomationErrorCode.Forbidden));
    }

    private Reply<ExecutionResult> Lookup(AutomationCaller caller, Guid id)
    {
        if (!Authorized(caller, AutomationPermissions.ExecutionRead)) return Fail<ExecutionResult>(AutomationErrorCode.Forbidden);
        if (id == Guid.Empty) return Fail<ExecutionResult>(AutomationErrorCode.InvalidArgument);
        Prune();
        if (!jobs.TryGetValue(id, out var job)) return Fail<ExecutionResult>(tombstones.TryGetValue(id, out var owner) && SameOwner(caller, owner)
            ? AutomationErrorCode.ResultExpired : AutomationErrorCode.NotFound);
        if (!Owns(caller, job)) return Fail<ExecutionResult>(AutomationErrorCode.NotFound);
        return job.Expired ? Fail<ExecutionResult>(AutomationErrorCode.ResultExpired, id) : Ok(job.Result);
    }

    public Task<Reply<ExecutionResult>> GetExecutionAsync(AutomationCaller caller, Guid executionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (sync) return Task.FromResult(Lookup(caller, executionId));
    }

    public Task<Reply<DiagnosticSnapshot>> GetDiagnosticsAsync(AutomationCaller caller, Guid executionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (sync)
        {
            var reply = Lookup(caller, executionId);
            return Task.FromResult(reply.Ok ? Ok(new DiagnosticSnapshot(executionId,
                AutomationContracts.IsTerminal(reply.Data!.State), reply.Data.Diagnostics, reply.Data.Truncated))
                : Reply<DiagnosticSnapshot>.Failure(InstanceId, reply.Error!));
        }
    }

    public Task<Reply<CancelReceipt>> CancelExecutionAsync(AutomationCaller caller, Guid executionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (sync)
        {
            var reply = Lookup(caller, executionId);
            if (!reply.Ok) return Task.FromResult(Reply<CancelReceipt>.Failure(InstanceId, reply.Error!));
            ct.ThrowIfCancellationRequested();
            var job = jobs[executionId];
            bool accepted = RequestCancel(job, CancellationReason.Explicit);
            return Task.FromResult(Ok(new CancelReceipt(executionId, accepted, job.Result.State)));
        }
    }

    // Called only with sync held. Cancellation callbacks must not execute under the registry lock.
    private bool RequestCancel(Job job, CancellationReason reason)
    {
        if (job.Expired || job.CancellationClosed || AutomationContracts.IsTerminal(job.Result.State) || job.Reason is not null) return false;
        job.Reason = reason;
        job.Result = job.Result with { State = ExecutionState.CancellationRequested, CancellationReason = reason };
        // CancelAsync marks the source synchronously, then runs callbacks asynchronously.
        job.CancellationCallbacks = job.Cancellation.CancelAsync();
        return true;
    }

    private static async Task ObserveCancellation(Task task) { try { await task.ConfigureAwait(false); } catch { } }

    private Job CreateJob(AutomationCaller caller, Guid id, string fingerprint, ExecutionSource source, int timeout)
    {
        var now = clock.GetUtcNow();
        return new Job { Id = id, Caller = caller, Fingerprint = fingerprint, AcceptedTimestamp = clock.GetTimestamp(),
            Result = new(id, ExecutionState.Running, now, now.AddMilliseconds(timeout), now, null, null, source,
                null, null, Version, false, Effects.None, [], [], variables with { Stale = true }, false, null) };
    }

    private void StartTimer(Job job, int timeout)
    {
        var remaining = TimeSpan.FromMilliseconds(timeout) - clock.GetElapsedTime(job.AcceptedTimestamp);
        job.Timer = CreateTimer(static state => {
            var target = (TimerTarget)state!;
            lock (target.Owner.sync)
                if (target.Owner.jobs.TryGetValue(target.Id, out var activeJob) && activeJob.RegistrationId == target.RegistrationId)
                    target.Owner.RequestCancel(activeJob, CancellationReason.Timeout);
        }, new TimerTarget(this, job.Id, job.RegistrationId), remaining);
    }

    private sealed record TimerTarget(WorkbenchAutomationService Owner, Guid Id, Guid RegistrationId);

    private void StartRetention(Job job)
    {
        var remaining = AutomationContracts.ResultRetention - clock.GetElapsedTime(job.FinishedTimestamp);
        job.RetentionTimer = CreateTimer(static state => {
            var target = (TimerTarget)state!;
            lock (target.Owner.sync)
                if (target.Owner.jobs.TryGetValue(target.Id, out var completed) && completed.RegistrationId == target.RegistrationId)
                    target.Owner.Expire(completed);
        }, new TimerTarget(this, job.Id, job.RegistrationId), remaining);
    }

    private ITimer CreateTimer(TimerCallback callback, object state, TimeSpan due)
    {
        using (ExecutionContext.SuppressFlow())
            return clock.CreateTimer(callback, state, due > TimeSpan.Zero ? due : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    // Caller holds sync and reservation. No worker can enter until the complete
    // initialization succeeds. After RunOwnedAsync returns its task, only it owns cleanup.
    private (Job Job, Task<T> Work) Accept<T>(IDisposable reservation, AutomationCaller caller, Guid id,
        string fingerprint, ExecutionSource source, int timeout, CancellationToken requestCancellation,
        CancellationToken userCancellation, Func<WorkbenchExecutionPipeline, Task<T>> operation)
    {
        Job? job = null;
        try
        {
            requestCancellation.ThrowIfCancellationRequested();
            job = CreateJob(caller, id, fingerprint, source, timeout);
            jobs.Add(id, job);
            active = job;
            StartTimer(job, timeout);
            job.UserCancellation = userCancellation.UnsafeRegister(static state => {
                var target = (TimerTarget)state!;
                lock (target.Owner.sync)
                    if (target.Owner.jobs.TryGetValue(target.Id, out var registered) && registered.RegistrationId == target.RegistrationId)
                        target.Owner.RequestCancel(registered, CancellationReason.User);
            }, new TimerTarget(this, id, job.RegistrationId));
            AdmissionCheckpoint?.Invoke("registration");
            AdmissionCheckpoint?.Invoke("worker");
            requestCancellation.ThrowIfCancellationRequested();
            var work = RunOwnedAsync(job, reservation, operation);
            _ = ObserveCancellation(work);
            return (job, work);
        }
        catch
        {
            if (job is not null)
            {
                jobs.Remove(id);
                if (ReferenceEquals(active, job)) active = null;
                job.CancellationClosed = true;
                // The registration callback only looks up the ID under sync. Once
                // removed it cannot touch the CTS, even if already queued on another thread.
                job.UserCancellation.Unregister();
                try { job.Timer?.Dispose(); } catch { /* Preserve the acceptance failure. */ }
                job.Timer = null;
                _ = ObserveCancellation(job.CancellationCallbacks);
                job.Cancellation.Dispose();
                job.Completion.TrySetResult();
            }
            reservation.Dispose();
            throw;
        }
    }

    public async Task<Reply<ExecutionResult>> ExecuteTextAsync(AutomationCaller caller, TextExecution request, CancellationToken requestCancellation)
    {
        requestCancellation.ThrowIfCancellationRequested();
        Job job;
        lock (sync)
        {
            if (!Authorized(caller, AutomationPermissions.ExecutionRun) || caller.Source != CallerSource.Gui)
                return Fail<ExecutionResult>(AutomationErrorCode.Forbidden);
            if (request.InstanceId != InstanceId) return Fail<ExecutionResult>(AutomationErrorCode.InstanceMismatch);
            try { AutomationJson.Validate(request); }
            catch (JsonException) { return Fail<ExecutionResult>(AutomationErrorCode.InvalidArgument); }
            if (request.ExecutionId == Guid.Empty) return Fail<ExecutionResult>(AutomationErrorCode.InvalidArgument);
            if (request.OutputPolicy == OutputPolicy.IncludeText) return Fail<ExecutionResult>(AutomationErrorCode.Forbidden);
            // Hash normalized immutable arguments; never retain script text in the result register.
            string fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request with
                { TimeoutMs = request.TimeoutMs ?? AutomationContracts.DefaultTimeoutMs, OutputPolicy = OutputPolicy.MetadataOnly }, AutomationJson.Options)));
            Prune();
            if (tombstones.TryGetValue(request.ExecutionId, out var expiredOwner))
                return Fail<ExecutionResult>(SameOwner(caller, expiredOwner) ? AutomationErrorCode.ResultExpired : AutomationErrorCode.NotFound);
            if (jobs.TryGetValue(request.ExecutionId, out var previous))
            {
                if (!Owns(caller, previous)) return Fail<ExecutionResult>(AutomationErrorCode.NotFound);
                if (previous.Expired) return Fail<ExecutionResult>(AutomationErrorCode.ResultExpired, previous.Id);
                if (previous.Fingerprint != fingerprint) return Fail<ExecutionResult>(AutomationErrorCode.InvalidArgument);
                return Ok(previous.Result);
            }
            if (stopping) return Fail<ExecutionResult>(AutomationErrorCode.ShuttingDown);
            if (request.ExpectedStateVersion != Version) return Fail<ExecutionResult>(AutomationErrorCode.StateConflict);
            if (jobs.Count + tombstones.Count >= AutomationContracts.MaxExecutionIds) return Fail<ExecutionResult>(AutomationErrorCode.LimitExceeded);
            IDisposable reservation;
            try { reservation = InterpreterHostQueue.Reserve(); }
            catch (ExecutionBusyException) { return Fail<ExecutionResult>(AutomationErrorCode.Busy); }
            int timeout = request.TimeoutMs ?? AutomationContracts.DefaultTimeoutMs;
            job = Accept(reservation, caller, request.ExecutionId, fingerprint, ExecutionSource.Text, timeout,
                requestCancellation, default, async pipeline =>
            {
                using var password = new PasswordDialogHost();
                if (request.Mode == TextExecutionMode.Submission)
                    await pipeline.ExecuteAsync(request.Text, password.RequestAsync, pipeline.ExecutionToken).ConfigureAwait(false);
                else
                    await ExecuteScriptBookAsync(pipeline, request.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'),
                        password.RequestAsync, (_, _) => Task.CompletedTask, (_, _, _) => Task.FromResult(true), pipeline.ExecutionToken).ConfigureAwait(false);
                return true;
            }).Job;
        }
        await job.Task.WaitAsync(requestCancellation).ConfigureAwait(false);
        lock (sync) return job.Expired ? Fail<ExecutionResult>(AutomationErrorCode.ResultExpired, job.Id) : Ok(job.Result);
    }

    // GUI cancellation is an explicit local user/lifetime action, not a transport request token.
    private Task<T> RunGuiAsync<T>(Func<WorkbenchExecutionPipeline, Task<T>> operation, CancellationToken userCancellation, string requestIdentity)
    {
        userCancellation.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (stopping) throw new ExecutionBusyException();
            Prune();
            if (jobs.Count + tombstones.Count >= AutomationContracts.MaxExecutionIds) throw new ExecutionBusyException();
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestIdentity)));
            var reservation = InterpreterHostQueue.Reserve();
            return Accept(reservation, LocalCaller, Guid.NewGuid(), fingerprint, ExecutionSource.GuiLine,
                AutomationContracts.DefaultTimeoutMs, userCancellation, userCancellation, operation).Work;
        }
    }

    private async Task<T> RunOwnedAsync<T>(Job job, IDisposable reservation, Func<WorkbenchExecutionPipeline, Task<T>> operation)
    {
        try
        {
            return await InterpreterHostQueue.RunAdmittedAsync(async () =>
            {
                current.Value = job;
                try
                {
                    InterpreterExecution.AccessState(() => { EnsureRecovered(); return true; });
                    lock (sync)
                    {
                        job.PendingVariables = variables;
                        job.Result = job.Result with { Variables = variables with { Stale = true } };
                    }
                    var pipeline = new WorkbenchExecutionPipeline {
                        ExecutionToken = job.Cancellation.Token, Publish = result => RecordUnit(job, result),
                        CancellationObserved = reason => { lock (sync) RequestCancel(job, reason); },
                        WaitingForUser = waiting => { lock (sync) if (job.Reason is null)
                            job.Result = job.Result with { State = waiting ? ExecutionState.WaitingForUser : ExecutionState.Running }; }
                    };
                    var value = await operation(pipeline).ConfigureAwait(false);
                    if (value is bool success && !success && job.Outcome == ExecutionState.Completed) job.Outcome = ExecutionState.Failed;
                    return value;
                }
                catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested)
                { job.Outcome = ExecutionState.Cancelled; return default!; }
                catch
                {
                    job.Outcome = ExecutionState.Failed;
                    // GUI callers retain their local exception handling; public text execution observes the owned task below.
                    throw;
                }
                finally
                {
                    await ReleaseResourcesAsync(job).ConfigureAwait(false);
                    FinalizeJob(job);
                    current.Value = null;
                }
            }).ConfigureAwait(false);
        }
        catch
        {
            // A direct Core owner can race the host reservation. Even a failure
            // before the host delegate starts must terminate the accepted job.
            lock (sync)
            {
                if (!AutomationContracts.IsTerminal(job.Result.State))
                {
                    job.Outcome = ExecutionState.Failed;
                }
            }
            await ReleaseResourcesAsync(job).ConfigureAwait(false);
            lock (sync) if (!AutomationContracts.IsTerminal(job.Result.State)) FinalizeJob(job);
            throw;
        }
        finally
        {
            // Keep admission through registry maintenance as well as script/password/UI finalizers.
            lock (sync)
            {
                try
                {
                    if (ReferenceEquals(active, job)) active = null;
                    if (job.Revoked) Expire(job);
                    Prune();
                    if (!job.Expired)
                    {
                        try { StartRetention(job); }
                        catch { Expire(job); } // No timer means no safe retention guarantee.
                    }
                }
                finally { reservation.Dispose(); job.Completion.TrySetResult(); }
            }
        }
    }

    private async Task ReleaseResourcesAsync(Job job)
    {
        if (job.ResourcesReleased) return;
        // Never wait while holding sync: an already running timer/registration
        // may itself be waiting for sync. Close cancellation atomically only once
        // all registered execution callbacks have really drained.
        try { if (job.Timer is not null) await job.Timer.DisposeAsync().ConfigureAwait(false); }
        catch { job.CleanupFailed = true; }
        try { await job.UserCancellation.DisposeAsync().ConfigureAwait(false); }
        catch { job.CleanupFailed = true; }
        while (true)
        {
            Task callbacks;
            lock (sync)
            {
                callbacks = job.CancellationCallbacks;
                if (callbacks.IsCompleted) { job.CancellationClosed = true; break; }
            }
            try { await callbacks.ConfigureAwait(false); }
            catch { job.CleanupFailed = true; }
        }
        try { await job.CancellationCallbacks.ConfigureAwait(false); }
        catch { job.CleanupFailed = true; }
        job.CancellationCallbacks = Task.CompletedTask;
        job.Cancellation.Dispose();
        job.Timer = null;
        job.UserCancellation = default;
        job.ResourcesReleased = true;
    }

    private void RecordUnit(Job job, ExecutionPresentation result)
    {
        lock (sync)
        {
            job.Effects |= result.EffectsPossible;
            job.Outcome = result.Outcome;
            if (result.Outcome == ExecutionState.Cancelled && job.Reason is null) job.Reason = CancellationReason.User;
            job.CaptureFailed |= result.CaptureFailed;
            if (result.CaptureFailed) { faulted = true; variables = variables with { Stale = true }; }
            else job.PendingVariables = AutomationProjection.Variables(result.Variables, Version, clock.GetUtcNow());
            var diagnostics = job.Result.Diagnostics.ToBuilder();
            bool truncated = job.Result.Truncated;
            foreach (var diagnostic in AutomationProjection.Diagnostics(result))
                if (diagnostics.Count < AutomationContracts.MaxCollectionEntries)
                    diagnostics.Add(diagnostic.Line is int line ? diagnostic with { Line = line + job.SourceLineOffset } : diagnostic);
                else truncated = true;
            var outputs = job.Result.Outputs.ToBuilder();
            foreach (var output in result.Output)
                if (outputs.Count < AutomationContracts.MaxCollectionEntries)
                    outputs.Add(new(outputs.Count + 1, output.Kind == "print" ? OutputKind.Print : OutputKind.Info, null, true, false));
                else truncated = true;
            job.Result = job.Result with { Diagnostics = diagnostics.ToImmutable(), Outputs = outputs.ToImmutable(),
                HasErrors = job.Result.HasErrors || result.Diagnostics.Any(d => d.Severity == CryptoScript.ErrorListner.SemanticDiagnosticSeverity.Error)
                    || result.SyntaxDiagnostics.Count > 0 || result.CaptureFailed,
                Effects = job.Effects ? Effects.Possible : Effects.None, Truncated = truncated || job.PendingVariables?.Truncated == true };
            Bound(job);
        }
    }

    private void FinalizeJob(Job job)
    {
        lock (sync)
        {
            if (job.Effects) ++version;
            if (job.CaptureFailed) variables = variables with { Stale = true };
            else if (job.PendingVariables is { } captured)
                variables = captured with { StateVersion = Version, CapturedAt = clock.GetUtcNow() };
            job.PendingVariables = null;
            var outcome = job.Outcome;
            if (job.CaptureFailed || job.CleanupFailed) outcome = ExecutionState.Failed;
            else if (outcome == ExecutionState.Cancelled && job.Reason == CancellationReason.Timeout) outcome = ExecutionState.TimedOut;
            // Do not turn a successfully completed final statement into cancellation on a late request.
            job.Result = job.Result with { State = outcome, FinishedAt = clock.GetUtcNow(), StateVersion = Version,
                CancellationReason = job.Reason, Variables = variables, Error = job.CaptureFailed || outcome == ExecutionState.Failed
                    ? AutomationErrors.Create(AutomationErrorCode.InternalError, job.Id) : null };
            job.FinishedTimestamp = clock.GetTimestamp();
            job.CompletionSequence = ++completionSequence;
            last = job.Id;
            Bound(job);
            Prune();
        }
    }

    private void Bound(Job job)
    {
        job.Bytes = JsonSerializer.SerializeToUtf8Bytes(job.Result, AutomationJson.Options).Length;
        // Metadata is capped before this point. This defensive bound also covers future DTO additions.
        if (job.Bytes > SnapshotBudget)
        {
            job.Result = job.Result with { Diagnostics = [], Outputs = [],
                Variables = job.Result.Variables with { Items = [], Truncated = true }, Truncated = true };
            job.Bytes = JsonSerializer.SerializeToUtf8Bytes(job.Result, AutomationJson.Options).Length;
        }
    }

    private void Expire(Job job)
    {
        if (job.Expired) return;
        job.Expired = true;
        job.RetentionTimer?.Dispose();
        job.RetentionTimer = null;
        job.Bytes = 0;
        // Tombstones carry no DTOs, tasks, timers, source text or cancellation sources.
        tombstones[job.Id] = job.Caller;
        jobs.Remove(job.Id);
    }

    private void Prune()
    {
        var completed = jobs.Values.Where(j => !j.Expired && !ReferenceEquals(j, active) && AutomationContracts.IsTerminal(j.Result.State))
            .OrderBy(j => j.CompletionSequence).ToList();
        foreach (var job in completed)
            if (clock.GetElapsedTime(job.FinishedTimestamp) >= AutomationContracts.ResultRetention) Expire(job);
        completed.RemoveAll(j => j.Expired);
        int bytes = completed.Sum(j => j.Bytes);
        // Reserve 2 MiB each for the active result, its pending capture and published variables.
        while (completed.Count > AutomationContracts.MaxRetainedResults || bytes > AutomationContracts.MaxResultBytes - 3 * SnapshotBudget)
        {
            var oldest = completed[0]; completed.RemoveAt(0); bytes -= oldest.Bytes; Expire(oldest);
        }
    }

    private void AdvanceVersion() { lock (sync) ++version; }

    private IReadOnlyList<VariableModel> CaptureMutation()
    {
        try
        {
            var captured = VariablePresentationCapture.Capture();
            lock (sync)
            {
                var snapshot = AutomationProjection.Variables(captured, Version, clock.GetUtcNow());
                if (current.Value is { } job) job.PendingVariables = snapshot;
                else variables = snapshot;
                faulted = false;
            }
            return captured;
        }
        catch { lock (sync) { faulted = true; variables = variables with { Stale = true }; } throw; }
    }

    // Recapture only; never repeat an operation to repair a snapshot.
    private void EnsureRecovered()
    {
        lock (sync) { if (!faulted && !variables.Stale) return; }
        // Recovery needs a reliable metadata basis, not GUI values. Never touch Value/password/parameter payloads.
        try
        {
            var models = new List<VariableModel>();
            foreach (var variable in VariableDictionary.Instance().GetVariables())
            {
                var model = new VariableModel { Identifier = variable.Id, Type = variable.Type?.Name ?? "PATH" };
                if (variable is KeyVariableDeclaration key)
                {
                    model.IsKey = true;
                    model.Algorithm = key.KeyType.Algorithm.ToString().ToUpperInvariant();
                    model.Material = key.KeyType.MaterialKind.ToString();
                    model.HasKeySize = key.KeySizeInBits.IsKnown;
                    model.KeySize = key.KeySizeInBits.IsKnown ? key.KeySizeInBits.Bits.ToString(CultureInfo.InvariantCulture) : string.Empty;
                }
                models.Add(model);
            }
            lock (sync) { variables = AutomationProjection.Variables(models, Version, clock.GetUtcNow()); faulted = false; }
        }
        catch { lock (sync) { faulted = true; variables = variables with { Stale = true }; } throw; }
    }

    internal IReadOnlyList<VariableModel> Refresh(Action<IReadOnlyList<VariableModel>>? present = null) =>
        InterpreterHostQueue.RunState(CaptureMutation, present);

    public void StopAccepting() { lock (sync) stopping = true; }
    public void BeginShutdown()
    {
        lock (sync) { stopping = true; if (active is not null) RequestCancel(active, CancellationReason.Shutdown); RevokeMcpCore(); }
    }
    public Task DrainAsync(CancellationToken ct = default)
    {
        lock (sync) return (active?.Task ?? Task.CompletedTask).WaitAsync(ct);
    }
    public async ValueTask DisposeAsync()
    {
        BeginShutdown();
        await DrainAsync().ConfigureAwait(false);
        lock (sync)
            foreach (var job in jobs.Values.ToArray()) Expire(job);
    }
    public void DisableMcp() { lock (sync) RevokeMcpCore(); }
    private void RevokeMcpCore()
    {
        if (epoch is Guid old) revokedEpochs.Add(old);
        epoch = null;
        foreach (var job in jobs.Values.Where(j => j.Caller.Source == CallerSource.Mcp).ToArray())
        {
            job.Revoked = true;
            if (!ReferenceEquals(active, job) && AutomationContracts.IsTerminal(job.Result.State)) Expire(job);
            else RequestCancel(job, CancellationReason.McpDisabled);
        }
    }
    // Host-only lifecycle hook; it neither authenticates clients nor enables execution.
    internal void ActivateMcp(Guid activationEpoch)
    {
        lock (sync)
        {
            if (activationEpoch == Guid.Empty || revokedEpochs.Contains(activationEpoch) || stopping) throw new ArgumentException("Invalid activation epoch.");
            RevokeMcpCore(); epoch = activationEpoch;
        }
    }

    private Task<Reply<T>> EditorUnavailable<T>(AutomationCaller caller, string permission, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();
        lock (sync) return Task.FromResult(Fail<T>(Authorized(caller, permission) ? AutomationErrorCode.EditorUnavailable : AutomationErrorCode.Forbidden));
    }
    public Task<Reply<EditorSnapshot>> GetEditorAsync(AutomationCaller caller, CancellationToken ct) => EditorUnavailable<EditorSnapshot>(caller, AutomationPermissions.EditorRead, ct);
    public Task<Reply<EditorMutation>> UpdateEditorAsync(AutomationCaller caller, EditorEdit request, CancellationToken ct) => EditorUnavailable<EditorMutation>(caller, AutomationPermissions.EditorWrite, ct);
    public Task<Reply<ExecutionResult>> ExecuteEditorAsync(AutomationCaller caller, EditorExecution request, CancellationToken requestCancellation) =>
        EditorUnavailable<ExecutionResult>(caller, AutomationPermissions.ExecutionRun, requestCancellation);
}
