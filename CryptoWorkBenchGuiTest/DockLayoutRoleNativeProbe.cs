using System.ComponentModel;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task RunLayoutRoles(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var shell = (DockShellWindow)desktop.MainWindow!;
        var layout = shell.Layout;
        var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        var checks = new List<object>(); int failed = 0;
        using var journal = new RedockJournal(layout, session, desktop, Output + ".windows.jsonl");
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failed++; }
        async Task Wait(string checkpoint, Func<bool> condition)
        {
            var end = DateTime.UtcNow.AddSeconds(15);
            while (!condition() && DateTime.UtcNow < end) await Task.Delay(20);
            if (!condition()) throw new TimeoutException(checkpoint);
        }
        async Task Render()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            foreach (var window in desktop.Windows) window.UpdateLayout();
        }
        try
        {
            await Wait("main shown", () => shell.IsVisible && shell.TryGetPlatformHandle()?.Handle != 0);
            var editor = session.Editor; var text = editor.Document; var undo = text.UndoStack;
            service.Mutate(() => editor.Text = "// A02 native retained work");
            service.Mutate(() => text.Insert(text.TextLength, " edited"));
            var snapshot = service.CaptureSaveIntent()!.Snapshot;
            var generation = service.EditorCoordinator.WorkspaceGeneration;
            layout.Float(DockShellState.DiagnosticsId);
            var retained = (Window)layout.Factory.FindRoot(layout.Pane(DockShellState.DiagnosticsId))!.Window!.Host!;
            await Wait("other floating host", () => retained.IsVisible);
            var retainedHwnd = retained.TryGetPlatformHandle()!.Handle;
            foreach (var scenario in new[] { "tab", "group", "anchor" })
            foreach (var operation in new[] { DockOperation.Fill, DockOperation.Left, DockOperation.Right, DockOperation.Top, DockOperation.Bottom })
            {
                var pane = layout.Pane(DockShellState.DocumentId);
                if (scenario == "anchor") { layout.Dock(pane.Id); layout.Factory.FloatDockable((IDock)pane.Owner!); }
                else layout.Float(pane.Id);
                var source = scenario == "tab" ? pane : pane.Owner!;
                var window = layout.Factory.FindRoot(pane)!.Window!;
                var host = (Window)window.Host!;
                await Wait("source host", () => host.IsVisible);
                var hwnd = host.TryGetPlatformHandle()!.Handle;
                var target = DockShellLayout.VisibleTree(layout.Root, false).OfType<IDocumentDock>().Single(d => d.Id == DockShellLayout.Documents);
                var key = scenario + "-" + operation;
                Check(key + "-drop", new DockManager(new DockService()).ValidateDockable(source, target, DragAction.Move, operation, true));
                await Wait(key + " host destroyed", () => !IsWindow(hwnd));
                Check(key + "-source-released", !host.IsVisible && !layout.Factory.HostWindows.Contains((IHostWindow)host) && !layout.Root.Windows!.Contains(window));
                Check(key + "-other-hwnd", IsWindow(retainedHwnd) && retained.IsVisible);
                layout.Show(pane.Id); shell.Activate();
                await Wait("main active", () => GetForegroundWindow() == shell.TryGetPlatformHandle()!.Handle);
                await Wait("editor attached", () => TopLevel.GetTopLevel(session.View) == shell);
                await Render();
                var normal = layout.Capture(DockLayoutRoleTests.Bounds);
                Check(key + "-capture", DockLayoutStore.Decode(normal) != null);
                foreach (var entry in new[] { "main", "pane" })
                {
                    DockLayoutRoleTests.Click(shell, session.View, entry, "Maximize Active View");
                    Check(key + "-" + entry + "-maximize", layout.IsMaximized && layout.LastLayoutResult!.Outcome == DockLayoutOutcome.Completed);
                    await Render();
                    await Wait("focused editor attached", () => TopLevel.GetTopLevel(session.View) == shell);
                    DockLayoutRoleTests.Click(shell, session.View, entry, "Restore Layout");
                    Check(key + "-" + entry + "-restore", !layout.IsMaximized && layout.LastLayoutResult!.Outcome == DockLayoutOutcome.Completed);
                    await Render();
                    await Wait("restored editor attached", () => TopLevel.GetTopLevel(session.View) == shell);
                    Check(key + "-" + entry + "-normal-tree", layout.Capture(DockLayoutRoleTests.Bounds) == normal);
                }
                Check(key + "-business-identity", ReferenceEquals(editor, session.Editor) && ReferenceEquals(text, editor.Document) && ReferenceEquals(undo, text.UndoStack) &&
                    service.CaptureSaveIntent()!.Snapshot == snapshot && service.EditorCoordinator.WorkspaceGeneration == generation);
            }
            // A tool group, a document host and an independent tool host remain open.
            layout.Factory.FloatDockable((IDock)layout.Pane(DockShellState.VariablesId).Owner!);
            layout.Float(DockShellState.DocumentId);
            layout.Close(DockShellState.StatusId);
            Check("closed-status-capture", DockLayoutStore.Decode(layout.Capture(DockLayoutRoleTests.Bounds)).ClosedTools.Contains(DockShellState.StatusId));
            layout.Show(DockShellState.StatusId);
            foreach (var id in DockLayoutStore.PaneIds)
            {
                layout.Show(id); await Render();
                var hosts = layout.Factory.HostWindows.Cast<Window>().ToArray();
                var hwnds = hosts.Select(h => h.TryGetPlatformHandle()!.Handle).ToArray();
                var normal = layout.Capture(DockLayoutRoleTests.Bounds);
                Check("complex-maximize-" + id, layout.ExecuteLayout(DockLayoutCommand.Maximize, id).Outcome == DockLayoutOutcome.Completed);
                await Render();
                Check("complex-restore-" + id, layout.ExecuteLayout(DockLayoutCommand.Restore).Outcome == DockLayoutOutcome.Completed);
                await Render();
                Check("complex-preserved-" + id, layout.Capture(DockLayoutRoleTests.Bounds) == normal && hwnds.All(IsWindow) && hosts.All(h => h.IsVisible));
            }
            // Native pane menu on the floating document: original failure signature is
            // controlled at the preflight boundary, with no implicit repair/reset.
            layout.Show(DockShellState.DocumentId); await Render();
            var tools = DockShellLayout.VisibleTree(layout.Root, false).OfType<IToolDock>().Single(d => d.Id == DockShellLayout.Tools);
            var prior = tools.Id; tools.Id = DockShellLayout.Diagnostics;
            try
            {
                DockLayoutRoleTests.Click(shell, session.View, "pane", "Maximize Active View");
                Check("controlled-error-rejected", layout.LastLayoutResult!.Outcome == DockLayoutOutcome.Rejected && !layout.IsMaximized);
                Check("controlled-error-process-alive", IsWindow(shell.TryGetPlatformHandle()!.Handle) && service.HasRegisteredEditor);
            }
            finally { tools.Id = prior; }
            var dock = (IDock)layout.Pane(DockShellState.DocumentId).Owner!;
            int calls = 0;
            PropertyChangedEventHandler fail = (_, e) => { if (e.PropertyName == nameof(IDock.VisibleDockables) && calls++ == 0) throw new InvalidOperationException("A02 native focus fault"); };
            ((INotifyPropertyChanged)dock).PropertyChanged += fail;
            try { Check("partial-failure-recovered", layout.ExecuteLayout(DockLayoutCommand.Maximize, DockShellState.DocumentId).Outcome == DockLayoutOutcome.Recovered); }
            finally { ((INotifyPropertyChanged)dock).PropertyChanged -= fail; }
            Check("after-error-still-usable", layout.Maximize(DockShellState.DocumentId) && layout.Restore() && DockLayoutStore.Decode(layout.Capture(DockLayoutRoleTests.Bounds)) != null);
            Check("same-work-after-errors", ReferenceEquals(text, session.Editor.Document) && service.CaptureSaveIntent()!.Snapshot == snapshot);
            await Render();
            foreach (var entry in new[] { "pane", "main" })
            {
                DockLayoutRoleTests.Click(shell, session.View, entry, "Reset to Default Layout");
                Check("reset-menu-" + entry, layout.LastLayoutResult!.Outcome == DockLayoutOutcome.Completed);
                await Render();
                await Wait("reset editor attached", () => TopLevel.GetTopLevel(session.View) == shell);
                Check("reset-same-editor-" + entry, ReferenceEquals(text, session.Editor.Document) && service.CaptureSaveIntent()!.Snapshot == snapshot && layout.Factory.HostWindows.Count == 0);
            }
            layout.Float(DockShellState.DocumentId); await Render();
            var closingHwnds = layout.Factory.HostWindows.Cast<Window>().Select(h => h.TryGetPlatformHandle()!.Handle).Append(shell.TryGetPlatformHandle()!.Handle).ToArray();
            shell.Close(); await service.DrainAsync();
            await Wait("shutdown native windows", () => closingHwnds.All(h => !IsWindow(h)));
            Check("shutdown-releases-all", !service.HasRegisteredEditor && layout.Factory.HostWindows.Count == 0 && layout.Factory.DockControls.Count == 0 && VisibleProcessWindows().Count == 0);
        }
        catch (Exception error) { failed++; checks.Add(new { name = "exception", passed = false, detail = error.ToString() }); }
        File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, input = "programmatic DockManager plus routed menu Click events; NOT actual mouse gestures", passed = checks.Count - failed, failed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        desktop.Shutdown(failed == 0 ? 0 : 1);
    }
}
