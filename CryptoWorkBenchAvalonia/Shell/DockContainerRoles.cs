using System.Collections.Generic;
using System.Linq;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>
/// Presentation roles follow the actual main-tree structure. An ID in a floating
/// tree records provenance, not a main-window protection or admission privilege.
/// Shared by schema-1 projection and live factory initialization.
/// </summary>
internal static class DockContainerRoles
{
    private static bool IsAnchor(IDock dock) =>
        dock is IDocumentDock && dock.Id == DockShellLayout.Documents ||
        dock is IToolDock && dock.Id is DockShellLayout.Tools or DockShellLayout.Diagnostics;

    private static IProportionalDock? MainStructure(IRootDock root) =>
        DockShellLayout.VisibleTree(root, false).OfType<IProportionalDock>().SingleOrDefault(d => d.Id == "dock:main");

    internal static IEnumerable<IDock> MainAnchors(IRootDock root)
    {
        if (MainStructure(root) is not { } main) return [];
        return DockShellLayout.VisibleTree(main, false).OfType<IDock>().Where(IsAnchor);
    }

    internal static bool IsProtectedOnInit(IRootDock root, IDock dock, IDockable? owner)
    {
        if (ProtectedMainDocks(root).Contains(dock)) return true;
        // Add/InsertDockable initialize BEFORE inserting into VisibleDockables.
        // Recognize a replacement return anchor only at its actual main-tree
        // destination, with the expected type and no existing role occupant.
        return IsAnchor(dock) && MainStructure(root) is { } main &&
            DockShellLayout.VisibleTree(main, false).Contains(owner) &&
            !MainAnchors(root).Any(existing => existing.Id == dock.Id);
    }

    internal static HashSet<IDock> ProtectedMainDocks(IRootDock root)
    {
        var result = MainAnchors(root).ToHashSet();
        result.Add(root);
        if (MainStructure(root) is { } main) result.Add(main);
        return result;
    }
}
