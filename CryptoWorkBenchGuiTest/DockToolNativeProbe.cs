using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;
using Markdown.Avalonia;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task RunTools(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var checks = new List<object>(); int failures = 0;
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failures++; }
        static async Task Wait(Func<bool> condition, int seconds = 10)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition() && DateTime.UtcNow < until) await Task.Delay(30);
            if (!condition()) throw new TimeoutException("Native tool checkpoint did not settle.");
        }
        static int Subscribers(object source, string field) => ((Delegate?)source.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source))?.GetInvocationList().Length ?? 0;
        var window = (DockShellWindow)desktop.MainWindow!;
        var layout = window.Layout;
        var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        var variables = Container.Resolve<VariableViewModel>();
        var info = Container.Resolve<InfoViewModel>();
        var history = Container.Resolve<IHistoryService>();
        bool external = Mode == "tools-input";
        async Task Stage(string stage, Window top, string instruction, Func<bool> complete, Action simulated)
        {
            top.Title = "G1-04 " + stage + " — " + instruction; top.Activate();
            if (external)
            {
                File.WriteAllText(Output + ".stage.json", JsonSerializer.Serialize(new { stage, instruction, windowTitle = top.Title }));
                await Wait(complete, 180);
            }
            else { simulated(); await Wait(complete); }
            Check(stage, complete());
        }
        async Task CopyStage(string stage, Window top, string expected, Action simulated, bool prose = false)
        {
            await top.Clipboard!.SetTextAsync("G1-04 clipboard sentinel");
            top.Title = "G1-04 " + stage + (stage.StartsWith("copy-example") ? " — hover code and click Copy" : " — select text with mouse, Ctrl+C"); top.Activate();
            if (external) File.WriteAllText(Output + ".stage.json", JsonSerializer.Serialize(new { stage, expected, windowTitle = top.Title }));
            else simulated();
            string? actual = null; var until = DateTime.UtcNow.AddSeconds(external ? 180 : 10);
            while (DateTime.UtcNow < until)
            {
                actual = await top.Clipboard!.TryGetTextAsync();
                if ((prose ? actual?.TrimEnd('\r', '\n') : actual) == expected) break;
                await Task.Delay(50);
            }
            bool passed = (prose ? actual?.TrimEnd('\r', '\n') : actual) == expected;
            checks.Add(new { name = stage, passed, expected, actual, input = external ? "external mouse/keyboard at checkpoint" : "programmatic copy command" });
            if (!passed) { failures++; throw new TimeoutException("Clipboard checkpoint: " + stage); }
        }
        async Task<Window> Float(string id)
        {
            layout.Float(id); await Task.Delay(150);
            var host = (Window)layout.Factory.FindRoot(layout.Pane(id))!.Window!.Host!;
            host.Width = 850; host.Height = 700; host.Position = new Avalonia.PixelPoint(150, 100);
            host.Activate(); await Task.Delay(150); return host;
        }
        try
        {
            window.Width = 1400; window.Height = 850;
            await session.ViewModel.ParseLineAsync("VAR nativeValue=0x(AABB) PARAM p=Parameters(SECRET_SHAMIR,#PARTS:2,#MINPARTS:2) Info(HASH-SHA256)");
            layout.Show(DockShellState.VariablesId); await Task.Delay(200);
            var view = window.GetVisualDescendants().OfType<DockVariablesView>().Single();
            Check("variables-prism-singleton", ReferenceEquals(view.DataContext, variables));
            Check("variables-gui-snapshot", variables.DataVariables.Any(v => v.Identifier == "nativeValue" && v.Value == "0x(AABB)"));
            Check("editor-info-prism-identity", ReferenceEquals(typeof(CryptoScriptEditViewModel).GetField("_infoViewModel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session.ViewModel), info));
            Check("dynamic-info-result", info.InfoText.Contains("HASH"));
            variables.SelectedVariable = variables.DataVariables.Single(v => v.Identifier == "nativeValue");
            await Task.Delay(100);
            var snapshot = service.CaptureSaveIntent()!.Snapshot;
            var revision = InterpreterHostQueue.StateRevision;
            var generation = service.EditorCoordinator.WorkspaceGeneration;
            var document = session.Editor.Document; var undo = document.UndoStack;
            int listeners = Subscribers(history, "InfoHistoryChanged");
            SelectableTextBlock Value(string name) => view.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == name);
            static void Copy(SelectableTextBlock text) { text.Focus(); text.SelectionStart = 0; text.SelectionEnd = text.Text!.Length; text.Copy(); }
            await CopyStage("variables-docked-identifier", window, "nativeValue", () => Copy(Value("SelectedIdentifier")));
            var variableHost = await Float(DockShellState.VariablesId);
            Check("variables-native-owner", GetWindow(variableHost.TryGetPlatformHandle()!.Handle, 4) == window.TryGetPlatformHandle()!.Handle);
            Check("variables-same-floating-view", ReferenceEquals(TopLevel.GetTopLevel(view), variableHost));
            await CopyStage("variables-floating-value", variableHost, "0x(AABB)", () => Copy(Value("SelectedValue")));
            Check("variables-readonly-controls", view.GetVisualDescendants().OfType<DataGrid>().All(g => g.IsReadOnly && g.Columns.All(c => c.IsReadOnly)) && !view.GetVisualDescendants().OfType<TextBox>().Any(t => !t.IsReadOnly));
            variables.SelectedVariable = variables.DataVariables.Single(v => v.Identifier == "p"); await Task.Delay(100);
            var parameterGrid = view.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "ParametersGrid");
            var parameter = parameterGrid.GetVisualDescendants().OfType<SelectableTextBlock>().First(t => t.Text == "2");
            Copy(parameter); await Task.Delay(100);
            Check("parameter-value-copy-readonly", await variableHost.Clipboard!.TryGetTextAsync() == "2" && parameterGrid.IsReadOnly);
            layout.Dock(DockShellState.VariablesId); await Task.Delay(100);
            Check("variables-redock-cleanup", !variableHost.IsVisible && ReferenceEquals(TopLevel.GetTopLevel(view), window));
            layout.Close(DockShellState.VariablesId); layout.Show(DockShellState.VariablesId); await Task.Delay(100);
            Check("variables-close-show-identity", window.GetVisualDescendants().OfType<DockVariablesView>().Single() == view && variables.SelectedVariable.Identifier == "p");

            const string prose = "Native selection ä€😀";
            info.SetInfoText(prose); layout.Show(DockShellState.InfoId); await Task.Delay(100);
            var infoView = window.GetVisualDescendants().OfType<InfoView>().Single();
            var markdown = infoView.FindControl<MarkdownScrollViewer>("InfoMarkdownViewer")!;
            Check("info-prism-singleton-selection-enabled", ReferenceEquals(infoView.DataContext, info) && markdown.SelectionEnabled);
            // Real mouse selection is deliberately separate from the programmatic native run.
            if (external) await CopyStage("info-docked-prose", window, prose, () => { }, prose: true);
            var infoHost = await Float(DockShellState.InfoId);
            Check("info-native-owner-and-identity", GetWindow(infoHost.TryGetPlatformHandle()!.Handle, 4) == window.TryGetPlatformHandle()!.Handle && TopLevel.GetTopLevel(infoView) == infoHost);
            if (external) await CopyStage("info-floating-prose", infoHost, prose, () => { }, prose: true);
            const string example = "VAR hex=0x(AABB)\n\nVAR b64=0b64(QUI=)\nPrint(\"ä€😀\")\n";
            foreach (bool floating in new[] { true, false })
            {
                if (!floating) { layout.Dock(DockShellState.InfoId); await Task.Delay(100); }
                var top = floating ? infoHost : window;
                info.SetInfoText("```cryptoscript\n" + example + "\n```\n"); await Task.Delay(150);
                var code = infoView.GetVisualDescendants().OfType<TextEditor>().Single();
                Check("example-exact-" + floating, code.Text == example && code.IsReadOnly);
                // CodePad attaches its expandable menu only while the pointer is inside.
                // Resolve the existing button without fabricating a pointer event in programmatic mode.
                var pad = code.GetVisualAncestors().Single(c => c.GetType().Name == "CodePad");
                var button = (Button)pad.GetType().GetProperty("ExandableMenu")!.GetValue(pad)!;
                bool clicked = false; button.Click += (_, _) => clicked = true;
                await CopyStage("copy-example-" + (floating ? "floating" : "docked"), top, example, () => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                Check("copy-example-button-clicked-" + floating, clicked);
            }
            Check("info-redock-cleanup", !infoHost.IsVisible && TopLevel.GetTopLevel(infoView) == window);
            const string links = "[AES-CBC](cryptoscript-info://mechanism/AES-CBC)";
            info.SetInfoText(links); await Task.Delay(100);
            await Stage("info-link", window, "click AES-CBC link", () => info.InfoText.Contains("# MECHANISM AES-CBC"),
                () => ((Markdown.Avalonia.Markdown)markdown.Engine).HyperlinkCommand!.Execute("cryptoscript-info://mechanism/AES-CBC"));
            await Stage("info-back", window, "click Back", () => info.InfoText == links, () => info.NavigateBackCommand.Execute());
            layout.Close(DockShellState.InfoId); layout.Show(DockShellState.InfoId); await Task.Delay(100);
            Check("info-close-show-identity-history", window.GetVisualDescendants().OfType<InfoView>().Single() == infoView && info.InfoText == links && Subscribers(history, "InfoHistoryChanged") == listeners);
            markdown.Focus();
            await Stage("focus-editor", window, "click CryptoScript editor", () => session.Editor.IsKeyboardFocusWithin, () => session.Editor.TextArea.Focus());
            Check("tool-copy-layout-no-editor-or-interpreter-change", service.CaptureSaveIntent()!.Snapshot == snapshot && InterpreterHostQueue.StateRevision == revision && service.EditorCoordinator.WorkspaceGeneration == generation && session.Editor.Document == document && document.UndoStack == undo);

            // Exercise the production MCP service/publication path. TLS/STDIO is separately
            // covered by the unchanged G1-03 editor modes in the same native runner.
            session.LoadText(service.CaptureSaveIntent()!, "VAR nativeMcp=0x(CAFE) Info(HMAC-SHA256)");
            var previousInfo = info.InfoText;
            service.ActivateMcp();
            var caller = service.IssueCredential("g104-native", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
            var current = service.CaptureSaveIntent()!.Snapshot;
            var lease = (await service.AcquireEditorAsync(caller, new("2", service.InstanceId, current.EditorId, Guid.NewGuid(), current.Revision), default)).Result!;
            service.ExecutionConfirmation = (_, _, _) => Task.FromResult<bool?>(true);
            var status = (await service.GetStatusV2Async(caller, new("2"), default)).Result!;
            var result = await service.ExecuteEditorV2Async(caller, new("2", service.InstanceId, current.EditorId, lease.LeaseId, Guid.NewGuid(), current.Revision, status.StateVersion), default);
            Check("mcp-v2-output-boundary", result.Error == null && result.Result!.State == ExecutionState.Completed && info.InfoText == previousInfo);
            variables.RefreshCommand.Execute(); layout.Float(DockShellState.VariablesId); layout.Float(DockShellState.InfoId);
            Check("lease-blocks-refresh-and-tool-relocation", variables.IsSnapshotStale && layout.Root.Windows!.Count == 0);
            await service.ReleaseEditorAsync(caller, new("2", service.InstanceId, current.EditorId, lease.LeaseId), default);
            variables.RefreshCommand.Execute(); Check("post-lease-refresh-same-global-state", !variables.IsSnapshotStale && variables.DataVariables.Any(v => v.Identifier == "nativeMcp" && v.Value == "0x(CAFE)"));
            var finalVariables = await Float(DockShellState.VariablesId); var finalInfo = await Float(DockShellState.InfoId);
            window.Close(); await Wait(() => !window.IsVisible);
            Check("shutdown-floating-tools", !finalVariables.IsVisible && !finalInfo.IsVisible && desktop.Windows.Count == 0);
            Check("shutdown-registration-and-hosts", !service.HasRegisteredEditor && layout.Factory.HostWindows.Count == 0 && layout.Factory.DockControls.Count == 0);
            Check("shutdown-global-subscriptions", Subscribers(history, "InfoHistoryChanged") == 0 && Subscribers(service.EditorCoordinator, "WorkspaceChanged") == 0);
            File.WriteAllText(Output + ".stage.json", "{\"stage\":\"complete\"}");
        }
        catch (Exception ex) { failures++; checks.Add(new { name = "probe-exception", passed = false, detail = ex.ToString() }); }
        finally
        {
            File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, inputSource = external ? "external native Windows mouse and keyboard at named checkpoints" : "programmatic native UI", mcpTransport = "service API; TLS/STDIO covered by G1-03 modes", passed = checks.Count - failures, failed = failures, checks }, new JsonSerializerOptions { WriteIndented = true }));
            desktop.Shutdown(failures == 0 ? 0 : 1);
        }
    }
}
