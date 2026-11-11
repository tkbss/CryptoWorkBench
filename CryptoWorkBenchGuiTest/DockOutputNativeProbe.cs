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
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task RunOutput(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var checks = new List<object>(); int failures = 0; bool external = Mode == "output-input";
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failures++; }
        static async Task Wait(Func<bool> condition, int seconds = 15)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition() && DateTime.UtcNow < until) await Task.Delay(30);
            if (!condition()) throw new TimeoutException("Output checkpoint did not settle.");
        }
        var window = (DockShellWindow)desktop.MainWindow!;
        var layout = window.Layout; var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>(); var editor = session.Editor;
        async Task Stage(string name, Window top, string instruction, Func<bool> done, Action simulate)
        {
            top.Activate(); top.Title = "G1-05 " + name + " — " + instruction;
            await Task.Delay(150);
            if (name is "return" or "numpad-enter" || name.StartsWith("undo-") || name.StartsWith("redo-")) editor.TextArea.Focus();
            if (external) File.WriteAllText(Output + ".stage.json", JsonSerializer.Serialize(new { stage = name, instruction, windowTitle = top.Title }));
            else simulate();
            await Wait(done, external ? 180 : 15); Check(name, done());
        }
        async Task<Window> Float(string id)
        {
            layout.Float(id); await Task.Delay(180);
            return (Window)layout.Factory.FindRoot(layout.Pane(id))!.Window!.Host!;
        }
        try
        {
            await Wait(() => window.GetVisualDescendants().Any(v => ReferenceEquals(v, editor)));
            await Task.Delay(150);
            window.Width = 1400; window.Height = 900; window.Activate();
            await Wait(() => GetForegroundWindow() == window.TryGetPlatformHandle()!.Handle, 45);
            var document = editor.Document; var undo = document.UndoStack;
            foreach (bool floating in new[] { false, true })
            {
                var top = floating ? await Float(DockShellState.DocumentId) : window;
                editor.Text = floating ? "Print(0x(22))" : "Print(0x(11))";
                Check("fixture-source-" + floating, editor.Text == (floating ? "Print(0x(22))" : "Print(0x(11))"));
                editor.CaretOffset = editor.Text.Length; editor.TextArea.Focus(); int count = session.Output.Entries.Count;
                await Stage(floating ? "numpad-enter" : "return", top, floating ? "press KP_Enter" : "press Return",
                    () => session.Output.Entries.Count == count + 1, () => editor.TextArea.PerformTextInput("\n"));
                await session.View.PendingExecution;
                Check("no-generated-text-" + floating, editor.Text == (floating ? "Print(0x(22))" + Environment.NewLine : "Print(0x(11))" + Environment.NewLine));
                Check("source-position-" + floating, session.Output.Entries.Last() is { SourceLine: 1, SourceColumn: 1 });
                await Stage("undo-" + floating, top, "press Ctrl+Z", () => !editor.Text.EndsWith('\n'), () => ApplicationCommands.Undo.Execute(null, editor.TextArea));
                await Stage("redo-" + floating, top, "press Ctrl+Y", () => editor.Text.EndsWith('\n'), () => ApplicationCommands.Redo.Execute(null, editor.TextArea));
                Check("undo-does-not-reexecute-" + floating, session.Output.Entries.Count == count + 1);
                if (floating) { layout.Dock(DockShellState.DocumentId); await Task.Delay(180); }
            }
            Check("editor-identity", editor.Document == document && document.UndoStack == undo);
            session.Output.Clear();
            editor.Text = "  Print(0x(ABCD))";
            await session.ViewModel.ParseLineAsync(editor.Text);
            var source = session.Output.Entries.Single(); var snapshot = service.CaptureSaveIntent()!.Snapshot;
            long revision = InterpreterHostQueue.StateRevision;
            layout.Show(DockShellState.OutputId); await Task.Delay(180);
            var view = window.GetVisualDescendants().OfType<DockOutputView>().Single(); var model = view.DataContext;
            foreach (bool floating in new[] { false, true })
            {
                var top = floating ? await Float(DockShellState.OutputId) : window;
                var text = view.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "PrintText");
                await top.Clipboard!.SetTextAsync("g105-sentinel");
                bool copied = false;
                var polling = Task.Run(async () =>
                {
                    // Clipboard is UI owned; poll through the existing dispatcher.
                    var until = DateTime.UtcNow.AddSeconds(external ? 180 : 15);
                    while (!copied && DateTime.UtcNow < until)
                    {
                        copied = await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => await top.Clipboard!.TryGetTextAsync() == "out: 0x(ABCD)");
                        await Task.Delay(40);
                    }
                });
                await Stage("copy-" + floating, top, "select full output text with mouse, Ctrl+C", () => copied,
                    () => { text.Focus(); text.SelectionStart = 0; text.SelectionEnd = text.Text!.Length; text.Copy(); });
                await polling;
                Check("copy-exact-" + floating, await top.Clipboard!.TryGetTextAsync() == source.Text);
                Check("same-global-view-" + floating, ReferenceEquals(view.DataContext, model));
                if (floating) { layout.Dock(DockShellState.OutputId); await Task.Delay(180); }
            }
            var navigate = view.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, source.Label));
            await Stage("navigate", window, "click CryptoScript Line 1 Column 3", () => editor.CaretOffset == 2 && editor.IsKeyboardFocusWithin,
                () => navigate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            Check("navigation-no-effects", service.CaptureSaveIntent()!.Snapshot == snapshot && InterpreterHostQueue.StateRevision == revision);
            layout.Close(DockShellState.OutputId); layout.Show(DockShellState.OutputId); await Task.Delay(180);
            Check("close-show-no-duplicates", session.Output.Entries.Count == 1 && view.DataContext == model);
            var clear = view.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Clear Output"));
            await Stage("clear", window, "click Clear Output", () => session.Output.Entries.Count == 0,
                () => clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            Check("clear-no-document-effects", service.CaptureSaveIntent()!.Snapshot == snapshot);
            await session.ViewModel.ParseLineAsync("Print(0x(01)) Print(0x(02))");
            Check("multiple-prints", session.Output.Entries.Count == 2 && session.Output.Entries[1].Sequence == 2);
            var finalHost = await Float(DockShellState.OutputId);
            window.Close(); await Wait(() => !window.IsVisible);
            Check("shutdown-floating-output", !finalHost.IsVisible && desktop.Windows.Count == 0);
            Check("shutdown-releases-output", session.Output.Entries.Count == 0 && !service.HasRegisteredEditor);
            File.WriteAllText(Output + ".stage.json", "{\"stage\":\"complete\"}");
        }
        catch (Exception ex) { failures++; checks.Add(new { name = "probe-exception", passed = false, detail = ex.ToString() }); }
        finally
        {
            File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, inputSource = external ? "external Windows mouse/keyboard at named checkpoints" : "programmatic native UI", passed = checks.Count - failures, failed = failures, checks }, new JsonSerializerOptions { WriteIndented = true }));
            desktop.Shutdown(failures == 0 ? 0 : 1);
        }
    }
}
