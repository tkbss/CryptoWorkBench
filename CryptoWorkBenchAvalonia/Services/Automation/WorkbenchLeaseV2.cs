using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CryptoScript.Model;
using CryptoWorkBench.Automation.Abstractions;

namespace CryptoWorkBenchAvalonia.Services.Automation;

public sealed partial class WorkbenchAutomationService : IWorkbenchAutomationV2
{
    private sealed class ActiveLease(ProcessAcquisitions.Slot slot, Acquired receipt, ulong deadline)
    {
        internal readonly ProcessAcquisitions.Slot Slot = slot;
        internal readonly Acquired Receipt = receipt;
        internal ulong Deadline = deadline;
        internal ulong Version = 1;
        internal readonly CancellationTokenSource Lifetime = new();
        internal Task CancelledCallbacks = Task.CompletedTask;
        internal Renewed? LastRenew;
        internal EditorCoordinator.DrainTicket? Drain;
    }

    private ActiveLease? lease;
    private DispatcherTimer? leaseTimer;
    private ulong lastLeaseTime;
    private bool clockBroken;
    internal Func<ulong> LeaseMilliseconds { get; init; } = LeaseClock.Create();
    private const ulong Duration = 600_000;

    private ulong? ReadTime()
    {
        if (clockBroken) return null;
        try
        {
            ulong now = LeaseMilliseconds();
            if (now >= lastLeaseTime && now <= ulong.MaxValue - Duration) return lastLeaseTime = now;
        }
        catch { }
        clockBroken = true;
        return null;
    }

    private sealed class QuiescentEpochException(string message) : InvalidOperationException(message);

    private async Task FinishEpochCloseAsync(TaskCompletionSource completion, Task serviceWork, EditorCoordinator? owner)
    {
        try
        {
            EditorCoordinator.DrainTicket? certificate = null;
            Task leaseCallbacks = Task.CompletedTask;
            Task producers;
            try
            {
                if (owner != null)
                {
                    // Always enqueue, even for a UI caller. L1 finishes revocation before any UI callbacks.
                    certificate = await UiDispatch.InvokeAsync(() =>
                    {
                        lock (sync)
                        {
                            if (lease is { } original)
                            {
                                StopLeaseTimer(); // Closing owns the remaining drain. Deadline ticks can no longer admit work.
                                RevokeLease(original, stopping ? ReleaseReason.Shutdown : ReleaseReason.Revoked);
                            }
                            leaseCallbacks = lease?.CancelledCallbacks ?? Task.CompletedTask;
                        }
                        return owner.BeginDrain();
                    });
                }
            }
            finally
            {
                // Even failure to obtain an editor certificate cannot skip known producers.
                producers = Task.WhenAll(serviceWork, leaseCallbacks);
                await producers.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }

            if (certificate != null)
            {
                await certificate.Quiescent.Task.ConfigureAwait(false);
                await OnEditorUiAsync(() =>
                {
                    lock (sync)
                    {
                        // Cancellation callbacks, original service work and editor callbacks have quiesced.
                        // The coordinator and epochClose retain the native fault independently of the timer.
                        if (lease != null) RetireLease();
                        if (leaseCleanupFailed) throw new InvalidOperationException("Resource cleanup incomplete.");
                    }
                    // Closing still fences admission while the dispatcher publishes the phase.
                    if (!producers.IsCompletedSuccessfully) throw new QuiescentEpochException("Epoch callbacks failed.");
                    if (!certificate.Owner.EpochWorkFinalized) throw new QuiescentEpochException("Native editor work remains open.");
                    if (certificate.Completion.IsCompletedSuccessfully && !certificate.Owner.ResumeGui(certificate) &&
                        certificate.Owner.Phase == EditorControlPhase.Releasing)
                        throw new InvalidOperationException("Editor drain incomplete.");
                    return true;
                }).ConfigureAwait(false);
            }
            lock (sync)
            {
                if (leaseCleanupFailed) throw new InvalidOperationException("Resource cleanup incomplete.");
                if (!producers.IsCompletedSuccessfully) throw new QuiescentEpochException("Epoch callbacks failed.");
                epochClose = null;
                completion.TrySetResult();
            }
        }
        catch (Exception error)
        {
            // No successful drain or readmission after failed cleanup.
            lock (sync) epochClose = completion;
            completion.TrySetException(error is QuiescentEpochException ? error : new InvalidOperationException("Epoch close failed."));
        }
    }

    private void RetireLease()
    {
        lease!.Lifetime.Dispose();
        StopLeaseTimer();
        lease = null;
    }

    private void StopLeaseTimer()
    {
        if (leaseTimer == null) return;
        leaseTimer.Stop();
        leaseTimer.Tick -= TickLease;
        leaseTimer = null;
    }
    // A tick is only a wakeup. Recheck the current binding and absolute deadline.
    // Publish the decided workspace state on its dispatcher turn, outside the registry monitor.
    private bool workspaceChanged;
    private void MarkWorkspaceChanged() => workspaceChanged = true;
    private void PublishWorkspace()
    {
        if (!workspaceChanged || Monitor.IsEntered(sync)) return;
        workspaceChanged = false;
        EditorCoordinator.NotifyWorkspaceChanged();
    }

    private void TickLease(object? sender, EventArgs e)
    {
        try { lock (sync) CheckLease(); }
        finally { PublishWorkspace(); }
    }
    private void CheckLease()
    {
        if (lease is not { } current) return;
        if (current.Slot.Reason == null)
        {
            var reason = stopping ? ReleaseReason.Shutdown : epoch != current.Slot.Epoch ? ReleaseReason.Revoked :
                !editorBridge.IsLiveRegistration(current.Slot.Editor) ? ReleaseReason.Detached :
                leaseCleanupFailed || EditorCoordinator.Phase != EditorControlPhase.Mcp ? ReleaseReason.Reclaimed :
                ReadTime() is not { } now || now >= current.Deadline ? ReleaseReason.Expired : (ReleaseReason?)null;
            if (reason is not null) EndLease(current, reason.Value);
        }
        if (!leaseCleanupFailed && epochClose == null && current.Slot.Reason != null && current.CancelledCallbacks.IsCompletedSuccessfully && current.Drain is { Completion.IsCompletedSuccessfully: true } drain &&
            active == null && editCompletion == null && fileDialogCompletion == null && fileStepCompletion == null && saveWork == null &&
            EditorCoordinator.ResumeGui(drain, MarkWorkspaceChanged))
        {
            RetireLease();
        }
    }

    private void RevokeLease(ActiveLease original, ReleaseReason reason)
    {
        if (original.Slot.Reason.HasValue) return;
        original.Slot.Reason = reason;
        original.CancelledCallbacks = original.Lifetime.CancelAsync();
        if (active is { Caller.Source: CallerSource.Mcp } job) RequestCancel(job, CancellationReason.User);
    }

    private void EndLease(ActiveLease original, ReleaseReason reason)
    {
        if (original.Slot.Reason.HasValue) return;
        RevokeLease(original, reason);
        original.Drain = EditorCoordinator.BeginDrain(MarkWorkspaceChanged);
    }

    internal void ReclaimEditor()
    {
        Dispatcher.UIThread.VerifyAccess();
        try { lock (sync) { if (lease != null) EndLease(lease, ReleaseReason.Reclaimed); CheckLease(); } }
        finally { PublishWorkspace(); }
    }

    private AutomationErrorCode? V2Access(AutomationCaller caller, IRequestV2 request, string grant, Guid? instance = null)
    {
        if (caller.Source != CallerSource.Mcp || caller.PrincipalId is not { Length: > 0 and <= 128 } || !Authorized(caller, grant))
            return AutomationErrorCode.Forbidden;
        if (request.ContractVersion != "2") return AutomationErrorCode.UnsupportedContractVersion;
        try { AutomationJson.Validate(request); } catch (System.Text.Json.JsonException) { return AutomationErrorCode.InvalidArgument; }
        return instance.HasValue && instance.Value != InstanceId ? AutomationErrorCode.InstanceMismatch : null;
    }

    private AutomationErrorCode? CheckBinding(AutomationCaller caller, Guid editor, Guid id)
    {
        CheckLease();
        if (lease is not { } current || current.Slot.Epoch != caller.ActivationEpoch || epoch != caller.ActivationEpoch ||
            current.Slot.Principal != caller.PrincipalId || current.Slot.Editor != editor || current.Slot.LeaseId != id)
        {
            if (epoch != caller.ActivationEpoch) return AutomationErrorCode.Forbidden;
            var slot = ProcessAcquisitions.Shared.Find(InstanceId, caller, new("2", InstanceId, editor, id));
            return slot?.Reason switch
            {
                ReleaseReason.Expired => AutomationErrorCode.LeaseExpired,
                null => AutomationErrorCode.LeaseNotFound,
                _ => AutomationErrorCode.LeaseRevoked
            };
        }
        return current.Slot.Reason switch { null => null, ReleaseReason.Expired => AutomationErrorCode.LeaseExpired, _ => AutomationErrorCode.LeaseRevoked };
    }

    private LeaseReplyV2<T> LeaseResult<T>(T? result, AutomationErrorCode? error, bool replay = false) where T : class =>
        new("2", InstanceId, result, error.HasValue ? SafeError.From(error.Value) : null, replay,
            error.HasValue ? RetryPolicy.NoAutomaticRetry : RetryPolicy.SameRequestOnly, error.HasValue);

    public Task<LeaseReplyV2<Acquired>> AcquireEditorAsync(AutomationCaller caller, AcquireEditorV2 request, CancellationToken ct) =>
        OnEditorUiAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (sync)
            {
                var denied = V2Access(caller, request, AutomationPermissions.EditorControl, request.InstanceId);
                if (denied.HasValue || !Authorized(caller, AutomationPermissions.EditorWrite))
                    return LeaseResult<Acquired>(null, denied ?? AutomationErrorCode.Forbidden);
                CheckLease();
                if (!Authorized(caller, AutomationPermissions.EditorControl)) return LeaseResult<Acquired>(null, AutomationErrorCode.Forbidden);
                var (slot, replay, conflict) = ProcessAcquisitions.Shared.Reserve(InstanceId, caller, request);
                if (conflict.HasValue) return LeaseResult<Acquired>(null, conflict, replay);
                if (replay)
                {
                    if (lease is { } owned && owned.Slot == slot)
                        return LeaseResult(owned.Receipt with { LeaseState = slot!.Reason == null ? LeaseState.Active : LeaseState.Releasing }, null, true);
                    return LeaseResult<Acquired>(null, slot!.Error ?? AutomationErrorCode.AcquisitionRetired, true);
                }
                LeaseReplyV2<Acquired> Reject(AutomationErrorCode code) => LeaseResult<Acquired>(null, slot!.Error = code);
                if (lease != null) return Reject(AutomationErrorCode.EditorOwned);
                var target = editorBridge.Find(request.EditorId);
                if (target == null) return Reject(AutomationErrorCode.EditorUnavailable);
                if (fileDialogCompletion != null || fileStepCompletion != null || saveWork != null || active != null || editCompletion != null ||
                    EditorCoordinator.Pending != 0 || !EditorCoordinator.CanTakeControl) return Reject(AutomationErrorCode.Busy);
                target.ObserveText();
                if (target.Revision.ToString(CultureInfo.InvariantCulture) != request.ExpectedRevision) return Reject(AutomationErrorCode.RevisionConflict);
                try
                {
                    using var reservation = InterpreterHostQueue.Reserve();
                    if (ReadTime() is not { } now) return Reject(AutomationErrorCode.TimerUnavailable);
                    var accepted = clock.GetUtcNow();
                    var receipt = new Acquired(AcquiredKind.Acquired, request.AcquisitionId, request.EditorId, slot!.AssignedId,
                        request.ExpectedRevision, "1", accepted, accepted.AddMinutes(10), LeaseState.Active);
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    timer.Tick += TickLease;
                    try
                    {
                        try { timer.Start(); }
                        catch { return Reject(AutomationErrorCode.TimerUnavailable); }
                        if (!Authorized(caller, AutomationPermissions.EditorControl)) return Reject(AutomationErrorCode.Forbidden);
                        target.ObserveText();
                        if (target.Id != request.EditorId || target.Revision.ToString(CultureInfo.InvariantCulture) != request.ExpectedRevision)
                            return Reject(AutomationErrorCode.RevisionConflict);
                        editorBridge.TakeControl(target);
                        lease = new ActiveLease(slot!, receipt, now + Duration);
                        slot!.LeaseId = receipt.LeaseId;
                        slot.Error = null;
                        leaseTimer = timer;
                    }
                    finally { if (leaseTimer != timer) { timer.Stop(); timer.Tick -= TickLease; } }
                    MarkWorkspaceChanged();
                    return LeaseResult(receipt, null);
                }
                catch (ExecutionBusyException) { return Reject(AutomationErrorCode.Busy); }
            }
        });

    public Task<LeaseReplyV2<Renewed>> RenewEditorAsync(AutomationCaller caller, RenewEditorV2 request, CancellationToken ct) =>
        OnEditorUiAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (sync)
            {
                var error = V2Access(caller, request, AutomationPermissions.EditorControl, request.InstanceId);
                if (error.HasValue || !Authorized(caller, AutomationPermissions.EditorWrite))
                    return LeaseResult<Renewed>(null, error ?? AutomationErrorCode.Forbidden);
                if (CheckBinding(caller, request.EditorId, request.LeaseId) is { } denied) return LeaseResult<Renewed>(null, denied);
                var current = lease!;
                if (current.LastRenew?.InputLeaseVersion == request.ExpectedLeaseVersion) return LeaseResult(current.LastRenew, null, true);
                if (current.Version.ToString(CultureInfo.InvariantCulture) != request.ExpectedLeaseVersion)
                    return LeaseResult<Renewed>(null, AutomationErrorCode.LeaseVersionConflict);
                if (current.Version == ulong.MaxValue) return LeaseResult<Renewed>(null, AutomationErrorCode.VersionExhausted);
                var accepted = clock.GetUtcNow();
                if (CheckBinding(caller, request.EditorId, request.LeaseId) is { } stale) return LeaseResult<Renewed>(null, stale);
                var receipt = new Renewed(RenewedKind.Renewed, request.EditorId, request.LeaseId, request.ExpectedLeaseVersion,
                    (current.Version + 1).ToString(CultureInfo.InvariantCulture), accepted, accepted.AddMinutes(10));
                current.Version++;
                current.Deadline = lastLeaseTime + Duration;
                current.LastRenew = receipt;
                return LeaseResult(receipt, null);
            }
        });

    public Task<LeaseReplyV2<Released>> ReleaseEditorAsync(AutomationCaller caller, ReleaseEditorV2 request, CancellationToken ct) =>
        OnEditorUiAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (V2Access(caller, request, AutomationPermissions.EditorControl, request.InstanceId) is { } error)
                    return LeaseResult<Released>(null, error);
                CheckLease();
                if (!Authorized(caller, AutomationPermissions.EditorControl)) return LeaseResult<Released>(null, AutomationErrorCode.Forbidden);
                var slot = ProcessAcquisitions.Shared.Find(InstanceId, caller, request);
                if (slot == null) return LeaseResult<Released>(null, AutomationErrorCode.LeaseNotFound);
                bool replay = slot.Reason != null;
                if (lease?.Slot == slot) { EndLease(lease, ReleaseReason.Released); CheckLease(); }
                return LeaseResult(new Released(ReleasedKind.Released, request.EditorId, request.LeaseId,
                    lease?.Slot == slot ? ReleasePhase.Releasing : ReleasePhase.Released, slot.Reason!.Value), null, replay);
            }
        });
}
