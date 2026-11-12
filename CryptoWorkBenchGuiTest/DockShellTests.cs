using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;
using Prism.DryIoc;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockShellTests
{
    private sealed class RegistrationProbe : App
    {
        public void Register(IContainerRegistry registry) => RegisterTypes(registry);
    }

    private static Task Run(Action<DockShellLayout, IContainerExtension, DockShellWindow> test)
        => GuiTestSession.Session.Dispatch(() =>
        {
            var app = Application.Current!;
            var container = new DryIocContainerExtension();
            new RegistrationProbe().Register(container);
            using var presentation = new DockShellPresentation(app);
            var layout = container.Resolve<DockShellLayout>();
            var window = container.Resolve<DockShellWindow>();
            try { window.Show(); Pump(window); test(layout, container, window); }
            finally { window.Close(); window.ReleaseLayout(); (container as IDisposable)?.Dispose(); }
        }, CancellationToken.None);

    private static void Pump(Window window)
    {
        window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    }

    [Test] public Task ProductPrismRegistrationsResolveOneShellAndSharedServices() => Run((layout, c, window) =>
    {
        Assert.That(c.Resolve<DockShellWindow>(), Is.SameAs(window));
        Assert.That(c.Resolve<DockShellLayout>(), Is.SameAs(layout));
        Assert.That(c.Resolve<DockShellState>(), Is.SameAs(layout.State));
        Assert.That(c.Resolve<WorkbenchAutomationService>(), Is.SameAs(WorkbenchAutomationService.Shared));
        Assert.That(c.Resolve<IWorkbenchAutomationService>(), Is.SameAs(WorkbenchAutomationService.Shared));
        Assert.That(c.Resolve<IWorkbenchAutomationV2>(), Is.SameAs(WorkbenchAutomationService.Shared));
    });

    [Test] public Task LayoutContainsOneDocumentFiveUniqueToolsAndOnlyPlaceholders() => Run((layout, _, window) =>
    {
        var leaves = DockShellLayout.VisibleTree(layout.Root).Where(d => d is IDocument or ITool).ToArray();
        Assert.That(leaves.Count(d => d is IDocument and not ITool), Is.EqualTo(1));
        Assert.That(leaves.Count(d => d is ITool), Is.EqualTo(5));
        Assert.That(leaves.Select(d => d.Id).Distinct().Count(), Is.EqualTo(6));
        Assert.That(leaves.All(d => d.Context is DockPaneState && d.Owner is IDock), Is.True);
        Assert.That(layout.Pane(DockShellState.DocumentId).Owner, Is.InstanceOf<IDocumentDock>());
        Assert.That(window.GetVisualDescendants().OfType<TextEditor>(), Is.Empty);
        Assert.That(window.GetVisualDescendants().OfType<DockPlaceholderView>().Any(), Is.True);
    });

    [Test] public Task RepeatedMovesKeepIdentitiesAndDoNotTouchExecutionLifecycle() => Run((layout, _, window) =>
    {
        var coordinator = WorkbenchAutomationService.Shared.EditorCoordinator;
        var before = (coordinator.Generation, coordinator.WorkspaceGeneration, coordinator.Phase, coordinator.Pending);
        var panes = new[] { layout.State.Document }.Concat(layout.State.Tools).Select(s => layout.Pane(s.Id)).ToArray();
        foreach (var state in layout.State.Tools)
        {
            state.Note = state.Id;
            for (var i = 0; i < 20; i++)
            {
                layout.Move(state.Id, DockShellLayout.Diagnostics);
                layout.Move(state.Id, DockShellLayout.Tools);
                layout.Show(state.Id);
            }
        }
        Pump(window);
        Assert.That(DockShellLayout.VisibleTree(layout.Root).Where(d => d is IDocument or ITool), Is.EquivalentTo(panes));
        Assert.That(layout.State.Tools.All(s => s.Note == s.Id && ReferenceEquals(layout.Pane(s.Id).Context, s)), Is.True);
        Assert.That((coordinator.Generation, coordinator.WorkspaceGeneration, coordinator.Phase, coordinator.Pending), Is.EqualTo(before));
    });

    [Test] public Task CloseAndReopenEveryToolPreservesLogicalState() => Run((layout, _, window) =>
    {
        foreach (var state in layout.State.Tools)
        {
            var pane = layout.Pane(state.Id);
            state.Note = "preserved " + state.Id;
            for (var i = 0; i < 3; i++)
            {
                layout.Close(state.Id); layout.Close(state.Id);
                Assert.That(DockShellLayout.VisibleTree(layout.Root), Does.Not.Contain(pane));
                layout.Show(state.Id); layout.Show(state.Id); Pump(window);
                Assert.That(DockShellLayout.VisibleTree(layout.Root).Count(d => ReferenceEquals(d, pane)), Is.EqualTo(1));
                var view = window.GetVisualDescendants().OfType<DockPlaceholderView>().Single(v => ReferenceEquals(v.State, state));
                Assert.That(view.Notes.Text, Is.EqualTo("preserved " + state.Id));
            }
        }
    });

    [Test] public Task RepeatedNativeHostModelFloatAndRedockPreserveAllPanes() => Run((layout, _, window) =>
    {
        foreach (var state in new[] { layout.State.Document }.Concat(layout.State.Tools))
        {
            var pane = layout.Pane(state.Id);
            state.Note = "retained";
            for (var i = 0; i < 3; i++)
            {
                layout.Float(state.Id); layout.Float(state.Id);
                Assert.That(layout.Root.Windows, Has.Count.EqualTo(1));
                var floating = (Window)layout.Root.Windows!.Single().Host!; Pump(floating);
                layout.Dock(state.Id); Pump(window);
                Assert.That(floating.IsVisible, Is.False);
                Assert.That(layout.Root.Windows, Is.Empty);
                Assert.That(layout.Pane(state.Id), Is.SameAs(pane));
                Assert.That(pane.Context, Is.SameAs(state));
                Assert.That(state.Note, Is.EqualTo("retained"));
            }
        }
    });

    [Test] public Task FloatingWholeToolGroupRecreatesMissingMainAnchor() => Run((layout, _, window) =>
    {
        var group = (IDock)layout.Pane(DockShellState.VariablesId).Owner!;
        layout.Factory.FloatDockable(group);
        Assert.That(layout.Root.Windows, Has.Count.EqualTo(1));
        foreach (var state in layout.State.Tools.Take(3)) layout.Dock(state.Id);
        Pump(window);
        Assert.That(layout.Root.Windows, Is.Empty);
        Assert.That(DockShellLayout.VisibleTree(layout.Root, false).Count(d => d.Id == DockShellLayout.Tools), Is.EqualTo(1));
    });

    [Test] public Task ShutdownClosesEveryFloatingHostAndUnregistersDockControls() => Run((layout, _, window) =>
    {
        layout.Float(DockShellState.DocumentId); layout.Float(DockShellState.OutputId);
        var hosts = layout.Root.Windows!.Select(w => (Window)w.Host!).ToArray();
        Assert.That(hosts.Length, Is.EqualTo(2));
        window.Close(); Dispatcher.UIThread.RunJobs();
        Assert.That(hosts.All(w => !w.IsVisible), Is.True);
        Assert.That(layout.Root.Windows, Is.Empty);
        Assert.That(layout.Factory.HostWindows, Is.Empty);
        Assert.That(layout.Factory.DockControls, Is.Empty);
        Assert.That(layout.IsDisposed, Is.True);
        Assert.Throws<ObjectDisposedException>(() => layout.Show(DockShellState.OutputId));
    });

    [Test] public Task NativeDocumentHostCloseCannotRemoveTheSingleDocument() => Run((layout, _, window) =>
    {
        layout.Float(DockShellState.DocumentId);
        var host = (Window)layout.Root.Windows!.Single().Host!;
        host.Close(); Pump(window);
        Assert.That(host.IsVisible, Is.True);
        Assert.That(DockShellLayout.VisibleTree(layout.Root).Count(d => d is IDocument and not ITool), Is.EqualTo(1));
        layout.Dock(DockShellState.DocumentId);
        Assert.That(host.IsVisible, Is.False);
    });

    [Test] public Task NativeToolHostCloseAllowsReopenWithSameState() => Run((layout, _, window) =>
    {
        var state = layout.State.Tools.Single(s => s.Id == DockShellState.OutputId);
        state.Note = "native close";
        layout.Float(state.Id);
        var host = (Window)layout.Root.Windows!.Single().Host!;
        host.Close(); Pump(window);
        Assert.That(host.IsVisible, Is.False);
        Assert.That(layout.Root.Windows, Is.Empty);
        layout.Show(state.Id); Pump(window);
        Assert.That(layout.Pane(state.Id).Context, Is.SameAs(state));
        Assert.That(window.GetVisualDescendants().OfType<DockPlaceholderView>().Single(v => v.State == state).Notes.Text, Is.EqualTo("native close"));
    });

    [Test] public Task CanceledCloseKeepsLayoutAndFloatingHostAlive() => Run((layout, _, window) =>
    {
        layout.Float(DockShellState.OutputId);
        void Cancel(object? sender, WindowClosingEventArgs e) => e.Cancel = true;
        window.Closing += Cancel;
        try
        {
            window.Close();
            Assert.That(layout.IsDisposed, Is.False);
            Assert.That(((Window)layout.Root.Windows!.Single().Host!).IsVisible, Is.True);
        }
        finally { window.Closing -= Cancel; }
    });

    [Test] public Task InvalidCommandsAndDocumentCloseLeaveLayoutIntact() => Run((layout, _, _) =>
    {
        var before = DockShellLayout.VisibleTree(layout.Root).ToArray();
        Assert.Throws<ArgumentException>(() => layout.Show("unknown"));
        Assert.Throws<ArgumentException>(() => layout.Move(DockShellState.OutputId, "unknown"));
        Assert.Throws<ArgumentException>(() => layout.Move(DockShellState.DocumentId, DockShellLayout.Tools));
        layout.Close(DockShellState.DocumentId);
        Assert.That(DockShellLayout.VisibleTree(layout.Root), Is.EqualTo(before));
    });

    [Test] public Task DefaultSelectionNeverInitializesPreview() => GuiTestSession.Session.Dispatch(() =>
    {
        var legacy = new Window();
        Assert.That(DockShellStartup.Select(false, () => throw new AssertionException("preview must be lazy"),
            () => legacy, _ => Assert.Fail("no failure expected")), Is.SameAs(legacy));
    }, CancellationToken.None);

    [Test] public Task FailedInitializationCleansPartialShellBeforeLegacyFallback() => GuiTestSession.Session.Dispatch(() =>
    {
        var app = Application.Current!;
        var styles = app.Styles.Count; var templates = app.DataTemplates.Count;
        using var presentation = new DockShellPresentation(app);
        using var layout = new DockShellLayout(new DockShellState());
        var partial = new DockShellWindow(layout);
        var failure = new InvalidOperationException("injected initialization failure");
        Exception? observed = null;
        var legacy = new Window();
        var selected = DockShellStartup.Select(true, () => throw failure, () =>
        {
            Assert.That(layout.IsDisposed, Is.True);
            Assert.That(app.Styles.Count, Is.EqualTo(styles));
            Assert.That(app.DataTemplates.Count, Is.EqualTo(templates));
            return legacy;
        }, ex => { observed = ex; partial.ReleaseLayout(); presentation.Dispose(); });
        Assert.That(selected, Is.SameAs(legacy));
        Assert.That(observed, Is.SameAs(failure));
        Assert.That(layout.Factory.DockControls, Is.Empty);
    }, CancellationToken.None);
}
