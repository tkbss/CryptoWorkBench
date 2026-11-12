using Avalonia.Controls;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockMouseRedockTests
{
    internal static bool Drop(DockShellLayout layout, IDockable source, DockOperation operation = DockOperation.Fill)
    {
        var target = DockShellLayout.VisibleTree(layout.Root, false).OfType<IToolDock>().Single(d => d.Id == DockShellLayout.Tools);
        // This is the same manager entry used by DockManagerState.Execute after mouse hit-testing.
        // It deliberately does NOT call Layout.Dock/Move and their post-command cleanup.
        return new DockManager(new DockService()).ValidateDockable(source, target, DragAction.Move, operation, true);
    }

    [TestCase(false, false)][TestCase(false, true)][TestCase(true, false)][TestCase(true, true)]
    public Task DropCommitClosesOnlyEmptiedSourceHost(bool group, bool otherHost) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var editor = h.Session.Editor; var document = editor.Document; var undo = document.UndoStack;
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
        var variablesView = h.Presentation.Variables; var infoView = h.Presentation.Info;
        var other = otherHost ? h.Float(DockShellState.DiagnosticsId) : null;
        for (int cycle = 0; cycle < 4; cycle++)
        {
            var host = h.Float(DockShellState.VariablesId);
            var pane = h.Layout.Pane(DockShellState.VariablesId);
            if (group)
            {
                var output = h.Layout.Pane(DockShellState.OutputId);
                h.Layout.Factory.MoveDockable((IDock)output.Owner!, (IDock)pane.Owner!, output, null); h.Pump();
            }
            var model = h.Layout.Factory.FindRoot(pane)!.Window!;
            int closed = 0; host.Closed += (_, _) => closed++;
            bool closeEvent = false; host.Closing += (_, _) => closeEvent = true;
            Assert.That(Drop(h.Layout, group ? pane.Owner! : pane), Is.True);
            h.Pump();
            TestContext.Out.WriteLine($"group={group}; cycle={cycle}; visible={host.IsVisible}; closed={closed}; closingRaised={closeEvent}; registered={h.Layout.Factory.HostWindows.Contains((IHostWindow)host)}; modelRetained={h.Layout.Root.Windows!.Contains(model)}; sourcePanes={(model.Layout is null ? -1 : DockShellLayout.VisibleTree(model.Layout).Count(d => d is ITool or IDocument))}; observer={h.Service.EditorCoordinator.ObserverDepth}");
            Assert.That(host.IsVisible, Is.False, "Direct drag/drop manager commit left an empty native host.");
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(h.Layout.Root.Windows, Does.Not.Contain(model));
            Assert.That(h.Layout.Factory.HostWindows, Does.Not.Contain(host));
            Assert.That(h.Layout.Factory.HostWindows.Count, Is.EqualTo(otherHost ? 1 : 0));
            Assert.That(h.Layout.Factory.DockControls.Count, Is.EqualTo(otherHost ? 2 : 1));
            if (other != null) Assert.That(other.IsVisible, Is.True);
            Assert.That(h.Layout.Factory.FindRoot(pane), Is.SameAs(h.Layout.Root));
            foreach (var id in DockLayoutStore.PaneIds)
                Assert.That(DockShellLayout.VisibleTree(h.Layout.Root).Count(d => ReferenceEquals(d, h.Layout.Pane(id))), Is.EqualTo(1));
            Assert.That(h.Session.Editor, Is.SameAs(editor)); Assert.That(editor.Document, Is.SameAs(document));
            Assert.That(document.UndoStack, Is.SameAs(undo));
            Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
            Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
            Assert.That(h.Presentation.Variables, Is.SameAs(variablesView)); Assert.That(h.Presentation.Info, Is.SameAs(infoView));
            Assert.That(variablesView!.DataContext, Is.SameAs(h.Variables)); Assert.That(infoView!.DataContext, Is.SameAs(h.Info));
        }
        h.Window.Close(); await DockEditorTests.Wait(() => !h.Window.IsVisible);
        Assert.That(h.Layout.Factory.HostWindows, Is.Empty); Assert.That(h.Layout.Factory.DockControls, Is.Empty);
        Assert.That(h.Service.HasRegisteredEditor, Is.False);
        h.Layout.Dispose(); h.Session.Dispose();
        return true;
    }, CancellationToken.None);

    [TestCase("pending")][TestCase("active")][TestCase("observer")][TestCase("busy")][TestCase("modal")]
    public Task DirectDropStillRejectsProtectedStates(string state) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var host = h.Float(DockShellState.VariablesId); var pane = h.Layout.Pane(DockShellState.VariablesId);
        var owner = pane.Owner; var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        void Rejected()
        {
            Assert.That(Drop(h.Layout, pane), Is.False);
            Assert.That(pane.Owner, Is.SameAs(owner)); Assert.That(host.IsVisible, Is.True);
            host.Close(); Assert.That(host.IsVisible, Is.True, "User close during protected state remains blocked.");
        }
        switch (state)
        {
            case "pending": using (h.Service.EditorCoordinator.TryBegin()) Rejected(); break;
            case "active": h.Service.EditorCoordinator.RunGui(Rejected); break;
            case "observer": h.Service.EditorCoordinator.Observe(Rejected); break;
            case "busy": using (InterpreterHostQueue.Reserve()) Rejected(); break;
            case "modal": h.Window.IsEnabled = false; try { Rejected(); } finally { h.Window.IsEnabled = true; } break;
        }
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
        Assert.That(Drop(h.Layout, pane), Is.True); h.Pump(); Assert.That(host.IsVisible, Is.False);
        return true;
    }, CancellationToken.None);

    [TestCase(false)][TestCase(true)]
    public Task CleanupPermissionDoesNotAuthorizeClosingAnotherHost(bool documentHost) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var other = h.Float(documentHost ? DockShellState.DocumentId : DockShellState.DiagnosticsId);
        var source = h.Float(DockShellState.VariablesId);
        bool observed = false;
        source.Closing += (_, _) =>
        {
            observed = true;
            Assert.That(h.Service.EditorCoordinator.ObserverDepth, Is.GreaterThan(0));
            Assert.That(h.Session.CanRelocate, Is.False);
            other.Close();
            Assert.That(other.IsVisible, Is.True, "Cleanup authorization is scoped to the emptied source only.");
        };
        Assert.That(Drop(h.Layout, h.Layout.Pane(DockShellState.VariablesId)), Is.True);
        h.Pump();
        Assert.That(observed, Is.True); Assert.That(source.IsVisible, Is.False);
        Assert.That(other.IsVisible, Is.True); Assert.That(h.Layout.Factory.HostWindows, Has.Count.EqualTo(1));
        return true;
    }, CancellationToken.None);

    [Test]
    public Task DirectDropDoesNotReleaseOrBypassMcpLease() => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var host = h.Float(DockShellState.VariablesId); var pane = h.Layout.Pane(DockShellState.VariablesId);
        var before = h.Service.CaptureSaveIntent()!.Snapshot;
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("redock-test", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var lease = (await h.Service.AcquireEditorAsync(caller, new("2", h.Service.InstanceId, before.EditorId, Guid.NewGuid(), before.Revision), default)).Result!;
        Assert.That(lease, Is.Not.Null);
        Assert.That(Drop(h.Layout, pane), Is.False); host.Close(); Assert.That(host.IsVisible, Is.True);
        var edit = await h.Service.UpdateEditorV2Async(caller, new("2", h.Service.InstanceId, before.EditorId, lease.LeaseId, before.Revision, EditorEditMode.Append, "// lease retained"), default);
        Assert.That(edit.Error, Is.Null);
        Assert.That(Drop(h.Layout, pane), Is.False);
        await h.Service.ReleaseEditorAsync(caller, new("2", h.Service.InstanceId, before.EditorId, lease.LeaseId), default);
        Assert.That(Drop(h.Layout, pane), Is.True); h.Pump(); Assert.That(host.IsVisible, Is.False);
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot.EditorId, Is.EqualTo(before.EditorId));
        return true;
    }, CancellationToken.None);
}
