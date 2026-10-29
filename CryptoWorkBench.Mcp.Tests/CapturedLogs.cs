using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CryptoWorkBench.Mcp.Tests;

internal sealed class CapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<string> Entries { get; } = new();
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(Entries);
    public void Dispose() { }
    private sealed class CaptureLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(formatter(state, exception) + (exception?.ToString() ?? ""));
    }
}
