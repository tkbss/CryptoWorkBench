using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaEdit;
using CryptoScript.Model;
using CryptoWorkBench.Automation.Abstractions;

namespace CryptoWorkBenchAvalonia.Services.Automation;

internal sealed record ExecutionApproval(string Principal, EditorSnapshot Snapshot, EditorExecutionV2 Request, string Directory);

public sealed partial class WorkbenchAutomationService
{
    internal Func<TextEditor, ExecutionApproval, CancellationToken, Task<bool?>> ExecutionConfirmation { get; set; } =
        (editor, approval, token) => EditorReplaceConfirmation.ShowAsync(editor, "Execute editor snapshot",
            $"Client: {approval.Principal}\nEditor: {approval.Snapshot.EditorId}   Revision: {approval.Snapshot.Revision}\n" +
            "CryptoScript runs with your desktop permissions. It can read or export files and cryptographic keys.",
            "Script", approval.Snapshot.Text, "Execution context",
            $"Working directory: {approval.Directory}\nState version: {approval.Request.ExpectedStateVersion}\nOutput: {approval.Request.OutputPolicy}",
            "Execute", token);

    public async Task<ResultV2<ExecutionResult>> ExecuteEditorV2Async(AutomationCaller caller, EditorExecutionV2 request, CancellationToken ct)
    {
        Job? accepted = null;
        var reply = await OnEditorUiAsync(() =>
        {
            lock (sync)
            {
                ct.ThrowIfCancellationRequested();
                var entryGrant = Authorized(caller, AutomationPermissions.ExecutionRead) ? AutomationPermissions.ExecutionRead : AutomationPermissions.ExecutionRun;
                if (V2Access(caller, request, entryGrant, request.InstanceId) is { } denied)
                    return Project<ExecutionResult>(null, denied);
                var normalized = request with { TimeoutMs = request.TimeoutMs ?? AutomationContracts.DefaultTimeoutMs,
                    OutputPolicy = request.OutputPolicy ?? OutputPolicy.MetadataOnly };
                string fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(normalized, AutomationJson.Options)));
                Prune();
                var key = new JobKey(caller, request.ExecutionId);
                bool canRead = entryGrant == AutomationPermissions.ExecutionRead;
                if (canRead && (jobs.ContainsKey(key) || tombstones.Contains(key)))
                {
                    var historical = Lookup(caller, request.ExecutionId);
                    if (!historical.Ok) return Project<ExecutionResult>(null, historical.Error!.Code);
                    return jobs[key].Fingerprint == fingerprint ? Project(historical.Data) :
                        Project<ExecutionResult>(null, AutomationErrorCode.IdempotencyConflict);
                }
                if (!Authorized(caller, AutomationPermissions.ExecutionRun) || !Authorized(caller, AutomationPermissions.EditorControl) ||
                    request.OutputPolicy == OutputPolicy.IncludeText && !Authorized(caller, AutomationPermissions.OutputRead))
                    return Project<ExecutionResult>(null, AutomationErrorCode.Forbidden);
                if (CheckBinding(caller, request.EditorId, request.LeaseId) is { } error)
                    return Project<ExecutionResult>(null, canRead ? error : AutomationErrorCode.Forbidden);
                // A run-only caller can create one job but cannot replay it, even with a live lease.
                if (!canRead && (jobs.ContainsKey(key) || tombstones.Contains(key)))
                    return Project<ExecutionResult>(null, AutomationErrorCode.Forbidden);
                if (jobs.Count + tombstones.Count >= AutomationContracts.MaxExecutionIds) return Project<ExecutionResult>(null, AutomationErrorCode.LimitExceeded);
                var target = editorBridge.Find(request.EditorId);
                if (target == null) return Project<ExecutionResult>(null, AutomationErrorCode.EditorUnavailable);
                if (target.Document.TextLength > AutomationContracts.MaxTextLength) return Project<ExecutionResult>(null, AutomationErrorCode.LimitExceeded);
                var snapshot = target.Snapshot();
                if (snapshot.Revision != request.ExpectedRevision) return Project<ExecutionResult>(null, AutomationErrorCode.RevisionConflict);
                if (Version != request.ExpectedStateVersion) return Project<ExecutionResult>(null, AutomationErrorCode.StateConflict);
                var approval = new ExecutionApproval(caller.PrincipalId, snapshot, normalized, Environment.CurrentDirectory);
                var original = lease!;
                bool Admissible() => Authorized(caller, AutomationPermissions.ExecutionRun) && CheckBinding(caller, request.EditorId, request.LeaseId) == null &&
                    EditorCoordinator.Pending == 0 && !EditorCoordinator.Active && EditorCoordinator.ObserverDepth == 0 &&
                    editorBridge.Matches(target, snapshot) && Version == request.ExpectedStateVersion && approval.Directory == Environment.CurrentDirectory;

                try
                {
                    var reservation = InterpreterHostQueue.Reserve();
                    accepted = Accept(reservation, caller, request.ExecutionId, fingerprint, ExecutionSource.Editor, normalized.TimeoutMs!.Value, ct, default,
                        async pipeline =>
                        {
                            var job = current.Value!;
                            void Fence()
                            {
                                lock (sync)
                                {
                                    if (!Authorized(caller, AutomationPermissions.ExecutionRun) || lease != original || original.Slot.Reason != null ||
                                        ReadTime() is not { } now || now >= original.Deadline || approval.Directory != Environment.CurrentDirectory ||
                                        Version != request.ExpectedStateVersion)
                                        RequestCancel(job, CancellationReason.User);
                                    pipeline.ExecutionToken.ThrowIfCancellationRequested();
                                }
                            }
                            pipeline.BeforeRunnerStart = Fence;
                            var answer = await UiDispatch.InvokeAsync(async () =>
                            {
                                try
                                {
                                    lock (sync)
                                        if (!Admissible()) { RequestCancel(job, CancellationReason.User); return false; }
                                }
                                finally { PublishWorkspace(); }
                                return await ExecutionConfirmation(target.Editor, approval, pipeline.ExecutionToken);
                            });
                            if (answer != true)
                            {
                                lock (sync)
                                {
                                    if (job.Reason != null) job.Outcome = ExecutionState.Cancelled;
                                    else if (answer == false) job.Outcome = ExecutionState.Rejected;
                                    else
                                    {
                                        job.Outcome = ExecutionState.Failed;
                                        job.FailureCode = AutomationErrorCode.ConfirmationUnavailable;
                                    }
                                }
                                return false;
                            }
                            if (!await OnEditorUiAsync(() => { lock (sync) return Admissible(); }))
                            {
                                lock (sync) { RequestCancel(job, CancellationReason.User); job.Outcome = ExecutionState.Cancelled; }
                                return false;
                            }
                            lock (sync)
                            {
                                Fence();
                                job.ApprovedOutput = normalized.OutputPolicy == OutputPolicy.IncludeText;
                                job.Result = job.Result with { State = ExecutionState.Running, StartedAt = clock.GetUtcNow() };
                            }
                            using var password = new PasswordDialogHost();
                            return await ExecuteScriptBookAsync(pipeline, snapshot.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'),
                                (sender, args) => { Fence(); return password.RequestAsync(sender, args); },
                                (_, _) => { Fence(); return Task.CompletedTask; }, (_, _, _) => Task.FromResult(true), pipeline.ExecutionToken);
                        }, Admissible).Job;
                    accepted.Result = accepted.Result with { EditorId = request.EditorId, SourceRevision = snapshot.Revision, State = ExecutionState.AwaitingConfirmation, StartedAt = null };
                }
                catch (ExecutionBusyException) { return Project<ExecutionResult>(null, AutomationErrorCode.Busy); }
                return (ResultV2<ExecutionResult>?)null;
            }
        });
        if (reply != null) return reply;
        await accepted!.Task.WaitAsync(ct).ConfigureAwait(false);
        lock (sync)
        {
            if (!Authorized(caller, AutomationPermissions.ExecutionRun)) return Project<ExecutionResult>(null, AutomationErrorCode.Forbidden);
            return accepted.Expired ? Project<ExecutionResult>(null, AutomationErrorCode.ResultExpired) : Project(VisibleResult(caller, accepted));
        }
    }
}
