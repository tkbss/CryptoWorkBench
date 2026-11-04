using System;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services.Automation;

internal enum McpDisconnectState { Revoking, WaitingForDrain, Disconnected, SafeFault }

/// <summary>Observes the original epoch completion independently of transport shutdown.</summary>
internal sealed class McpDisconnect(Action revoke, Func<Task> closeEpoch, Func<Task> stopTransport,
    Action<McpDisconnectState> report)
{
    private Task? completion;

    internal Task RunAsync()
    {
        if (completion != null) return completion;
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        completion = source.Task;
        _ = FinishAsync(source);
        return completion;
    }

    private async Task FinishAsync(TaskCompletionSource completion)
    {
        try
        {
            revoke();
            report(McpDisconnectState.Revoking);
            Task drain = closeEpoch();
            Task transport = stopTransport();
            report(McpDisconnectState.WaitingForDrain);
            // WhenAll observes both faults and never abandons a still-pending original drain.
            await Task.WhenAll(drain, transport);
            report(McpDisconnectState.Disconnected);
            completion.TrySetResult();
        }
        catch
        {
            try { report(McpDisconnectState.SafeFault); } catch { }
            completion.TrySetException(new InvalidOperationException("Workbench disconnect could not certify completion."));
        }
    }
}
