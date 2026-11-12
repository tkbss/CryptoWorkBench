using System;
using System.Collections.Generic;
using System.Linq;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>Layout-only gate. Execution authority remains exclusively in the existing coordinator.</summary>
internal sealed class EditorDockFactory : Factory, IDockDropPolicy, IDockOperationObserver
{
    internal DockShellLayout? LayoutOwner { get; set; }
    internal DockEditorSession? Session { get; set; }
    internal bool Exiting { get; set; }
    internal bool FocusMode { get; set; }
    internal DockContainerIdentity Containers { get; } = new();
    private int _mutationDepth;
    private IDockWindow? _removingEmptyWindow;
    private CompositeDropScope? _compositeDrop;

    public override void InitDockable(IDockable dockable, IDockable? owner)
    {
        if (dockable is IDock dock)
        {
            Containers.Initialize(dock);
            if (LayoutOwner is { Root: { } root })
                dock.IsCollapsable = !DockContainerRoles.IsProtectedOnInit(root, dock, owner);
        }
        base.InitDockable(dockable, owner);
        // Dock 12.1 promotes a sole child when collapsing a proportional wrapper,
        // but leaves the grandparent's selection pointing at the removed wrapper.
        // Transfer only this exact promotion's selection; malformed unrelated tabs
        // remain validation errors. No additional operation is admitted here.
        if (owner is IDock parent && parent.ActiveDockable is IProportionalDock previous &&
            ReferenceEquals(previous.Owner, parent) && previous.VisibleDockables is { Count: 1 } children &&
            ReferenceEquals(children[0], dockable) && parent.VisibleDockables?.Contains(dockable) == true &&
            !parent.VisibleDockables.Contains(previous))
            parent.ActiveDockable = dockable;
    }

    public IDockOperationScope? BeginDockOperation(IDockable source, IDockable target, DockOperation operation)
    {
        // DockManager enumerates the live child list of proportional groups. Removing
        // empty child docks during that enumeration invalidates its next index.
        if (_compositeDrop != null || source is not IProportionalDock || !Allowed) return null;
        return _compositeDrop = new CompositeDropScope(this, source);
    }

    private sealed class CompositeDropScope(EditorDockFactory factory, IDockable source) : IDockOperationScope
    {
        internal readonly HashSet<IDock> Sources = DockShellLayout.VisibleTree(source, false).OfType<IDock>().ToHashSet();
        internal readonly HashSet<IDock> Empty = new();
        internal Exception? Failure;
        public void Complete() => DockLifecycleTrace.Write("composite-drop:complete", source);
        public void Dispose()
        {
            factory._compositeDrop = null;
            // Synchronous end of this operation, using the normal admission gate.
            // No unrelated host is inspected; only emptied source docks are retried.
            try { factory.Change(() =>
            {
                foreach (var dock in Empty)
                    if (dock.Owner is IDock parent && parent.VisibleDockables?.Contains(dock) == true)
                        factory.CollapseDock(dock);
            }); }
            catch (Exception cleanup) when (Failure != null)
            { DockShellLayout.RecordSecondaryFailure(Failure, "Dock group cleanup", cleanup); }
            finally
            {
                if (Failure != null && !factory.Exiting)
                    factory.LayoutOwner?.VerifyFailedDockOperation(Failure);
            }
        }
    }
    internal bool IsCompletingEmptyWindowRemoval(IDockWindow? window) =>
        _mutationDepth > 0 && window != null && ReferenceEquals(window, _removingEmptyWindow) && IsOwnedEmptyWindow(window);

    private bool IsOwnedEmptyWindow(IDockWindow window) =>
        LayoutOwner is { } owner && ReferenceEquals(window.Factory, this) &&
        ReferenceEquals(window.Owner, owner.Root) && owner.Root.Windows?.Contains(window) == true &&
        window.Layout is { } root && !DockShellLayout.VisibleTree(root).Any(d => d is IDocument or ITool);

    public override void RemoveWindow(IDockWindow window)
    {
        // CollapseDock closes the emptied source synchronously inside an admitted relocation.
        // That observer scope forbids NEW actions, but must not veto this operation's cleanup.
        var previous = _removingEmptyWindow;
        _removingEmptyWindow = _mutationDepth > 0 && IsOwnedEmptyWindow(window) ? window : null;
        DockLifecycleTrace.Write("RemoveWindow:before", window);
        try { base.RemoveWindow(window); }
        finally { _removingEmptyWindow = previous; DockLifecycleTrace.Write("RemoveWindow:after", window); }
    }
    private bool Allowed => Exiting || LayoutOwner?.LayoutFaulted != true && (_mutationDepth > 0 || !FocusMode && (Session == null || Session.CanRelocate));
    internal DockCapabilityPolicy Policy => new EditorPolicy(() => Allowed);
    private sealed class EditorPolicy(Func<bool> allowed) : DockCapabilityPolicy
    {
        public override bool? Get(DockCapability capability) => !allowed() ? false : base.Get(capability);
    }
    public bool CanDrop(IDockable source, IDockable target, DockOperation operation)
    {
        var allowed = Allowed;
        DockLifecycleTrace.Write($"drop-policy:source={source.Id}:sourceType={source.GetType().Name}:target={target.Id}:targetType={target.GetType().Name}:operation={operation}:allowed={allowed}", source);
        return allowed;
    }
    public override void CollapseDock(IDock dock)
    {
        DockLifecycleTrace.Write("CollapseDock:before", dock);
        if (_compositeDrop is { } composite && composite.Sources.Contains(dock) && dock.VisibleDockables is { Count: 0 })
        {
            composite.Empty.Add(dock);
            DockLifecycleTrace.Write("CollapseDock:deferred-to-group-completion", dock);
            return;
        }
        // Initialization/projection already assigned the actual structural role.
        // Floating former anchors use the same ordinary cleanup as other groups.
        try { base.CollapseDock(dock); }
        finally
        {
            DockLifecycleTrace.Write("CollapseDock:after", dock);
        }
    }
    public override IRootDock CreateRootDock()
    {
        var root = base.CreateRootDock();
        root.RootDockCapabilityPolicy = Policy;
        return root;
    }
    private void Change(Action action, [System.Runtime.CompilerServices.CallerMemberName] string operation = "")
    {
        var outer = _mutationDepth == 0;
        Exception? failure = null;
        try
        {
            DockLifecycleTrace.Write(operation + ":before");
            if (!Allowed) { DockLifecycleTrace.Write(operation + ":denied"); return; }
            if (!outer) { action(); return; }
            void Apply()
            {
                _mutationDepth++;
                try { action(); }
                finally { _mutationDepth--; }
            }
            if (Session == null || Exiting) Apply(); else Session.Relocate(Apply);
        }
        catch (Exception error)
        {
            failure = error;
            if (outer && !Exiting)
            {
                // A group still has its already scheduled source cleanup to do.
                // Verify its end state after that existing continuation, not midway.
                if (_compositeDrop is { } group) group.Failure ??= error;
                else LayoutOwner?.VerifyFailedDockOperation(error);
            }
            throw;
        }
        finally
        {
            try { DockLifecycleTrace.Write(operation + ":after"); }
            catch (Exception trace) when (failure != null)
            { DockShellLayout.RecordSecondaryFailure(failure, "Dock failure trace", trace); }
        }
    }
    internal bool ChangeLayout(Action action)
    {
        // Focus blocks ordinary docking, but Restore/Reset still use the original admission gate.
        if (Exiting || LayoutOwner?.LayoutFaulted == true || Session != null && !Session.CanRelocate) return false;
        void Apply()
        {
            _mutationDepth++;
            try { action(); }
            finally { _mutationDepth--; }
        }
        if (Session == null) Apply(); else Session.Relocate(Apply);
        return true;
    }
    public override void HideDockable(IDockable pane) => Change(() => base.HideDockable(pane));
    public override void RestoreDockable(IDockable pane) => Change(() => base.RestoreDockable(pane));
    public override void MoveDockable(IDock dock, IDockable source, IDockable target) => Change(() => base.MoveDockable(dock, source, target));
    public override void MoveDockable(IDock source, IDock target, IDockable pane, IDockable? relative) => Change(() => base.MoveDockable(source, target, pane, relative));
    public override void SwapDockable(IDock dock, IDockable source, IDockable target) => Change(() => base.SwapDockable(dock, source, target));
    public override void SwapDockable(IDock source, IDock target, IDockable pane, IDockable other) => Change(() => base.SwapDockable(source, target, pane, other));
    public override void SplitToDock(IDock dock, IDockable pane, DockOperation operation) => Change(() => base.SplitToDock(dock, pane, operation));
    public override void SplitToWindow(IDock dock, IDockable pane, double x, double y, double width, double height, DockWindowOptions? options) => Change(() =>
    {
        var movedAnchor = pane is IDock anchor && LayoutOwner is { } owner && DockContainerRoles.MainAnchors(owner.Root).Contains(anchor);
        base.SplitToWindow(dock, pane, x, y, width, height, options);
        // Dragging a whole dock removes the anchor itself, unlike a tab drag.
        if (movedAnchor && LayoutOwner is { } layout && !ReferenceEquals(FindRoot(pane), layout.Root))
            layout.EnsureReturnAnchor(pane.Id);
    });
    public override void FloatDockable(IDockable pane, DockWindowOptions? options) => Change(() => base.FloatDockable(pane, options));
    public override void FloatAllDockables(IDockable pane, DockWindowOptions? options) => Change(() => base.FloatAllDockables(pane, options));
    public override void NewHorizontalDocumentDock(IDockable pane) => Change(() => base.NewHorizontalDocumentDock(pane));
    public override void NewVerticalDocumentDock(IDockable pane) => Change(() => base.NewVerticalDocumentDock(pane));
    public override void CloseDockable(IDockable? pane) => Change(() => base.CloseDockable(pane));
}
