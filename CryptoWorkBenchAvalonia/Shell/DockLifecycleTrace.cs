using System;

namespace CryptoWorkBenchAvalonia.Shell;

// Opt-in observation only. The desktop recorder owns and clears this process-local sink.
internal static class DockLifecycleTrace
{
    private static Action<string, object?>? sink;
    internal static DockTraceFailure? Failure { get; private set; }
    internal static Action<string, object?>? Sink
    {
        get => sink;
        set { sink = value; if (value != null) Failure = null; }
    }
    internal static void Write(string operation, object? subject = null)
    {
        try { sink?.Invoke(operation, subject); }
        catch (Exception error) when (DockTraceFailure.IsIo(error))
        {
            sink = null;
            Failure = DockTraceFailure.Report(error);
        }
    }
}
