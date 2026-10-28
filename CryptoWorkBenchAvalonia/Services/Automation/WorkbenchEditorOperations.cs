using CryptoScript.Model;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaEdit;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBench.Automation.Abstractions;

namespace CryptoWorkBenchAvalonia.Services.Automation;

public sealed partial class WorkbenchAutomationService
{
    private readonly EditorBridge editorBridge = new();
    internal EditorCoordinator EditorCoordinator
    {
        get { Dispatcher.UIThread.VerifyAccess(); return editorBridge.Coordinator ?? new EditorCoordinator(editorBridge); }
    }
    internal IEditorReplaceConfirmation ReplaceConfirmation { get; set; } = new EditorReplaceConfirmation();

    private static async Task<T> OnEditorUiAsync<T>(Func<T> action) =>
        Dispatcher.UIThread.CheckAccess() ? action() : await Dispatcher.UIThread.InvokeAsync(action);

    internal EditorBridge.Registration? AttachEditor(TextEditor editor, IEditorExecutionPresentation presentation)
    {
        Dispatcher.UIThread.VerifyAccess();
        lock (sync)
        {
            if (stopping) return null;
            _ = EditorCoordinator;
            return editorBridge.Attach(editor, presentation);
        }
    }
    internal void DetachEditor(EditorBridge.Registration registration) => editorBridge.Detach(registration);
    internal bool IsEditorLive(EditorBridge.Registration registration, Guid id) => editorBridge.IsLive(registration, id);

    private AutomationErrorCode? CheckEditor(AutomationCaller caller, string permission, Guid? instance = null)
    {
        if (!Authorized(caller, permission)) return AutomationErrorCode.Forbidden;
        if (instance.HasValue && instance != InstanceId) return AutomationErrorCode.InstanceMismatch;
        if (stopping) return AutomationErrorCode.ShuttingDown;
        return null;
    }

    public Task<Reply<EditorSnapshot>> GetEditorAsync(AutomationCaller caller, CancellationToken ct) => OnEditorUiAsync(() =>
    {
        ct.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (CheckEditor(caller, AutomationPermissions.EditorRead) is { } error) return Fail<EditorSnapshot>(error);
            if (editorBridge.Current is not { } registration) return Fail<EditorSnapshot>(AutomationErrorCode.EditorUnavailable);
            if (registration.Document.TextLength > AutomationContracts.MaxTextLength) return Fail<EditorSnapshot>(AutomationErrorCode.LimitExceeded);
            return Ok(registration.Snapshot());
        }
    });

    public async Task<Reply<EditorMutation>> UpdateEditorAsync(AutomationCaller caller, EditorEdit request, CancellationToken ct)
    {
        IDisposable? reservation = null;
        EditorBridge.Registration? registration = null;
        EditorSnapshot? before = null;
        try
        {
            var initial = await OnEditorUiAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                lock (sync)
                {
                    if (CheckEditor(caller, AutomationPermissions.EditorWrite, request.InstanceId) is { } error) return error;
                    try { AutomationJson.Validate(request); }
                    catch (JsonException) { return AutomationErrorCode.InvalidArgument; }
                    registration = editorBridge.Current;
                    if (registration is null || registration.Id != request.EditorId) return AutomationErrorCode.EditorUnavailable;
                    if (registration.Document.IsInUpdate) return AutomationErrorCode.Busy;
                    before = registration.Snapshot();
                    if (before.Revision != request.ExpectedRevision) return AutomationErrorCode.RevisionConflict;
                    if (request.Mode == EditorEditMode.Insert && !AutomationContracts.IsValidInsertionOffset(before.Text, request.Offset!.Value))
                        return AutomationErrorCode.InvalidRange;
                    if ((long)(request.Mode == EditorEditMode.Replace ? 0 : before.Length) + request.Text.Length > AutomationContracts.MaxTextLength)
                        return AutomationErrorCode.LimitExceeded;
                    try { reservation = InterpreterHostQueue.Reserve(); }
                    catch (ExecutionBusyException) { return AutomationErrorCode.Busy; }
                    return (AutomationErrorCode?)null;
                }
            });
            if (initial.HasValue) return Fail<EditorMutation>(initial.Value);
            bool noOp = request.Mode == EditorEditMode.Replace ? before!.Text == request.Text : request.Text.Length == 0;
            if (!noOp && request.Mode == EditorEditMode.Replace && (before!.Length != 0 || before.DirtyState != DirtyState.Clean))
            {
                bool? confirmed;
                try { confirmed = await ReplaceConfirmation.ConfirmAsync(registration!.Editor,
                    new(caller.PrincipalId, InstanceId, before, request), ct); }
                catch (OperationCanceledException) { throw; }
                catch { return Fail<EditorMutation>(AutomationErrorCode.ConfirmationUnavailable); }
                if (confirmed != true) return Fail<EditorMutation>(confirmed == false
                    ? AutomationErrorCode.ConfirmationDenied : AutomationErrorCode.ConfirmationUnavailable);
            }
            return await OnEditorUiAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                lock (sync)
                {
                    if (CheckEditor(caller, AutomationPermissions.EditorWrite, request.InstanceId) is { } error) return Fail<EditorMutation>(error);
                    if (!editorBridge.IsLive(registration!, request.EditorId)) return Fail<EditorMutation>(AutomationErrorCode.EditorUnavailable);
                    if (!editorBridge.Matches(registration!, before!)) return Fail<EditorMutation>(AutomationErrorCode.RevisionConflict);
                    if (registration!.Document.IsInUpdate) return Fail<EditorMutation>(AutomationErrorCode.Busy);
                    EditorSnapshot? after = null;
                    if (noOp) after = registration.Snapshot();
                    else
                    {
                        var doc = registration.Document;
                        var originalVersion = doc.Version;
                        try
                        {
                            var guarded = registration.Editor as WorkbenchTextEditor
                                ?? throw new InvalidOperationException("Editor does not support an identity lease.");
                            using var identity = guarded.LockIdentity();
                            Exception? callbackFailure = null;
                            try
                            {
                                using (doc.RunUpdate())
                                {
                                    guarded.AllowSingleEditChange();
                                    if (request.Mode == EditorEditMode.Replace) doc.Replace(0, doc.TextLength, request.Text);
                                    else doc.Insert(request.Mode == EditorEditMode.Append ? doc.TextLength : request.Offset!.Value, request.Text);
                                }
                            }
                            catch (Exception callbackError) { callbackFailure = callbackError; }
                            finally
                            {
                                // Changed/UpdateFinished can throw after the text
                                // effect. Close any still-open library update;
                                // never undo an already applied document change.
                                if (doc.IsInUpdate) doc.EndUpdate();
                            }
                            if (ReferenceEquals(originalVersion, doc.Version))
                                return Fail<EditorMutation>(callbackFailure is ExecutionBusyException
                                    ? AutomationErrorCode.Busy : AutomationErrorCode.InternalError);
                            try { registration.Editor.IsModified = true; }
                            catch (Exception callbackError) { callbackFailure = callbackError; }
                            after = registration.Snapshot();
                        }
                        catch (Exception)
                        {
                            // Deferred lifecycle callbacks run only after the
                            // original response has been copied. Their failure
                            // cannot transfer the result to a successor or erase
                            // the already applied edit from its response.
                            if (after is null) return Fail<EditorMutation>(AutomationErrorCode.InternalError);
                        }
                    }
                    return Ok(new EditorMutation(request.EditorId, before!.Revision, after!.Revision, after.Length, after.DirtyState));
                }
            });
        }
        finally { reservation?.Dispose(); }
    }

    public async Task<Reply<ExecutionResult>> ExecuteEditorAsync(AutomationCaller caller, EditorExecution request, CancellationToken requestCancellation)
    {
        requestCancellation.ThrowIfCancellationRequested();
        lock (sync)
        {
            // Preserve the existing unavailable-editor error for the host with
            // no editor. An available editor still never opens external execution
            // before MCP-01.5, even with an authenticated execution.run grant.
            if (caller.Source != CallerSource.Gui)
                return Fail<ExecutionResult>(Authorized(caller, AutomationPermissions.ExecutionRun) && !editorBridge.Available
                    ? AutomationErrorCode.EditorUnavailable : AutomationErrorCode.Forbidden);
        }
        Job? accepted = null;
        var reply = await OnEditorUiAsync(() =>
        {
            requestCancellation.ThrowIfCancellationRequested();
            lock (sync)
            {
                // External execution remains closed until MCP-01.5 supplies local
                // execution approval and authenticated credentials. Grants alone do not suffice.
                if (!Authorized(caller, AutomationPermissions.ExecutionRun) || caller.Source != CallerSource.Gui)
                    return Fail<ExecutionResult>(AutomationErrorCode.Forbidden);
                if (request.InstanceId != InstanceId) return Fail<ExecutionResult>(AutomationErrorCode.InstanceMismatch);
                try { AutomationJson.Validate(request); }
                catch (JsonException) { return Fail<ExecutionResult>(AutomationErrorCode.InvalidArgument); }
                if (request.ExecutionId == Guid.Empty) return Fail<ExecutionResult>(AutomationErrorCode.InvalidArgument);
                if (request.OutputPolicy == OutputPolicy.IncludeText) return Fail<ExecutionResult>(AutomationErrorCode.Forbidden);
                string fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request with
                    { TimeoutMs = request.TimeoutMs ?? AutomationContracts.DefaultTimeoutMs, OutputPolicy = OutputPolicy.MetadataOnly }, AutomationJson.Options)));
                Prune();
                if (tombstones.TryGetValue(request.ExecutionId, out var owner)) return Fail<ExecutionResult>(SameOwner(caller, owner)
                    ? AutomationErrorCode.ResultExpired : AutomationErrorCode.NotFound);
                if (jobs.TryGetValue(request.ExecutionId, out var previous))
                {
                    if (!Owns(caller, previous)) return Fail<ExecutionResult>(AutomationErrorCode.NotFound);
                    if (previous.Expired) return Fail<ExecutionResult>(AutomationErrorCode.ResultExpired, previous.Id);
                    return previous.Fingerprint == fingerprint ? Ok(previous.Result) : Fail<ExecutionResult>(AutomationErrorCode.InvalidArgument);
                }
                if (stopping) return Fail<ExecutionResult>(AutomationErrorCode.ShuttingDown);
                var registration = editorBridge.Current;
                if (registration is null || registration.Id != request.EditorId) return Fail<ExecutionResult>(AutomationErrorCode.EditorUnavailable);
                if (registration.Document.IsInUpdate) return Fail<ExecutionResult>(AutomationErrorCode.Busy);
                var snapshot = registration.Snapshot();
                if (snapshot.Revision != request.ExpectedRevision) return Fail<ExecutionResult>(AutomationErrorCode.RevisionConflict);
                if (snapshot.Length > AutomationContracts.MaxTextLength) return Fail<ExecutionResult>(AutomationErrorCode.LimitExceeded);
                if (request.ExpectedStateVersion != Version) return Fail<ExecutionResult>(AutomationErrorCode.StateConflict);
                if (jobs.Count + tombstones.Count >= AutomationContracts.MaxExecutionIds) return Fail<ExecutionResult>(AutomationErrorCode.LimitExceeded);
                IDisposable reservation;
                try { reservation = InterpreterHostQueue.Reserve(); }
                catch (ExecutionBusyException) { return Fail<ExecutionResult>(AutomationErrorCode.Busy); }
                accepted = Accept(reservation, caller, request.ExecutionId, fingerprint, ExecutionSource.Editor,
                    request.TimeoutMs ?? AutomationContracts.DefaultTimeoutMs, requestCancellation, default, async pipeline =>
                {
                    using var password = new PasswordDialogHost();
                    bool ContinueEditor()
                    {
                        if (editorBridge.IsLive(registration, snapshot.EditorId)) return true;
                        lock (sync)
                        {
                            RequestCancel(accepted!, CancellationReason.User);
                            accepted!.Outcome = ExecutionState.Cancelled;
                        }
                        return false;
                    }
                    if (!await OnEditorUiAsync(() =>
                    {
                        if (!ContinueEditor()) return false;
                        registration.Presentation?.BeginExecution();
                        return ContinueEditor();
                    }).ConfigureAwait(false)) return false;
                    return await ExecuteScriptBookAsync(pipeline, snapshot.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'),
                        password.RequestAsync,
                        (line, _) => OnEditorUiAsync(() =>
                        {
                            if (!ContinueEditor()) return false;
                            registration.Presentation?.Begin(line);
                            return ContinueEditor();
                        }),
                        (line, number, result) => OnEditorUiAsync(() =>
                        {
                            // Suppress stale presentation. A subsequent parse
                            // unit is stopped by beforeLine; a successfully
                            // finished final unit keeps its completed outcome.
                            if (!editorBridge.IsLive(registration, snapshot.EditorId)) return true;
                            if (result is null) return true;
                            bool success = registration.Presentation?.Present(result, number) ?? true;
                            if (!editorBridge.IsLive(registration, snapshot.EditorId)) return true;
                            string output = registration.Presentation?.Output ?? string.Empty;
                            if (output.Length != 0) registration.Document.Insert(registration.Document.TextLength, "\n" + output);
                            return success;
                        }), pipeline.ExecutionToken).ConfigureAwait(false);
                }).Job;
                accepted.Result = accepted.Result with { EditorId = snapshot.EditorId, SourceRevision = snapshot.Revision };
                return (Reply<ExecutionResult>?)null;
            }
        });
        if (reply is not null) return reply;
        await accepted!.Task.WaitAsync(requestCancellation).ConfigureAwait(false);
        lock (sync) return accepted.Expired ? Fail<ExecutionResult>(AutomationErrorCode.ResultExpired, accepted.Id) : Ok(accepted.Result);
    }
}
