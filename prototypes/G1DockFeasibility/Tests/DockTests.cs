using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;
using Prism.DryIoc;
using Prism.Ioc;

namespace G1DockFeasibility.Tests;

public class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    public override void Initialize() => ProbeApp.ConfigureVisuals(this);
}

[NonParallelizable]
public sealed class DockTests
{
    private HeadlessUnitTestSession _session = null!;
    [OneTimeSetUp] public void Start() => _session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
    [OneTimeTearDown] public async Task Stop() => await _session.DisposeAsync();

    private Task Run(Action<ProbeWorkspace, IContainerExtension, ProbeWindow> test) => _session.Dispatch(() =>
    {
        var container = new DryIocContainerExtension();
        Registration.Register(container);
        var workspace = container.Resolve<ProbeWorkspace>();
        var window = new ProbeWindow(workspace, regionHost: false);
        try { window.Show(); Pump(window); test(workspace, container, window); }
        finally { window.Close(); workspace.Dispose(); (container as IDisposable)?.Dispose(); }
    }, CancellationToken.None);

    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static EditorView Editor(Window window) => window.GetVisualDescendants().OfType<EditorView>().Single();

    [Test] public Task LayoutHasDocumentAndTwoDistinctTools() => Run((w, _, _) =>
    {
        var leaves = ProbeWorkspace.Walk(w.Root).Where(d => d is IDocument or ITool).ToArray();
        // Dock.Tool implements IDocument too (tools may be docked as documents).
        Assert.That(leaves.Count(d => d is IDocument and not ITool), Is.EqualTo(1));
        Assert.That(leaves.Count(d => d is ITool), Is.EqualTo(2));
        Assert.That(leaves.Select(d => d.Id).Distinct().Count(), Is.EqualTo(3));
        Assert.That(leaves.All(d => d.Owner is IDock && ReferenceEquals(d.Factory, w.Factory)), Is.True);
        Assert.That(w.Find("documents"), Is.InstanceOf<IDocumentDock>());
    });

    [Test] public Task PrismResolvesSingletonsAndSharedState() => Run((w, c, _) =>
    {
        var variables = c.Resolve<VariablesViewModel>();
        Assert.That(c.Resolve<VariablesViewModel>(), Is.SameAs(variables));
        Assert.That(c.Resolve<DiagnosticsViewModel>().State, Is.SameAs(variables.State));
        Assert.That(w.Find(DocumentSession.Id).Context, Is.SameAs(c.Resolve<DocumentSession>()));
        Assert.That(w.Find(VariablesViewModel.Id).Context, Is.SameAs(variables));
    });

    [Test] public Task MovingToolsDoesNotDuplicateState() => Run((w, c, shell) =>
    {
        var tool = w.Find(VariablesViewModel.Id);
        var vm = c.Resolve<VariablesViewModel>();
        vm.State.ToolVisits = 17;
        for (var i = 0; i < 20; i++)
        {
            w.Move(VariablesViewModel.Id, "diagnostics-dock");
            w.Move(VariablesViewModel.Id, "variables-dock");
        }
        Pump(shell);
        Assert.That(w.Find(VariablesViewModel.Id), Is.SameAs(tool));
        Assert.That(tool.Context, Is.SameAs(vm));
        Assert.That(vm.State.ToolVisits, Is.EqualTo(17));
        Assert.That(ProbeWorkspace.Walk(w.Root).Count(d => d is IDocument and not ITool), Is.EqualTo(1));
    });

    [Test] public Task SaveRestorePreservesIdsAndRebindsApplicationState() => Run((w, c, shell) =>
    {
        w.Move(VariablesViewModel.Id, "diagnostics-dock");
        var document = w.Session.Document;
        document.Text = "preserved content";
        Editor(shell).Editor.CaretOffset = 7;
        var json = w.SaveLayout();
        Assert.That(json, Does.Not.Contain("preserved content"));
        w.RestoreLayout(json, shell.Host); Pump(shell);
        Assert.That(w.Find(VariablesViewModel.Id).Owner!.Id, Is.EqualTo("diagnostics-dock"));
        Assert.That(w.Find(DocumentSession.Id).Context, Is.SameAs(w.Session));
        Assert.That(w.Find(VariablesViewModel.Id).Context, Is.SameAs(c.Resolve<VariablesViewModel>()));
        Assert.That(Editor(shell).Editor.Document, Is.SameAs(document));
        Assert.That(Editor(shell).Editor.CaretOffset, Is.EqualTo(7));
        Assert.That(w.Session.AttachedViews, Is.EqualTo(1));
    });

    [Test] public Task FloatRedockPreservesDocumentTextCaretAndLogicalIdentity() => Run((w, _, shell) =>
    {
        var original = w.Find(DocumentSession.Id);
        var document = w.Session.Document;
        document.Text = "0123456789\n";
        Editor(shell).Editor.CaretOffset = 6;
        for (var i = 0; i < 10; i++)
        {
            w.Float(DocumentSession.Id);
            var floating = (Window)w.Root.Windows!.Single().Host!; Pump(floating);
            Assert.That(Editor(floating).Editor.Document, Is.SameAs(document));
            Assert.That(Editor(floating).Editor.CaretOffset, Is.EqualTo(6));
            w.Move(DocumentSession.Id, "documents"); Pump(shell);
            Assert.That(w.Find(DocumentSession.Id), Is.SameAs(original));
            Assert.That(w.Root.Windows, Is.Empty);
            Assert.That(w.Session.AttachedViews, Is.EqualTo(1));
            Assert.That(Editor(shell).Editor.CaretOffset, Is.EqualTo(6));
        }
        Assert.That(document.Text, Is.EqualTo("0123456789\n"));
        Assert.That(w.Session.AttachCount, Is.GreaterThan(10));
        Assert.That(w.Session.DisposeCount, Is.Zero);
    });

    [Test] public Task ActivationSelectsDockable() => Run((w, _, shell) =>
    {
        var tool = w.Find(VariablesViewModel.Id);
        w.Move(VariablesViewModel.Id, "diagnostics-dock"); Pump(shell);
        Assert.That(((IDock)tool.Owner!).ActiveDockable, Is.SameAs(tool));
        w.Factory.SetFocusedDockable(w.Root, tool);
        Assert.That(w.Root.FocusedDockable, Is.SameAs(tool));
    });

    [Test] public Task FloatingLayoutRoundTripRebindsSameDocument() => Run((w, _, shell) =>
    {
        w.Float(DocumentSession.Id);
        var previous = (Window)w.Root.Windows!.Single().Host!; Pump(previous);
        Editor(previous).Editor.CaretOffset = 4;
        w.RestoreLayout(w.SaveLayout(), shell.Host);
        var restored = (Window)w.Root.Windows!.Single().Host!; Pump(restored);
        Assert.That(previous.IsVisible, Is.False);
        Assert.That(restored, Is.Not.SameAs(previous));
        Assert.That(Editor(restored).Editor.Document, Is.SameAs(w.Session.Document));
        Assert.That(Editor(restored).Editor.CaretOffset, Is.EqualTo(4));
        Assert.That(w.Session.AttachedViews, Is.EqualTo(1));
    });

    [Test] public Task InvalidIdentityLayoutLeavesCurrentWorkspaceIntact() => Run((w, _, shell) =>
    {
        var root = w.Root;
        var malformed = w.SaveLayout().Replace(VariablesViewModel.Id, DocumentSession.Id);
        Assert.Throws<InvalidDataException>(() => w.RestoreLayout(malformed, shell.Host));
        Assert.That(w.Root, Is.SameAs(root));
        Assert.That(w.Session.AttachedViews, Is.EqualTo(1));
    });

    [Test] public Task RetiredEditorVisualCanBeCollectedWhileDocumentLives() => _session.Dispatch(async () =>
    {
        var container = new DryIocContainerExtension();
        Registration.Register(container);
        using var w = container.Resolve<ProbeWorkspace>();
        var shell = new ProbeWindow(w, regionHost: false);
        shell.Show(); Pump(shell);
        var retired = RetireLayoutView(w, shell);
        Assert.That(retired.Target, Is.Not.SameAs(Editor(shell)), "Probe must target a retired, not a recycled active view.");
        // Allow deferred presentation, focus and closed-window callbacks to unwind before collecting.
        await Task.Delay(100);
        for (var i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        try
        {
            Assert.That(retired.IsAlive, Is.False, "A detached editor must not be retained by its shared TextDocument.");
            Assert.That(w.Session.Document.TextLength, Is.GreaterThan(0));
        }
        finally { shell.Close(); w.Dispose(); (container as IDisposable)?.Dispose(); }
    }, CancellationToken.None);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference RetireLayoutView(ProbeWorkspace w, ProbeWindow shell)
    {
        var retired = new WeakReference(Editor(shell));
        w.RestoreLayout(w.SaveLayout(), shell.Host); Pump(shell);
        return retired;
    }

    [Test] public Task ClosingFloatKeepsSingletonAlive() => Run((w, c, shell) =>
    {
        var vm = c.Resolve<VariablesViewModel>();
        w.Float(VariablesViewModel.Id);
        var floating = (Window)w.Root.Windows!.Single().Host!; Pump(floating);
        floating.Close(); Pump(shell);
        Assert.That(w.Root.Windows, Is.Empty);
        Assert.That(c.Resolve<VariablesViewModel>(), Is.SameAs(vm));
        Assert.That(w.Session.DisposeCount, Is.Zero);
    });

    [Test] public Task WholeToolGroupCanReturnAfterItsMainAnchorWasMoved() => Run((w, _, shell) =>
    {
        var variables = w.Find(VariablesViewModel.Id);
        w.Factory.FloatDockable(w.Find("variables-dock"));
        var floating = (Window)w.Root.Windows!.Single().Host!; Pump(floating);
        Assert.That(ProbeWorkspace.Walk(w.Root, false).Any(d => d.Id == "variables-dock"), Is.False);
        w.Move(VariablesViewModel.Id, "variables-dock"); Pump(shell);
        Assert.That(w.Find(VariablesViewModel.Id), Is.SameAs(variables));
        Assert.That(w.Root.Windows, Is.Empty);
        Assert.That(variables.Owner, Is.SameAs(w.Find("variables-dock")));
    });

    [Test] public Task ShutdownReleasesHostsViewsAndDocumentSubscriptions() => Run((w, _, shell) =>
    {
        w.Float(DocumentSession.Id);
        var floating = (Window)w.Root.Windows!.Single().Host!; Pump(floating);
        var view = Editor(floating);
        shell.Close(); w.Dispose(); Dispatcher.UIThread.RunJobs();
        Assert.That(floating.IsVisible, Is.False);
        Assert.That(w.Factory.DockControls, Is.Empty);
        Assert.That(w.Factory.HostWindows, Is.Empty);
        Assert.That(w.Session.AttachedViews, Is.Zero);
        Assert.That(w.Session.AttachCount, Is.EqualTo(w.Session.DetachCount));
        Assert.That(view.Editor.Document, Is.Null);
        w.Dispose(); Assert.That(w.Session.DisposeCount, Is.EqualTo(1));
    });
}
