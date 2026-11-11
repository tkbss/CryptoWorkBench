using System.ComponentModel;
using Avalonia.Controls;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockLayoutFailureTests
{
    [Test]
    public Task ActiveSelectionCycleIsRejectedWithoutEnteringUnboundedTraversal() => GuiTestSession.Session.Dispatch(() =>
    {
        using var layout = new DockShellLayout(new());
        var active = layout.Root.ActiveDockable;
        // Inject malformed metadata without asking Dock's active-tab setter to
        // initialize a root as its own child, and without constructing UI templates.
        layout.Root.Factory = null;
        layout.Root.ActiveDockable = layout.Root;
        layout.Root.Factory = layout.Factory;
        try { Assert.That(layout.ExecuteLayout(DockLayoutCommand.Maximize).Outcome, Is.EqualTo(DockLayoutOutcome.Rejected)); }
        finally
        {
            layout.Root.Factory = null; layout.Root.ActiveDockable = active; layout.Root.Factory = layout.Factory;
        }
        Assert.That(layout.Maximize(DockShellState.DocumentId) && layout.Restore(), Is.True);
        return true;
    }, CancellationToken.None);

    [Test]
    public Task SameIdReplacementCannotMasqueradeAsTheRegisteredPane() => Run(h =>
    {
        var pane = h.Layout.Pane(DockShellState.DocumentId);
        var owner = (IDock)pane.Owner!;
        var fake = new Dock.Model.Mvvm.Controls.Document { Id = pane.Id, Owner = owner, Factory = h.Layout.Factory };
        owner.VisibleDockables![0] = fake; owner.ActiveDockable = fake;
        try { Assert.Throws<InvalidDataException>(() => h.Layout.Capture(DockLayoutRoleTests.Bounds)); }
        finally { owner.VisibleDockables[0] = pane; owner.ActiveDockable = pane; }
        return Task.CompletedTask;
    });

    [Test]
    public Task BoundedTreeTraversalCanBeEnumeratedAgain() => Run(h =>
    {
        var tree = DockShellLayout.VisibleTree(h.Layout.Root);
        var first = tree.ToArray();
        Assert.That(tree.ToArray(), Is.EqualTo(first));
        return Task.CompletedTask;
    });

    private static Task Run(Func<DockToolTests.Host, Task> test) => GuiTestSession.Session.Dispatch(async () =>
    { await using var h = new DockToolTests.Host(); await test(h); return true; }, CancellationToken.None);

    [TestCase("main", false)][TestCase("pane", false)][TestCase("pane", true)]
    public Task InvalidAnchorIsRejectedAtBothMenusWithoutMutation(string entry, bool floating) => Run(h =>
    {
        if (floating) h.Float(DockShellState.DocumentId);
        h.Show(DockShellState.DocumentId);
        var tools = DockShellLayout.VisibleTree(h.Layout.Root, false).OfType<IToolDock>().Single(d => d.Id == DockShellLayout.Tools);
        var before = h.Layout.Capture(DockLayoutRoleTests.Bounds);
        var root = h.Layout.Root;
        var editor = h.Session.Editor; var text = editor.Document;
        tools.Id = DockShellLayout.Diagnostics;
        try
        {
            DockLayoutRoleTests.Click(h.Window, h.Session.View, entry, "Maximize Active View");
            Assert.That(h.Layout.LastLayoutResult!.Outcome, Is.EqualTo(DockLayoutOutcome.Rejected));
            Assert.That(h.Layout.IsMaximized, Is.False);
            Assert.That(h.Layout.Root, Is.SameAs(root));
            Assert.That(h.Window.IsVisible, Is.True);
            Assert.That(h.Service.HasRegisteredEditor, Is.True);
        }
        finally { tools.Id = DockShellLayout.Tools; }
        Assert.That(h.Layout.Capture(DockLayoutRoleTests.Bounds), Is.EqualTo(before));
        Assert.That(h.Layout.ExecuteLayout(DockLayoutCommand.Maximize, DockShellState.DocumentId).Outcome, Is.EqualTo(DockLayoutOutcome.Completed));
        Assert.That(h.Layout.ExecuteLayout(DockLayoutCommand.Restore).Outcome, Is.EqualTo(DockLayoutOutcome.Completed));
        Assert.That(h.Session.Editor, Is.SameAs(editor)); Assert.That(editor.Document, Is.SameAs(text));
        return Task.CompletedTask;
    });

    [TestCase(false, false)][TestCase(true, false)][TestCase(false, true)][TestCase(true, true)]
    public Task FocusNotificationFailureCompensatesExactReferences(bool restore, bool persistent) => Run(h =>
    {
        h.Layout.Float(DockShellState.InfoId); h.Pump();
        var before = h.Layout.Capture(DockLayoutRoleTests.Bounds);
        var hosts = h.Layout.Factory.HostWindows.ToArray();
        var text = h.Session.Editor.Document;
        if (restore) Assert.That(h.Layout.Maximize(DockShellState.DocumentId), Is.True);
        var target = (IDock)h.Layout.Pane(DockShellState.DocumentId).Owner!;
        var children = target.VisibleDockables; var active = target.ActiveDockable; var proportion = target.Proportion;
        var notify = (INotifyPropertyChanged)target;
        int calls = 0;
        PropertyChangedEventHandler fail = (_, args) =>
        {
            if (args.PropertyName == nameof(IDock.VisibleDockables) && (calls++ == 0 || persistent))
                throw new InvalidOperationException("A02 injected focus notification failure");
        };
        notify.PropertyChanged += fail;
        try
        {
            var result = h.Layout.ExecuteLayout(restore ? DockLayoutCommand.Restore : DockLayoutCommand.Maximize, DockShellState.DocumentId);
            Assert.That(result.Outcome, Is.EqualTo(DockLayoutOutcome.Recovered));
        }
        finally { notify.PropertyChanged -= fail; }
        Assert.That(calls, Is.GreaterThanOrEqualTo(1));
        Assert.That(target.VisibleDockables, Is.SameAs(children));
        Assert.That(target.ActiveDockable, Is.SameAs(active));
        Assert.That(target.Proportion, Is.EqualTo(proportion));
        Assert.That(h.Layout.IsMaximized, Is.EqualTo(restore));
        Assert.That(h.Layout.Capture(DockLayoutRoleTests.Bounds), Is.EqualTo(before));
        Assert.That(h.Layout.Factory.HostWindows, Is.EquivalentTo(hosts));
        Assert.That(h.Session.Editor.Document, Is.SameAs(text));
        if (restore) Assert.That(h.Layout.Restore(), Is.True);
        Assert.That(h.Layout.Maximize(DockShellState.DocumentId), Is.True);
        Assert.That(h.Layout.Restore(), Is.True);
        return Task.CompletedTask;
    });

    [Test]
    public Task FailedResetCompensationDisablesLayoutButPreservesSaveAndShutdown() => Run(async h =>
    {
        h.Service.Mutate(() => h.Session.Editor.Text = "// preserve after layout fault");
        h.Float(DockShellState.DocumentId);
        var editor = h.Session.Editor; var text = editor.Document;
        var intent = h.Service.CaptureSaveIntent();
        void FailPresentation() => throw new InvalidOperationException("A02 persistent reset presentation failure");
        h.Layout.RootChanged += FailPresentation;
        try { Assert.That(h.Layout.ExecuteLayout(DockLayoutCommand.Reset).Outcome, Is.EqualTo(DockLayoutOutcome.Faulted)); }
        finally { h.Layout.RootChanged -= FailPresentation; }
        Assert.That(h.Layout.LayoutFaulted, Is.True);
        Assert.That(h.Layout.ExecuteLayout(DockLayoutCommand.Maximize, DockShellState.DocumentId).Outcome, Is.EqualTo(DockLayoutOutcome.Faulted));
        Assert.Throws<InvalidOperationException>(() => h.Layout.Capture(DockLayoutRoleTests.Bounds));
        var root = h.Layout.Root;
        DockLayoutStore.Validate(root, []);
        var windows = root.Windows!.ToArray();
        h.Layout.Factory.FloatDockable(h.Layout.Pane(DockShellState.InfoId));
        Assert.That(root.Windows, Is.EqualTo(windows));
        Assert.That(h.Session.Editor, Is.SameAs(editor)); Assert.That(editor.Document, Is.SameAs(text));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(intent!.Snapshot));
        Assert.That(h.Service.HasRegisteredEditor, Is.True);
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "a02-save-" + Guid.NewGuid().ToString("N") + ".sbk");
        try
        {
            await h.Service.CommitSaveAsync(h.Service.CaptureSaveIntent()!, new(path, false, null, PreserveText: true));
            Assert.That(File.ReadAllText(path), Is.EqualTo("// preserve after layout fault"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    });

    [Test]
    public Task ResetPresentationFailureReinstallsPreviousLayoutWithSameBusinessObjects() => Run(h =>
    {
        h.Float(DockShellState.InfoId);
        h.Layout.Close(DockShellState.StatusId);
        var text = h.Session.Editor.Document; var undo = text.UndoStack;
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var prior = DockLayoutStore.Decode(h.Layout.Capture(DockLayoutRoleTests.Bounds));
        int notifications = 0;
        void FailOnce() { if (notifications++ == 0) throw new InvalidOperationException("A02 injected reset presentation failure"); }
        h.Layout.RootChanged += FailOnce;
        try { Assert.That(h.Layout.ExecuteLayout(DockLayoutCommand.Reset).Outcome, Is.EqualTo(DockLayoutOutcome.Recovered)); }
        finally { h.Layout.RootChanged -= FailOnce; }
        h.Pump();
        var after = DockLayoutStore.Decode(h.Layout.Capture(DockLayoutRoleTests.Bounds));
        Assert.That(after.ClosedTools, Is.EqualTo(prior.ClosedTools));
        Assert.That(DockShellLayout.VisibleTree(after.Root).Select(p => p.Id), Is.EqualTo(DockShellLayout.VisibleTree(prior.Root).Select(p => p.Id)));
        Assert.That(h.Layout.Root.Windows, Has.Count.EqualTo(1));
        Assert.That(h.Layout.Root.Windows![0].Host, Is.InstanceOf<Window>());
        Assert.That(((Window)h.Layout.Root.Windows[0].Host!).IsVisible, Is.True);
        Assert.That(h.Session.Editor.Document, Is.SameAs(text)); Assert.That(text.UndoStack, Is.SameAs(undo));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
        Assert.That(h.Layout.ExecuteLayout(DockLayoutCommand.Reset).Outcome, Is.EqualTo(DockLayoutOutcome.Completed));
        return Task.CompletedTask;
    });

    [TestCase("owner")][TestCase("cycle")][TestCase("dynamic-id")][TestCase("role-type")][TestCase("floating-owner")]
    public Task InvalidRuntimeStructureIsStillRejected(string defect) => Run(h =>
    {
        h.Float(DockShellState.InfoId);
        var pane = h.Layout.Pane(DockShellState.DocumentId);
        var owner = pane.Owner;
        var dock = (IDock)owner!;
        var id = dock.Id;
        var floating = h.Layout.Root.Windows![0];
        var floatingOwner = floating.Owner;
        var floatingDock = DockShellLayout.VisibleTree(floating.Layout!).OfType<IToolDock>().Single();
        try
        {
            switch (defect)
            {
                case "owner": pane.Owner = h.Layout.Root; break;
                case "cycle": dock.VisibleDockables!.Add(h.Layout.Root); break;
                case "dynamic-id": dock.Id = floatingDock.Id; break;
                case "role-type": dock.Id = DockShellLayout.Tools; break;
                case "floating-owner": floating.Owner = null; break;
            }
            Assert.Throws<InvalidDataException>(() => h.Layout.Capture(DockLayoutRoleTests.Bounds));
            Assert.That(h.Layout.ExecuteLayout(DockLayoutCommand.Maximize, pane.Id).Outcome, Is.EqualTo(DockLayoutOutcome.Rejected));
        }
        finally
        {
            pane.Owner = owner; dock.Id = id; dock.VisibleDockables!.Remove(h.Layout.Root); floating.Owner = floatingOwner;
        }
        Assert.That(DockLayoutStore.Decode(h.Layout.Capture(DockLayoutRoleTests.Bounds)), Is.Not.Null);
        return Task.CompletedTask;
    });

    [Test]
    public Task ComplexLayoutFocusCyclesPreserveAllSixPanesHostsHistoryAndRegistration() => Run(h =>
    {
        var layout = h.Layout;
        h.Service.Mutate(() => h.Session.Editor.Text = "// original");
        h.Service.Mutate(() => h.Session.Editor.Document.Insert(h.Session.Editor.Document.TextLength, " changed"));
        h.History.AddLine("history one"); h.History.AddLine("history two");
        h.History.AddInfo("info one"); h.History.AddInfo("info two");
        var history = h.History;
        var text = h.Session.Editor.Document; var undo = text.UndoStack;
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
        var contexts = layout.State.Tools.ToArray();
        layout.Float(DockShellState.DocumentId); h.Pump();
        var document = layout.Pane(DockShellState.DocumentId);
        var target = DockShellLayout.VisibleTree(layout.Root, false).OfType<IDocumentDock>().Single();
        Assert.That(new DockManager(new DockService()).ValidateDockable(document, target, DragAction.Move, DockOperation.Right, true), Is.True);
        Assert.That(DockShellLayout.VisibleTree(layout.Root, false).OfType<IDocumentDock>().Count(), Is.EqualTo(2));
        layout.Close(DockShellState.StatusId);
        layout.Move(DockShellState.OutputId, DockShellLayout.Diagnostics);
        layout.Move(DockShellState.OutputId, DockShellLayout.Tools);
        layout.Factory.FloatDockable((IDock)layout.Pane(DockShellState.VariablesId).Owner!);
        h.Float(DockShellState.DiagnosticsId);
        Assert.That(DockLayoutStore.Decode(layout.Capture(DockLayoutRoleTests.Bounds)).ClosedTools, Does.Contain(DockShellState.StatusId));
        layout.Show(DockShellState.StatusId);
        for (int cycle = 0; cycle < 3; cycle++)
        foreach (var id in DockLayoutStore.PaneIds)
        {
            layout.Show(id); h.Pump();
            var before = layout.Capture(DockLayoutRoleTests.Bounds);
            var tree = DockShellLayout.VisibleTree(layout.Root).ToArray();
            var ids = tree.Select(p => p.Id).ToArray();
            var hosts = layout.Factory.HostWindows.ToArray();
            var paneRoot = layout.Factory.FindRoot(layout.Pane(id));
            Assert.That(layout.ExecuteLayout(DockLayoutCommand.Maximize, id).Outcome, Is.EqualTo(DockLayoutOutcome.Completed));
            h.Pump();
            Assert.That(layout.ExecuteLayout(DockLayoutCommand.Restore).Outcome, Is.EqualTo(DockLayoutOutcome.Completed));
            h.Pump();
            Assert.That(layout.Capture(DockLayoutRoleTests.Bounds), Is.EqualTo(before));
            Assert.That(DockShellLayout.VisibleTree(layout.Root), Is.EqualTo(tree));
            Assert.That(tree.Select(p => p.Id), Is.EqualTo(ids));
            Assert.That(layout.Factory.FindRoot(layout.Pane(id)), Is.SameAs(paneRoot));
            Assert.That(layout.Factory.HostWindows, Is.EquivalentTo(hosts));
            Assert.That(hosts.Cast<Window>().All(w => w.IsVisible), Is.True);
            Assert.That(h.Session.Editor.Document, Is.SameAs(text)); Assert.That(text.UndoStack, Is.SameAs(undo));
            Assert.That(h.History, Is.SameAs(history)); Assert.That(h.History.LineCanBack && h.History.InfoCanBack, Is.True);
            Assert.That(layout.State.Tools, Is.EqualTo(contexts));
            Assert.That(h.Presentation.Variables!.DataContext, Is.SameAs(h.Variables));
            Assert.That(h.Presentation.Info!.DataContext, Is.SameAs(h.Info));
            Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
            Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
        }
        h.Service.Mutate(() => h.Session.Editor.Undo()); Assert.That(text.Text, Is.EqualTo("// original"));
        h.Service.Mutate(() => h.Session.Editor.Redo()); Assert.That(text.Text, Is.EqualTo("// original changed"));
        return Task.CompletedTask;
    });
}
