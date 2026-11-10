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
    internal readonly record struct JobKey(CallerSource Source, string Principal, Guid Epoch, Guid Id)
    {
        internal JobKey(AutomationCaller caller, Guid id) : this(caller.Source, caller.PrincipalId, caller.ActivationEpoch, id) { }
    }
    private readonly Dictionary<JobKey, Job> jobs = new();
    private readonly HashSet<JobKey> tombstones = new();
    private readonly AsyncLocal<Job?> current = new();
    // Host-issued immutable grant snapshots. A record copy with modified claims has no authority.
    private readonly HashSet<AutomationCaller> credentials = new(ReferenceEqualityComparer.Instance);
    private bool activationUsed;
    private volatile bool leaseCleanupFailed;
    private volatile TaskCompletionSource? epochClose;
    internal AutomationCaller IssueCredential(string principal, ImmutableHashSet<string> grants)
    {
        lock (sync)
        {
            if (!epoch.HasValue || stopping || credentials.Count >= 64 || principal is not { Length: > 0 and <= 128 } ||
                grants.Count > 16 || grants.Any(g => g is not { Length: > 0 and <= 64 }))
                throw new InvalidOperationException("Credential unavailable.");
            var credential = new AutomationCaller(principal, CallerSource.Mcp,
                grants.ToImmutableHashSet(StringComparer.Ordinal), epoch.Value);
            credentials.Add(credential);
            return credential;
        }
    }
    private Guid? epoch;
    private Job? active;
    private Job? preparingReturn;
    private JobKey? last;
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
            AutomationPermissions.ExecutionRead, AutomationPermissions.ExecutionRun, AutomationPermissions.EditorRead,
            AutomationPermissions.EditorWrite), Guid.Empty);

    private sealed class Job
    {
        internal EditorCoordinator.ExecutionIntent? EditorIntent;
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
        internal AutomationErrorCode FailureCode = AutomationErrorCode.InternalError;
        internal bool Effects;
        internal bool ApprovedOutput;
        internal int OutputBytes;
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
            epoch == caller.ActivationEpoch && credentials.Contains(caller)));

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
                ? WorkbenchExecutionState.WaitingForUser : active.Result.State == ExecutionState.AwaitingConfirmation
                ? WorkbenchExecutionState.AwaitingConfirmation : WorkbenchExecutionState.Running;
            bool Visible(Job? job) => job != null && Authorized(caller, AutomationPermissions.ExecutionRead) && Owns(caller, job);
            var activeId = caller.Source == CallerSource.Gui || Visible(active) ? active?.Id : null;
            var lastId = caller.Source == CallerSource.Gui || last is { } key && jobs.TryGetValue(key, out var previous) && Visible(previous)
                ? last?.Id : null;
            return Task.FromResult(Ok(new WorkbenchStatus(stopping ? Lifecycle.Stopping : faulted ? Lifecycle.Faulted : Lifecycle.Ready,
                epoch.HasValue, editorBridge.Available, state, activeId, IsBusy, Version, lastId,
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
        var key = new JobKey(caller, id);
        if (!jobs.TryGetValue(key, out var job)) return Fail<ExecutionResult>(tombstones.Contains(key)
            ? AutomationErrorCode.ResultExpired : AutomationErrorCode.NotFound);
        if (!Owns(caller, job)) return Fail<ExecutionResult>(AutomationErrorCode.NotFound);
        return job.Expired ? Fail<ExecutionResult>(AutomationErrorCode.ResultExpired, id) : Ok(VisibleResult(caller, job));
    }

    private ExecutionResult VisibleResult(AutomationCaller caller, Job job)
    {
        var result = job.Result;
        if (!Authorized(caller, AutomationPermissions.VariablesMetadataRead))
            result = result with { Variables = new(result.StateVersion, result.FinishedAt ?? result.AcceptedAt, true, [], false) };
        if (job.ApprovedOutput && !Authorized(caller, AutomationPermissions.OutputRead))
            result = result with { Outputs = result.Outputs.Select(o => o with { Text = null, Redacted = true }).ToImmutableArray() };
        return result;
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
            var job = jobs[new(caller, executionId)];
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
            {
                var candidate = target.Owner.active ?? target.Owner.preparingReturn;
                if (candidate?.RegistrationId == target.RegistrationId)
                    target.Owner.RequestCancel(candidate, CancellationReason.Timeout);
            }
        }, new TimerTarget(this, job.Id, job.RegistrationId), remaining);
    }

    private sealed record TimerTarget(WorkbenchAutomationService Owner, Guid Id, Guid RegistrationId);

    private void StartRetention(Job job)
    {
        var remaining = AutomationContracts.ResultRetention - clock.GetElapsedTime(job.FinishedTimestamp);
        job.RetentionTimer = CreateTimer(static state => {
            var target = (TimerTarget)state!;
            lock (target.Owner.sync)
                if (target.Owner.jobs.Values.FirstOrDefault(j => j.RegistrationId == target.RegistrationId && j.Id == target.Id) is { } completed)
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
        CancellationToken userCancellation, Func<WorkbenchExecutionPipeline, Task<T>> operation, Func<bool>? validateIntent = null,
        EditorCoordinator.ExecutionIntent? editorIntent = null)
    {
        Job? job = null;
        try
        {
            requestCancellation.ThrowIfCancellationRequested();
            job = CreateJob(caller, id, fingerprint, source, timeout);
            job.EditorIntent = editorIntent;
            if (editorIntent != null) job.Result = job.Result with { EditorId = editorIntent.EditorId, SourceRevision = editorIntent.SourceRevision };
            void Publish()
            {
                jobs.Add(new(caller, id), job);
                active = job;
            }
            if (validateIntent == null) Publish();
            else preparingReturn = job;
            StartTimer(job, timeout);
            job.UserCancellation = userCancellation.UnsafeRegister(static state => {
                var target = (TimerTarget)state!;
                lock (target.Owner.sync)
                    if (target.Owner.active is { } registered && registered.RegistrationId == target.RegistrationId)
                        target.Owner.RequestCancel(registered, CancellationReason.User);
            }, new TimerTarget(this, id, job.RegistrationId));
            AdmissionCheckpoint?.Invoke("registration");
            AdmissionCheckpoint?.Invoke("worker");
            requestCancellation.ThrowIfCancellationRequested();
            if (validateIntent != null)
            {
                if (!validateIntent() || stopping || job.Cancellation.IsCancellationRequested) throw new ExecutionBusyException();
                requestCancellation.ThrowIfCancellationRequested();
                Publish();
                preparingReturn = null;
            }
            var work = RunOwnedAsync(job, reservation, operation);
            _ = ObserveCancellation(work);
            return (job, work);
        }
        catch
        {
            if (job is not null)
            {
                if (ReferenceEquals(preparingReturn, job)) preparingReturn = null;
                var key = new JobKey(caller, id);
                if (jobs.TryGetValue(key, out var published) && ReferenceEquals(published, job)) jobs.Remove(key);
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
        if (editorBridge.Registered && !Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            return await UiDispatch.InvokeAsync(() => ExecuteTextAsync(caller, request, requestCancellation));
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
            var key = new JobKey(caller, request.ExecutionId);
            if (tombstones.Contains(key)) return Fail<ExecutionResult>(AutomationErrorCode.ResultExpired);
            if (jobs.TryGetValue(key, out var previous))
            {
                if (!Owns(caller, previous)) return Fail<ExecutionResult>(AutomationErrorCode.NotFound);
                if (previous.Expired) return Fail<ExecutionResult>(AutomationErrorCode.ResultExpired, previous.Id);
                if (previous.Fingerprint != fingerprint) return Fail<ExecutionResult>(AutomationErrorCode.InvalidArgument);
                return Ok(previous.Result);
            }
            if (stopping) return Fail<ExecutionResult>(AutomationErrorCode.ShuttingDown);
            if (InPresentationObserver || GuiOwnsSlotOrDraining) return Fail<ExecutionResult>(AutomationErrorCode.Busy);
            if (request.ExpectedStateVersion != Version) return Fail<ExecutionResult>(AutomationErrorCode.StateConflict);
            if (jobs.Count + tombstones.Count >= AutomationContracts.MaxExecutionIds) return Fail<ExecutionResult>(AutomationErrorCode.LimitExceeded);
            IDisposable reservation;
            try { reservation = InterpreterHostQueue.Reserve(); }
            catch (ExecutionBusyException) { return Fail<ExecutionResult>(AutomationErrorCode.Busy); }
            int timeout = request.TimeoutMs ?? AutomationContracts.DefaultTimeoutMs;
            EditorCoordinator.ExecutionIntent? intent = null;
            try
            {
                if (editorBridge.Registered) intent = new(EditorCoordinator, null);
            }
            catch (ExecutionBusyException) { reservation.Dispose(); return Fail<ExecutionResult>(AutomationErrorCode.Busy); }
            try
            {
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
                }, intent == null ? null : () => intent.Current, intent).Job;
            }
            catch (ExecutionBusyException) { intent?.Dispose(); return Fail<ExecutionResult>(AutomationErrorCode.Busy); }
            catch { intent?.Dispose(); throw; }
        }
        await job.Task.WaitAsync(requestCancellation).ConfigureAwait(false);
        lock (sync) return job.Expired ? Fail<ExecutionResult>(AutomationErrorCode.ResultExpired, job.Id) : Ok(job.Result);
    }

    // GUI cancellation is an explicit local user/lifetime action, not a transport request token.
    private Task<T> RunGuiAsync<T>(Func<WorkbenchExecutionPipeline, Task<T>> operation, CancellationToken userCancellation, string requestIdentity, Func<bool>? validateIntent, EditorCoordinator.OperationTicket? native)
    {
        userCancellation.ThrowIfCancellationRequested();
        if (InPresentationObserver) throw new ExecutionBusyException();
        lock (sync)
        {
            if (stopping || epochClose != null) throw new ExecutionBusyException();
            Prune();
            if (jobs.Count + tombstones.Count >= AutomationContracts.MaxExecutionIds) throw new ExecutionBusyException();
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestIdentity)));
            if (validateIntent?.Invoke() == false || stopping) throw new ExecutionBusyException();
            EditorCoordinator.ExecutionIntent? intent = null;
            if (editorBridge.Registered || native != null)
            {
                if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) throw new ExecutionBusyException();
                intent = new(EditorCoordinator, native);
            }
            else if (GuiOwnsSlotOrDraining) throw new ExecutionBusyException();
            try
            {
                if (intent != null && validateIntent == null) validateIntent = () => intent.Current;
                var reservation = InterpreterHostQueue.Reserve();
                return Accept(reservation, LocalCaller, Guid.NewGuid(), fingerprint, ExecutionSource.GuiLine,
                    AutomationContracts.DefaultTimeoutMs, userCancellation, userCancellation, operation, validateIntent, intent).Work;
            }
            catch { intent?.Dispose(); throw; }
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
                        ExecutionId = job.Id,
                        ValidateJob = () =>
                        {
                            lock (sync) if (!ReferenceEquals(current.Value, job) || !ReferenceEquals(active, job) || job.ResourcesReleased)
                                throw new ExecutionBusyException();
                        },
                        EditorIntent = job.EditorIntent, ExecutionToken = job.Cancellation.Token, Publish = result => RecordUnit(job, result),
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
        if (job.EditorIntent is { } intent)
        {
            try { await OnEditorUiAsync(() => { intent.Dispose(); return true; }).ConfigureAwait(false); }
            catch { job.CleanupFailed = true; }
            job.EditorIntent = null;
        }
        job.ResourcesReleased = true;
    }

    private void RecordUnit(Job job, ExecutionPresentation result)
    {
        lock (sync)
        {
            if (!ReferenceEquals(active, job) || job.ResourcesReleased) throw new ExecutionBusyException();
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
                {
                    string? text = null;
                    bool omitted = false;
                    if (job.ApprovedOutput && Authorized(job.Caller, AutomationPermissions.OutputRead))
                    {
                        var remaining = AutomationContracts.MaxOutputTextBytes - job.OutputBytes;
                        if (output.Text.Length <= remaining && Encoding.UTF8.GetByteCount(output.Text) is var bytes && bytes <= remaining)
                        {
                            text = output.Text;
                            job.OutputBytes += bytes;
                        }
                        else { omitted = true; truncated = true; }
                    }
                    outputs.Add(new(outputs.Count + 1, output.Kind == "print" ? OutputKind.Print : OutputKind.Info, text, text == null, omitted));
                }
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
            if (job.Effects) AdvanceVersion();
            if (job.CaptureFailed) variables = variables with { Stale = true };
            else if (job.PendingVariables is { } captured)
                variables = captured with { StateVersion = Version, CapturedAt = clock.GetUtcNow() };
            job.PendingVariables = null;
            var outcome = job.Outcome;
            if (job.CleanupFailed && (lease != null || epochClose != null)) leaseCleanupFailed = true;
            if (job.CaptureFailed || job.CleanupFailed) outcome = ExecutionState.Failed;
            else if (outcome == ExecutionState.Cancelled && job.Reason == CancellationReason.Timeout) outcome = ExecutionState.TimedOut;
            // Do not turn a successfully completed final statement into cancellation on a late request.
            job.Result = job.Result with { State = outcome, FinishedAt = clock.GetUtcNow(), StateVersion = Version,
                CancellationReason = job.Reason, Variables = variables, Error = job.CaptureFailed || outcome == ExecutionState.Failed
                    ? AutomationErrors.Create(job.CaptureFailed || job.CleanupFailed ? AutomationErrorCode.InternalError :
                        job.FailureCode, job.Id) : null };
            job.FinishedTimestamp = clock.GetTimestamp();
            job.CompletionSequence = ++completionSequence;
            last = new(job.Caller, job.Id);
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
        var key = new JobKey(job.Caller, job.Id);
        tombstones.Add(key);
        jobs.Remove(key);
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

    private void AdvanceVersion()
    {
        lock (sync)
        {
            ++version;
            InterpreterHostQueue.AdvanceStateRevision();
        }
    }

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
        catch
        {
            lock (sync)
            {
                if (current.Value is { } job) job.CaptureFailed = true;
                faulted = true;
                variables = variables with { Stale = true };
            }
            throw;
        }
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
    private Task? shutdownQuiescence;
    public void BeginShutdown()
    {
        lock (sync)
        {
            if (shutdownQuiescence != null) return;
            stopping = true;
            if (active is not null) RequestCancel(active, CancellationReason.Shutdown);
            RevokeMcpCore();
            // A completed disable may have readmitted GUI producers. Capture their original
            // coordinator now. An in-progress or failed close still owns the same obligations.
            if (epochClose == null && editorBridge.Coordinator != null) StartEditorClose();
            shutdownQuiescence = epochClose?.Task ?? OutstandingServiceWork();
        }
    }

    public Task DrainAsync(CancellationToken ct = default)
    {
        lock (sync) return Task.WhenAll(OutstandingServiceWork(), epochClose?.Task ?? Task.CompletedTask,
            shutdownQuiescence ?? Task.CompletedTask).WaitAsync(ct);
    }
    private Task OutstandingServiceWork() => Task.WhenAll(active?.Task ?? Task.CompletedTask,
        editCompletion?.Task ?? Task.CompletedTask, saveWork?.Completion.Task ?? Task.CompletedTask, fileDialogCompletion?.Task ?? Task.CompletedTask,
        fileStepCompletion?.Task ?? Task.CompletedTask);

    internal async Task DrainForExitAsync()
    {
        try { await DrainAsync().ConfigureAwait(false); }
        catch (QuiescentEpochException) when (IsStopping)
        {
            // Strict certification failed after the original work quiesced. Only exit is permitted.
        }
    }

    public async ValueTask DisposeAsync()
    {
        BeginShutdown();
        await DrainForExitAsync().ConfigureAwait(false);
        // No control access under the register lock or from a worker thread.
        if (editorBridge.Registered)
            await OnEditorUiAsync(() => { editorBridge.DetachCurrent(); return true; }).ConfigureAwait(false);
        lock (sync)
            foreach (var job in jobs.Values.ToArray()) Expire(job);
    }
    public void DisableMcp() { lock (sync) RevokeMcpCore(); }
    internal Task DisableMcpAndDrainAsync()
    {
        Task close;
        lock (sync)
        {
            RevokeMcpCore();
            // Capture this epoch's completion while still under the admission lock.
            close = epochClose?.Task ?? Task.CompletedTask;
        }
        return CertifyGuiReturnAsync(close);
    }
    private async Task CertifyGuiReturnAsync(Task close)
    {
        await close.ConfigureAwait(false);
        // A quiescent native fault may complete epoch cleanup without restoring GUI ownership.
        if (!await OnEditorUiAsync(() => !IsStopping && editorBridge.Coordinator?.Phase == EditorControlPhase.Gui))
            throw new InvalidOperationException("Editor return could not be certified.");
    }
    private void RevokeMcpCore()
    {
        bool wasActive = epoch.HasValue;
        epoch = null;
        credentials.Clear();
        if (wasActive) StartEditorClose();
        foreach (var job in jobs.Values.Where(j => j.Caller.Source == CallerSource.Mcp).ToArray())
        {
            job.Revoked = true;
            if (!ReferenceEquals(active, job) && AutomationContracts.IsTerminal(job.Result.State)) Expire(job);
            else RequestCancel(job, CancellationReason.McpDisabled);
        }
    }
    private void StartEditorClose()
    {
        epochClose = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = FinishEpochCloseAsync(epochClose, OutstandingServiceWork(), editorBridge.Coordinator);
    }

    // Host-only lifecycle hook; it neither authenticates clients nor enables execution.
    internal Guid ActivateMcp()
    {
        lock (sync)
        {
            // Conservative D02 fallback. No new credentials after L1 in this host.
            if (activationUsed || stopping) throw new InvalidOperationException("Activation unavailable.");
            activationUsed = true;
            epoch = Guid.NewGuid();
            return epoch.Value;
        }
    }

    internal bool IsStopping => Volatile.Read(ref stopping);
}
