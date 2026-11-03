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
    // The published operation pins the only target and payload it can commit.
    private sealed record SaveOperation(TextEditor Editor, string Text, SaveTarget Target)
    {
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Accepted;
    }
    private SaveOperation? saveWork;
    private TaskCompletionSource? fileDialogCompletion;
    private TaskCompletionSource? fileStepCompletion;
    internal Func<string, Stream> OpenSaveAppend { get; init; } = path => File.Open(path, FileMode.Append);

    internal SaveIntent? CaptureSaveIntent()
    {
        Dispatcher.UIThread.VerifyAccess();
        var registration = editorBridge.Current;
        if (registration == null) return null;
        long generation = EditorCoordinator.WorkspaceGeneration;
        lock (sync)
            if (stopping || GuiOwnsSlotOrDraining || InPresentationObserver || IsBusy) return null;
        var intent = new SaveIntent(registration, registration.Snapshot(), generation);
        lock (sync) return IsFileIntentCurrent(intent) ? intent : null;
    }

    internal bool IsFileIntentCurrent(SaveIntent intent) =>
        !IsStopping && epochClose == null && EditorCoordinator.WorkspaceGeneration == intent.WorkspaceGeneration &&
        editorBridge.Matches(intent.Registration, intent.Snapshot);

    internal async Task<T?> StartFileIntentStep<T>(SaveIntent intent, Func<Task<T?>> start) where T : class
    {
        Dispatcher.UIThread.VerifyAccess();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (sync)
        {
            if (fileStepCompletion != null || !IsFileIntentCurrent(intent)) return null;
            fileStepCompletion = done; // Accept this specific provider call, including its synchronous prefix.
        }
        try { return await start(); }
        finally { lock (sync) { fileStepCompletion = null; done.TrySetResult(); } }
    }

    // The second check, after IsReadOnly observers, linearizes acceptance against L1.
    private bool AcceptSave(SaveOperation work, SaveIntent? intent, AvaloniaEdit.Document.TextDocument document, CancellationToken ct = default)
    {
        lock (sync)
        {
            ct.ThrowIfCancellationRequested();
            if (!ReferenceEquals(saveWork, work) || work.Accepted || stopping || epochClose != null ||
                !ReferenceEquals(work.Editor.Document, document) || (intent != null && !IsFileIntentCurrent(intent))) return false;
            work.Accepted = true;
            return true;
        }
    }

    private void FinishSave(SaveOperation work, IDisposable admission)
    {
        lock (sync)
        {
            admission.Dispose();
            saveWork = null;
            work.Completion.TrySetResult();
        }
    }

    internal async Task<AutomationErrorCode?> CommitSaveAsync(SaveIntent intent, SaveTarget target, CancellationToken ct = default)
    {
        var work = new SaveOperation(intent.Registration.Editor, intent.Snapshot.Text, target);
        IDisposable? admission = null;
        IDisposable? mutationLock = null;
        EditorCoordinator.OperationTicket? ticket = null;
        Task? deletion = null;
        try
        {
            var error = await OnEditorUiAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                // Coordinator work and property notifications are UI-affine and run outside sync.
                if (IsStopping) return AutomationErrorCode.ShuttingDown;
                if (GuiOwnsSlotOrDraining || InPresentationObserver) return AutomationErrorCode.Busy;
                if (target.Delete != null && !target.OverwriteConfirmed) return AutomationErrorCode.ConfirmationDenied;
                if (!editorBridge.IsLive(intent.Registration, intent.Snapshot.EditorId)) return AutomationErrorCode.EditorUnavailable;
                if (!IsFileIntentCurrent(intent)) return AutomationErrorCode.RevisionConflict;
                ticket = EditorCoordinator.TryBegin();
                if (ticket == null) return AutomationErrorCode.Busy;
                lock (sync)
                {
                    if (!IsFileIntentCurrent(intent)) return AutomationErrorCode.RevisionConflict;
                    try { admission = InterpreterHostQueue.Reserve(); }
                    catch (ExecutionBusyException) { return AutomationErrorCode.Busy; }
                    saveWork = work;
                }
                try { mutationLock = EditorBridge.Lock(intent.Registration); }
                catch (ExecutionBusyException) { return AutomationErrorCode.Busy; }
                if (!AcceptSave(work, intent, ticket.Document, ct))
                {
                    if (IsStopping) return AutomationErrorCode.ShuttingDown;
                    return editorBridge.IsLive(intent.Registration, intent.Snapshot.EditorId)
                        ? AutomationErrorCode.RevisionConflict : AutomationErrorCode.EditorUnavailable;
                }
                deletion = work.Target.Delete?.Invoke();
                return (AutomationErrorCode?)null;
            });
            if (error.HasValue) return error;
            if (deletion != null) await deletion.ConfigureAwait(false);
            await Task.Run(() => AppendSave(work)).ConfigureAwait(false);
            return null;
        }
        finally
        {
            try
            {
                if (ticket != null)
                    await OnEditorUiAsync(() => { try { mutationLock?.Dispose(); } finally { ticket.Dispose(); } return true; }).ConfigureAwait(false);
            }
            finally { if (admission != null) FinishSave(work, admission); }
        }
    }

    internal void SaveScriptBook(TextEditor editor, string path)
    {
        Dispatcher.UIThread.VerifyAccess();
        var document = editor.Document;
        var registration = editorBridge.Current;
        if (editorBridge.Registered && !ReferenceEquals(registration?.Editor, editor)) throw new ExecutionBusyException();
        var intent = registration == null ? null : new SaveIntent(registration, registration.Snapshot(), EditorCoordinator.WorkspaceGeneration);
        var work = new SaveOperation(editor, intent?.Snapshot.Text ?? document.Text, new(path, false, null));
        IDisposable admission;
        lock (sync)
        {
            if (stopping || epochClose != null || GuiOwnsSlotOrDraining || InPresentationObserver) throw new ExecutionBusyException();
            admission = InterpreterHostQueue.Reserve();
            saveWork = work;
        }
        try
        {
            using var mutation = (editor as WorkbenchTextEditor)?.LockMutations();
            if (!AcceptSave(work, intent, document)) throw new ExecutionBusyException();
            AppendSave(work);
        }
        finally { FinishSave(work, admission); }
    }

    private void AppendSave(SaveOperation work)
    {
        using var stream = OpenSaveAppend(work.Target.Path);
        WriteSnapshot(stream, work.Text);
    }

    private static void WriteSnapshot(Stream stream, string text)
    {
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
            if (stopping || epochClose != null || fileDialogCompletion != null) return;
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
