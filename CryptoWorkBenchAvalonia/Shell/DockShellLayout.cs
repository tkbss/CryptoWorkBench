using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>Owns one presentation layout. All mutations are UI-thread operations; no execution dependencies.</summary>
public sealed partial class DockShellLayout : IDisposable
{
    public const string Documents = "dock:documents";
    public const string Tools = "dock:tools";
    public const string Diagnostics = "dock:diagnostics";
    private const string Main = "dock:main";
    private readonly Dictionary<string, IDockable> _panes = new(StringComparer.Ordinal);
    public DockShellState State { get; }
    public Factory Factory { get; }
    public IRootDock Root { get; private set; }
    public bool IsDisposed { get; private set; }
    internal event Action<string>? Feedback;
    internal void ReportFeedback(string message) => Feedback?.Invoke(message);

    public DockShellLayout(DockShellState state)
    {
        Dispatcher.UIThread.VerifyAccess();
        State = state;
        Factory = new EditorDockFactory { HideToolsOnClose = false, LayoutOwner = this };
        Factory.HostWindowLocator = new() { [nameof(IDockWindow)] = () => new DocumentSafeHostWindow(this) };
        var document = new Document { Id = state.Document.Id, Title = state.Document.Title, Context = state.Document, CanClose = false, CanPin = false };
        _panes.Add(document.Id, document);
        foreach (var pane in state.Tools)
            _panes.Add(pane.Id, new Tool { Id = pane.Id, Title = pane.Title, Context = pane, CanPin = false });
        var documents = NewAnchor(Documents);
        documents.VisibleDockables = Factory.CreateList<IDockable>(document);
        documents.ActiveDockable = document;
        var tools = NewAnchor(Tools);
        tools.VisibleDockables = Factory.CreateList<IDockable>(Pane(DockShellState.VariablesId), Pane(DockShellState.InfoId));
        tools.ActiveDockable = Pane(DockShellState.VariablesId);
        var diagnostics = NewAnchor(Diagnostics);
        diagnostics.VisibleDockables = Factory.CreateList<IDockable>(Pane(DockShellState.OutputId), Pane(DockShellState.DiagnosticsId));
        diagnostics.ActiveDockable = Pane(DockShellState.OutputId);
        documents.Proportion = .70;
        tools.Proportion = .30;
        diagnostics.Proportion = .25;
        var workspace = new ProportionalDock
        {
            Id = "dock:workspace", IsCollapsable = false, Orientation = Orientation.Horizontal, Proportion = .75,
            VisibleDockables = Factory.CreateList<IDockable>(documents, new ProportionalDockSplitter(), tools),
            ActiveDockable = documents
        };
        var main = new ProportionalDock
        {
            Id = Main, IsCollapsable = false, Orientation = Orientation.Vertical,
            VisibleDockables = Factory.CreateList<IDockable>(workspace, new ProportionalDockSplitter(), diagnostics),
            ActiveDockable = workspace
        };
        Root = new RootDock
        {
            Id = "dock:root", IsCollapsable = false, FloatingWindowHostMode = DockFloatingWindowHostMode.Native,
            VisibleDockables = Factory.CreateList<IDockable>(main), ActiveDockable = main, DefaultDockable = main
        };
        Root.RootDockCapabilityPolicy = ((EditorDockFactory)Factory).Policy;
        ((EditorDockFactory)Factory).Containers.RegisterTree(Root);
        Factory.InitLayout(Root);
    }

    internal void BindEditor(DockEditorSession session) => ((EditorDockFactory)Factory).Session = session;
    private bool CanChange => !LayoutFaulted && !IsMaximized && (((EditorDockFactory)Factory).Session?.CanRelocate ?? true);

    public IDockable Pane(string id) => _panes.TryGetValue(id, out var pane) ? pane : throw new ArgumentException("Unknown pane identity.", nameof(id));

    public static IEnumerable<IDockable> VisibleTree(IDockable item, bool includeFloating = true)
    {
        var seen = new HashSet<IDockable>(ReferenceEqualityComparer.Instance);
        foreach (var descendant in Walk(item, 0)) yield return descendant;
        IEnumerable<IDockable> Walk(IDockable current, int depth)
        {
            if (current == null || depth > 20 || seen.Count >= 100 || !seen.Add(current))
                throw new InvalidDataException("Invalid or cyclic dock tree.");
            yield return current;
            if (current is IDock { VisibleDockables: { } children })
                foreach (var child in children)
                    foreach (var descendant in Walk(child, depth + 1)) yield return descendant;
            if (includeFloating && current is IRootDock { Windows: { } windows })
                foreach (var window in windows)
                    if (window?.Layout is { } layout)
                        foreach (var descendant in Walk(layout, depth + 1)) yield return descendant;
        }
    }

    public void Show(string id)
    {
        Verify();
        var pane = Pane(id);
        if (!CanChange) return;
        if (!VisibleTree(Root).Contains(pane)) Factory.AddDockable(Anchor(Home(id)), pane);
        Factory.SetActiveDockable(pane);
        var root = Factory.FindRoot(pane) ?? Root;
        Factory.SetFocusedDockable(root, pane);
        root.Window?.Host?.SetActive();
    }

    public void Float(string id)
    {
        Verify();
        if (!CanChange) return;
        Show(id);
        var pane = Pane(id);
        // Repeated Show/Float must not create nested or duplicate floating hosts.
        if (Factory.FindRoot(pane) != Root) return;
        Factory.FloatDockable(pane);
    }

    public void Dock(string id) => Move(id, Home(id));

    public void Move(string id, string destination)
    {
        Verify();
        if (destination != Documents && destination != Tools && destination != Diagnostics)
            throw new ArgumentException("Unknown dock anchor.", nameof(destination));
        var pane = Pane(id);
        if (((DockPaneState)pane.Context!).IsDocument && destination != Documents)
            throw new ArgumentException("Documents belong in the document area.", nameof(destination));
        if (!CanChange) return;
        var target = Anchor(destination);
        if (VisibleTree(Root).Contains(pane) && pane.Owner is IDock source)
        {
            if (!ReferenceEquals(source, target)) Factory.MoveDockable(source, target, pane, null);
        }
        else Factory.AddDockable(target, pane);
        Factory.SetActiveDockable(pane);
        Factory.SetFocusedDockable(Root, pane);
    }

    public void Close(string id)
    {
        Verify();
        var pane = Pane(id);
        if (!CanChange) return;
        if (VisibleTree(Root).Contains(pane)) Factory.CloseDockable(pane);
    }

    private static string Home(string id) => id == DockShellState.DocumentId ? Documents
        : id is DockShellState.OutputId or DockShellState.DiagnosticsId or DockShellState.StatusId ? Diagnostics : Tools;

    private IDock NewAnchor(string id)
    {
        IDock dock = id == Documents ? new DocumentDock { CanCreateDocument = false }
            : new ToolDock { Alignment = id == Tools ? Alignment.Right : Alignment.Bottom, Proportion = id == Tools ? .30 : .25 };
        dock.Id = id; dock.IsCollapsable = false; dock.VisibleDockables = Factory.CreateList<IDockable>();
        ((EditorDockFactory)Factory).Containers.Register(dock);
        return dock;
    }

    private IDock Anchor(string id)
    {
        // Floating groups may copy their source IDs; anchors must be resolved in the main tree only.
        if (DockContainerRoles.MainAnchors(Root).SingleOrDefault(d => d.Id == id) is { } existing) return existing;
        var main = VisibleTree(Root, false).OfType<IDock>().Single(d => d.Id == Main);
        // The G2 upper row owns document/right anchors. Legacy saved layouts retain
        // their original structure and continue to use the main container.
        if (id != Diagnostics)
            main = VisibleTree(Root, false).OfType<IDock>().SingleOrDefault(d => d.Id == "dock:workspace") ?? main;
        var anchor = NewAnchor(id);
        var index = id == Documents ? 0 : main.VisibleDockables!.Count;
        if (index > 0) Factory.InsertDockable(main, new ProportionalDockSplitter(), index++);
        Factory.InsertDockable(main, anchor, index);
        if (index == 0) Factory.InsertDockable(main, new ProportionalDockSplitter(), 1);
        return anchor;
    }

    internal void EnsureReturnAnchor(string id) => Anchor(id);

    private void Verify()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (IsDisposed) return;
        DockLifecycleTrace.Write("layout-dispose:before", this);
        IsDisposed = true;
        _focus = null;
        _focusProportions = null;
        RootChanged = null;
        Feedback = null;
        ((EditorDockFactory)Factory).Exiting = true;
        ((EditorDockFactory)Factory).LayoutOwner = null;
        Root.ExitWindows.Execute(null);
        Factory.HostWindowLocator?.Clear();
        Factory.ContextLocator?.Clear();
        Factory.DockableLocator?.Clear();
        Factory.DefaultContextLocator = null;
        Factory.DefaultHostWindowLocator = null;
        DockLifecycleTrace.Write("layout-dispose:after", this);
    }

    private sealed class DocumentSafeHostWindow(DockShellLayout owner) : HostWindow
    {
        protected override void OnInitialized()
        {
            // A native caption moves only this OS window. Dock tabs retain the
            // framework's drag/drop route; no replacement content template.
            WindowDecorations = Avalonia.Controls.WindowDecorations.Full;
            ExtendClientAreaToDecorationsHint = false;
            ToolChromeControlsWholeWindow = false;
            DocumentChromeControlsWholeWindow = false;
            ShowInTaskbar = true;
            base.OnInitialized();
        }

        protected override void OnOpened(EventArgs args)
        {
            UpdateTitle(this, EventArgs.Empty);
            LayoutUpdated += UpdateTitle;
            base.OnOpened(args);
        }

        private void UpdateTitle(object? sender, EventArgs args)
        {
            if (owner.IsDisposed || owner.LayoutFaulted || Window?.Layout is not { } floating) return;
            // Restored models have the framework placeholder title "IDockWindow".
            // Derive a taskbar/caption label from the existing session panes, also
            // when a floating group gains or loses content. No persisted metadata.
            var title = string.Join(" · ", VisibleTree(floating, false)
                .Where(d => d.Context is DockPaneState).Select(d => d.Title));
            if (!string.IsNullOrEmpty(title) && Title != title) Title = title;
        }

        protected override void OnClosed(EventArgs args)
        {
            LayoutUpdated -= UpdateTitle;
            base.OnClosed(args);
        }

        protected override void OnClosing(Avalonia.Controls.WindowClosingEventArgs args)
        {
            DockLifecycleTrace.Write($"host-OnClosing:enter:reason={args.CloseReason}:cancel={args.Cancel}", this);
            try
            {
                // Native host close does not consult Document.CanClose. Shell disposal
                // and layout replacement must still be allowed to close unowned hosts.
                if (!owner.IsDisposed && !owner._replacing && args.CloseReason is Avalonia.Controls.WindowCloseReason.WindowClosing or Avalonia.Controls.WindowCloseReason.Undefined &&
                    (!owner.CanChange && !((EditorDockFactory)owner.Factory).IsCompletingEmptyWindowRemoval(Window) ||
                     Window?.Layout is { } floating && VisibleTree(floating).Contains(owner.Pane(DockShellState.DocumentId))))
                {
                    args.Cancel = true;
                    DockLifecycleTrace.Write("host-OnClosing:integration-veto", this);
                    return;
                }
                base.OnClosing(args);
            }
            finally { DockLifecycleTrace.Write($"host-OnClosing:exit:reason={args.CloseReason}:cancel={args.Cancel}", this); }
        }
    }
}
