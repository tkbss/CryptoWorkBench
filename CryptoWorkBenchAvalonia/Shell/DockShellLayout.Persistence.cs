using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace CryptoWorkBenchAvalonia.Shell;

public sealed partial class DockShellLayout
{
    private sealed record FocusPart(IDock Dock, IList<IDockable>? Children, IDockable? Active, double Proportion);
    private List<FocusPart>? _focus;
    private (IDockable Pane, double Value, double Collapsed)[]? _focusProportions;
    private string? _normalDockJson;
    private string? _defaultJson;
    internal event Action? RootChanged;
    internal LoadedLayout? StartupLayout { get; set; }
    private bool _replacing;
    public bool IsMaximized => _focus != null;
    internal string? MaximizedPaneId { get; private set; }
    private static readonly LayoutWindow PlaceholderWindow = new(0, 0, 1200, 800);

    private void ValidateCurrentDockModel()
    {
        var visible = VisibleTree(Root).Where(p => p is IDocument or ITool).Select(p => p.Id).ToHashSet();
        DockLayoutStore.Validate(Root, DockLayoutStore.PaneIds.Where(id => !visible.Contains(id)).ToArray());
    }

    internal string Capture(LayoutWindow main)
    {
        Verify();
        if (LayoutFaulted)
        {
            // Retain the structural diagnostic for an invalid tree; a structurally
            // valid A02 presentation fault still gets the existing refusal below.
            // Focus projections use A02's own validation, not the normal-tree rules.
            if (_normalDockJson == null) ValidateCurrentDockModel();
            throw new InvalidOperationException("The failed layout cannot be persisted.");
        }
        if (_normalDockJson != null)
        {
            var saved = DockLayoutStore.Decode(_normalDockJson);
            return DockLayoutStore.Encode(saved.Root, saved.ClosedTools, main);
        }
        foreach (var window in Root.Windows ?? [])
        {
            if (window == null) throw new InvalidDataException("Invalid floating configuration.");
            window.Save();
        }
        var visible = VisibleTree(Root).Where(p => p is IDocument or ITool).Select(p => p.Id).ToHashSet();
        return DockLayoutStore.Encode(Root, DockLayoutStore.PaneIds.Where(id => !visible.Contains(id)).ToArray(), main);
    }

    // Only before session binding and before any DockControl is attached. No interactive detach/attach sequence.
    internal void RestoreAtStartup(LoadedLayout saved)
    {
        Verify();
        if (((EditorDockFactory)Factory).Session != null || Factory.DockControls.Count != 0)
            throw new InvalidOperationException("Startup restore must precede editor/host binding.");
        _defaultJson ??= Capture(PlaceholderWindow);
        Install(saved.Root, present: false);
    }

    private void Install(IRootDock next, bool present)
    {
        ((EditorDockFactory)Factory).Containers.RegisterTree(next);
        void Rebind(IDock dock)
        {
            var active = dock.ActiveDockable;
            for (int i = 0; i < (dock.VisibleDockables?.Count ?? 0); i++)
            {
                var child = dock.VisibleDockables![i];
                if (child is IDocument or ITool)
                {
                    var pane = Pane(child.Id);
                    pane.Proportion = child.Proportion;
                    dock.VisibleDockables[i] = pane;
                    if (ReferenceEquals(active, child)) dock.ActiveDockable = pane;
                }
                else if (child is IDock nested) Rebind(nested);
            }
            if (dock is IRootDock root)
            {
                root.RootDockCapabilityPolicy = ((EditorDockFactory)Factory).Policy;
                foreach (var window in root.Windows ?? []) Rebind(window.Layout!);
            }
        }
        Rebind(next);
        _replacing = true;
        try { Root.ExitWindows.Execute(null); }
        finally { _replacing = false; }
        Root = next;
        // InitDockable wires contexts, owners and native hosts but does not show windows.
        // Startup windows are first presented by DockControl after the main shell is ready.
        Factory.InitDockable(Root, null);
        RootChanged?.Invoke();
        if (present) Root.ShowWindows.Execute(null);
    }

    public bool Maximize(string id)
    {
        Verify();
        if (IsMaximized || !VisibleTree(Root).Contains(Pane(id))) return false;
        return ((EditorDockFactory)Factory).ChangeLayout(() =>
        {
            var normal = Capture(PlaceholderWindow);
            var proportions = VisibleTree(Root).Select(p => (Pane: p, Value: p.Proportion, Collapsed: p.CollapsedProportion)).ToArray();
            var parts = new List<FocusPart>();
            var focused = new List<FocusPart>();
            IDockable child = Pane(id);
            while (child.Owner is IDock parent)
            {
                parts.Add(new(parent, parent.VisibleDockables, parent.ActiveDockable, parent.Proportion));
                focused.Add(new(parent, Factory.CreateList(child), child, double.NaN));
                child = parent;
            }
            // Everything above is preflight. Compensation below is specific to these
            // three focus properties; it does not promise rollback of Dock operations.
            try { ApplyFocusParts(focused); }
            catch (Exception error) when (DockLayoutStore.Recoverable(error))
            {
                throw MutationFailed(error, () => { CompensateFocus(parts); RestoreProportions(proportions); },
                    () => Matches(parts) && Capture(PlaceholderWindow) == normal);
            }
            _focusProportions = proportions;
            _focus = parts; _normalDockJson = normal; MaximizedPaneId = id;
            ((EditorDockFactory)Factory).FocusMode = true;
        });
    }

    public bool Restore()
    {
        Verify();
        if (!IsMaximized) return false;
        return ((EditorDockFactory)Factory).ChangeLayout(RestoreCore);
    }

    private void RestoreCore()
    {
        if (_focus == null) return;
        var original = _focus;
        var normal = _normalDockJson;
        var paneId = MaximizedPaneId;
        var proportions = _focusProportions!;
        var focusedProportions = proportions.Select(p => (p.Pane, Value: p.Pane.Proportion, Collapsed: p.Pane.CollapsedProportion)).ToArray();
        // Validate the retained normal model before touching the focused presentation.
        DockLayoutStore.Decode(normal!);
        var focused = original.Select(p => new FocusPart(p.Dock, p.Dock.VisibleDockables, p.Dock.ActiveDockable, p.Dock.Proportion)).ToList();
        try
        {
            ApplyFocusParts(original.AsEnumerable().Reverse());
            RestoreProportions(proportions);
            ClearFocus();
            Capture(PlaceholderWindow);
        }
        catch (Exception error) when (DockLayoutStore.Recoverable(error))
        {
            _focus = original; _normalDockJson = normal; MaximizedPaneId = paneId;
            _focusProportions = proportions;
            ((EditorDockFactory)Factory).FocusMode = true;
            throw MutationFailed(error, () => { CompensateFocus(focused); RestoreProportions(focusedProportions); },
                () => Matches(focused) && focusedProportions.All(p => p.Pane.Proportion.Equals(p.Value) && p.Pane.CollapsedProportion.Equals(p.Collapsed)));
        }
    }

    private void ClearFocus()
    {
        _focus = null; _normalDockJson = null; MaximizedPaneId = null;
        _focusProportions = null;
        ((EditorDockFactory)Factory).FocusMode = false;
    }

    private static void ApplyFocusParts(IEnumerable<FocusPart> parts)
    {
        foreach (var part in parts)
        {
            part.Dock.VisibleDockables = part.Children;
            part.Dock.ActiveDockable = part.Active;
            part.Dock.Proportion = part.Proportion;
        }
    }

    private static bool Matches(IEnumerable<FocusPart> parts) => parts.All(p =>
        ReferenceEquals(p.Dock.VisibleDockables, p.Children) && ReferenceEquals(p.Dock.ActiveDockable, p.Active) && p.Dock.Proportion.Equals(p.Proportion));

    private static void RestoreProportions(IEnumerable<(IDockable Pane, double Value, double Collapsed)> proportions)
    {
        foreach (var part in proportions)
        {
            part.Pane.CollapsedProportion = part.Collapsed;
            part.Pane.Proportion = part.Value;
        }
    }

    private static void CompensateFocus(IEnumerable<FocusPart> parts)
    {
        // A property notification can throw AFTER assigning its backing field. Finish
        // each saved field independently, then verify the resulting references/values.
        void Set(Action assign)
        {
            try { assign(); }
            catch (Exception error) when (DockLayoutStore.Recoverable(error))
            { System.Diagnostics.Trace.WriteLine($"Focus compensation notification: {error}"); }
        }
        foreach (var part in parts.Reverse())
        {
            Set(() => part.Dock.VisibleDockables = part.Children);
            Set(() => part.Dock.ActiveDockable = part.Active);
            Set(() => part.Dock.Proportion = part.Proportion);
        }
    }

    public bool Reset()
    {
        Verify();
        return ((EditorDockFactory)Factory).ChangeLayout(() =>
        {
            if (_defaultJson == null)
            {
                using var original = new DockShellLayout(new DockShellState());
                _defaultJson = original.Capture(PlaceholderWindow);
            }
            var next = DockLayoutStore.Decode(_defaultJson).Root;
            var previous = DockLayoutStore.Decode(Capture(PlaceholderWindow));
            RestoreCore();
            try
            {
                Install(next, present: true);
                Capture(PlaceholderWindow);
            }
            catch (Exception error) when (DockLayoutStore.Recoverable(error))
            {
                // Reset alone owns replacement of the complete presentation. Reinstall
                // its validated previous model once, rebinding the SAME six pane objects.
                // Native hosts may be recreated; no business session is reset or replayed.
                throw MutationFailed(error, () => Install(previous.Root, present: true), () =>
                {
                    DockLayoutStore.Validate(Root, previous.ClosedTools);
                    return ReferenceEquals(Root, previous.Root) && (Root.Windows ?? []).All(w =>
                        w.Host is Avalonia.Controls.Window { IsVisible: true }) &&
                        VisibleTree(Root).Where(p => p is IDocument or ITool).All(p => ReferenceEquals(p, Pane(p.Id)));
                });
            }
        });
    }

    public bool MaximizeActive()
    {
        Verify();
        var root = (Root.Windows ?? []).FirstOrDefault(w => w.Host is Avalonia.Controls.Window { IsActive: true })?.Layout ?? Root;
        var selected = root.FocusedDockable;
        if (selected is not (IDocument or ITool))
        {
            selected = root;
            var visited = new HashSet<IDockable>(ReferenceEqualityComparer.Instance);
            while (selected is IDock dock)
            {
                if (!visited.Add(selected) || visited.Count > 100)
                    throw new InvalidDataException("Invalid selected tab path.");
                selected = dock.ActiveDockable;
            }
        }
        return selected is IDocument or ITool && Maximize(selected.Id);
    }
}
