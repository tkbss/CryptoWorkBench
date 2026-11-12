using Avalonia.Controls;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model.Core;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockIndependentWindowTests
{
    internal static readonly string[] Panes = [DockShellState.DocumentId, DockShellState.VariablesId, DockShellState.InfoId];

    [Test]
    public Task ThreeHostsHaveIndependentStateAndCloseWithShell() => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var editor = h.Session.Editor; var document = editor.Document; var undo = document.UndoStack;
        var hosts = Panes.Select(h.Float).ToArray();
        Window[] windows = [h.Window, .. hosts];
        foreach (var host in hosts)
        {
            Assert.That(DockNativeChromeTests.HasNativeChrome(host), Is.True);
            var model = h.Layout.Factory.HostWindows.Single(w => ReferenceEquals(w, host)).Window!;
            Assert.That(model.OwnerMode, Is.EqualTo(DockWindowOwnerMode.None));
            Assert.That(model.ShowInTaskbar, Is.True);
        }
        foreach (var selected in windows)
        foreach (var state in new[] { WindowState.Minimized, WindowState.Normal, WindowState.Maximized, WindowState.Normal })
        {
            selected.WindowState = state; h.Pump();
            Assert.That(selected.WindowState, Is.EqualTo(state));
            Assert.That(windows.Where(w => w != selected).All(w => w.WindowState == WindowState.Normal), Is.True);
        }
        h.Layout.Dock(DockShellState.VariablesId); h.Pump();
        Assert.That(hosts[1].IsVisible, Is.False);
        Assert.That(hosts[0].IsVisible && hosts[2].IsVisible, Is.True);
        Assert.That(h.Session.Editor, Is.SameAs(editor));
        Assert.That(editor.Document, Is.SameAs(document));
        Assert.That(document.UndoStack, Is.SameAs(undo));
        h.Window.Close(); await DockEditorTests.Wait(() => !h.Window.IsVisible);
        Assert.That(hosts.All(w => !w.IsVisible), Is.True);
        Assert.That(h.Layout.Factory.HostWindows, Is.Empty);
        Assert.That(h.Service.HasRegisteredEditor, Is.False);
    }, CancellationToken.None);

    [Test]
    public Task RestoreReappliesUnownedPresentationAndResetPreservesSession() => GuiTestSession.Session.Dispatch(async () =>
    {
        string saved;
        await using (var h = new DockToolTests.Host())
        {
            foreach (var id in Panes) h.Float(id);
            saved = h.Layout.Capture(DockLayoutRoleTests.Bounds);
        }
        await using var restored = new DockToolTests.Host(DockLayoutStore.Decode(saved));
        var hosts = restored.Layout.Factory.HostWindows.Cast<Window>().ToArray();
        Assert.That(hosts, Has.Length.EqualTo(3));
        Assert.That(hosts.All(DockNativeChromeTests.HasNativeChrome), Is.True);
        Assert.That(hosts.Select(w => w.Title), Is.EquivalentTo(new[] { "CryptoScript", "Variables", "Info" }));
        var editor = restored.Session.Editor;
        Assert.That(restored.Layout.Reset(), Is.True); restored.Pump();
        Assert.That(hosts.All(w => !w.IsVisible), Is.True);
        Assert.That(restored.Layout.Factory.HostWindows, Is.Empty);
        Assert.That(restored.Session.Editor, Is.SameAs(editor));
    }, CancellationToken.None);
}
