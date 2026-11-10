using AvaloniaEdit.Document;
using Prism.Ioc;

namespace G1DockFeasibility;

// Application ownership, independent of Dock and of the visual tree.
public sealed class WorkspaceState
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public int ToolVisits { get; set; }
}

public sealed class DocumentSession : IDisposable
{
    public const string Id = "document:cryptoscript:sample";
    public Guid InstanceId { get; } = Guid.NewGuid();
    public TextDocument Document { get; } = new("// G1 feasibility — no interpreter attached\nAES(00112233)\n");
    public int CaretOffset { get; set; }
    public int AttachCount { get; set; }
    public int DetachCount { get; set; }
    public int ViewCount { get; set; }
    public int AttachedViews { get; set; }
    public int DisposeCount { get; private set; }
    public void Dispose() { if (DisposeCount == 0) DisposeCount++; }
}

public sealed class VariablesViewModel(WorkspaceState state)
{
    public const string Id = "tool:variables";
    public WorkspaceState State { get; } = state;
}

public sealed class DiagnosticsViewModel(WorkspaceState state)
{
    public const string Id = "tool:diagnostics";
    public WorkspaceState State { get; } = state;
}

public static class Registration
{
    public static void Register(IContainerRegistry registry)
    {
        registry.RegisterSingleton<WorkspaceState>();
        // One document in this probe only. G1 multi-document sessions will be keyed by document ID.
        registry.RegisterSingleton<DocumentSession>();
        registry.RegisterSingleton<VariablesViewModel>();
        registry.RegisterSingleton<DiagnosticsViewModel>();
        registry.RegisterSingleton<ProbeWorkspace>();
    }
}
