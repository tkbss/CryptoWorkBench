using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CryptoScript.Documentation;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using CryptoWorkBenchAvalonia.ViewModels;
using Markdown.Avalonia;
using NUnit.Framework;
using Prism.DryIoc;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockToolTests
{
    internal sealed class Host : IAsyncDisposable
    {
        internal readonly WorkbenchAutomationService Service = new() { LeaseMilliseconds = () => 1000UL };
        internal readonly HistoryService History = new();
        internal readonly VariableViewModel Variables = new();
        internal readonly StatusViewModel Status = new();
        internal readonly InfoViewModel Info;
        internal readonly DockEditorSession Session;
        internal readonly DockShellLayout Layout = new(new());
        internal readonly DockShellWindow Window;
        internal readonly DockShellPresentation Presentation;
        private readonly StyleInclude gridTheme = new(new Uri("avares://CryptoWorkBenchAvalonia"))
            { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") };
        internal MarkdownScrollViewer Markdown => Presentation.Info!.FindControl<MarkdownScrollViewer>("InfoMarkdownViewer")!;
        internal Host(LoadedLayout? startup = null)
        {
            Service.Mutate(() => VariableDictionary.Instance().Clear());
            Info = new(History);
            var main = new MainViewModel(History);
            var vm = new CryptoScriptEditViewModel(Status, Variables, Info, main, null!, History, Service);
            Session = new(vm, main, Service);
            Application.Current!.Styles.Add(gridTheme);
            Presentation = new(Application.Current!, Session, Variables, Info);
            Layout.StartupLayout = startup;
            Window = new(Layout) { Width = 1400, Height = 850 };
            Session.Start(Window); Presentation.StartTools(); Window.BindEditor(Session);
            App.CoordinateClosing(Window, Service);
            Window.Closed += (_, _) => { Session.Dispose(); Info.Dispose(); Presentation.Dispose(); };
            Window.Show(); Pump();
        }
        internal void Pump() => DockEditorTests.Pump(Window);
        internal void Show(string id) { Layout.Show(id); Pump(); }
        internal Window Float(string id)
        {
            Layout.Float(id); Pump();
            var host = (Window)Layout.Factory.FindRoot(Layout.Pane(id))!.Window!.Host!;
            host.Width = 850; host.Height = 700; DockEditorTests.Pump(host); return host;
        }
        public async ValueTask DisposeAsync()
        {
            Window.Close(); await DockEditorTests.Wait(() => !Window.IsVisible);
            Session.Dispose(); Info.Dispose(); Presentation.Dispose(); Layout.Dispose();
            Application.Current!.Styles.Remove(gridTheme);
            await Service.DisposeAsync();
        }
    }
    private static Task Run(Func<Host, Task> test) => GuiTestSession.Session.Dispatch(async () =>
    { await using var host = new Host(); await test(host); return true; }, CancellationToken.None);
    private static int Subscribers(object source, string name) =>
        ((Delegate?)source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source))?.GetInvocationList().Length ?? 0;
    private sealed class RegistrationProbe : App { internal void Register(IContainerRegistry r) => RegisterTypes(r); }

    [Test] public Task ExistingPrismToolViewModelsAreSingletons() => GuiTestSession.Session.Dispatch(() =>
    {
        var container = new DryIocContainerExtension(); new RegistrationProbe().Register(container);
        try
        {
            var variables = container.Resolve<VariableViewModel>(); var info = container.Resolve<InfoViewModel>();
            Assert.That(container.Resolve<VariableViewModel>(), Is.SameAs(variables));
            Assert.That(container.Resolve<InfoViewModel>(), Is.SameAs(info));
            container.Resolve<InfoViewModel>().Dispose();
        }
        finally { (container as IDisposable)?.Dispose(); }
    }, CancellationToken.None);

    [TestCase(DockShellState.VariablesId)][TestCase(DockShellState.InfoId)]
    public Task ToolCyclesKeepOneViewOneModelAndOriginalEditor(string id) => Run(async h =>
    {
        await h.Session.ViewModel.ParseLineAsync("VAR g104=0x(ABCD) Info(HASH-SHA256)");
        var view = id == DockShellState.VariablesId ? (Control)h.Presentation.Variables! : h.Presentation.Info!;
        var context = view.DataContext; var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var document = h.Session.Editor.Document; var undo = document.UndoStack;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration; var revision = InterpreterHostQueue.StateRevision;
        int historyListeners = Subscribers(h.History, "InfoHistoryChanged");
        int workspaceListeners = Subscribers(h.Service.EditorCoordinator, "WorkspaceChanged");
        var refresh = h.Variables.RefreshCommand; var selected = h.Variables.DataVariables.Single(v => v.Identifier == "g104");
        h.Variables.SelectedVariable = selected;
        for (int i = 0; i < 6; i++)
        {
            var host = h.Float(id); h.Layout.Dock(id); h.Pump(); Assert.That(host.IsVisible, Is.False);
            h.Layout.Close(id); h.Pump(); h.Show(id);
            Assert.That(view.DataContext, Is.SameAs(context));
            Assert.That(h.Session.Editor.Document, Is.SameAs(document)); Assert.That(document.UndoStack, Is.SameAs(undo));
            Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
            Assert.That(h.Variables.SelectedVariable, Is.SameAs(selected));
            Assert.That(h.Variables.RefreshCommand, Is.SameAs(refresh));
        }
        Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
        Assert.That(Subscribers(h.History, "InfoHistoryChanged"), Is.EqualTo(historyListeners));
        Assert.That(Subscribers(h.Service.EditorCoordinator, "WorkspaceChanged"), Is.EqualTo(workspaceListeners));
    });

    [TestCase(false)][TestCase(true)]
    public Task ExecutionPublishesVariablesAndInfoEvenWhileToolsAreClosed(bool diagnostics) => Run(async h =>
    {
        h.Layout.Close(DockShellState.VariablesId); h.Layout.Close(DockShellState.InfoId);
        await h.Session.ViewModel.ParseLineAsync(diagnostics
            ? "VAR updated=0x(11) Info(HASH-SHA256) KEY missing=Check(0x(AA))"
            : "VAR updated=0x(11) Info(HASH-SHA256)");
        Assert.That(h.Variables.DataVariables.Any(v => v.Identifier == "updated" && v.Value.Contains("11")), Is.True);
        Assert.That(h.Info.InfoText, Does.Contain("HASH"));
        Assert.That(h.Variables.IsSnapshotStale, Is.False);
        Assert.That(h.Status.HasDiagnostics, Is.EqualTo(diagnostics));
        h.Show(DockShellState.VariablesId);
        Assert.That(h.Presentation.Variables!.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "VariablesGrid").ItemsSource, Is.SameAs(h.Variables.DataVariables));
        h.Show(DockShellState.InfoId); Assert.That(h.Markdown.Markdown, Is.EqualTo(h.Info.InfoText));
    });

    [Test] public Task ReadOnlyBindingsAndSelectionCopyCannotMutateVariablesOrParameters() => Run(async h =>
    {
        await h.Session.ViewModel.ParseLineAsync("PARAM p=Parameters(SECRET_SHAMIR,#PARTS:2,#MINPARTS:2)");
        h.Variables.SelectedVariable = h.Variables.DataVariables.Single(v => v.Identifier == "p");
        var window = h.Float(DockShellState.VariablesId);
        var view = h.Presentation.Variables!;
        Assert.That(view.GetVisualDescendants().OfType<DataGrid>().All(g => g.IsReadOnly && g.Columns.All(c => c.IsReadOnly)), Is.True);
        Assert.That(view.GetVisualDescendants().OfType<TextBox>().Any(t => !t.IsReadOnly), Is.False);
        Assert.That(view.GetVisualDescendants().OfType<Button>().Any(b => Equals(b.Content, "Modify")), Is.False);
        var value = view.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "SelectedValue");
        string original = h.Variables.SelectedVariable.Value;
        var parameters = h.Variables.SelectedVariable.Parameters.Select(p => p.Value).ToArray();
        long revision = InterpreterHostQueue.StateRevision;
        value.Focus(); value.SelectionStart = 0; value.SelectionEnd = value.Text!.Length;
        window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
        await Task.Delay(30);
        Assert.That(await window.Clipboard!.TryGetTextAsync(), Is.EqualTo(original));
        window.KeyPress(Key.A, RawInputModifiers.None, PhysicalKey.A, "a"); window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, "v");
        Assert.That(h.Variables.SelectedVariable.Value, Is.EqualTo(original));
        Assert.That(h.Variables.SelectedVariable.Parameters.Select(p => p.Value), Is.EqualTo(parameters));
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
    });

    [Test] public Task McpExecutionUsesSameToolsAndRefreshCannotBypassLease() => Run(async h =>
    {
        h.Session.LoadText(h.Service.CaptureSaveIntent()!, "VAR mcpValue=0x(CAFE) Info(HMAC-SHA256)");
        var previousInfo = h.Info.InfoText;
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("g104-test", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var lease = (await h.Service.AcquireEditorAsync(caller, new("2", h.Service.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
        h.Service.ExecutionConfirmation = (_, _, _) => Task.FromResult<bool?>(true);
        var state = (await h.Service.GetStatusV2Async(caller, new("2"), default)).Result!;
        var executed = await h.Service.ExecuteEditorV2Async(caller, new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId, Guid.NewGuid(), snapshot.Revision, state.StateVersion), default);
        Assert.That(executed.Error, Is.Null);
        Assert.That(executed.Result!.State, Is.EqualTo(ExecutionState.Completed));
        // V2 keeps its output in the MCP result and invalidates GUI snapshots. Preserve
        // that boundary: the existing Refresh command publishes after lease release.
        Assert.That(h.Info.InfoText, Is.EqualTo(previousInfo));
        var rows = h.Variables.DataVariables.ToArray(); long revision = InterpreterHostQueue.StateRevision;
        h.Variables.RefreshCommand.Execute();
        Assert.That(h.Variables.IsSnapshotStale, Is.True);
        Assert.That(h.Variables.DataVariables, Is.EqualTo(rows));
        foreach (string id in new[] { DockShellState.VariablesId, DockShellState.InfoId }) { h.Layout.Float(id); h.Layout.Close(id); }
        Assert.That(h.Layout.Root.Windows?.Count ?? 0, Is.Zero);
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
        await h.Service.ReleaseEditorAsync(caller, new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId), default);
        h.Variables.RefreshCommand.Execute(); Assert.That(h.Variables.IsSnapshotStale, Is.False);
        Assert.That(h.Variables.DataVariables.Any(v => v.Identifier == "mcpValue" && v.Value == "0x(CAFE)"), Is.True);
    });

    [TestCase("observer")][TestCase("busy")][TestCase("pending")]
    public Task RefreshAndDockRespectExistingAdmission(string mode) => Run(async h =>
    {
        long revision = InterpreterHostQueue.StateRevision;
        void Attempt()
        {
            h.Variables.RefreshCommand.Execute(); h.Layout.Float(DockShellState.VariablesId); h.Layout.Float(DockShellState.InfoId);
            Assert.That(h.Layout.Root.Windows?.Count ?? 0, Is.Zero);
        }
        if (mode == "observer") h.Service.EditorCoordinator.Observe(Attempt);
        else if (mode == "busy") { using (InterpreterHostQueue.Reserve()) Attempt(); }
        else { using (h.Service.EditorCoordinator.TryBegin()) Attempt(); }
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
        await Task.CompletedTask;
    });

    [TestCase("HASH-SHA256")][TestCase("HMAC-SHA256")][TestCase("paddings")][TestCase("AES-CBC")]
    public Task ExistingInfoDocumentsAndHistoryRemainAvailable(string name) => Run(async h =>
    {
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocumentation(name, out string text), Is.True);
        h.Info.SetInfoText("previous"); h.Info.SetInfoText(text); h.Show(DockShellState.InfoId);
        Assert.That(h.Markdown.Markdown, Is.EqualTo(text)); Assert.That(h.Markdown.SelectionEnabled, Is.True);
        h.Float(DockShellState.InfoId); h.History.InfoBack(); Assert.That(h.Info.InfoText, Is.EqualTo("previous"));
        h.History.InfoForward(); Assert.That(h.Info.InfoText, Is.EqualTo(text));
        Assert.That(h.Info.IsMechanismDocumentation, Is.EqualTo(text.TrimStart().StartsWith("# MECHANISM ")));
        await Task.CompletedTask;
    });

    [Test] public Task InternalLinksAndBackSurviveCloseShowWithRetiredCallbacksInert() => Run(async h =>
    {
        new FileInfoDocumentationProvider().TryGetDocumentation("mechanisms", out string text);
        h.Info.SetInfoText(text); h.Show(DockShellState.InfoId);
        var engine = (Markdown.Avalonia.Markdown)h.Markdown.Engine;
        var old = engine.HyperlinkCommand!;
        h.Layout.Close(DockShellState.InfoId); h.Pump();
        old.Execute("cryptoscript-info://mechanism/AES-CBC"); Assert.That(h.Info.CanNavigateBack, Is.False);
        h.Show(DockShellState.InfoId);
        engine.HyperlinkCommand!.Execute("cryptoscript-info://mechanism/AES-CBC");
        Assert.That(h.Info.InfoText, Does.Contain("# MECHANISM AES-CBC"));
        h.Float(DockShellState.InfoId); h.Info.NavigateBackCommand.Execute();
        Assert.That(h.Info.InfoText, Does.Contain("cryptoscript-info:"));
        Assert.That(old.CanExecute("cryptoscript-info://mechanism/AES-CBC"), Is.False);
        await Task.CompletedTask;
    });

    [TestCase(false)][TestCase(true)]
    public Task CopyExampleCopiesWholeRenderedCodeWhileCtrlCCopiesOnlySelection(bool floating) => Run(async h =>
    {
        const string example = "VAR hex=0x(AABB)\nVAR b64=0b64(QUI=)\nPrint(\"ä€😀\")\n";
        // The newline immediately before a closing fence belongs to Markdown syntax.
        // An additional newline keeps the example's own final line break in the payload.
        h.Info.SetInfoText("# Example\n\n```text\n" + example + "\n```\n");
        h.Show(DockShellState.InfoId);
        var window = floating ? h.Float(DockShellState.InfoId) : h.Window;
        var code = h.Presentation.Info!.GetVisualDescendants().OfType<TextEditor>().Single();
        Assert.That(code.IsReadOnly, Is.True); Assert.That(code.Text, Is.EqualTo(example));
        var original = h.Service.CaptureSaveIntent()!.Snapshot; long revision = InterpreterHostQueue.StateRevision;
        await window.Clipboard!.SetTextAsync("sentinel");
        h.Pump(); Assert.That(await window.Clipboard!.TryGetTextAsync(), Is.EqualTo("sentinel"));
        code.TextArea.Focus(); code.Select(0, 7);
        window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
        await Task.Delay(30); Assert.That(await window.Clipboard!.TryGetTextAsync(), Is.EqualTo(example[..7]));
        var pad = code.GetVisualAncestors().Single(c => c.GetType().Name == "CodePad");
        var copy = (Button)pad.GetType().GetProperty("ExandableMenu")!.GetValue(pad)!;
        copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(30); Assert.That(await window.Clipboard!.TryGetTextAsync(), Is.EqualTo(example));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(original));
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
    });

    [TestCase(false)][TestCase(true)]
    public Task MarkdownProseSupportsMouseSelectionAndCtrlC(bool floating) => Run(async h =>
    {
        const string prose = "Native selection ä€😀";
        h.Info.SetInfoText(prose); h.Show(DockShellState.InfoId);
        var window = floating ? h.Float(DockShellState.InfoId) : h.Window;
        var paragraph = h.Markdown.GetVisualDescendants().OfType<Control>()
            .Single(c => c.GetType().Name == "CTextBlock");
        var start = paragraph.TranslatePoint(new Point(0, paragraph.Bounds.Height / 2), window)!.Value;
        var end = paragraph.TranslatePoint(new Point(paragraph.Bounds.Width - 1, paragraph.Bounds.Height / 2), window)!.Value;
        await window.Clipboard!.SetTextAsync("sentinel");
        window.MouseDown(start, MouseButton.Left); window.MouseMove(end, RawInputModifiers.LeftMouseButton); window.MouseUp(end, MouseButton.Left);
        window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
        await Task.Delay(30);
        Assert.That((await window.Clipboard!.TryGetTextAsync())?.TrimEnd('\r', '\n'), Is.EqualTo(prose));
    });

    [Test] public Task FinalShutdownRetiresToolsAndSubscriptionsWithFloatingHosts() => Run(async h =>
    {
        var variables = h.Float(DockShellState.VariablesId); var info = h.Float(DockShellState.InfoId);
        var refresh = h.Variables.RefreshCommand;
        h.Window.Close(); await DockEditorTests.Wait(() => !h.Window.IsVisible);
        Assert.That(variables.IsVisible || info.IsVisible || h.Service.HasRegisteredEditor, Is.False);
        Assert.That(Subscribers(h.History, "InfoHistoryChanged"), Is.Zero);
        Assert.That(Subscribers(h.Service.EditorCoordinator, "WorkspaceChanged"), Is.Zero);
        Assert.That(refresh.CanExecute(), Is.False);
        Assert.That(h.Layout.Factory.HostWindows, Is.Empty);
    });
}
