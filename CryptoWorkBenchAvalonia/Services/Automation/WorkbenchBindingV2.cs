using System;
using System.Threading;
using System.Threading.Tasks;
using CryptoScript.Model;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Controls;

namespace CryptoWorkBenchAvalonia.Services.Automation;

public sealed partial class WorkbenchAutomationService
{
    private ResultV2<T> Project<T>(T? data, AutomationErrorCode? error = null) where T : class =>
        new("2", InstanceId, data, error.HasValue ? SafeError.From(error.Value) : null);

    public Task<ResultV2<ControlStatus>> GetStatusV2Async(AutomationCaller caller, StatusV2 request, CancellationToken ct) =>
        OnEditorUiAsync(() =>
        {
            lock (sync)
            {
                if (V2Access(caller, request, AutomationPermissions.StatusRead) is { } error) return Project<ControlStatus>(null, error);
                CheckLease();
                var reply = GetStatusAsync(caller, ct).GetAwaiter().GetResult();
                if (!reply.Ok) return Project<ControlStatus>(null, reply.Error!.Code);
                var baseline = reply.Data!;
                var own = lease is { } l && l.Slot.Epoch == caller.ActivationEpoch && l.Slot.Principal == caller.PrincipalId
                    ? new OwnLease(l.Slot.Editor, l.Slot.LeaseId, l.LastRenew?.LeaseVersion ?? "1", l.LastRenew?.ExpiresAt ?? l.Receipt.ExpiresAt) : null;
                return Project(new ControlStatus(baseline.Lifecycle, baseline.McpEnabled, baseline.EditorAvailable, baseline.ExecutionState,
                    baseline.ActiveExecutionId, baseline.MutationBusy, baseline.StateVersion,
                    baseline.LastExecutionId, baseline.Capabilities.Add("editor.exclusiveControl.v2"),
                    editorBridge.Current?.Id, Enum.Parse<ControlPhase>(EditorCoordinator.Phase.ToString()),
                    !EditorCoordinator.CanStartGui, own));
            }
        });

    public Task<ResultV2<VariableSnapshot>> GetVariablesV2Async(AutomationCaller caller, VariablesV2 request, CancellationToken ct)
    {
        lock (sync)
        {
            if (V2Access(caller, request, AutomationPermissions.VariablesMetadataRead, request.InstanceId) is { } denied)
                return Task.FromResult(Project<VariableSnapshot>(null, denied));
            var reply = GetVariablesAsync(caller, ct).GetAwaiter().GetResult();
            return Task.FromResult(Project(reply.Data, reply.Error?.Code));
        }
    }

    public Task<ResultV2<EditorSnapshot>> GetEditorV2Async(AutomationCaller caller, ReadEditorV2 request, CancellationToken ct) =>
        OnEditorUiAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (V2Access(caller, request, AutomationPermissions.EditorRead, request.InstanceId) is { } denied) return Project<EditorSnapshot>(null, denied);
                var target = editorBridge.Find(request.EditorId);
                if (target == null) return Project<EditorSnapshot>(null, AutomationErrorCode.EditorUnavailable);
                if (target.Document.IsInUpdate || EditorCoordinator.Active) return Project<EditorSnapshot>(null, AutomationErrorCode.Busy);
                return target.Document.TextLength > AutomationContracts.MaxTextLength ? Project<EditorSnapshot>(null, AutomationErrorCode.LimitExceeded) : Project(target.Snapshot());
            }
        });

    private ResultV2<T> JobRequest<T>(AutomationCaller caller, JobRequestV2 request, Func<Task<Reply<T>>> query) where T : class
    {
        lock (sync)
        {
            if (V2Access(caller, request, AutomationPermissions.ExecutionRead, request.InstanceId) is { } denied) return Project<T>(null, denied);
            var reply = query().GetAwaiter().GetResult(); // Registry lookup/cancel completes under this same lock.
            return Project(reply.Data, reply.Error?.Code);
        }
    }
    public Task<ResultV2<ExecutionResult>> GetExecutionV2Async(AutomationCaller c, JobRequestV2 r, CancellationToken ct) =>
        Task.FromResult(JobRequest(c, r, () => GetExecutionAsync(c, r.ExecutionId, ct)));
    public Task<ResultV2<DiagnosticSnapshot>> GetDiagnosticsV2Async(AutomationCaller c, JobRequestV2 r, CancellationToken ct) =>
        Task.FromResult(JobRequest(c, r, () => GetDiagnosticsAsync(c, r.ExecutionId, ct)));
    public Task<ResultV2<CancelReceipt>> CancelExecutionV2Async(AutomationCaller c, JobRequestV2 r, CancellationToken ct) =>
        Task.FromResult(JobRequest(c, r, () => CancelExecutionAsync(c, r.ExecutionId, ct)));

    public async Task<EditorMutationReceiptV2> UpdateEditorV2Async(AutomationCaller caller, EditorEditV2 request, CancellationToken ct)
    {
        EditorCoordinator.OperationTicket? ticket = null;
        IDisposable? reservation = null;
        TaskCompletionSource? done = null;
        EditorSnapshot? before = null;
        string? expected = null;
        CancellationToken leaseToken = default;
        EditorMutationReceiptV2 Reject(AutomationErrorCode code) => new("2", InstanceId, request.EditorId, request.ExpectedRevision,
            null, TextEffect.None, HistoryEffect.None, Finalization.Complete, SafeError.From(code), RetryPolicy.NoAutomaticRetry, true);
        AutomationErrorCode? Access() => V2Access(caller, request, AutomationPermissions.EditorWrite, request.InstanceId) ??
            (!Authorized(caller, AutomationPermissions.EditorControl) ? AutomationErrorCode.Forbidden : CheckBinding(caller, request.EditorId, request.LeaseId));
        try
        {
            var denial = await OnEditorUiAsync(() =>
            {
                lock (sync)
                {
                    ct.ThrowIfCancellationRequested();
                    if (Access() is { } denied) return denied;
                    if (EditorCoordinator.Active || EditorCoordinator.ObserverDepth != 0) return AutomationErrorCode.Busy;
                    try { reservation = InterpreterHostQueue.Reserve(); } catch (ExecutionBusyException) { return AutomationErrorCode.Busy; }
                    ticket = EditorCoordinator.TryBeginMcp();
                    if (ticket == null) return AutomationErrorCode.Busy;
                    before = ticket.Registration.Snapshot();
                    if (before.Revision != request.ExpectedRevision) return AutomationErrorCode.RevisionConflict;
                    int offset = request.Mode == EditorEditMode.Insert ? request.Offset!.Value : before.Length;
                    if (!AutomationContracts.IsValidInsertionOffset(before.Text, offset)) return AutomationErrorCode.InvalidRange;
                    if ((long)(request.Mode == EditorEditMode.Replace ? 0 : before.Length) + request.Text.Length > AutomationContracts.MaxTextLength)
                        return AutomationErrorCode.LimitExceeded;
                    expected = request.Mode == EditorEditMode.Replace ? request.Text : before.Text.Insert(offset, request.Text);
                    leaseToken = lease!.Lifetime.Token;
                    editCompletion = done = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    return (AutomationErrorCode?)null;
                }
            });
            if (denial.HasValue) return Reject(denial.Value);
            if (request.Mode == EditorEditMode.Replace && expected != before!.Text && (before.Length != 0 || before.DirtyState != DirtyState.Clean))
            {
                var edit = new EditorEdit(InstanceId, request.EditorId, request.ExpectedRevision, request.Mode, request.Text);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(leaseToken, ct);
                bool? answer;
                try { answer = await ReplaceConfirmation.ConfirmAsync(ticket!.Registration.Editor, new(caller.PrincipalId, InstanceId, before, edit), linked.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Reject(AutomationErrorCode.LeaseRevoked); }
                catch (OperationCanceledException) { throw; }
                catch { return Reject(AutomationErrorCode.ConfirmationUnavailable); }
                if (answer != true) return Reject(answer == false ? AutomationErrorCode.ConfirmationDenied : AutomationErrorCode.ConfirmationUnavailable);
            }
            var original = ticket!;
            var rejected = await OnEditorUiAsync(() =>
            {
                lock (sync)
                {
                    ct.ThrowIfCancellationRequested();
                    if (Access() is { } denied) return Reject(denied);
                    if (!EditorCoordinator.IsCurrent(original)) return Reject(AutomationErrorCode.RevisionConflict);
                    // Publishing active on the original ticket is the one-way admission decision.
                    if (!EditorCoordinator.TryStartCommit(original)) return Reject(original.BodyDone ? AutomationErrorCode.RevisionConflict : AutomationErrorCode.Busy);
                }
                var editor = (WorkbenchTextEditor)original.Registration.Editor;
                var doc = original.Document;
                EditorCoordinator.FinishCommit(original, () =>
                {
                    if (before!.Text == expected) return;
                    using var identity = editor.LockIdentity();
                    AdmissionCheckpoint?.Invoke("edit.accepted");
                    using (doc.RunUpdate())
                    {
                        editor.AllowSingleEditChange();
                        if (request.Mode == EditorEditMode.Replace) doc.Replace(0, doc.TextLength, request.Text);
                        else doc.Insert(request.Offset ?? doc.TextLength, request.Text);
                    }
                    EditorCoordinator.Observe(() => editor.IsModified = true);
                }, expectedText: expected);
                return (EditorMutationReceiptV2?)null;
            });
            if (rejected != null) return rejected;
            var evidence = await original.Completion.ConfigureAwait(false);
            var code = evidence.Error switch
            {
                EditorOperationError.Stale => AutomationErrorCode.RevisionConflict,
                EditorOperationError.CallbackFailed => AutomationErrorCode.CallbackFailed,
                EditorOperationError.FinalizationFailed => AutomationErrorCode.FinalizationFailed,
                EditorOperationError.MutationFailed => AutomationErrorCode.MutationFailed,
                null => (AutomationErrorCode?)null, _ => AutomationErrorCode.InternalError
            };
            return new EditorMutationReceiptV2("2", InstanceId, original.EditorId, original.OriginalRevision, evidence.ObservedRevision,
                (TextEffect)evidence.TextEffect, (HistoryEffect)evidence.HistoryEffect, (Finalization)evidence.Finalization,
                code.HasValue ? SafeError.From(code.Value) : null, RetryPolicy.NoAutomaticRetry, code != null);
        }
        finally
        {
            if (reservation != null || ticket != null)
                await OnEditorUiAsync(() =>
                {
                    ticket?.Dispose();
                    lock (sync)
                    {
                        reservation?.Dispose();
                        if (editCompletion == done) editCompletion = null;
                        done?.TrySetResult();
                        CheckLease();
                        return true;
                    }
                });
        }
    }
}
