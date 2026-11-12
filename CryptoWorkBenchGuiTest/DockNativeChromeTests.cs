using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockNativeChromeTests
{
    internal static bool HasDocumentGrip(Window host) => host.GetVisualDescendants().OfType<Border>()
        .Any(c => AutomationProperties.GetAutomationId(c) == "DockDocumentWindowGrip" && c.IsEffectivelyVisible);

    [TestCase("single", false)]
    [TestCase("group", false)]
    [TestCase("document-tab", true)]
    [TestCase("mixed", true)]
    public Task ChromeFollowsActualSessionDocument(string scenario, bool documentChrome) => GuiTestSession.Session.Dispatch(async () =>
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows host chrome.");
        await using var h = new DockToolTests.Host();
        var editor = h.Session.Editor; var document = editor.Document; var undo = document.UndoStack;
        var source = DockGroupDocumentRedockTests.FloatScenario(h.Layout, scenario == "mixed" ? "document-tab" : scenario);
        var host = (Window)h.Layout.Factory.FindRoot(source)!.Window!.Host!;
        if (scenario == "mixed")
            Assert.That(new DockManager(new DockService()).ValidateDockable(h.Layout.Pane(DockShellState.VariablesId),
                source.Owner!, DragAction.Move, DockOperation.Right, true), Is.True);
        DockEditorTests.Pump(host);
        Assert.That(HasDocumentGrip(host), Is.EqualTo(documentChrome));
        Assert.That(h.Session.Editor, Is.SameAs(editor));
        Assert.That(editor.Document, Is.SameAs(document));
        Assert.That(document.UndoStack, Is.SameAs(undo));
        host.Close(); h.Pump();
        Assert.That(host.IsVisible, Is.EqualTo(documentChrome), "Only document-bearing hosts must veto user close.");
        return true;
    }, CancellationToken.None);

    [Test]
    public Task MixedHostReclassifiesWhenDocumentEntersAndLeaves() => GuiTestSession.Session.Dispatch(async () =>
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows host chrome.");
        await using var h = new DockToolTests.Host();
        var editor = h.Session.Editor; var document = editor.Document; var undo = document.UndoStack;
        var host = h.Float(DockShellState.VariablesId);
        Assert.That(HasDocumentGrip(host), Is.False);
        var manager = new DockManager(new DockService());
        // A document requires a document target: move its group beside the tool group.
        var group = (IDock)h.Layout.Pane(DockShellState.DocumentId).Owner!;
        Assert.That(manager.ValidateDockable(group, h.Layout.Pane(DockShellState.VariablesId).Owner!,
            DragAction.Move, DockOperation.Right, true), Is.True);
        DockEditorTests.Pump(host);
        Assert.That(HasDocumentGrip(host), Is.True);
        h.Layout.Dock(DockShellState.DocumentId); DockEditorTests.Pump(host); h.Pump();
        Assert.That(host.IsVisible, Is.True);
        Assert.That(HasDocumentGrip(host), Is.False);
        Assert.That(h.Session.Editor, Is.SameAs(editor));
        Assert.That(editor.Document, Is.SameAs(document));
        Assert.That(document.UndoStack, Is.SameAs(undo));
        host.Close(); h.Pump(); Assert.That(host.IsVisible, Is.False);
        return true;
    }, CancellationToken.None);
}
