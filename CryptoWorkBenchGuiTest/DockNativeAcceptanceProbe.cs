using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task RunNativeAcceptance(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var shell = (DockShellWindow)desktop.MainWindow!;
        var layout = shell.Layout;
        var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        var editor = session.Editor; var document = editor.Document; var undo = document.UndoStack;
        var editorId = service.CaptureSaveIntent()!.Snapshot.EditorId;
        var generation = service.EditorCoordinator.WorkspaceGeneration;
        bool external = Mode == "native-acceptance-input";
        var checks = new List<object>(); int failed = 0;
        using var journal = new RedockJournal(layout, session, desktop, Output + ".windows.jsonl");
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failed++; }
        async Task Wait(string name, Func<bool> condition, int seconds = 15)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition() && DateTime.UtcNow < until) await Task.Delay(30);
            if (!condition()) throw new TimeoutException(name + "; foreground HWND=" + GetForegroundWindow());
        }
        async Task Focus(Window top)
        {
            top.Activate(); editor.TextArea.Focus();
            await Wait("foreground/editor focus", () => GetForegroundWindow() == top.TryGetPlatformHandle()!.Handle && editor.IsKeyboardFocusWithin);
        }
        async Task Stage(string name, Window top, string instruction, Func<bool> done, Action simulate)
        {
            await Focus(top); top.Title = "G1-A05 " + name + " — " + instruction;
            File.WriteAllText(Output + ".stage.json", JsonSerializer.Serialize(new { stage = name, instruction, windowTitle = top.Title, external }));
            if (!external) simulate();
            await Wait(name, done, external ? 240 : 15);
            Check(name, done());
        }
        CompletionWindow? Completion()
        {
            var controller = typeof(CryptoScriptEditView).GetField("_infoCompletionController", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session.View);
            return controller == null ? null : (CompletionWindow?)typeof(InfoCompletionController)
                .GetField("_window", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(controller);
        }
        OverloadInsightWindow? Insight() => (OverloadInsightWindow?)typeof(CryptoScriptEditView)
            .GetField("_insightWindow", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session.View);
        try
        {
            await Wait("editor attached", () => TopLevel.GetTopLevel(editor) == shell);
            await Focus(shell);
            if (!external)
            {
                foreach (var scenario in new[] { "single", "group", "document-tab", "mixed" })
                {
                    var pane = DockGroupDocumentRedockTests.FloatScenario(layout, scenario == "mixed" ? "document-tab" : scenario);
                    var model = layout.Factory.FindRoot(pane)!.Window!; var host = (Window)model.Host!;
                    if (scenario == "mixed")
                        Check("mixed-drop", new DockManager(new DockService()).ValidateDockable(layout.Pane(DockShellState.VariablesId), pane.Owner!, DragAction.Move, DockOperation.Right, true));
                    await Task.Delay(200);
                    bool hasDocument = scenario is "document-tab" or "mixed";
                    Check(scenario + "-chrome", DockNativeChromeTests.HasDocumentGrip(host) == hasDocument);
                    var hwnd = host.TryGetPlatformHandle()!.Handle;
                    Check(scenario + "-hwnd-owner", IsWindow(hwnd) && IsWindowVisible(hwnd) && GetWindow(hwnd, 4) == shell.TryGetPlatformHandle()!.Handle);
                    host.Activate(); await Wait(scenario + " activation", () => GetForegroundWindow() == hwnd);
                    var position = host.Position; host.Position = new PixelPoint(position.X + 20, position.Y + 20);
                    Check(scenario + "-move", host.Position != position);
                    host.Close(); await Task.Delay(100);
                    Check(scenario + "-close", hasDocument ? IsWindow(hwnd) && host.IsVisible : !IsWindow(hwnd) && !layout.Root.Windows!.Contains(model));
                    layout.ExecuteLayout(DockLayoutCommand.Reset);
                    await Wait("reset", () => layout.Factory.HostWindows.Count == 0 && TopLevel.GetTopLevel(editor) == shell);
                }
                // Existing tool HWND acquires, then loses, the actual document.
                layout.Float(DockShellState.VariablesId);
                var mixedHost = (Window)layout.Factory.FindRoot(layout.Pane(DockShellState.VariablesId))!.Window!.Host!;
                var mixedHwnd = mixedHost.TryGetPlatformHandle()!.Handle;
                Check("mixed-transition-drop", new DockManager(new DockService()).ValidateDockable(layout.Pane(DockShellState.DocumentId).Owner!,
                    layout.Pane(DockShellState.VariablesId).Owner!, DragAction.Move, DockOperation.Right, true));
                await Wait("mixed chrome", () => DockNativeChromeTests.HasDocumentGrip(mixedHost));
                layout.Dock(DockShellState.DocumentId);
                await Wait("tool chrome restored", () => !DockNativeChromeTests.HasDocumentGrip(mixedHost));
                Check("mixed-transition-preserves-tool-mixedHwnd", mixedHost.IsVisible && mixedHost.TryGetPlatformHandle()!.Handle == mixedHwnd && IsWindow(mixedHwnd));
                mixedHost.Close(); await Wait("tool mixedHost destroyed", () => !IsWindow(mixedHwnd));
                layout.ExecuteLayout(DockLayoutCommand.Reset); await Task.Delay(200);
            }
            foreach (var phase in new[] { "main", "floating", "redocked" })
            {
                if (phase == "floating") layout.Float(DockShellState.DocumentId);
                if (phase == "redocked") layout.Dock(DockShellState.DocumentId);
                await Wait("editor host", () => TopLevel.GetTopLevel(editor) is Window w && (phase == "floating" ? w != shell : w == shell));
                var top = (Window)TopLevel.GetTopLevel(editor)!;
                const string sample = "// ä€😀 clipboard";
                service.Mutate(() => { editor.Text = sample; editor.CaretOffset = sample.Length; });
                await top.Clipboard!.SetTextAsync("A05 sentinel");
                bool copied = false;
                var copyPoll = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var until = DateTime.UtcNow.AddSeconds(external ? 240 : 15);
                    while (!copied && DateTime.UtcNow < until)
                    { copied = await top.Clipboard!.TryGetTextAsync() == sample; await Task.Delay(40); }
                });
                await Stage(phase + "-copy", top, "select all editor text and Ctrl+C", () => copied,
                    () => { editor.SelectAll(); ApplicationCommands.Copy.Execute(null, editor.TextArea); });
                await copyPoll;
                Check(phase + "-copy-selection", editor.SelectedText == sample);
                editor.Select(0, 0); editor.CaretOffset = editor.Text.Length;
                await Stage(phase + "-paste", top, "Ctrl+V", () => editor.Text == sample + sample,
                    () => ApplicationCommands.Paste.Execute(null, editor.TextArea));
                await Stage(phase + "-undo", top, "Ctrl+Z", () => editor.Text == sample,
                    () => ApplicationCommands.Undo.Execute(null, editor.TextArea));
                await Stage(phase + "-redo", top, "Ctrl+Y", () => editor.Text == sample + sample,
                    () => ApplicationCommands.Redo.Execute(null, editor.TextArea));
                service.Mutate(() => { editor.Text = "Info"; editor.CaretOffset = 4; });
                await Stage(phase + "-completion", top, "press the ( key", () => editor.Text == "Info(", () => editor.TextArea.PerformTextInput("("));
                var popup = Completion();
                Check(phase + "-completion-open", popup?.IsOpen == true);
                Check(phase + "-completion-owner", popup?.PlacementTarget is { } target && ReferenceEquals(TopLevel.GetTopLevel(target), top));
                var before = editor.Text;
                if (phase == "floating") layout.Dock(DockShellState.DocumentId); else layout.Float(DockShellState.DocumentId);
                await Task.Delay(200);
                Check(phase + "-completion-retired", popup != null && !popup.IsOpen);
                popup?.CompletionList.RequestInsertion(EventArgs.Empty);
                Check(phase + "-late-insertion-inert", editor.Text == before);
                if (phase == "floating") layout.Float(DockShellState.DocumentId); else layout.Dock(DockShellState.DocumentId);
                await Task.Delay(200); top = (Window)TopLevel.GetTopLevel(editor)!;
                service.Mutate(() => { editor.Text = "Print"; editor.CaretOffset = 5; });
                await Stage(phase + "-insight", top, "press the ( key", () => editor.Text == "Print(", () => editor.TextArea.PerformTextInput("("));
                var insight = Insight();
                Check(phase + "-insight-open", insight?.IsOpen == true);
                Check(phase + "-insight-owner", insight?.PlacementTarget is { } insightTarget && ReferenceEquals(TopLevel.GetTopLevel(insightTarget), top));
                if (phase == "floating") layout.Dock(DockShellState.DocumentId); else layout.Float(DockShellState.DocumentId);
                await Task.Delay(200);
                Check(phase + "-insight-retired", insight != null && !insight.IsOpen);
                layout.Dock(DockShellState.DocumentId); await Task.Delay(200);
                Check(phase + "-no-orphan-popup", VisibleProcessWindows().SetEquals(new[] { shell.TryGetPlatformHandle()!.Handle }));
                Check(phase + "-identity", ReferenceEquals(session.Editor, editor) && ReferenceEquals(editor.Document, document) && ReferenceEquals(document.UndoStack, undo) &&
                    service.CaptureSaveIntent()!.Snapshot.EditorId == editorId && service.EditorCoordinator.WorkspaceGeneration == generation);
            }
            shell.Close(); await Wait("shutdown", () => !shell.IsVisible);
            Check("shutdown-native-clean", VisibleProcessWindows().Count == 0 && layout.Factory.HostWindows.Count == 0 && layout.Factory.DockControls.Count == 0 && !service.HasRegisteredEditor);
        }
        catch (Exception ex) { Check("probe-exception", false); checks.Add(new { detail = ex.ToString() }); }
        finally
        {
            File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, processId = Environment.ProcessId,
                inputSource = external ? "external Windows keys; programmatic layout operations" : "programmatic native hosts and routed editor input",
                passed = checks.Count(c => c.GetType().GetProperty("passed")?.GetValue(c) is true), failed, checks }, new JsonSerializerOptions { WriteIndented = true }));
            desktop.Shutdown(failed == 0 ? 0 : 1);
        }
    }
}
