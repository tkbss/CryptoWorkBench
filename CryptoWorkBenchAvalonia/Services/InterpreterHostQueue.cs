using CryptoScript.Model;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services;

/// <summary>One host queue covers parsing, execution and detached state capture.</summary>
internal static class InterpreterHostQueue
{
    private static readonly SemaphoreSlim Queue = new(1, 1);
    private static readonly AsyncLocal<bool> WithinJob = new();

    internal static async Task<T> RunAsync<T>(Func<Task<T>> job, CancellationToken token)
    {
        if (WithinJob.Value) throw new ExecutionBusyException();
        await Queue.WaitAsync(token);
        WithinJob.Value = true;
        try
        {
            return await Task.Run(() => InterpreterExecution.RunAsync(job, token), token);
        }
        finally { WithinJob.Value = false; Queue.Release(); }
    }
}
