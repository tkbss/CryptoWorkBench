using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Serializer.SystemTextJson;

namespace G1DockFeasibility;

public sealed class ProbeFactory : Factory
{
    public ProbeFactory(DocumentSession document, VariablesViewModel variables, DiagnosticsViewModel diagnostics)
    {
        ContextLocator = new()
        {
            [DocumentSession.Id] = () => document,
            [VariablesViewModel.Id] = () => variables,
            [DiagnosticsViewModel.Id] = () => diagnostics
        };
        HostWindowLocator = new() { [nameof(IDockWindow)] = () => new HostWindow() };
    }

    public override IRootDock CreateLayout()
    {
        var document = new Document { Id = DocumentSession.Id, Title = "Sample.crypto", CanClose = false };
        var variables = new Tool { Id = VariablesViewModel.Id, Title = "Variables", CanPin = false };
        var diagnostics = new Tool { Id = DiagnosticsViewModel.Id, Title = "Diagnostics", CanPin = false };
        var documents = new DocumentDock
        {
            Id = "documents", IsCollapsable = false, CanCreateDocument = false,
            VisibleDockables = CreateList<IDockable>(document), ActiveDockable = document
        };
        var left = new ToolDock
        {
            Id = "variables-dock", IsCollapsable = false, Proportion = .22, Alignment = Alignment.Left,
            VisibleDockables = CreateList<IDockable>(variables), ActiveDockable = variables
        };
        var right = new ToolDock
        {
            Id = "diagnostics-dock", IsCollapsable = false, Proportion = .22, Alignment = Alignment.Right,
            VisibleDockables = CreateList<IDockable>(diagnostics), ActiveDockable = diagnostics
        };
        var main = new ProportionalDock
        {
            Id = "main-layout", Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(left, new ProportionalDockSplitter(), documents,
                new ProportionalDockSplitter(), right)
        };
        return new RootDock
        {
            Id = "root", IsCollapsable = false, VisibleDockables = CreateList<IDockable>(main),
            ActiveDockable = main, DefaultDockable = main
        };
    }
}

public sealed class ProbeWorkspace : IDisposable
{
    private readonly DockSerializer _serializer = new();
    private bool _disposed;
    public DocumentSession Session { get; }
    public ProbeFactory Factory { get; }
    public IRootDock Root { get; private set; }
    public ProbeWorkspace(DocumentSession document, VariablesViewModel variables, DiagnosticsViewModel diagnostics)
    {
        Session = document;
        Factory = new(document, variables, diagnostics);
        Root = Factory.CreateLayout();
        Factory.InitLayout(Root);
    }

    public static IEnumerable<IDockable> Walk(IDockable item, bool includeFloating = true)
    {
        yield return item;
        if (item is IDock dock && dock.VisibleDockables is { } children)
            foreach (var child in children)
                foreach (var descendant in Walk(child, includeFloating)) yield return descendant;
        if (includeFloating && item is IRootDock root && root.Windows is { } windows)
            foreach (var window in windows)
                if (window.Layout is { } layout)
                    foreach (var descendant in Walk(layout)) yield return descendant;
    }

    public IDockable Find(string id) => Walk(Root).Single(d => d.Id == id);
    public void Float(string id) => Factory.FloatDockable(Find(id));
    public void Move(string id, string targetDockId)
    {
        var item = Find(id);
        // Floating containers can copy their source container ID. Resolve the destination in the main tree.
        var target = Walk(Root, includeFloating: false).OfType<IDock>().SingleOrDefault(d => d.Id == targetDockId);
        if (target is null)
        {
            // Dragging a whole group moves the original container into the float. Recreate its main anchor.
            target = targetDockId == "documents" ? new DocumentDock { CanCreateDocument = false }
                : new ToolDock { Alignment = targetDockId == "variables-dock" ? Alignment.Left : Alignment.Right, Proportion = .22 };
            target.Id = targetDockId;
            target.IsCollapsable = false;
            target.VisibleDockables = Factory.CreateList<IDockable>();
            var main = (IDock)Walk(Root, includeFloating: false).Single(d => d.Id == "main-layout");
            Factory.InsertDockable(main, target, 0);
            Factory.InsertDockable(main, new ProportionalDockSplitter(), 1);
        }
        Factory.MoveDockable((IDock)item.Owner!, target, item, null);
        Factory.SetActiveDockable(item);
        // A floated non-collapsible group can otherwise leave an empty native host behind.
        foreach (var window in Root.Windows?.ToArray() ?? [])
            if (window.Layout is { } layout && !Walk(layout).Any(d => d is IDocument or ITool))
                Factory.RemoveWindow(window);
    }

    public string SaveLayout() => _serializer.Serialize<IRootDock>(Root);

    // Application state is deliberately not serialized into the layout.
    public void RestoreLayout(string json, DockControl? host = null)
    {
        var restored = _serializer.Deserialize<IRootDock>(json) ?? throw new InvalidDataException("Empty layout");
        var ids = Walk(restored).Where(d => d is IDocument or ITool).Select(d => d.Id).ToArray();
        if (ids.Length != 3 || ids.Distinct().Count() != 3 ||
            !ids.Order().SequenceEqual(new[] { DocumentSession.Id, VariablesViewModel.Id, DiagnosticsViewModel.Id }.Order()))
            throw new InvalidDataException("The G1 layout must contain exactly the three known dockables.");
        Root.ExitWindows.Execute(null);
        if (host is not null) host.Layout = null;
        Root = restored;
        Factory.InitLayout(Root);
        if (host is not null) host.Layout = Root;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Root.ExitWindows.Execute(null);
        Factory.ContextLocator?.Clear();
        Factory.HostWindowLocator?.Clear();
        Factory.DockableLocator?.Clear();
        Factory.DefaultContextLocator = null;
        Factory.DefaultHostWindowLocator = null;
        Session.Dispose();
    }
}
