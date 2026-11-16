using System;
using System.Runtime.CompilerServices;
using System.Linq;
using Dock.Model.Core;
using Dock.Model.Controls;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>
/// An anchor role is assigned explicitly by the shell (or a validated schema-1 layout).
/// Dock copies source IDs when creating structural containers; that copy conveys no role.
/// This registry owns presentation identity only, never admission or a business pane.
/// </summary>
internal sealed class DockContainerIdentity
{
    internal const string DynamicPrefix = "dock:container:";
    private readonly ConditionalWeakTable<IDock, object> _known = new();

    internal void Register(IDock dock) => _known.GetValue(dock, _ => new object());

    internal void RegisterTree(IDock root)
    {
        foreach (var dock in DockShellLayout.VisibleTree(root).OfType<IDock>()) Register(dock);
    }

    internal static void NameLegacyContainers(IRootDock root)
    {
        // Schema 1 historically emitted empty IDs for floating roots and split
        // wrappers. Give only unnamed structural objects an identity, then run
        // the full validator. Never infer a missing main role or rename a pane.
        foreach (var dock in DockShellLayout.VisibleTree(root).OfType<IDock>())
            if (!ReferenceEquals(dock, root) && string.IsNullOrEmpty(dock.Id))
                dock.Id = DynamicPrefix + Guid.NewGuid().ToString("N");
    }

    internal void Initialize(IDock dock)
    {
        if (_known.TryGetValue(dock, out _)) return;
        // Before Factory.InitDockable publishes owners/contexts, replace a copied ID.
        // Never rename a registered object during reparenting, focusing or persistence.
        dock.Id = DynamicPrefix + Guid.NewGuid().ToString("N");
        Register(dock);
    }
}
