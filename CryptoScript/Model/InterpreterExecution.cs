namespace CryptoScript.Model;

/// <summary>Excludes competing interpreter/host work; contains no passwords.
/// Hosts must include parsing and state presentation in the same scope.</summary>
public static class InterpreterExecution
{
    private static readonly object Gate = new();
    private static readonly AsyncLocal<Scope?> Ambient = new();
    private static readonly AsyncLocal<bool> InHostCallback = new();
    [ThreadStatic] private static Scope? synchronous;
    [ThreadStatic] private static Scope? stateAccess;
    private static Scope? owner;

    public static bool IsBusy { get { lock (Gate) return owner is not null; } }

    /// <summary>Fail-fast synchronous host scope. Same-thread synchronous reentrancy is supported.</summary>
    public static IDisposable Enter()
    {
        lock (Gate)
        {
            if (InHostCallback.Value) throw new ExecutionBusyException();
            if (owner is not null)
            {
                if (ReferenceEquals(owner, synchronous)) return new Release(() => { });
                throw new ExecutionBusyException();
            }
            var scope = new Scope();
            owner = synchronous = scope;
            return new Release(() => { lock (Gate) { synchronous = null; owner = null; } });
        }
    }

    /// <summary>Fail-fast asynchronous host scope, including parsing, execution and snapshot capture.
    /// Does not move work to a worker thread; GUI hosts must do so.</summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        var scope = new Scope();
        lock (Gate)
        {
            if (owner is not null || InHostCallback.Value) throw new ExecutionBusyException();
            owner = scope;
        }
        Ambient.Value = scope;
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            Ambient.Value = null;
            lock (Gate) owner = null;
        }
    }

    internal static async Task<T> RunRunnerAsync<T>(Func<Task<T>> action, CancellationToken token)
    {
        if (Ambient.Value is null)
            return await RunAsync(() => RunRunnerAsync(action, token), token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var scope = Ambient.Value;
        lock (Gate)
        {
            if (InHostCallback.Value || !ReferenceEquals(owner, scope) || scope.RunnerActive)
                throw new ExecutionBusyException();
            scope.RunnerActive = true;
        }
        try { return await action().ConfigureAwait(false); }
        finally { lock (Gate) scope.RunnerActive = false; }
    }

    internal static IDisposable HostCallback()
    {
        bool previous = InHostCallback.Value;
        InHostCallback.Value = true;
        return new Release(() => InHostCallback.Value = previous);
    }

    /// <summary>Run a short synchronous host state step inside RunAsync (e.g. detached
    /// presentation capture after awaiting the runner). Never hold it across await.</summary>
    public static T AccessState<T>(Func<T> action)
    {
        using var access = EnterState();
        return action();
    }

    internal static IDisposable EnterState(bool runner = false)
    {
        lock (Gate)
        {
            var scope = Ambient.Value;
            if (InHostCallback.Value || scope is null || !ReferenceEquals(owner, scope) ||
                scope.StateThread != 0 && scope.StateThread != Environment.CurrentManagedThreadId ||
                scope.RunnerActive && !runner && !ReferenceEquals(stateAccess, scope))
                throw new ExecutionBusyException();
            var previous = stateAccess;
            int previousThread = scope.StateThread;
            scope.StateThread = Environment.CurrentManagedThreadId;
            stateAccess = scope;
            return new Release(() =>
            {
                lock (Gate) { stateAccess = previous; scope.StateThread = previousThread; }
            });
        }
    }

    /// <summary>Checks access at host parsing/state boundaries. A lease is still needed to
    /// protect a multi-step operation; this is not a concurrent dictionary abstraction.</summary>
    public static void VerifyAccess()
    {
        lock (Gate)
            if (InHostCallback.Value || owner is not null &&
                !ReferenceEquals(owner, synchronous) && !ReferenceEquals(owner, stateAccess))
                throw new ExecutionBusyException();
    }

    private sealed class Scope { internal bool RunnerActive; internal int StateThread; }
    private sealed class Release(Action release) : IDisposable
    {
        private Action? action = release;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}

public sealed class ExecutionBusyException : InvalidOperationException
{
    public ExecutionBusyException() : base("Interpreter execution is busy.") { }
}
