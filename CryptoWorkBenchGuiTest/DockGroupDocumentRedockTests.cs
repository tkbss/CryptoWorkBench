using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockGroupDocumentRedockTests
{
    [Test]
    public Task DocumentCaptionStartsDockDragAndRespectsProtectedStates() => GuiTestSession.Session.Dispatch(async () =>
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows document caption integration.");
        await using var h = new DockToolTests.Host();
        var host = h.Float(DockShellState.DocumentId);
        var grip = host.GetVisualDescendants().OfType<Border>().Single(c => AutomationProperties.GetAutomationId(c) == "DockDocumentWindowGrip");
        var point = grip.TranslatePoint(new Avalonia.Point(12, 12), host)!.Value;
        int begins = 0;
        h.Layout.Factory.WindowMoveDragBegin += (_, _) => begins++;
        void Press() { host.MouseDown(point, MouseButton.Left); host.MouseUp(point, MouseButton.Left); }
        h.Service.EditorCoordinator.Observe(Press);
        Assert.That(begins, Is.Zero, "The caption must respect observer admission.");
        using (h.Service.EditorCoordinator.TryBegin()) Press();
        Assert.That(begins, Is.Zero, "The caption must respect pending coordinator work.");
        Press();
        Assert.That(begins, Is.EqualTo(1), "Real routed caption input must enter Dock's whole-window pipeline.");
        host.Close(); Assert.That(host.IsVisible, Is.True, "The only document remains protected.");
        Assert.That(h.Layout.Pane(DockShellState.DocumentId).CanClose, Is.False);
        return true;
    }, CancellationToken.None);

    internal static IDockable FloatScenario(DockShellLayout layout, string scenario)
    {
        var document = scenario.StartsWith("document", StringComparison.Ordinal);
        var pane = layout.Pane(document ? DockShellState.DocumentId : DockShellState.VariablesId);
        if (scenario == "group")
        {
            // Match the real mouse reproduction: the original fixed Diagnostics anchor
            // travels into the native host, then receives all five original tool models.
            var anchor = (IDock)layout.Pane(DockShellState.DiagnosticsId).Owner!;
            foreach (var id in DockLayoutStore.PaneIds.Where(id => id != DockShellState.DocumentId))
            {
                var tool = layout.Pane(id);
                if (!ReferenceEquals(tool.Owner, anchor)) layout.Factory.MoveDockable((IDock)tool.Owner!, anchor, tool, null);
            }
            layout.Factory.FloatDockable(anchor);
            return anchor;
        }
        if (scenario == "document-anchor")
        {
            var anchor = (IDock)pane.Owner!;
            layout.Factory.FloatDockable(anchor);
            return anchor;
        }
        layout.Float(pane.Id);
        if (scenario == "nested")
        {
            var output = layout.Pane(DockShellState.OutputId);
            new DockManager(new DockService()).ValidateDockable(output, pane.Owner!, DragAction.Move, DockOperation.Right, true);
            return layout.Factory.FindRoot(pane)!.ActiveDockable!;
        }
        return pane;
    }

    internal static IDock Target(DockShellLayout layout, string scenario) => scenario.StartsWith("document", StringComparison.Ordinal)
        ? DockShellLayout.VisibleTree(layout.Root, false).OfType<IDocumentDock>().First(d => d.Id == DockShellLayout.Documents)
        : DockShellLayout.VisibleTree(layout.Root, false).OfType<IToolDock>().First(d => d.Id == DockShellLayout.Tools);

    [TestCase("single")][TestCase("group")][TestCase("nested")][TestCase("document-tab")][TestCase("document-anchor")]
    public Task RepeatedDropPreservesIdentityOtherHostAndShutdown(string scenario) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var otherId = scenario == "group" ? DockShellState.DocumentId : DockShellState.DiagnosticsId;
        var other = h.Float(otherId);
        var editor = h.Session.Editor; var text = editor.Document; var undo = text.UndoStack;
        h.Service.Mutate(() => editor.Text = "// before");
        h.Service.Mutate(() => text.Insert(text.TextLength, " after"));
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
        Control[] views = [h.Presentation.Variables!, h.Presentation.Output!, h.Presentation.Info!, h.Presentation.Diagnostics!, h.Presentation.Status!];
        var contexts = views.Select(v => v.DataContext).ToArray();
        foreach (var operation in new[] { DockOperation.Fill, DockOperation.Right, DockOperation.Bottom })
        {
            var source = FloatScenario(h.Layout, scenario); h.Pump();
            var model = h.Layout.Factory.FindRoot(source)!.Window!;
            var host = (Window)model.Host!;
            int closed = 0; host.Closed += (_, _) => closed++;
            Assert.That(new DockManager(new DockService()).ValidateDockable(source, Target(h.Layout, scenario), DragAction.Move, operation, true), Is.True);
            h.Pump();
            Assert.That(host.IsVisible, Is.False, $"{scenario}/{operation}");
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(h.Layout.Root.Windows, Does.Not.Contain(model));
            Assert.That(h.Layout.Factory.HostWindows, Does.Not.Contain(host));
            Assert.That(other.IsVisible, Is.True);
            Assert.That(h.Layout.Factory.HostWindows, Has.Count.EqualTo(1));
            foreach (var id in DockLayoutStore.PaneIds)
                Assert.That(DockShellLayout.VisibleTree(h.Layout.Root).Count(d => ReferenceEquals(d, h.Layout.Pane(id))), Is.EqualTo(1));
            Assert.That(h.Session.Editor, Is.SameAs(editor)); Assert.That(editor.Document, Is.SameAs(text));
            Assert.That(text.UndoStack, Is.SameAs(undo)); Assert.That(undo.CanUndo, Is.True);
            Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
            Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
            Assert.That(views.Select(v => v.DataContext), Is.EqualTo(contexts));
            Assert.That(h.Presentation.Variables!.DataContext, Is.SameAs(h.Variables));
            Assert.That(h.Presentation.Info!.DataContext, Is.SameAs(h.Info));
        }
        h.Service.Mutate(() => editor.Undo()); Assert.That(text.Text, Is.EqualTo("// before"));
        h.Service.Mutate(() => editor.Redo()); Assert.That(text.Text, Is.EqualTo("// before after"));
        h.Window.Close(); await DockEditorTests.Wait(() => !h.Window.IsVisible);
        Assert.That(other.IsVisible, Is.False);
        Assert.That(h.Layout.Factory.HostWindows, Is.Empty); Assert.That(h.Layout.Factory.DockControls, Is.Empty);
        Assert.That(h.Service.HasRegisteredEditor, Is.False);
        return true;
    }, CancellationToken.None);

    [TestCase("single", false)][TestCase("group", false)][TestCase("nested", false)][TestCase("document-tab", false)][TestCase("document-anchor", false)]
    [TestCase("single", true)][TestCase("group", true)][TestCase("nested", true)][TestCase("document-tab", true)][TestCase("document-anchor", true)]
    public Task McpLeaseRejectsDropAndKeepsEditorAuthority(string scenario, bool shutdown) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var source = FloatScenario(h.Layout, scenario); h.Pump();
        var model = h.Layout.Factory.FindRoot(source)!.Window!; var host = (Window)model.Host!;
        var before = h.Service.CaptureSaveIntent()!.Snapshot;
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("f03-redock", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var lease = (await h.Service.AcquireEditorAsync(caller, new("2", h.Service.InstanceId, before.EditorId, Guid.NewGuid(), before.Revision), default)).Result!;
        Assert.That(lease, Is.Not.Null);
        if (OperatingSystem.IsWindows() && scenario.StartsWith("document", StringComparison.Ordinal))
        {
            int begins = 0; h.Layout.Factory.WindowMoveDragBegin += (_, _) => begins++;
            var grip = host.GetVisualDescendants().OfType<Border>().Single(c => AutomationProperties.GetAutomationId(c) == "DockDocumentWindowGrip");
            var point = grip.TranslatePoint(new Point(12, 12), host)!.Value;
            host.MouseDown(point, MouseButton.Left); host.MouseUp(point, MouseButton.Left);
            Assert.That(begins, Is.Zero, "The document caption must not bypass an active MCP lease.");
        }
        var manager = new DockManager(new DockService());
        Assert.That(manager.ValidateDockable(source, Target(h.Layout, scenario), DragAction.Move, DockOperation.Fill, true), Is.False);
        host.Close(); Assert.That(host.IsVisible, Is.True);
        Assert.That(h.Layout.Root.Windows, Does.Contain(model));
        var edit = await h.Service.UpdateEditorV2Async(caller, new("2", h.Service.InstanceId, before.EditorId, lease.LeaseId, before.Revision, EditorEditMode.Append, "// lease retained"), default);
        Assert.That(edit.Error, Is.Null);
        if (shutdown)
        {
            h.Window.Close(); await DockEditorTests.Wait(() => !h.Window.IsVisible);
            Assert.That(host.IsVisible, Is.False);
            Assert.That(h.Layout.Factory.HostWindows, Is.Empty);
            Assert.That(h.Layout.Factory.DockControls, Is.Empty);
            Assert.That(h.Service.HasRegisteredEditor, Is.False);
            return true;
        }
        await h.Service.ReleaseEditorAsync(caller, new("2", h.Service.InstanceId, before.EditorId, lease.LeaseId), default);
        Assert.That(manager.ValidateDockable(source, Target(h.Layout, scenario), DragAction.Move, DockOperation.Fill, true), Is.True);
        h.Pump(); Assert.That(host.IsVisible, Is.False);
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot.EditorId, Is.EqualTo(before.EditorId));
        return true;
    }, CancellationToken.None);

    // Whole-anchor drags are distinct from moving the contents of a newly created floating dock.
    [TestCase(false)]
    [TestCase(true)]
    public Task WholeAnchorCanReturnAndReleaseItsNativeHost(bool document) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var pane = h.Layout.Pane(document ? DockShellState.DocumentId : DockShellState.VariablesId);
        var anchor = (IDock)pane.Owner!;
        var editor = h.Session.Editor;
        var text = editor.Document;
        var undo = text.UndoStack;
        h.Layout.Factory.FloatDockable(anchor);
        h.Pump();
        var model = h.Layout.Factory.FindRoot(pane)!.Window!;
        var host = (Window)model.Host!;
        var mainTree = DockShellLayout.VisibleTree(h.Layout.Root, false).ToArray();
        if (document)
            Assert.That(mainTree.OfType<IDocumentDock>().Any(), Is.True, "Whole document drag must leave a main-window return anchor.");
        IDock target = document
            ? mainTree.OfType<IDocumentDock>().Single()
            : mainTree.OfType<IToolDock>().Single(d => d.Id == DockShellLayout.Diagnostics);
        var manager = new DockManager(new DockService());
        Assert.That(manager.ValidateDockable(anchor, target, DragAction.Move, DockOperation.Fill, true), Is.True);
        h.Pump();
        Assert.That(host.IsVisible, Is.False, "An emptied non-collapsable anchor must not retain its floating host.");
        Assert.That(h.Layout.Root.Windows, Does.Not.Contain(model));
        Assert.That(h.Layout.Factory.HostWindows, Does.Not.Contain(host));
        Assert.That(h.Session.Editor, Is.SameAs(editor));
        Assert.That(editor.Document, Is.SameAs(text));
        Assert.That(text.UndoStack, Is.SameAs(undo));
        Assert.That(h.Layout.Pane(DockShellState.DocumentId).CanClose, Is.False);
        return true;
    }, CancellationToken.None);

    [Test]
    public Task DocumentTabCanReturnToEmptyMainAnchor() => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var pane = h.Layout.Pane(DockShellState.DocumentId);
        var anchor = (IDocumentDock)pane.Owner!;
        var host = h.Float(pane.Id);
        var manager = new DockManager(new DockService());
        Assert.That(DockShellLayout.VisibleTree(h.Layout.Root, false), Does.Contain(anchor));
        Assert.That(manager.ValidateDockable(pane, anchor, DragAction.Move, DockOperation.Fill, true), Is.True);
        h.Pump();
        Assert.That(pane.Owner, Is.SameAs(anchor));
        Assert.That(host.IsVisible, Is.False);
        return true;
    }, CancellationToken.None);
}
