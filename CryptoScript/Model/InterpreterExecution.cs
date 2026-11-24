using CryptoScript.Variables;

namespace CryptoScript.Model;

/// <summary>One process admission. Each runner gets a distinct, revocable frame.</summary>
public static class InterpreterExecution
{
    internal static readonly object Gate = new();
    private static readonly AsyncLocal<Frame?> Ambient = new();
    private static readonly AsyncLocal<bool> InHostCallback = new();
    [ThreadStatic] private static Frame? synchronous;
    [ThreadStatic] private static Frame? legacyHostEntry;
    [ThreadStatic] private static Frame? stateAccess;
    private static Frame? owner;

    internal sealed class Frame(VariableDictionary store)
    {
        internal VariableDictionary Store { get; } = store;
        internal int StateThread;
        internal bool IsRunner;
        internal OutputOperations.Observer? Output;
        internal Action<OperationBoundary>? ObserveBoundary;
        internal bool Closing;
        internal TaskCompletionSource? RunnerDrain;
        internal VariableDictionary? TransferDestination;
        internal bool Exporting;
    }

    public static bool IsBusy { get { lock (Gate) return owner != null; } }

    internal static IDisposable EnterStandaloneOperation()
    {
        var store = VariableDictionary.Instance();
        if (Ambient.Value == null) return Enter(store);
        VerifyStore(store);
        VerifyExecution();
        return new Release(() => { });
    }

    public static IDisposable Enter() => Enter(VariableDictionary.Instance());
    public static IDisposable Enter(VariableDictionary store)
    {
        ArgumentNullException.ThrowIfNull(store);
        lock (Gate)
        {
            store.EnsureLive();
            var parent = owner;
            if (InHostCallback.Value || Ambient.Value != parent ||
                parent != null && (parent != synchronous || parent.Store != store || parent.TransferDestination != null))
                throw new ExecutionBusyException();
            var frame = new Frame(store);
            Ambient.Value = synchronous = owner = frame;
            return new Release(() =>
            {
                lock (Gate)
                {
                    if (owner != frame || synchronous != frame || Ambient.Value != frame)
                        throw new ExecutionBusyException();
                    Ambient.Value = synchronous = owner = parent;
                }
            });
        }
    }

    public static Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) =>
        RunHostCore(VariableDictionary.Instance(), _ => action(), false, cancellationToken);

    public sealed class HostAdmission
    {
        internal HostAdmission(Frame frame) => Frame = frame;
        internal Frame Frame { get; }
    }

    public static Task<T> RunAsync<T>(VariableDictionary store, Func<HostAdmission, Task<T>> action,
        CancellationToken cancellationToken = default) => RunHostCore(store, action, true, cancellationToken);

    private static async Task<T> RunHostCore<T>(VariableDictionary store, Func<HostAdmission, Task<T>> action,
        bool requiresAdmission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        var frame = new Frame(store);
        lock (Gate)
        {
            if (owner != null || Ambient.Value != null || InHostCallback.Value) throw new ExecutionBusyException();
            store.EnsureLive();
            owner = frame;
        }
        Ambient.Value = frame;
        try
        {
            Task<T> operation;
            // A tokenless host can designate a runner directly on entry. A flowed task cannot inherit that right.
            var previous = legacyHostEntry;
            try
            {
                if (!requiresAdmission) legacyHostEntry = frame;
                operation = action(new HostAdmission(frame));
            }
            finally { legacyHostEntry = previous; }
            return await operation.ConfigureAwait(false);
        }
        finally
        {
            Task? drain;
            lock (Gate)
            {
                frame.Closing = true;
                drain = frame.RunnerDrain?.Task;
            }
            if (drain != null) await drain.ConfigureAwait(false);
            lock (Gate) { Ambient.Value = null; owner = null; }
        }
    }

    internal static async Task<T> RunRunnerAsync<T>(VariableDictionary store, Func<Task<T>> action, CancellationToken token, HostAdmission? admission = null, OutputOperations.Observer? output = null)
    {
        var parent = Ambient.Value;
        if (parent == null)
        {
            if (admission != null) throw new ExecutionBusyException();
            return await RunAsync(store, host => RunRunnerAsync(store, action, token, host, output), token).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        var frame = new Frame(store) { IsRunner = true, Output = output };
        lock (Gate)
        {
            if ((admission == null ? legacyHostEntry != parent : admission.Frame != parent) ||
                owner != parent || parent.Closing || parent.Store != store || parent.IsRunner || parent.TransferDestination != null ||
                parent.StateThread != 0 || synchronous != null || InHostCallback.Value)
                throw new ExecutionBusyException();
            parent.RunnerDrain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Ambient.Value = owner = frame;
        }
        try
        {
            using var subscription = output?.Subscribe();
            return await action().ConfigureAwait(false);
        }
        finally
        {
            lock (Gate)
            {
                Ambient.Value = owner = parent;
                parent.RunnerDrain!.SetResult();
                parent.RunnerDrain = null;
            }
        }
    }

    // The transfer token authorizes only source export and destination snapshot import.
    internal static void RunTransfer(VariableDictionary source, VariableDictionary destination,
        long sourceRevision, long destinationRevision, Action<Transfer> action)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(action);
        if (source == destination) throw new ArgumentException("Transfer requires two stores.");
        lock (Gate)
            if (owner != null) throw new ExecutionBusyException();
        using var lease = Enter(source);
        var frame = Ambient.Value!;
        lock (Gate)
        {
            destination.EnsureLive();
            if (!source.AtRevision(sourceRevision) || !destination.AtRevision(destinationRevision))
                throw new ExecutionBusyException();
            frame.TransferDestination = destination;
        }
        var permit = new Transfer(frame, sourceRevision, destinationRevision);
        try { action(permit); }
        finally { permit.End(); }
    }

    internal sealed class Transfer(Frame frame, long sourceRevision, long destinationRevision)
    {
        internal sealed class ExportedGraph(string json)
        {
            internal string Json { get; } = json;
        }

        private ExportedGraph? exported;
        internal void End() => exported = null;

        private void Verify()
        {
            if (frame != owner || frame != synchronous || frame != Ambient.Value || frame.TransferDestination == null || InHostCallback.Value)
                throw new ExecutionBusyException();
        }

        internal void VerifyEndpoint(VariableDictionary store, bool importing)
        {
            Verify();
            if (store != (importing ? frame.TransferDestination : frame.Store)) throw new ExecutionBusyException();
        }

        internal ExportedGraph Export()
        {
            lock (Gate) { Verify(); frame.Exporting = true; }
            try { return exported = new ExportedGraph(frame.Store.ExportForTransfer(this)); }
            finally { lock (Gate) frame.Exporting = false; }
        }

        internal void Import(ExportedGraph snapshot)
        {
            lock (Gate)
            {
                Verify();
                if (exported == null || !ReferenceEquals(snapshot, exported))
                    throw new InvalidOperationException("Import requires the snapshot exported under this transfer.");
            }
            var roots = new Serialization.VariableSnapshotJsonSerializer().Deserialize(snapshot.Json);
            lock (Gate)
            {
                Verify();
                if (!frame.Store.AtRevision(sourceRevision) || !frame.TransferDestination!.AtRevision(destinationRevision))
                    throw new ExecutionBusyException();
                frame.TransferDestination.ImportForTransfer(this, roots);
            }
        }
    }

    internal static IDisposable EnterOutput()
    {
        lock (Gate)
        {
            if (owner == null) return Enter();
            VerifyExecution();
            return new Release(() => { });
        }
    }

    internal static void VerifyExecution()
    {
        lock (Gate)
        {
            VerifyAccess();
            if (owner != null && owner != synchronous && !(owner.IsRunner && owner == stateAccess))
                throw new ExecutionBusyException();
        }
    }

    // A host may explicitly reset its own store without gaining script execution authority.
    public static void ClearState(HostAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        using var access = EnterState(admission: admission);
        admission.Frame.Store.ClearAdmitted(admission);
    }

    internal static IDisposable HostOutputCallback() =>
        Ambient.Value == null || synchronous != null ? new Release(() => { }) : HostCallback();

    internal static IDisposable HostCallback()
    {
        bool previous = InHostCallback.Value;
        InHostCallback.Value = true;
        return new Release(() => InHostCallback.Value = previous);
    }

    public static T AccessState<T>(Func<T> action) => AccessState(null, action);

    public static T AccessState<T>(HostAdmission? admission, Func<T> action)
    {
        using var access = EnterState(admission: admission);
        return action();
    }

    internal static IDisposable EnterState(bool runner = false, HostAdmission? admission = null)
    {
        lock (Gate)
        {
            var frame = Ambient.Value;
            if (admission != null && admission.Frame != frame ||
                !runner && frame != stateAccess && frame != legacyHostEntry && admission?.Frame != frame ||
                InHostCallback.Value || frame == null || frame != owner || frame.Closing || frame.TransferDestination != null ||
                frame.StateThread != 0 && frame.StateThread != Environment.CurrentManagedThreadId ||
                frame.IsRunner && !runner && stateAccess != frame)
                throw new ExecutionBusyException();
            var previous = stateAccess;
            int previousThread = frame.StateThread;
            frame.StateThread = Environment.CurrentManagedThreadId;
            stateAccess = frame;
            return new Release(() =>
            {
                lock (Gate) { stateAccess = previous; frame.StateThread = previousThread; }
            });
        }
    }

    internal static VariableDictionary UnqualifiedRunnerStore() =>
        synchronous?.Store ?? VariableDictionary.Instance();

    internal static Frame? CaptureFrame() => Ambient.Value;

    internal static void VerifyFrame(Frame? frame)
    {
        lock (Gate)
        {
            VerifyExecution();
            if (frame != Ambient.Value) throw new ExecutionBusyException();
        }
    }

    internal static void VerifyObjectRead(VariableDictionary store, VariableDeclaration value)
    {
        lock (Gate)
        {
            if (owner is { Exporting: true } && owner == synchronous && owner == Ambient.Value &&
                owner.Store == store && !InHostCallback.Value) return;
            VerifyStore(store);
            if (store != VariableDictionary.Instance() && !store.ContainsObject(value))
                throw new InvalidOperationException("Variable is no longer in the owning graph.");
        }
    }

    internal static void VerifyStore(VariableDictionary store)
    {
        lock (Gate)
        {
            VerifyAccess();
            store.EnsureLive();
            if (owner != null && owner.Store != store) throw new ExecutionBusyException();
        }
    }

    public static void VerifyAccess()
    {
        lock (Gate)
            if (InHostCallback.Value || Ambient.Value != owner || owner?.TransferDestination != null ||
                owner != null && owner != synchronous && owner != stateAccess)
                throw new ExecutionBusyException();
    }

    private sealed class Release(Action release) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            lock (Gate)
            {
                if (disposed) return;
                release();
                disposed = true;
            }
        }
    }
}

public sealed class ExecutionBusyException : InvalidOperationException
{
    public ExecutionBusyException() : base("Interpreter execution is busy.") { }
}
