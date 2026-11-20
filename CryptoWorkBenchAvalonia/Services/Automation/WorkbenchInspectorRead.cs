using System;
using System.Collections.Generic;
using System.Threading;
using CryptoWorkBenchAvalonia.Services.Inspector;
using System.Threading.Tasks;
using Avalonia.Threading;
using CryptoScript.Model;
using CryptoWorkBench.Automation.Abstractions;

namespace CryptoWorkBenchAvalonia.Services.Automation;

public sealed partial class WorkbenchAutomationService
{
    // Only failed, unpublished disposals survive the read. No interpreter access is
    // required for retry. This lock is separate from the admission/registry lock.
    private readonly Dictionary<InspectorPayload, string> failedStaging = new();
    internal string? StagingCleanupError
    {
        get { lock (failedStaging) return failedStaging.Count == 0 ? null : string.Join(Environment.NewLine, failedStaging.Values); }
    }
    internal void RetireStaging(InspectorPayload payload)
    {
        try { payload.Dispose(); }
        catch (Exception error) { lock (failedStaging) failedStaging[payload] = error.Message; }
    }
    internal void RetryStagingCleanup()
    {
        lock (failedStaging)
        {
            foreach (var payload in new List<InspectorPayload>(failedStaging.Keys))
            {
                try { payload.Dispose(); failedStaging.Remove(payload); }
                catch (Exception error) { failedStaging[payload] = error.Message; }
            }
        }
    }
    private async Task FinishInspectorShutdownAsync(Task quiescent)
    {
        try { await quiescent.ConfigureAwait(false); }
        finally
        {
            bool retry;
            lock (failedStaging) retry = failedStaging.Count != 0;
            if (retry) await Task.Run(RetryStagingCleanup).ConfigureAwait(false);
        }
    }

    private int inspectorTransfers;
    private TaskCompletionSource? inspectorTransferDrain;
    internal IDisposable BeginInspectorTransfer()
    {
        lock (sync)
        {
            if (stopping) throw new OperationCanceledException("Workbench is stopping.");
            if (inspectorTransfers++ == 0) inspectorTransferDrain = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return new TransferTicket(this);
        }
    }
    private sealed class TransferTicket(WorkbenchAutomationService owner) : IDisposable
    {
        private WorkbenchAutomationService? service = owner;
        public void Dispose()
        {
            var value = Interlocked.Exchange(ref service, null);
            if (value == null) return;
            lock (value.sync) if (--value.inspectorTransfers == 0) value.inspectorTransferDrain!.TrySetResult();
        }
    }
    internal object? InspectorContext => editorBridge.Current?.RawDocument;
    internal object? InspectorDocumentVersion => editorBridge.Current?.RawDocument?.Version;
    internal event Action? InspectorInputInvalidated;
    private void OnInspectorInputChanged()
    {
        lock (sync) if (active is { InspectorRead: true } read) RequestCancel(read, CancellationReason.User);
        InspectorInputInvalidated?.Invoke();
    }
    internal event Action? InspectorWorkspaceReset;
    private void NotifyInspectorReset() => Dispatcher.UIThread.Post(() => InspectorWorkspaceReset?.Invoke());
    // Internal trusted reader only: no pipeline or execution API is exposed to the delegate.
    // The caller publishes detached results only after this task has finalized and revalidated its UI stamp.
    internal Task<T> RunInspectorReadAsync<T>(Func<CancellationToken, Task<T>> read,
        Func<bool> selectionCurrent, CancellationToken cancellation = default, Action? failureCleanup = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        cancellation.ThrowIfCancellationRequested();
        if (InPresentationObserver) throw new ExecutionBusyException();
        lock (sync)
        {
            if (stopping || epochClose != null || active != null || !editorBridge.Registered ||
                !EditorCoordinator.CanStartGui || !selectionCurrent()) throw new ExecutionBusyException();
            var intent = new EditorCoordinator.ExecutionIntent(EditorCoordinator, null);
            try
            {
                var reservation = InterpreterHostQueue.Reserve();
                return Accept(reservation, LocalCaller, Guid.NewGuid(), "inspector-read", ExecutionSource.GuiLine,
                    AutomationContracts.DefaultTimeoutMs, cancellation, cancellation,
                    async pipeline =>
                    {
                        var result = await read(pipeline.ExecutionToken).ConfigureAwait(false);
                        pipeline.ExecutionToken.ThrowIfCancellationRequested();
                        if (!await OnEditorUiAsync(() => intent.Current && selectionCurrent() && !stopping && epochClose == null &&
                            !cancellation.IsCancellationRequested && !pipeline.ExecutionToken.IsCancellationRequested).ConfigureAwait(false))
                            throw new OperationCanceledException("Inspector selection is stale.");
                        return result;
                    }, () => intent.Current && selectionCurrent() && !stopping && epochClose == null, intent, inspectorRead: true,
                    inspectorFailureCleanup: failureCleanup).Work;
            }
            catch { intent.Dispose(); throw; }
        }
    }
}
