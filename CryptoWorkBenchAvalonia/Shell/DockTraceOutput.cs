using System;
using System.IO;

namespace CryptoWorkBenchAvalonia.Shell;

// A synchronous diagnostic writer. One failure disables output; no backlog or retries.
internal sealed class DockTraceOutput : IDisposable
{
    private readonly StreamWriter writer;
    internal DockTraceFailure? Failure { get; private set; }
    private bool disposed;

    internal DockTraceOutput(string path) => writer = new StreamWriter(
        new FileStream(Path.GetFullPath(path), FileMode.CreateNew, FileAccess.Write, FileShare.Read, bufferSize: 1)) { AutoFlush = true };

    internal void Write(Func<string> record)
    {
        if (disposed || Failure != null) return;
        try { writer.WriteLine(record()); }
        catch (Exception error) when (DockTraceFailure.IsIo(error))
        { Failure = DockTraceFailure.Report(error); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            // Do not flush the failed StreamWriter buffer a second time.
            if (Failure != null) writer.BaseStream.Dispose(); else writer.Dispose();
        }
        catch (Exception error) when (DockTraceFailure.IsIo(error))
        { Failure ??= DockTraceFailure.Report(error); }
    }
}

internal sealed record DockTraceFailure(string ErrorType, int HResult, string Message)
{
    internal static bool IsIo(Exception error) => error is IOException or UnauthorizedAccessException;
    internal static DockTraceFailure Report(Exception error)
    {
        var message = error.Message;
        var failure = new DockTraceFailure(error.GetType().FullName!, error.HResult,
            message.Length <= 512 ? message : message[..512]);
        try { Console.Error.WriteLine($"Dock trace disabled: {failure.ErrorType} (0x{failure.HResult:X8}): {failure.Message}"); }
        catch (Exception reporting) when (IsIo(reporting) || reporting is ObjectDisposedException)
        { /* The bounded status remains observable even if stderr is unavailable. */ }
        return failure;
    }
}
