using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using CryptoWorkBenchAvalonia.ViewModels;
using Prism.Commands;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task RunIntegration(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var checks = new List<object>(); int failures = 0;
        bool external = Mode == "integration-input";
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failures++; }
        var shell = (DockShellWindow)desktop.MainWindow!;
        var layout = shell.Layout; var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        var variables = Container.Resolve<VariableViewModel>(); var info = Container.Resolve<InfoViewModel>();
        var editor = session.Editor;
        async Task Wait(Func<bool> done, int seconds = 20)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (!done() && DateTime.UtcNow < until) await Task.Delay(30);
            if (!done()) throw new TimeoutException("Integration checkpoint: " + shell.Title);
        }
        void Stage(string name, string instruction)
        {
            shell.Title = "G1-08 " + name + " — " + instruction;
            File.WriteAllText(Output + ".stage.json", JsonSerializer.Serialize(new { stage = name, instruction }));
        }
        void Write() => File.WriteAllText(Mode == "integration-restart" ? Output + ".integration.json" : Output,
            JsonSerializer.Serialize(new { mode = Mode, input = external ? "external keyboard and native file dialogs; other operations explicitly programmatic" : "programmatic", passed = checks.Count - failures, failed = failures, checks }, new JsonSerializerOptions { WriteIndented = true }));
        try
        {
            await Wait(() => session.View.IsLoaded);
            shell.Activate(); Stage("activate", "activate this window");
            await Wait(() => GetForegroundWindow() == shell.TryGetPlatformHandle()!.Handle, 120);
            Check("one-registered-editor", service.HasRegisteredEditor && service.EditorCoordinator.Targets(editor));
            if (Mode == "integration-restart")
            {
                Check("restore-floating-info", layout.Factory.FindRoot(layout.Pane(DockShellState.InfoId)) != layout.Root);
                Check("restore-closed-status", !DockShellLayout.VisibleTree(layout.Root).Contains(layout.Pane(DockShellState.StatusId)));
                Check("restore-source-empty", editor.Text.Length == 0);
                Check("restore-output-empty", session.Output.Entries.Count == 0);
                Check("restore-variables-empty", variables.DataVariables.Count == 0);
                Check("restore-no-startup-failure", DockShellStartupError == null);
                layout.Dock(DockShellState.InfoId); layout.Show(DockShellState.StatusId);
                Write();
                if (failures != 0) { desktop.Shutdown(1); return; }
                // Reuse the original native editor/real STDIO+TLS MCP acceptance, unchanged.
                await RunEditor(desktop);
                return;
            }
            shell.Width = 1400; shell.Height = 850;
            layout.Show(DockShellState.DocumentId); editor.TextArea.Focus();
            const string script = "VAR integrated=0x(AB) Print(0x(CD)) Info(HASH-SHA256)";
            Stage("type", script);
            if (!external) editor.TextArea.PerformTextInput(script);
            await Wait(() => editor.Text == script, 240); Check("script-input", true);
            Stage("enter", "press Return");
            if (!external)
            {
                await session.ViewModel.ParseLineAsync(script);
                editor.Document.Insert(editor.Text.Length, Environment.NewLine);
            }
            await Wait(() => session.Output.Entries.Count == 1 && !session.ViewModel.IsExecuting, 240);
            await session.View.PendingExecution;
            Check("enter-continuation", editor.Text == script + Environment.NewLine);
            Check("variables-result", variables.DataVariables.Any(v => v.Identifier == "integrated" && v.Value == "0x(AB)"));
            Check("print-result", session.Output.Entries.Single().Text == "out: 0x(CD)");
            Check("infodoc-result", info.InfoText.Contains("HASH"));
            // Use a short visible example; the long mechanism document virtualizes code below the viewport.
            info.SetInfoText("# Integrated example\n\n```text\nPrint(0x(CD))\n```\n");
            layout.Show(DockShellState.InfoId); await Task.Delay(200);
            var code = shell.GetVisualDescendants().OfType<TextEditor>().First(c => !ReferenceEquals(c, editor));
            var pad = code.GetVisualAncestors().Single(c => c.GetType().Name == "CodePad");
            ((Button)pad.GetType().GetProperty("ExandableMenu")!.GetValue(pad)!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(100);
            Check("copy-example-native-clipboard-programmatic-button", await shell.Clipboard!.TryGetTextAsync() == code.Text);
            session.LoadText(service.CaptureSaveIntent()!, "  @");
            await session.ViewModel.ParseLineAsync(editor.Text);
            var diagnostic = session.ViewModel.Status.Diagnostics.First();
            Check("syntax-diagnostic-navigation", session.NavigateDiagnostic(diagnostic) == SourceNavigationResult.Success && editor.CaretOffset == 2);
            editor.Document.Insert(0, " ");
            Check("stale-navigation", session.NavigateDiagnostic(diagnostic) == SourceNavigationResult.Stale);
            editor.Undo(); Check("undo", editor.Text == "  @"); editor.Redo(); Check("redo", editor.Text == "   @");
            session.LoadText(service.CaptureSaveIntent()!, "KEY missing=Check(0x(AA)) VAR continued=0x(01)");
            await session.ViewModel.ParseLineAsync(editor.Text);
            Check("semantic-diagnostic-and-continuation", session.ViewModel.Status.Diagnostics.Single().Text.Contains("KeyCheckValueNotFound") && variables.DataVariables.Any(v => v.Identifier == "continued"));
            var history = (HistoryService)Container.Resolve<IHistoryService>();
            history.AddLine("g108-history-old"); history.AddLine("g108-history-new");
            editor.Text = "g108-history-new"; history.LineBack(); Check("line-history", editor.Text == "g108-history-old");
            const string saved = "// G1-08 ä€😀\nVAR reopenMustNotRun=0x(99) Print(0x(EE))";
            session.LoadText(service.CaptureSaveIntent()!, saved);
            string path = Path.GetFullPath(Output + ".sbk");
            Stage("save-dialog", path);
            if (external) await ((IAsyncCommand)session.SaveCommand).ExecuteAsync(null).WaitAsync(TimeSpan.FromMinutes(4));
            else await service.CommitSaveAsync(service.CaptureSaveIntent()!, new(path, false, null, PreserveText: true));
            Check("save-exact-text", File.Exists(path) && File.ReadAllText(path) == saved);
            session.LoadText(service.CaptureSaveIntent()!, "");
            long revision = InterpreterHostQueue.StateRevision; int count = session.Output.Entries.Count;
            Stage("open-dialog", path);
            if (external) await ((IAsyncCommand)session.OpenCommand).ExecuteAsync(null).WaitAsync(TimeSpan.FromMinutes(4));
            else session.LoadText(service.CaptureSaveIntent()!, File.ReadAllText(path));
            Check("open-exact-text", editor.Text == saved);
            Check("open-without-execution", InterpreterHostQueue.StateRevision == revision && session.Output.Entries.Count == count && !variables.DataVariables.Any(v => v.Identifier == "reopenMustNotRun"));
            var document = editor.Document; var identity = service.CaptureSaveIntent()!.Snapshot.EditorId;
            foreach (var id in DockLayoutStore.PaneIds)
            {
                Stage("float", id);
                layout.Float(id); await Task.Delay(100);
                Check("float-native-" + id, layout.Factory.FindRoot(layout.Pane(id))!.Window!.Host is Window host && host.TryGetPlatformHandle()!.Handle != 0);
                Stage("dock", id); layout.Dock(id);
            }
            Check("relocation-preserves-editor", ReferenceEquals(document, editor.Document) && service.CaptureSaveIntent()!.Snapshot.EditorId == identity);
            Check("maximize", layout.Maximize(DockShellState.DocumentId)); Check("restore", layout.Restore());
            layout.Float(DockShellState.InfoId); layout.Close(DockShellState.StatusId);
            var floating = (Window)layout.Root.Windows!.Single().Host!;
            Check("normal-layout-capture-excludes-source", !layout.Capture(new(0, 0, 1000, 700)).Contains("reopenMustNotRun"));
            shell.Close(); await Wait(() => !shell.IsVisible);
            Check("shutdown-host-and-registration", !floating.IsVisible && !service.HasRegisteredEditor && layout.Factory.HostWindows.Count == 0);
            Check("persisted-layout", new DockLayoutStore(Output + ".layout.json").Load() is { ClosedTools.Length: 1 });
        }
        catch (Exception error) { failures++; checks.Add(new { name = "exception", passed = false, error = error.ToString() }); }
        Write(); desktop.Shutdown(failures == 0 ? 0 : 1);
    }
}
