using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace CryptoWorkBenchAvalonia.Services;

/// <summary>
/// UI handoffs carry explicit arguments, never ambient interpreter/job capabilities.
/// Avalonia 12 captures ExecutionContext when enqueueing. Suppress only that capture,
/// restoring flow before any await. Same-thread direct calls retain their existing contract.
/// </summary>
internal static class UiDispatch
{
    private static T Enqueue<T>(Func<T> schedule)
    {
        if (ExecutionContext.IsFlowSuppressed()) return schedule();
        using (ExecutionContext.SuppressFlow()) return schedule();
    }

    internal static DispatcherOperation InvokeAsync(Action action, DispatcherPriority priority = default) =>
        Enqueue(() => Dispatcher.UIThread.InvokeAsync(action, priority));

    internal static DispatcherOperation<T> InvokeAsync<T>(Func<T> action, DispatcherPriority priority = default) =>
        Enqueue(() => Dispatcher.UIThread.InvokeAsync(action, priority));

    internal static Task<T> InvokeAsync<T>(Func<Task<T>> action, DispatcherPriority priority = default) =>
        Enqueue(() => Dispatcher.UIThread.InvokeAsync(action, priority));

    internal static void Post(Action action, DispatcherPriority priority = default) =>
        Enqueue(() => { Dispatcher.UIThread.Post(action, priority); return true; });
}
