using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockLayoutRoleTests
{
    [Test]
    public Task AlternatingSideDropsAndFillKeepPromotedSelectionValid() => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        foreach (var operation in new[] { DockOperation.Fill, DockOperation.Left, DockOperation.Right, DockOperation.Top, DockOperation.Bottom, DockOperation.Fill })
        {
            h.Layout.Float(DockShellState.DocumentId); h.Pump();
            var pane = h.Layout.Pane(DockShellState.DocumentId);
            var target = DockShellLayout.VisibleTree(h.Layout.Root, false).OfType<IDocumentDock>().Single(d => d.Id == DockShellLayout.Documents);
            Assert.That(new DockManager(new DockService()).ValidateDockable(pane.Owner!, target, DragAction.Move, operation, true), Is.True);
            h.Show(pane.Id);
            var normal = h.Layout.Capture(Bounds);
            Assert.That(h.Layout.Maximize(pane.Id), Is.True); h.Pump();
            Assert.That(h.Layout.Restore(), Is.True); h.Pump();
            Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(normal));
        }
        return true;
    }, CancellationToken.None);

    internal static readonly LayoutWindow Bounds = new(0, 0, 1200, 800);
    public static IEnumerable<TestCaseData> Drops()
    {
        foreach (var operation in new[] { DockOperation.Fill, DockOperation.Left, DockOperation.Right, DockOperation.Top, DockOperation.Bottom })
        foreach (var group in new[] { false, true })
        foreach (var entry in new[] { "capture", "main", "pane" })
            yield return new TestCaseData(operation, group, entry);
    }

    internal static void Click(DockShellWindow shell, Control view, string entry, string command)
    {
        MenuItem menu;
        if (entry == "main") menu = (MenuItem)shell.FindControl<Menu>("ShellMenu")!.Items[1]!;
        else
        {
            var bar = view.GetVisualAncestors().OfType<DockPanel>()
                .SelectMany(p => p.Children.OfType<DockPanel>())
                .SelectMany(p => p.Children.OfType<Menu>()).First();
            menu = (MenuItem)bar.Items[0]!;
        }
        var item = menu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, command));
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    [TestCaseSource(nameof(Drops))]
    public Task LegalDocumentDropSupportsCaptureMenusRestoreAndReset(DockOperation operation, bool group, string entry)
        => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var editor = h.Session.Editor; var document = editor.Document; var undo = document.UndoStack;
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
        for (var cycle = 0; cycle < 3; cycle++)
        {
            h.Layout.Float(DockShellState.DocumentId); h.Pump();
            var pane = h.Layout.Pane(DockShellState.DocumentId);
            var source = group ? pane.Owner! : pane;
            var host = (Window)h.Layout.Factory.FindRoot(pane)!.Window!.Host!;
            var target = DockShellLayout.VisibleTree(h.Layout.Root, false).OfType<IDocumentDock>().Single(d => d.Id == DockShellLayout.Documents);
            Assert.That(new DockManager(new DockService()).ValidateDockable(source, target, DragAction.Move, operation, true), Is.True);
            h.Pump();
            Assert.That(host.IsVisible, Is.False);
            h.Layout.Show(pane.Id); h.Pump();
            if (entry == "capture")
            {
                // On the uncorrected product this line throws precisely:
                // System.IO.InvalidDataException: Invalid fixed-shell anchors.
                // Do not replace it with an assertion expecting an arbitrary exception.
                var before = h.Layout.Capture(Bounds);
                Assert.That(h.Layout.Maximize(pane.Id), Is.True);
                Assert.That(h.Layout.Restore(), Is.True);
                Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(before));
            }
            else
            {
                Click(h.Window, h.Session.View, entry, "Maximize Active View");
                Assert.That(h.Layout.IsMaximized, Is.True);
                h.Pump();
                Click(h.Window, h.Session.View, entry, "Restore Layout");
                Assert.That(h.Layout.IsMaximized, Is.False);
            }
            Assert.That(DockLayoutStore.Decode(h.Layout.Capture(Bounds)), Is.Not.Null);
            Assert.That(h.Session.Editor, Is.SameAs(editor));
            Assert.That(editor.Document, Is.SameAs(document));
            Assert.That(document.UndoStack, Is.SameAs(undo));
            Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
            Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
        }
        h.Show(DockShellState.DocumentId);
        Assert.That(TopLevel.GetTopLevel(h.Session.View), Is.SameAs(h.Window), "Restore must display the original editor, including when Dock reuses a cached tab template.");
        if (entry == "capture") Assert.That(h.Layout.Reset(), Is.True);
        else Click(h.Window, h.Session.View, entry, "Reset to Default Layout");
        Assert.That(DockShellLayout.VisibleTree(h.Layout.Root).Count(p => p is IDocument or ITool), Is.EqualTo(6));
        Assert.That(h.Session.Editor.Document, Is.SameAs(document));
        Assert.That(DockLayoutStore.Decode(h.Layout.Capture(Bounds)), Is.Not.Null);
        return true;
    }, CancellationToken.None);

    [TestCase(DockOperation.Fill)][TestCase(DockOperation.Left)][TestCase(DockOperation.Right)]
    [TestCase(DockOperation.Top)][TestCase(DockOperation.Bottom)]
    public Task PersistedFloatingDocumentCanRedockCaptureAndFocus(DockOperation operation)
        => GuiTestSession.Session.Dispatch(async () =>
    {
        LoadedLayout saved;
        await using (var original = new DockToolTests.Host())
        {
            original.Float(DockShellState.DocumentId);
            saved = DockLayoutStore.Decode(original.Layout.Capture(Bounds));
        }
        await using var h = new DockToolTests.Host(saved);
        var pane = h.Layout.Pane(DockShellState.DocumentId);
        var host = (Window)h.Layout.Factory.FindRoot(pane)!.Window!.Host!;
        Assert.That(host.IsVisible, Is.True);
        var target = DockShellLayout.VisibleTree(h.Layout.Root, false).OfType<IDocumentDock>().Single();
        Assert.That(new DockManager(new DockService()).ValidateDockable(pane.Owner!, target, DragAction.Move, operation, true), Is.True);
        h.Pump(); Assert.That(host.IsVisible, Is.False);
        Assert.That(h.Layout.Root.Windows, Is.Empty);
        var before = h.Layout.Capture(Bounds);
        Assert.That(h.Layout.Maximize(pane.Id) && h.Layout.Restore(), Is.True);
        Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(before));
        return true;
    }, CancellationToken.None);

    [TestCase(DockOperation.Fill)][TestCase(DockOperation.Left)][TestCase(DockOperation.Right)]
    [TestCase(DockOperation.Top)][TestCase(DockOperation.Bottom)]
    public Task WholeFixedDocumentAnchorGetsDistinctReturnRole(DockOperation operation)
        => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var original = (IDock)h.Layout.Pane(DockShellState.DocumentId).Owner!;
        h.Layout.Factory.FloatDockable(original); h.Pump();
        var host = (Window)h.Layout.Factory.FindRoot(original)!.Window!.Host!;
        var target = DockShellLayout.VisibleTree(h.Layout.Root, false).OfType<IDocumentDock>().Single();
        Assert.That(target, Is.Not.SameAs(original));
        Assert.That(new DockManager(new DockService()).ValidateDockable(original, target, DragAction.Move, operation, true), Is.True);
        h.Pump(); Assert.That(host.IsVisible, Is.False);
        Assert.That(DockShellLayout.VisibleTree(h.Layout.Root, false).Count(p => p.Id == DockShellLayout.Documents), Is.EqualTo(1));
        var before = h.Layout.Capture(Bounds);
        Assert.That(h.Layout.Maximize(DockShellState.DocumentId) && h.Layout.Restore(), Is.True);
        Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(before));
        return true;
    }, CancellationToken.None);
}
