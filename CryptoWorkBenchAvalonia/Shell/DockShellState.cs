using System.Collections.Generic;
using Prism.Mvvm;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>Presentation state only. Does not own an interpreter, editor registration or business workspace.</summary>
public sealed class DockShellState
{
    public const string DocumentId = "document:cryptoscript:primary";
    public const string VariablesId = "tool:variables";
    public const string OutputId = "tool:output";
    public const string InfoId = "tool:info";
    public const string DiagnosticsId = "tool:diagnostics";
    public const string StatusId = "tool:status";

    public DockPaneState Document { get; } = new(DocumentId, "CryptoScript", isDocument: true);
    public IReadOnlyList<DockPaneState> Tools { get; } = new[]
    {
        new DockPaneState(VariablesId, "Variables"), new DockPaneState(OutputId, "Output"),
        new DockPaneState(InfoId, "Info"), new DockPaneState(DiagnosticsId, "Diagnostics"),
        new DockPaneState(StatusId, "Status")
    };
}

/// <summary>Stable identity and placeholder state survive visual recreation and tool closure.</summary>
public sealed class DockPaneState : BindableBase
{
    public string Id { get; }
    public string Title { get; }
    public bool IsDocument { get; }
    private string _note = string.Empty;
    public string Note { get => _note; set => SetProperty(ref _note, value); }
    public DockPaneState(string id, string title, bool isDocument = false)
    {
        Id = id; Title = title; IsDocument = isDocument;
    }
}
