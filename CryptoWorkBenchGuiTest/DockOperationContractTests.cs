using System.ComponentModel;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

// These are executable acceptance criteria. Known A04 failures remain visible:
// no Explicit/Ignore, expected-failure inversion, sleeps, or automatic retry.
[NonParallelizable]
public sealed class DockOperationContractTests
{
    private static Task Run(Action<DockToolTests.Host> test) => GuiTestSession.Session.Dispatch(async () =>
    { await using var host = new DockToolTests.Host(); test(host); return true; }, CancellationToken.None);

    private static IDock Target(DockToolTests.Host h) => (IDock)h.Layout.Pane(DockShellState.DiagnosticsId).Owner!;
    private static void Move(DockToolTests.Host h)
    {
        var pane = h.Layout.Pane(DockShellState.VariablesId);
        h.Layout.Factory.MoveDockable((IDock)pane.Owner!, Target(h), pane, null);
    }

    [TestCase(false)]
    public Task ClosingMustRejectIndependentOtherHostClose(bool throughFactory) => Run(h =>
    {
        var other = h.Float(DockShellState.InfoId);
        var model = h.Layout.Factory.FindRoot(h.Layout.Pane(DockShellState.InfoId))!.Window!;
        bool fired = false;
        EventHandler<DockableClosingEventArgs> handler = (_, e) =>
        {
            if (fired || e.Dockable != h.Layout.Pane(DockShellState.VariablesId)) return;
            fired = true;
            if (throughFactory) h.Layout.Factory.CloseWindow(model); else other.Close();
        };
        h.Layout.Factory.DockableClosing += handler;
        try { h.Layout.Factory.CloseDockable(h.Layout.Pane(DockShellState.VariablesId)); }
        finally { h.Layout.Factory.DockableClosing -= handler; }
        Assert.That(fired, Is.True);
        Assert.That(other.IsVisible && h.Layout.Root.Windows!.Contains(model), Is.True,
            "A foreign host must survive both the native and raw Factory entry.");
    });

    [Test]
    public Task PropertyChangedRejectsReset() => Run(h =>
    {
        var pane = h.Layout.Pane(DockShellState.VariablesId);
        var root = h.Layout.Root; bool fired = false; bool? accepted = null;
        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (fired || e.PropertyName != nameof(IDockable.Owner)) return;
            fired = true; accepted = h.Layout.Reset();
        };
        var notify = (INotifyPropertyChanged)pane;
        notify.PropertyChanged += handler;
        try { Move(h); }
        finally { notify.PropertyChanged -= handler; }
        Assert.That(fired, Is.True);
        Assert.That(accepted, Is.False);
        Assert.That(h.Layout.Root, Is.SameAs(root));
        Assert.That(pane.Owner, Is.SameAs(Target(h)), "The legitimate move must finish.");
    });

    [Test]
    public Task LegitimateSourceCleanupCompletesInsideObserver() => Run(h =>
    {
        var other = h.Float(DockShellState.InfoId);
        var source = h.Float(DockShellState.VariablesId);
        var model = h.Layout.Factory.FindRoot(h.Layout.Pane(DockShellState.VariablesId))!.Window!;
        int observer = 0;
        EventHandler<WindowRemovedEventArgs> handler = (_, e) =>
        { if (e.Window == model) observer = Math.Max(observer, h.Service.EditorCoordinator.ObserverDepth); };
        var editor = h.Session.Editor; var text = editor.Document; var undo = text.UndoStack;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
        h.Layout.Factory.WindowRemoved += handler;
        try { Move(h); }
        finally { h.Layout.Factory.WindowRemoved -= handler; }
        Assert.That(observer, Is.GreaterThan(0));
        Assert.That(source.IsVisible, Is.False);
        Assert.That(h.Layout.Root.Windows, Does.Not.Contain(model));
        Assert.That(other.IsVisible, Is.True);
        Assert.That(h.Session.Editor, Is.SameAs(editor));
        Assert.That(editor.Document, Is.SameAs(text)); Assert.That(text.UndoStack, Is.SameAs(undo));
        Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
        Assert.That(DockLayoutStore.Decode(h.Layout.Capture(DockPersistenceCleanupTests.Bounds)), Is.Not.Null);
    });

    [Test]
    public Task PartialMoveFailureMustLatchExistingLayoutFault() => Run(h =>
    {
        var source = h.Layout.Pane(DockShellState.DocumentId);
        var original = new InvalidOperationException("A04 original move failure");
        EventHandler<DockableRemovedEventArgs> handler = (_, e) => { if (e.Dockable == source) throw original; };
        h.Layout.Factory.DockableRemoved += handler;
        Exception? caught;
        try { caught = Assert.Catch(() => h.Layout.Factory.MoveDockable((IDock)source.Owner!, Target(h), source, null)); }
        finally { h.Layout.Factory.DockableRemoved -= handler; }
        Assert.That(caught, Is.SameAs(original));
        Assert.That(h.Service.EditorCoordinator.ObserverDepth, Is.Zero);
        Assert.That(DockShellLayout.VisibleTree(h.Layout.Root), Does.Not.Contain(source), "The injected failure is after removal.");
        Assert.Throws<InvalidDataException>(() => h.Layout.Capture(DockPersistenceCleanupTests.Bounds));
        Assert.That(h.Layout.LayoutFaulted, Is.True, "An invalid partial tree must latch the existing A02 fault state.");
    });

    [Test]
    public Task LeaseRejectsIndependentFactoryAndManagerBeforeMutation() => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var before = h.Layout.Capture(DockPersistenceCleanupTests.Bounds);
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("a04-test", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var lease = (await h.Service.AcquireEditorAsync(caller, new("2", h.Service.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
        Assert.That(h.Session.CanRelocate, Is.False);
        Move(h);
        var accepted = new DockManager(new DockService()).ValidateDockable(
            h.Layout.Pane(DockShellState.InfoId), Target(h), DragAction.Move, DockOperation.Fill, true);
        Assert.That(accepted, Is.False);
        Assert.That(h.Layout.Capture(DockPersistenceCleanupTests.Bounds), Is.EqualTo(before));
        await h.Service.ReleaseEditorAsync(caller, new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId), default);
        return true;
    }, CancellationToken.None);

    // Public-hook characterization: green tests assert observed API behavior,
    // not that this behavior satisfies the A04 acceptance contract.
    private sealed class HookFactory : Dock.Model.Mvvm.Factory, IDockOperationObserver
    {
        internal int Begun;
        internal Exception? MoveError;
        internal Exception? CleanupError;
        public IDockOperationScope? BeginDockOperation(IDockable source, IDockable target, DockOperation operation)
        { Begun++; return CleanupError == null ? null : new ErrorScope(CleanupError); }
        public override void MoveDockable(IDock source, IDock target, IDockable pane, IDockable? relative)
        { if (MoveError != null) throw MoveError; base.MoveDockable(source, target, pane, relative); }
        private sealed class ErrorScope(Exception error) : IDockOperationScope
        { public void Complete() { } public void Dispose() => throw error; }
    }

    private static (HookFactory Factory, IDockable Pane, IDock Source, IDock Target) HookLayout()
    {
        var factory = new HookFactory();
        var pane = factory.CreateTool();
        var source = factory.CreateToolDock(); source.IsCollapsable = false;
        source.VisibleDockables = factory.CreateList<IDockable>(pane);
        var target = factory.CreateToolDock(); target.IsCollapsable = false;
        target.VisibleDockables = factory.CreateList<IDockable>(factory.CreateTool());
        var root = factory.CreateRootDock();
        root.VisibleDockables = factory.CreateList<IDockable>(source, target);
        factory.InitLayout(root);
        return (factory, pane, source, target);
    }

    [Test]
    public void PublicHookNullScopeDoesNotVetoManagerMutation()
    {
        var h = HookLayout();
        Assert.That(new DockManager(new DockService()).ValidateDockable(h.Pane, h.Target, DragAction.Move, DockOperation.Fill, true), Is.True);
        Assert.That(h.Factory.Begun, Is.GreaterThan(0));
        Assert.That(h.Pane.Owner, Is.SameAs(h.Target));
    }

    [Test]
    public void PublicHookDisposeExceptionMasksOriginalManagerException()
    {
        var h = HookLayout();
        h.Factory.MoveError = new InvalidOperationException("original");
        h.Factory.CleanupError = new InvalidOperationException("cleanup");
        var caught = Assert.Catch(() => new DockManager(new DockService()).ValidateDockable(h.Pane, h.Target, DragAction.Move, DockOperation.Fill, true));
        Assert.That(caught, Is.SameAs(h.Factory.CleanupError));
        Assert.That(caught, Is.Not.SameAs(h.Factory.MoveError));
    }
}

