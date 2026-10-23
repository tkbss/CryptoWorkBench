using CryptoScript.Model;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services;

/// <summary>Process-wide, non-waiting admission for whole host operations.</summary>
internal static class InterpreterHostQueue
{
    private static readonly SemaphoreSlim Queue = new(1, 1);
    private static readonly AsyncLocal<bool> WithinJob = new();
    internal static bool IsBusy => Queue.CurrentCount == 0 || InterpreterExecution.IsBusy;

    private static IDisposable Reserve()
    {
        if (WithinJob.Value || !Queue.Wait(0)) throw new ExecutionBusyException();
        return new Reservation();
    }

    internal static async Task<T> RunAsync<T>(Func<Task<T>> job, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var reservation = Reserve();
        WithinJob.Value = true;
        try
        {
            return await Task.Run(() => InterpreterExecution.RunAsync(job, token), token).ConfigureAwait(false);
        }
        finally { WithinJob.Value = false; }
    }

    // Short UI mutations only; never waits on a task or dispatcher. Also suitable
    // for acquiring admission before a future asynchronous save commit.
    internal static T RunState<T>(Func<T> action, Action<T>? present = null)
    {
        using var reservation = Reserve();
        T result;
        using (InterpreterExecution.Enter()) result = action();
        // Publish on the caller's UI thread while admission is still held.
        // CollectionChanged may reenter host commands; those must receive Busy.
        present?.Invoke(result);
        return result;
    }

    private sealed class Reservation : IDisposable
    {
        private int released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0) Queue.Release();
        }
    }
}
