using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using CryptoWorkBenchAvalonia.Views;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

// A separate executable boots the actual product Prism application with the native platform backend.
// No input is sent to another application. Each mode runs in its own process.
public sealed partial class DockShellNativeProbe : App
{
    public static string Mode { get; set; } = "dock";
    public static string Output { get; set; } = "native.json";
    internal override DockLayoutStore CreateLayoutStore() => new(Output + ".layout.json");
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);

    protected override Window CreateDockShell()
    {
        // Retain the G1-02 placeholder checks as a foundation fixture. Product editor
        // startup is exercised separately by editor/editor-input in this same runner.
        if (Mode == "dock")
        {
            _foundationPresentation = new DockShellPresentation(this);
            return Container.Resolve<DockShellWindow>();
        }
        var result = base.CreateDockShell();
        if (Mode == "fallback") throw new InvalidOperationException("G1-02 injected failure after shell construction");
        return result;
    }
    private DockShellPresentation? _foundationPresentation;
    private static DockEditorSession? _failedSession;
    private static DockShellWindow? _failedWindow;
    protected override void InitializeDockSession(DockShellWindow window)
    {
        if (Mode != "dock") base.InitializeDockSession(window);
        if (Mode == "startup-failure")
        {
            _failedSession = Container.Resolve<DockEditorSession>();
            _failedWindow = window;
            throw new InvalidOperationException("G1-03 injected failure after productive editor registration");
        }
    }

    public static int RecordStartupFailure(Exception error)
    {
        var service = WorkbenchAutomationService.Shared;
        var checks = new[]
        {
            new { name = "productive-start-was-reached", passed = _failedSession?.Started == true },
            new { name = "original-failure-propagated", passed = error.ToString().Contains("G1-03 injected failure") },
            new { name = "admission-revoked", passed = service.IsStopping },
            new { name = "no-replacement-registration", passed = !service.HasRegisteredEditor },
            new { name = "unshown-shell-layout-released", passed = _failedWindow is { IsVisible: false } && _failedWindow.Layout.IsDisposed }
        };
        int failed = checks.Count(c => !c.passed);
        File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, passed = checks.Length - failed, failed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        return failed == 0 ? 0 : 1;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        desktop.MainWindow!.Opened += async (_, _) => await Run(desktop);
    }

    private async Task Run(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (Mode is "native-acceptance" or "native-acceptance-input") { await RunNativeAcceptance(desktop); return; }
        if (Mode == "partial-failure") { await RunPartialFailure(desktop); return; }
        if (Mode == "operation-contract") { await RunOperationContract(desktop); return; }
        if (Mode is "cleanup-save" or "cleanup-restore") { await RunPersistenceCleanup(desktop); return; }
        if (Mode == "layout-roles") { await RunLayoutRoles(desktop); return; }
        if (Mode is "redock" or "redock-manual") { await RunRedock(desktop); return; }
        if (Mode is "integration" or "integration-input" or "integration-restart") { await RunIntegration(desktop); return; }
        if (Mode is "layout" or "layout-restart" or "layout-invalid" or "layout-input") { await RunLayout(desktop); return; }
        if (Mode is "editor" or "editor-input") { await RunEditor(desktop); return; }
        if (Mode is "diagnostics" or "diagnostics-input") { await RunDiagnostics(desktop); return; }
        if (Mode is "output" or "output-input") { await RunOutput(desktop); return; }
        if (Mode is "tools" or "tools-input") { await RunTools(desktop); return; }
        var checks = new List<object>();
        int failures = 0;
        void Check(string name, bool passed)
        {
            checks.Add(new { name, passed });
            if (!passed) failures++;
        }
        static async Task WaitFor(Func<bool> predicate)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(50);
            if (!predicate()) throw new TimeoutException("Native view/lifecycle did not settle.");
        }
        static DockPlaceholderView View(Window window, string id) => window.GetVisualDescendants()
            .OfType<DockPlaceholderView>().Single(v => v.State.Id == id);
        var shell = desktop.MainWindow!;
        try
        {
            await Task.Delay(300);
            Check("one-main-window", desktop.Windows.Count == 1);
            Check("shared-service", ReferenceEquals(Container.Resolve<WorkbenchAutomationService>(), WorkbenchAutomationService.Shared));
            Check("shared-v1-coordination", ReferenceEquals(Container.Resolve<IWorkbenchAutomationService>(), WorkbenchAutomationService.Shared));
            Check("shared-v2-coordination", ReferenceEquals(Container.Resolve<IWorkbenchAutomationV2>(), WorkbenchAutomationService.Shared));
            if (Mode != "dock")
            {
                Check("legacy-main-window", shell is MainWindow);
                Check("expected-startup-error", (DockShellStartupError != null) == (Mode == "fallback"));
                await WaitFor(() => shell.GetVisualDescendants().OfType<CryptoScriptEditView>().Any());
                var editor = shell.GetVisualDescendants().OfType<TextEditor>().Single();
                editor.Text = "// G1-02 native legacy editing probe";
                editor.CaretOffset = 8;
                Check("legacy-editor-editable", editor.Text.EndsWith("editing probe") && editor.CaretOffset == 8);
                Check("legacy-editor-registered", WorkbenchAutomationService.Shared.EditorCoordinator.Phase == EditorControlPhase.Gui);
                Check("no-dock-host-in-legacy", !shell.GetVisualDescendants().OfType<Dock.Avalonia.Controls.DockControl>().Any());
                if (Mode == "fallback")
                {
                    var failed = Container.Resolve<DockShellLayout>();
                    Check("failed-layout-disposed", failed.IsDisposed);
                    Check("failed-hosts-cleaned", failed.Factory.DockControls.Count == 0 && failed.Factory.HostWindows.Count == 0);
                    Check("preview-theme-removed", !Styles.OfType<Dock.Avalonia.Themes.Fluent.DockFluentTheme>().Any());
                }
            }
            else
            {
                var window = (DockShellWindow)shell;
                var layout = window.Layout;
                Check("product-shell-singleton", ReferenceEquals(Container.Resolve<DockShellWindow>(), shell));
                Check("product-state-singleton", ReferenceEquals(Container.Resolve<DockShellState>(), layout.State));
                Check("no-editor-view", !shell.GetVisualDescendants().OfType<TextEditor>().Any());
                var service = WorkbenchAutomationService.Shared;
                var coordinator = service.EditorCoordinator;
                var before = (coordinator.Generation, coordinator.WorkspaceGeneration, coordinator.Phase, coordinator.Pending, service.DraftRevision);
                Check("editor-never-attached", coordinator.Phase == EditorControlPhase.Unavailable && coordinator.Generation == 0);
                await WaitFor(() => shell.GetVisualDescendants().OfType<DockPlaceholderView>().Any());
                shell.Activate();
                var handle = shell.TryGetPlatformHandle()!.Handle;
                Check("native-main-handle", handle != 0);
                shell.Title = "G1-02 native probe — activate this window to start";
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (GetForegroundWindow() != handle && DateTime.UtcNow < deadline) await Task.Delay(50);
                Check("main-foreground", GetForegroundWindow() == handle);
                foreach (var state in new[] { layout.State.Document }.Concat(layout.State.Tools))
                {
                    var pane = layout.Pane(state.Id);
                    state.Note = "native " + state.Id;
                    layout.Float(state.Id);
                    var floating = (Window)layout.Root.Windows!.Single().Host!;
                    await WaitFor(() => floating.GetVisualDescendants().OfType<DockPlaceholderView>().Any());
                    var childHandle = floating.TryGetPlatformHandle()!.Handle;
                    Check(state.Id + ":native-handle", childHandle != 0 && childHandle != handle);
                    Check(state.Id + ":native-owner", ReferenceEquals(floating.Owner, shell) && GetWindow(childHandle, 4) == handle);
                    floating.Position = new Avalonia.PixelPoint(shell.Position.X + (int)(shell.Width * shell.RenderScaling) + 25, shell.Position.Y + 30);
                    shell.Activate(); layout.Show(state.Id); View(floating, state.Id).Notes.Focus();
                    await Task.Delay(150);
                    Check(state.Id + ":independent-position", floating.Position.X > shell.Position.X + shell.Width * shell.RenderScaling);
                    Check(state.Id + ":foreground-and-focus", GetForegroundWindow() == childHandle && View(floating, state.Id).Notes.IsKeyboardFocusWithin);
                    if (state.IsDocument)
                    {
                        floating.Close();
                        Check("document-native-close-protected", floating.IsVisible && layout.Root.Windows!.Count == 1);
                    }
                    View(floating, state.Id).Notes.Text += " edited";
                    layout.Dock(state.Id);
                    await WaitFor(() => shell.GetVisualDescendants().OfType<DockPlaceholderView>().Any(v => v.State.Id == state.Id));
                    shell.Activate(); View(shell, state.Id).Notes.Focus();
                    await Task.Delay(150);
                    Check(state.Id + ":redock-cleanup", !floating.IsVisible && layout.Root.Windows!.Count == 0);
                    Check(state.Id + ":redock-identity-state", ReferenceEquals(layout.Pane(state.Id), pane) && View(shell, state.Id).Notes.Text == "native " + state.Id + " edited");
                    Check(state.Id + ":redock-focus", GetForegroundWindow() == handle && View(shell, state.Id).Notes.IsKeyboardFocusWithin);
                    if (!state.IsDocument)
                    {
                        layout.Close(state.Id); layout.Show(state.Id);
                        await WaitFor(() => shell.GetVisualDescendants().OfType<DockPlaceholderView>().Any(v => v.State.Id == state.Id));
                        Check(state.Id + ":reopen-state", View(shell, state.Id).Notes.Text == state.Note && state.Note.EndsWith(" edited"));
                        layout.Float(state.Id);
                        var closingHost = (Window)layout.Root.Windows!.Single().Host!;
                        closingHost.Close();
                        layout.Show(state.Id);
                        await WaitFor(() => shell.GetVisualDescendants().OfType<DockPlaceholderView>().Any(v => v.State.Id == state.Id));
                        Check(state.Id + ":native-close-reopen", !closingHost.IsVisible && layout.Root.Windows!.Count == 0 && View(shell, state.Id).Notes.Text == state.Note);
                    }
                }
                layout.Float(DockShellState.OutputId); layout.Float(DockShellState.DocumentId);
                var hosts = layout.Root.Windows!.Select(w => (Window)w.Host!).ToArray();
                shell.Close(); await Task.Delay(200);
                Check("shutdown-all-hosts", hosts.All(w => !w.IsVisible) && layout.Root.Windows!.Count == 0);
                Check("shutdown-registries", layout.Factory.DockControls.Count == 0 && layout.Factory.HostWindows.Count == 0);
                Check("no-execution-lifecycle-effects", before == (coordinator.Generation, coordinator.WorkspaceGeneration, coordinator.Phase, coordinator.Pending, service.DraftRevision));
            }
            if (shell.IsVisible) { shell.Close(); await WaitFor(() => !shell.IsVisible); }
            Check("shutdown-no-windows", desktop.Windows.Count == 0);
        }
        catch (Exception ex) { failures++; checks.Add(new { name = "probe-exception", passed = false, detail = ex.ToString() }); }
        finally
        {
            File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, platform = RuntimeInformation.OSDescription, passed = checks.Count - failures, failed = failures, checks }, new JsonSerializerOptions { WriteIndented = true }));
            desktop.Shutdown(failures == 0 ? 0 : 1);
        }
    }
}
