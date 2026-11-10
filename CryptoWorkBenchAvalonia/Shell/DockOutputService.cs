using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CryptoWorkBenchAvalonia.Services.Automation;

namespace CryptoWorkBenchAvalonia.Shell;

internal sealed record OutputSource(Guid DocumentId, ITextSourceVersion Version, int? FirstLine);
internal sealed record PrintOutputEntry(Guid DocumentId, Guid ExecutionId, int Sequence,
    int? SourceLine, int? SourceColumn, string Text, ITextSourceVersion SourceRevision)
{
    public string Label => SourceLine is { } line
        ? $"CryptoScript · Line {line}" + (SourceColumn is { } column ? $" · Column {column}" : "") + " · Print result"
        : "CryptoScript · Source unknown · Print result";
}

/// <summary>UI-owned GUI presentation only. No interpreter access or persistence.</summary>
internal sealed class DockOutputService : IDisposable
{
    private readonly ObservableCollection<PrintOutputEntry> entries = new();
    private readonly HashSet<Guid> published = new();
    private readonly Dictionary<Guid, int> sequences = new();
    private bool disposed;
    internal ReadOnlyObservableCollection<PrintOutputEntry> Entries { get; }
    internal DockOutputService() => Entries = new(entries);
    internal void Publish(ExecutionPresentation result, OutputSource source, Func<bool> current)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (disposed || !current() || !published.Add(result.PresentationId)) return;
        int sequence = sequences.GetValueOrDefault(result.ExecutionId);
        foreach (var item in result.Output)
        {
            if (disposed || !current()) return;
            if (item.Kind != "print") continue;
            int? line = source.FirstLine is { } first && item.Source is { Line: > 0 } position
                ? first + position.Line - 1 : null;
            sequences[result.ExecutionId] = ++sequence;
            entries.Add(new(source.DocumentId, result.ExecutionId, sequence, line,
                line.HasValue && item.Source is { Column: > 0 } s ? s.Column : null,
                item.Text, source.Version));
        }
    }
    internal void Clear() { Dispatcher.UIThread.VerifyAccess(); entries.Clear(); }
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        disposed = true; entries.Clear(); published.Clear(); sequences.Clear();
    }
}
