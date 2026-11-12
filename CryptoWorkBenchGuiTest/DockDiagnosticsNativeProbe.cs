using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CryptoScript.ErrorListner;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task RunDiagnostics(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var checks = new List<object>(); int failures = 0; bool external = Mode == "diagnostics-input";
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failures++; }
        static async Task Wait(Func<bool> condition, int seconds = 15)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition() && DateTime.UtcNow < until) await Task.Delay(30);
            if (!condition()) throw new TimeoutException("Diagnostics checkpoint did not settle.");
        }
        var window = (DockShellWindow)desktop.MainWindow!;
        var layout = window.Layout; var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>(); var editor = session.Editor;
        var model = session.ViewModel.Status;
        async Task Stage(string name, Window top, string instruction, Func<bool> done, Action simulate)
        {
            top.Activate(); top.Title = "G1-06 " + name + " — " + instruction;
            await Task.Delay(150);
            if (external) File.WriteAllText(Output + ".stage.json", JsonSerializer.Serialize(new { stage = name, instruction, windowTitle = top.Title }));
            else simulate();
            await Wait(done, external ? 240 : 15); Check(name, done());
        }
        async Task<Window> Float(string id)
        {
            layout.Float(id); await Task.Delay(200);
            var host = (Window)layout.Factory.FindRoot(layout.Pane(id))!.Window!.Host!;
            host.Width = 850; host.Height = 650; return host;
        }
        try
        {
            await Wait(() => window.GetVisualDescendants().Any(v => ReferenceEquals(v, editor)));
            window.Width = 1450; window.Height = 900; window.Activate();
            await Wait(() => GetForegroundWindow() == window.TryGetPlatformHandle()!.Handle, 45);
            Check("native-main-window", window.TryGetPlatformHandle()!.Handle != IntPtr.Zero);
            editor.Text = "  @";
            await session.ViewModel.ParseLineAsync(editor.Text);
            var source = model.Diagnostics.Single();
            var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) =>
                sink.Report(new SemanticError { Severity = SemanticDiagnosticSeverity.Warning, Message = "G1-06 warning ä€😀" }));
            await session.ViewModel.ExecuteLineAsync("Print(0x(AB))", runner: runner);
            Check("real-parser-error", source.Category == DiagnosticCategory.SyntaxError && source.SourceLine == 1 && source.SourceColumn == 3);
            Check("real-semantic-warning", model.Diagnostics.Count == 2 && model.Diagnostics[1].Category == DiagnosticCategory.SemanticWarning);
            Check("separate-print", session.Output.Entries.Single().Text == "out: 0x(AB)");
            layout.Show(DockShellState.DiagnosticsId); await Task.Delay(200);
            var view = window.GetVisualDescendants().OfType<DockDiagnosticsView>().Single();
            Check("global-model", ReferenceEquals(view.DataContext, model));
            var snapshot = service.CaptureSaveIntent()!.Snapshot; long revision = InterpreterHostQueue.StateRevision;
            foreach (var filter in new[] { DiagnosticFilter.Warnings, DiagnosticFilter.Errors, DiagnosticFilter.All })
            {
                var button = view.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, filter.ToString()));
                await Stage("filter-" + filter, window, "click " + filter, () => model.Filter == filter,
                    () => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                Check("filter-count-" + filter, model.VisibleDiagnostics.Count == (filter == DiagnosticFilter.All ? 2 : 1));
            }
            Check("filter-preserves-source", model.Diagnostics.Count == 2 && service.CaptureSaveIntent()!.Snapshot == snapshot);
            model.Filter = DiagnosticFilter.Errors; await Task.Delay(150);
            foreach (bool floating in new[] { false, true })
            {
                var top = floating ? await Float(DockShellState.DiagnosticsId) : window;
                await Task.Delay(150);
                var text = view.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
                Check("rendered-error-" + floating, text.IsVisible && text.Text == source.Text && text.Bounds.Height > 0);
                if (floating) Check("native-floating-diagnostics", top.TryGetPlatformHandle()!.Handle != window.TryGetPlatformHandle()!.Handle);
                await top.Clipboard!.SetTextAsync("g106-sentinel"); bool copied = false;
                var polling = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var until = DateTime.UtcNow.AddSeconds(external ? 240 : 15);
                    while (!copied && DateTime.UtcNow < until)
                    { copied = await top.Clipboard!.TryGetTextAsync() == source.Text; await Task.Delay(40); }
                });
                await Stage("copy-" + floating, top, "select diagnostic message, Ctrl+A, Ctrl+C", () => copied,
                    () => { text.Focus(); text.SelectionStart = 0; text.SelectionEnd = text.Text!.Length; text.Copy(); });
                await polling;
                Check("copy-exact-" + floating, await top.Clipboard!.TryGetTextAsync() == source.Text);
                Check("copy-no-effects-" + floating, service.CaptureSaveIntent()!.Snapshot == snapshot && InterpreterHostQueue.StateRevision == revision);
                if (floating) { layout.Dock(DockShellState.DiagnosticsId); await Task.Delay(200); }
            }
            foreach (bool floating in new[] { false, true })
            {
                var editorTop = floating ? await Float(DockShellState.DocumentId) : window;
                editor.CaretOffset = 0;
                var navigate = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "DiagnosticSource");
                await Stage("navigate-" + floating, window, "click Go to line 1, column 3", () => editor.CaretOffset == 2 && editor.IsKeyboardFocusWithin,
                    () => navigate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                Check("foreground-editor-" + floating, GetForegroundWindow() == editorTop.TryGetPlatformHandle()!.Handle);
                Check("navigation-no-effects-" + floating, service.CaptureSaveIntent()!.Snapshot == snapshot && InterpreterHostQueue.StateRevision == revision);
                if (floating) { layout.Dock(DockShellState.DocumentId); await Task.Delay(200); }
            }
            editor.Text = "modified"; editor.CaretOffset = 0;
            var staleButton = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "DiagnosticSource");
            await Stage("stale", window, "click Go to line 1, column 3 again", () => view.Feedback.Text?.Contains("outdated") == true,
                () => staleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            Check("stale-does-not-move", editor.CaretOffset == 0 && editor.Text == "modified");
            layout.Close(DockShellState.DiagnosticsId); layout.Close(DockShellState.StatusId);
            model.StatusString = "G1-06 status while closed";
            layout.Show(DockShellState.StatusId); await Task.Delay(150);
            var status = window.GetVisualDescendants().OfType<DockStatusView>().Single();
            Check("status-global-model", ReferenceEquals(status.DataContext, model));
            Check("status-closed-update", status.GetVisualDescendants().OfType<SelectableTextBlock>().Single().Text == model.StatusString);
            layout.Show(DockShellState.DiagnosticsId); await Task.Delay(150);
            Check("close-show-retains-rows", model.Diagnostics.Count == 2 && ReferenceEquals(view.DataContext, model));
            model.Filter = DiagnosticFilter.Warnings; await Task.Delay(150);
            Check("rendered-warning", view.GetVisualDescendants().OfType<SelectableTextBlock>().Single().Text == model.Diagnostics[1].Text);
            var finalHost = await Float(DockShellState.DiagnosticsId); var statusHost = await Float(DockShellState.StatusId);
            window.Close(); await Wait(() => !window.IsVisible);
            Check("shutdown-floating-tools", !finalHost.IsVisible && !statusHost.IsVisible && desktop.Windows.Count == 0);
            Check("shutdown-editor-registration", !service.HasRegisteredEditor);
            File.WriteAllText(Output + ".stage.json", "{\"stage\":\"complete\"}");
        }
        catch (Exception error) { failures++; checks.Add(new { name = "probe-exception", passed = false, detail = error.ToString() }); }
        finally
        {
            File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode,
                inputSource = external ? "external Windows mouse/keyboard at named checkpoints" : "programmatic native UI",
                passed = checks.Count - failures, failed = failures, checks }, new JsonSerializerOptions { WriteIndented = true }));
            desktop.Shutdown(failures == 0 ? 0 : 1);
        }
    }
}
