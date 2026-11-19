using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CryptoWorkBenchAvalonia.ViewModels;

namespace CryptoWorkBenchAvalonia.Services.Inspector;

// UI-owned state; never reads the interpreter from a property observer.
internal sealed class InspectorSession : IDisposable
{
    private readonly VariableViewModel variables;
    private long request;
    private bool posted, disposed;
    private CancellationTokenSource? cancellation;
    private Task pending = Task.CompletedTask;
    private SelectionHandle? handle;
    private object? context;
    private readonly System.Collections.Generic.List<InspectorSnapshot> cleanup = new();
    private string? snapshotCleanupError;
    internal string? CleanupError => snapshotCleanupError ?? variables.Automation.StagingCleanupError;
    internal void RetryCleanup()
    {
        Dispatcher.UIThread.VerifyAccess();
        variables.Automation.RetryStagingCleanup();
        snapshotCleanupError = null;
        for (int index = cleanup.Count - 1; index >= 0; index--)
        {
            try { cleanup[index].Dispose(); cleanup.RemoveAt(index); }
            catch (Exception error) { snapshotCleanupError = error.Message; }
        }
        if (cleanup.Count == 0) variables.Automation.InspectorWorkspaceReset -= RetryCleanup;
    }
    internal InspectorSnapshot? Snapshot { get; private set; }
    internal string? Error { get; private set; }
    internal bool Historical { get; private set; }
    internal InspectorProgress? Progress { get; private set; }
    internal event Action? StateChanged;
    private void Notify() => variables.Automation.EditorCoordinator.Observe(() => StateChanged?.Invoke());
    internal Task Pending => pending;
    internal IDisposable BeginTransfer() => variables.Automation.BeginInspectorTransfer();
    internal bool Loading => cancellation != null;

    private void ReleaseSnapshot()
    {
        var previous = Snapshot; Snapshot = null;
        if (previous != null) DisposeOrRetain(previous);
    }

    private void DisposeOrRetain(InspectorSnapshot snapshot)
    {
        try { snapshot.Dispose(); }
        catch (Exception error)
        {
            if (cleanup.Count == 0) variables.Automation.InspectorWorkspaceReset += RetryCleanup;
            cleanup.Add(snapshot);
            snapshotCleanupError = error.Message;
        }
    }

    private readonly bool propertyInspector;
    internal InspectorSession(VariableViewModel variables, bool propertyInspector = false)
    {
        Dispatcher.UIThread.VerifyAccess(); this.variables = variables; this.propertyInspector = propertyInspector;
        variables.PropertyChanged += Changed;
        variables.Automation.EditorCoordinator.WorkspaceChanged += Invalidated;
        variables.Automation.InspectorWorkspaceReset += Reset;
        variables.Automation.InspectorInputInvalidated += Invalidated;
        context = variables.Automation.InspectorContext;
        Schedule();
    }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(VariableViewModel.SelectedVariable)) Schedule();
        else if (args.PropertyName == nameof(VariableViewModel.IsSnapshotStale) && variables.IsSnapshotStale) Invalidated();
    }
    private void Invalidated()
    {
        request++; handle = null; Historical = true;
        if (cancellation != null) _ = cancellation.CancelAsync();
        var next = variables.Automation.InspectorContext;
        InspectorSnapshot? retired = null;
        if (!ReferenceEquals(context, next))
        {
            WorkspaceReset?.Invoke();
            context = next;
            retired = Snapshot;
            Snapshot = null;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (retired != null) DisposeOrRetain(retired);
            if (!disposed) Notify();
        });
    }
    private void Schedule()
    {
        request++;
        if (cancellation != null) _ = cancellation.CancelAsync();
        if (posted || disposed) return;
        posted = true;
        Dispatcher.UIThread.Post(async () =>
        {
            posted = false;
            long selectedRequest = request;
            await pending;
            if (disposed || selectedRequest != request) return;
            Error = null;
            ReleaseSnapshot();
            handle = variables.InspectorSelection();
            Historical = false;
            Notify();
            if (handle != null)
            {
                Snapshot = new(handle.Stamp, variables.Automation.BeginInspectorTransfer, DisposeOrRetain);
                // Establish identity and length only. Selection never copies an entire potentially large value.
                await ReadAsync(propertyInspector ? new(InspectorMember.PropertyPage, Property: new([])) : new(InspectorMember.Text, Count: 0));
            }
        });
    }
    internal Task ReadAsync(ReadSpec spec)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (disposed || handle == null || !pending.IsCompleted) return Task.CompletedTask;
        var source = cancellation = new();
        var selection = handle;
        long id = request;
        Progress = null;
        pending = Capture();
        return pending;
        async Task Capture()
        {
            SnapshotPart? part = null;
            IDisposable? transfer = null;
            try
            {
                transfer = variables.Automation.BeginInspectorTransfer();
                part = await InspectorCapture.ReadAsync(variables.Automation, selection, spec,
                    () => !disposed && id == request && variables.InspectorSelectionCurrent(selection), source.Token,
                    progress: new Progress<InspectorProgress>(value => { if (!disposed && id == request) { Progress = value; Notify(); } }));
                if (disposed || id != request || !variables.InspectorSelectionCurrent(selection)) return;
                if (InterpreterHostQueue.StateRevision != selection.Stamp.Revision) throw new StaleInspectorSelectionException();
                Snapshot!.Add(part); part = null; Error = null;
            }
            catch (StaleInspectorSelectionException error)
            {
                if (!disposed && id == request) { handle = null; Historical = true; Error = error.Message; }
            }
            catch (Exception e) { if (!disposed && id == request) Error = e.Message; }
            finally
            {
                if (part != null) variables.Automation.RetireStaging(part.Payload);
                transfer?.Dispose();
                if (ReferenceEquals(cancellation, source)) cancellation = null;
                source.Dispose();
                if (!disposed) Notify();
            }
        }
    }
    // Called for an actual workspace/context reset, not generic WorkspaceChanged or layout reset.
    internal void CancelRead()
    {
        Dispatcher.UIThread.VerifyAccess(); request++;
        if (cancellation != null) _ = cancellation.CancelAsync();
    }

    internal event Action? WorkspaceReset;
    internal void Reset()
    {
        Dispatcher.UIThread.VerifyAccess(); WorkspaceReset?.Invoke(); Invalidated(); ReleaseSnapshot();
        if (!disposed) Notify();
    }
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess(); if (disposed) return;
        disposed = true; Reset();
        variables.PropertyChanged -= Changed;
        variables.Automation.EditorCoordinator.WorkspaceChanged -= Invalidated;
        variables.Automation.InspectorWorkspaceReset -= Reset;
        variables.Automation.InspectorInputInvalidated -= Invalidated;
    }
}
