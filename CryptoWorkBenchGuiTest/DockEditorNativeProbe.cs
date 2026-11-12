using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Prism.Ioc;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Views;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    public DockShellNativeProbe()
    {
        // Exercise the production desktop hook without adding a GUI-test -> desktop project cycle.
        ConfigureDesktopMcp = (window, button, service) =>
        {
            var desktop = Assembly.Load("CryptoWorkBenchAvalonia.Desktop");
            var attach = desktop.GetType("CryptoWorkBenchAvalonia.Desktop.DesktopMcpSession")!
                .GetMethod("Attach", BindingFlags.Static | BindingFlags.NonPublic)!;
            return (Func<Task>)attach.Invoke(null, [window, button, service])!;
        };
    }

    private async Task RunEditor(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var checks = new List<object>(); int failures = 0;
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failures++; }
        static async Task Wait(Func<bool> condition, int seconds = 10)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition() && DateTime.UtcNow < until) await Task.Delay(30);
            if (!condition()) throw new TimeoutException("Native editor checkpoint did not settle.");
        }
        static void Click(Window window, string text) => window.GetVisualDescendants().OfType<Button>()
            .Single(b => Equals(b.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var window = (DockShellWindow)desktop.MainWindow!;
        var session = Container.Resolve<DockEditorSession>();
        var editor = session.Editor; var service = Container.Resolve<WorkbenchAutomationService>();
        var layout = window.Layout;
        Process? bridge = null;
        try
        {
            await Wait(() => window.GetVisualDescendants().OfType<WorkbenchTextEditor>().Any());
            window.Activate(); editor.TextArea.Focus();
            await Wait(() => GetForegroundWindow() == window.TryGetPlatformHandle()!.Handle, 30);
            Check("product-editor-singleton", ReferenceEquals(Container.Resolve<DockEditorSession>(), session) &&
                window.GetVisualDescendants().OfType<WorkbenchTextEditor>().Count() == 1);
            Check("registered-before-input", service.HasRegisteredEditor && service.EditorCoordinator.Targets(editor));
            Check("desktop-mcp-hook-installed", window.McpButton.IsVisible && window.McpButton.IsEnabled);
            var identity = service.CaptureSaveIntent()!.Snapshot.EditorId;
            var generation = service.EditorCoordinator.WorkspaceGeneration;
            var document = editor.Document; var undo = document.UndoStack;
            var lifecycle = session.ViewModel.EditorLifecycle;
            for (int i = 0; i < 5; i++)
            {
                layout.Float(DockShellState.DocumentId);
                var host = (Window)layout.Root.Windows!.Single().Host!;
                await Wait(() => ReferenceEquals(TopLevel.GetTopLevel(editor), host));
                host.Activate(); editor.TextArea.Focus(); await Task.Delay(100);
                Check($"float-{i}-native-owner", GetWindow(host.TryGetPlatformHandle()!.Handle, 4) == window.TryGetPlatformHandle()!.Handle);
                Check($"float-{i}-focus", GetForegroundWindow() == host.TryGetPlatformHandle()!.Handle && editor.IsKeyboardFocusWithin);
                host.Close(); Check($"float-{i}-close-protected", host.IsVisible);
                layout.Dock(DockShellState.DocumentId);
                await Wait(() => ReferenceEquals(TopLevel.GetTopLevel(editor), window));
                Check($"redock-{i}-identity", ReferenceEquals(editor.Document, document) && ReferenceEquals(document.UndoStack, undo) &&
                    service.CaptureSaveIntent()!.Snapshot.EditorId == identity && service.EditorCoordinator.WorkspaceGeneration == generation && session.ViewModel.EditorLifecycle == lifecycle);
                Check($"redock-{i}-cleanup", !host.IsVisible && layout.Root.Windows!.Count == 0);
            }
            async Task Input(string stage, string instruction, Func<bool> complete, Action simulated)
            {
                var top = (Window)TopLevel.GetTopLevel(editor)!;
                top.Activate(); editor.TextArea.Focus();
                top.Title = "G1-03 " + stage + " — " + instruction;
                if (Mode == "editor-input")
                {
                    File.WriteAllText(Output + ".stage.json", JsonSerializer.Serialize(new { stage, instruction, windowTitle = top.Title }));
                    await Wait(complete, 180);
                }
                else { simulated(); await Wait(complete); }
                Check(stage, complete());
            }
            await Input("unicode", "type ä€😀", () => editor.Text == "ä€😀", () => editor.TextArea.PerformTextInput("ä€😀"));
            layout.Float(DockShellState.DocumentId);
            await Wait(() => TopLevel.GetTopLevel(editor) is Window floating && floating != window);
            await Input("undo", "press Ctrl+Z", () => editor.Text.Length == 0, () => ApplicationCommands.Undo.Execute(null, editor.TextArea));
            await Input("redo", "press Ctrl+Y", () => editor.Text == "ä€😀", () => ApplicationCommands.Redo.Execute(null, editor.TextArea));
            editor.Text = "Print(0x(11))"; editor.CaretOffset = editor.Text.Length;
            await Input("return", "press Return", () => session.Output.Entries.Any(e => e.Text == "out: 0x(11)") && editor.Text == "Print(0x(11))" + Environment.NewLine, () => editor.TextArea.PerformTextInput("\n"));
            await session.View.PendingExecution;
            layout.Dock(DockShellState.DocumentId); await Wait(() => TopLevel.GetTopLevel(editor) == window);
            editor.Text = "Print(0x(22))"; editor.CaretOffset = editor.Text.Length;
            await Input("numpad-enter", "press KP_Enter", () => session.Output.Entries.Any(e => e.Text == "out: 0x(22)") && editor.Text == "Print(0x(22))" + Environment.NewLine, () => editor.TextArea.PerformTextInput("\n"));
            await session.View.PendingExecution;
            var history = (HistoryService)Container.Resolve<IHistoryService>();
            int historyCount = ((System.Collections.ICollection)typeof(HistoryService).GetField("_lineHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(history)!).Count;
            Check("one-history-entry-per-enter", historyCount == 2);
            history.AddLine("history-old"); history.AddLine("history-new"); editor.Text = "history-new";
            layout.Float(DockShellState.DocumentId); await Wait(() => TopLevel.GetTopLevel(editor) is Window floating && floating != window);
            await Input("line-history", "press Alt+PageUp", () => editor.Text == "history-old", () => history.LineBack());
            editor.Text = "Info"; editor.CaretOffset = editor.Text.Length;
            await Input("completion", "type (", () => editor.Text == "Info(", () => editor.TextArea.PerformTextInput("("));
            var controller = typeof(CryptoScriptEditView).GetField("_infoCompletionController", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session.View)!;
            var popup = (CompletionWindow?)typeof(InfoCompletionController).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller);
            Check("completion-in-floating-host", popup != null && popup.IsOpen);
            layout.Dock(DockShellState.DocumentId); await Wait(() => TopLevel.GetTopLevel(editor) == window);
            Check("completion-retired-on-relocation", popup != null && !popup.IsOpen);
            Check("registration-still-original", service.CaptureSaveIntent()!.Snapshot.EditorId == identity && service.EditorCoordinator.WorkspaceGeneration == generation);

            layout.Float(DockShellState.DocumentId);
            await Wait(() => TopLevel.GetTopLevel(editor) is Window floating && floating != window);
            var passwordWork = session.ViewModel.ExecuteLineAsync("Print(0x(01))",
                testStatements: [new AsyncInfrastructureStatement(async context =>
                    { using var answer = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad); })]);
            await Wait(() => desktop.Windows.OfType<PasswordDialog>().Any());
            var password = desktop.Windows.OfType<PasswordDialog>().Single();
            var passwordHost = (Window)TopLevel.GetTopLevel(editor)!;
            layout.Dock(DockShellState.DocumentId); passwordHost.Close();
            Check("gui-password-blocks-dock-and-host-close", session.ViewModel.IsExecuting && passwordHost.IsVisible && TopLevel.GetTopLevel(editor) == passwordHost && !session.CanRelocate);
            password.Close(); await passwordWork;
            Check("gui-password-cancel-settles-original-job", !session.ViewModel.IsExecuting && password.PasswordInput.Text == "" && service.CaptureSaveIntent()!.Snapshot.EditorId == identity);
            layout.Dock(DockShellState.DocumentId); await Wait(() => TopLevel.GetTopLevel(editor) == window);

            // Real product consent/pairing UI + TLS listener + actual --mcp-stdio executable.
            // Test code activates the consent buttons; no credential is written to evidence/logs.
            Check("load-does-not-execute", session.LoadText(service.CaptureSaveIntent()!, "Print(0x(33))"));
            window.McpButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => desktop.Windows.Any(w => w.Title == "Allow local Codex access?"));
            Click(desktop.Windows.Single(w => w.Title == "Allow local Codex access?"), "Allow access");
            await Wait(() => desktop.Windows.Any(w => w.Title == "Pair local Codex"));
            var pairing = desktop.Windows.Single(w => w.Title == "Pair local Codex");
            string token = pairing.GetVisualDescendants().OfType<TextBox>().Single().Text!;
            string message = pairing.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.Contains("CRYPTOWORKBENCH_MCP_CERT_SHA256") == true).Text!;
            string pin = Regex.Match(message, @"CRYPTOWORKBENCH_MCP_CERT_SHA256 to\s+([A-F0-9]{64})").Groups[1].Value;
            Check("production-pairing", token.Length == 64 && pin.Length == 64);
            Click(pairing, "Done"); await Wait(() => window.IsEnabled);
#if DEBUG
            const string config = "Debug";
#else
            const string config = "Release";
#endif
            var executable = Path.GetFullPath($"CryptoWorkBenchAvalonia.Desktop/bin/{config}/net10.0/CryptoWorkBenchAvalonia.Desktop.exe");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("--mcp-stdio");
            start.Environment["CRYPTOWORKBENCH_MCP_TOKEN"] = token;
            start.Environment["CRYPTOWORKBENCH_MCP_CERT_SHA256"] = pin;
            bridge = Process.Start(start)!;
            var errors = bridge.StandardError.ReadToEndAsync();
            async Task<JsonElement> Send(object frame)
            {
                await bridge.StandardInput.WriteLineAsync(JsonSerializer.Serialize(frame)); await bridge.StandardInput.FlushAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var line = await bridge.StandardOutput.ReadLineAsync(timeout.Token) ?? throw new IOException("STDIO closed.");
                if (line.Contains(token)) throw new InvalidOperationException("Credential appeared in protocol output.");
                return JsonSerializer.Deserialize<JsonElement>(line);
            }
            var initialized = await Send(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "g1-03-probe", version = "1" } } });
            Check("actual-stdio-initialize", initialized.TryGetProperty("result", out _));
            await bridge.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            int sequence = 1;
            async Task<JsonElement> Call(string name, IContractV2 request)
            {
                var frame = await Send(new { jsonrpc = "2.0", id = ++sequence, method = "tools/call", @params = new { name = "workbench_" + name, arguments = JsonSerializer.SerializeToElement(request, request.GetType(), AutomationJson.Options) } });
                return frame.GetProperty("result").GetProperty("structuredContent");
            }
            var status = await Call("get_status", new StatusV2("2"));
            Check("transport-correct-editor", status.GetProperty("result").GetProperty("editorId").GetGuid() == identity);
            var snapshot = service.CaptureSaveIntent()!.Snapshot;
            var acquired = await Call("acquire_editor", new AcquireEditorV2("2", service.InstanceId, identity, Guid.NewGuid(), snapshot.Revision));
            var lease = acquired.GetProperty("result").GetProperty("leaseId").GetGuid();
            layout.Float(DockShellState.DocumentId);
            Check("transport-lease-blocks-docking", layout.Root.Windows!.Count == 0);
            editor.TextArea.PerformTextInput("FORBIDDEN"); Check("transport-lease-blocks-gui", editor.Text == snapshot.Text);
            var edited = await Call("update_editor", new EditorEditV2("2", service.InstanceId, identity, lease, snapshot.Revision, EditorEditMode.Append, " "));
            Check("transport-edit-original-editor", edited.GetProperty("error").ValueKind == JsonValueKind.Null && editor.Text == snapshot.Text + " ");
            var read = await Call("get_editor", new ReadEditorV2("2", service.InstanceId, identity));
            var state = await Call("get_status", new StatusV2("2"));
            var execution = Call("execute_editor", new EditorExecutionV2("2", service.InstanceId, identity, lease, Guid.NewGuid(),
                read.GetProperty("result").GetProperty("revision").GetString()!, state.GetProperty("result").GetProperty("stateVersion").GetString()!));
            await Wait(() => desktop.Windows.Any(w => w.Title == "Execute editor snapshot"));
            layout.Float(DockShellState.DocumentId);
            Check("transport-execution-confirmation-blocks-docking", layout.Root.Windows.Count == 0 && !session.CanRelocate);
            Click(desktop.Windows.Single(w => w.Title == "Execute editor snapshot"), "Execute");
            var executed = await execution;
            Check("transport-approved-execution", executed.GetProperty("error").ValueKind == JsonValueKind.Null && executed.GetProperty("result").GetProperty("state").GetString() == "completed");
            await Call("release_editor", new ReleaseEditorV2("2", service.InstanceId, identity, lease));
            layout.Float(DockShellState.DocumentId);
            await Wait(() => TopLevel.GetTopLevel(editor) is Window floating && floating != window);
            var lastHost = (Window)TopLevel.GetTopLevel(editor)!;
            using (var pending = service.EditorCoordinator.TryBegin())
            {
                window.Close(); await Task.Delay(150);
                Check("shutdown-waits-for-original-pending-ticket", window.IsVisible && lastHost.IsVisible && service.IsStopping && service.HasRegisteredEditor);
            }
            await Wait(() => !window.IsVisible);
            var info = Container.Resolve<CryptoWorkBenchAvalonia.ViewModels.InfoViewModel>();
            bool infoDisposed = (bool)info.GetType().GetField("disposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(info)!;
            Check("shutdown-no-zombie-registration", !service.HasRegisteredEditor && !lastHost.IsVisible && layout.Factory.HostWindows.Count == 0 && layout.Factory.DockControls.Count == 0 && infoDisposed);
            var revoked = await Send(new { jsonrpc = "2.0", id = ++sequence, method = "tools/call", @params = new { name = "workbench_get_status", arguments = new { contractVersion = "2" } } });
            Check("shutdown-revokes-existing-stdio-client", revoked.TryGetProperty("error", out _));
            bridge.StandardInput.Close(); await bridge.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Check("stdio-eof-exit", bridge.ExitCode == 0 && !(await errors).Contains(token));
            File.WriteAllText(Output + ".stage.json", "{\"stage\":\"complete\"}");
        }
        catch (Exception ex) { failures++; checks.Add(new { name = "probe-exception", passed = false, detail = ex.ToString() }); }
        finally
        {
            if (bridge is { HasExited: false }) { bridge.Kill(entireProcessTree: true); await bridge.WaitForExitAsync(); }
            bridge?.Dispose();
            File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, inputSource = Mode == "editor-input" ? "external native Windows keyboard" : "programmatic routed editor input", passed = checks.Count - failures, failed = failures, checks }, new JsonSerializerOptions { WriteIndented = true }));
            desktop.Shutdown(failures == 0 ? 0 : 1);
        }
    }
}
