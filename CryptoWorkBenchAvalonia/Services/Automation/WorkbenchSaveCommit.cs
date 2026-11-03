using CryptoScript.Model;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaEdit;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Controls;

namespace CryptoWorkBenchAvalonia.Services.Automation;

public sealed partial class WorkbenchAutomationService
{
    // Host-private capability: Sidebar carries it unchanged through all picker
    // awaits. Neither the snapshot nor the approval is a transport argument.
    internal sealed record SaveIntent(EditorBridge.Registration Registration, EditorSnapshot Snapshot, long WorkspaceGeneration);
    internal sealed record SaveTarget(string Path, bool OverwriteConfirmed, Func<Task>? Delete, IDisposable? ProviderItem = null);
    private TaskCompletionSource? saveCompletion;
    private TaskCompletionSource? fileDialogCompletion;

    internal SaveIntent? CaptureSaveIntent()
    {
        Dispatcher.UIThread.VerifyAccess();
        lock (sync)
        {
            if (stopping || GuiOwnsSlotOrDraining || InPresentationObserver || IsBusy || editorBridge.Current is not { } registration) return null;
            return new(registration, registration.Snapshot(), EditorCoordinator.WorkspaceGeneration);
        }
    }

    internal bool IsFileIntentCurrent(SaveIntent intent) =>
        !IsStopping && EditorCoordinator.WorkspaceGeneration == intent.WorkspaceGeneration &&
        editorBridge.Matches(intent.Registration, intent.Snapshot);

    internal async Task<AutomationErrorCode?> CommitSaveAsync(SaveIntent intent, SaveTarget target, CancellationToken ct = default)
    {
        IDisposable? admission = null;
        IDisposable? mutationLock = null;
        EditorCoordinator.OperationTicket? ticket = null;
        TaskCompletionSource? completion = null;
        try
        {
            var error = await OnEditorUiAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                lock (sync)
                {
                    if (stopping) return AutomationErrorCode.ShuttingDown;
                    if (GuiOwnsSlotOrDraining || InPresentationObserver) return AutomationErrorCode.Busy;
                    if (target.Delete is not null && !target.OverwriteConfirmed) return AutomationErrorCode.ConfirmationDenied;
                    try { admission = InterpreterHostQueue.Reserve(); }
                    catch (ExecutionBusyException) { return AutomationErrorCode.Busy; }
                    if (!editorBridge.IsLive(intent.Registration, intent.Snapshot.EditorId)) return AutomationErrorCode.EditorUnavailable;
                    if (!IsFileIntentCurrent(intent)) return AutomationErrorCode.RevisionConflict;
                    // Publish ownership before Lock changes IsReadOnly: its
                    // synchronous observers can reenter Shutdown/Drain/Dispose.
                    ticket = EditorCoordinator.TryBegin();
                    if (ticket == null) return AutomationErrorCode.Busy;
                    completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    saveCompletion = completion;
                    try { mutationLock = EditorBridge.Lock(intent.Registration); }
                    catch (ExecutionBusyException) { return AutomationErrorCode.Busy; }
                    ct.ThrowIfCancellationRequested();
                    if (stopping) return AutomationErrorCode.ShuttingDown;
                    if (!editorBridge.IsLive(intent.Registration, intent.Snapshot.EditorId)) return AutomationErrorCode.EditorUnavailable;
                    if (!IsFileIntentCurrent(intent)) return AutomationErrorCode.RevisionConflict;
                    // This is the commit's cancellation/shutdown boundary. The
                    // reservation and document lease are now service-owned.
                    return (AutomationErrorCode?)null;
                }
            });
            if (error.HasValue) return error;
            // No cancellation checks after this point: Delete must be followed
            // by the write attempt even when Shutdown starts during DeleteAsync.
            if (target.Delete is not null) await target.Delete().ConfigureAwait(false);
            await Task.Run(() => WriteSnapshot(target.Path, intent.Snapshot.Text)).ConfigureAwait(false);
            // ASCII/filtering/append is not a proven lossless baseline. In
            // particular, never clear IsModified or reset the dirty revision.
            return null;
        }
        finally
        {
            try
            {
                if (ticket is not null)
                    await OnEditorUiAsync(() => { try { mutationLock?.Dispose(); } finally { ticket.Dispose(); } return true; }).ConfigureAwait(false);
            }
            finally
            {
                lock (sync)
                {
                    admission?.Dispose();
                    if (ReferenceEquals(saveCompletion, completion)) saveCompletion = null;
                    completion?.TrySetResult();
                }
            }
        }
    }

    // Existing local synchronous Append API. No await/dialog can interleave with
    // capture/commit; it uses exactly the same admission, document lock and writer.
    internal void SaveScriptBook(TextEditor editor, string path)
    {
        Dispatcher.UIThread.VerifyAccess();
        lock (sync)
        {
            if (stopping || GuiOwnsSlotOrDraining || InPresentationObserver ||
                (editorBridge.Registered && !ReferenceEquals(editorBridge.Current?.Editor, editor))) throw new ExecutionBusyException();
            using var admission = InterpreterHostQueue.Reserve();
            // Legacy synchronous callers may supply a plain TextEditor (as the
            // existing Return/Save regression harness does). With no await or
            // callbacks between capture and write there is no UI interleaving.
            // The productive view always supplies WorkbenchTextEditor.
            var document = editor.Document;
            string snapshot = document.Text;
            var registration = editorBridge.Current;
            var bound = registration is not null && ReferenceEquals(registration.Editor, editor) ? registration.Snapshot() : null;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            saveCompletion = completion;
            try
            {
                using var mutation = (editor as WorkbenchTextEditor)?.LockMutations();
                if (stopping || !ReferenceEquals(editor.Document, document) ||
                    (bound is not null && !editorBridge.Matches(registration!, bound))) throw new ExecutionBusyException();
                WriteSnapshot(path, snapshot);
            }
            finally
            {
                saveCompletion = null;
                admission.Dispose();
                completion.TrySetResult();
            }
        }
    }

    private static void WriteSnapshot(string path, string text)
    {
        using var stream = File.Open(path, FileMode.Append);
        using var lines = new StringReader(text);
        while (lines.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            byte[] bytes = Encoding.ASCII.GetBytes(line);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte((byte)'\n');
        }
    }

    internal async Task WithFileDialogAsync(EditorBridge.Registration origin, Func<SaveIntent, Task> action)
    {
        var intent = CaptureSaveIntent();
        if (intent == null || !ReferenceEquals(intent.Registration, origin)) return;
        using var ticket = EditorCoordinator.TryBegin();
        if (ticket == null) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (sync)
        {
            if (stopping || fileDialogCompletion != null) return;
            fileDialogCompletion = completion;
        }
        try { await action(intent); }
        finally
        {
            try { ticket.Dispose(); }
            finally { lock (sync) { fileDialogCompletion = null; completion.TrySetResult(); } }
        }
    }
}
