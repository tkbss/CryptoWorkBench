using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.VisualTree;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using CryptoWorkBenchAvalonia.ViewModels;
using Dock.Model.Controls;
using Dock.Model.Core;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task RunLayout(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var checks = new List<object>(); int failures = 0;
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failures++; }
        async Task Wait(Func<bool> predicate, int seconds = 15)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(50);
            if (!predicate()) throw new TimeoutException("Layout probe checkpoint: " + desktop.MainWindow!.Title);
        }
        var shell = (DockShellWindow)desktop.MainWindow!;
        var layout = shell.Layout;
        var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        var screens = DockWindowGeometry.Screens(shell);
        try
        {
            await Task.Delay(500); shell.Activate();
            await Wait(() => GetForegroundWindow() == shell.TryGetPlatformHandle()!.Handle, 30);
            Check("native-backend", shell.TryGetPlatformHandle()!.Handle != 0);
            Check("single-editor-registration", session.Started && service.HasRegisteredEditor);
            Check("logical-document-binding", ReferenceEquals(layout.Pane(DockShellState.DocumentId).Context, layout.State.Document));
            Check("tool-context-bindings", layout.State.Tools.All(t => ReferenceEquals(layout.Pane(t.Id).Context, t)));
            Check("variables-singleton", ReferenceEquals(Container.Resolve<VariableViewModel>(), Container.Resolve<VariableViewModel>()));
            if (Mode == "layout-restart")
            {
                var expected = new DockLayoutStore(Output + ".layout.json").Load()!;
                Check("restart-window-state", shell.WindowState == (expected.MainWindow.Maximized ? WindowState.Maximized : WindowState.Normal));
                // Maximized Windows frames may extend beyond the working area. Check visible content
                // there, then require the normal top-left to be reachable after restoring the window.
                Check("restart-main-content-visible", screens.Any(s => s.WorkingArea.Contains(
                    shell.PointToScreen(new Avalonia.Point(shell.ClientSize.Width / 2, shell.ClientSize.Height / 2)))));
                Check("restart-floating-reachable", (layout.Root.Windows ?? []).All(w => w.Host is Window top && screens.Any(s => s.WorkingArea.Contains(top.Position))));
                if (shell.WindowState == WindowState.Maximized)
                {
                    shell.WindowState = WindowState.Normal;
                    await Wait(() => Math.Abs(shell.ClientSize.Width - expected.MainWindow.Width) < 5);
                }
                Check("restart-main-reachable", screens.Any(s => s.WorkingArea.Contains(shell.Position)));
                Check("restart-closed-status", !DockShellLayout.VisibleTree(layout.Root).Contains(layout.Pane(DockShellState.StatusId)));
                Check("restart-floating-info", layout.Factory.FindRoot(layout.Pane(DockShellState.InfoId)) != layout.Root);
                Check("restart-split", Math.Abs(DockShellLayout.VisibleTree(layout.Root, false).Single(p => p.Id == DockShellLayout.Tools).Proportion - .3) < .01);
                Check("restart-main-size", Math.Abs(shell.Width - 1000) < 5 && Math.Abs(shell.Height - 700) < 5);
                Check("source-not-persisted", string.IsNullOrEmpty(session.Editor.Text));
            }
            else
            {
                Check("default-six-panes", DockShellLayout.VisibleTree(layout.Root).Count(p => p is IDocument or ITool) == 6);
                if (Mode == "layout-invalid") Check("invalid-file-default-without-legacy-fallback", DockShellStartupError == null);
            }
            layout.Show(DockShellState.DocumentId); session.Editor.TextArea.Focus();
            await Wait(() => session.Editor.TextArea.IsFocused);
            Check("editor-focus-after-start", session.Editor.TextArea.IsFocused);
            session.Editor.TextArea.PerformTextInput("G1-07 transient editor ä€😀");
            var doc = session.Editor.Document; var undo = doc.UndoStack;
            var id = service.CaptureSaveIntent()!.Snapshot.EditorId;
            var generation = service.EditorCoordinator.WorkspaceGeneration;
            foreach (var paneId in DockLayoutStore.PaneIds)
            {
                layout.Show(paneId);
                var before = layout.Capture(new(0, 0, 1000, 700));
                Check("maximize-" + paneId, layout.Maximize(paneId));
                await Task.Delay(150);
                Check("normal-snapshot-" + paneId, layout.Capture(new(0, 0, 1000, 700)) == before);
                Check("restore-" + paneId, layout.Restore());
                await Task.Delay(150);
                Check("same-tree-" + paneId, layout.Capture(new(0, 0, 1000, 700)) == before);
            }
            Check("same-document-history", ReferenceEquals(session.Editor.Document, doc) && ReferenceEquals(doc.UndoStack, undo));
            Check("same-registration-generation", service.CaptureSaveIntent()!.Snapshot.EditorId == id && service.EditorCoordinator.WorkspaceGeneration == generation);
            Check("reset-default", layout.Reset());
            layout.Float(DockShellState.InfoId);
            await Wait(() => layout.Root.Windows?.SingleOrDefault()?.Host is Window { IsVisible: true });
            var floating = (Window)layout.Root.Windows!.Single().Host!;
            floating.Position = new(160, 180); floating.Width = 500; floating.Height = 420;
            await Task.Delay(200);
            Check("floating-native-handle", floating.TryGetPlatformHandle()!.Handle != shell.TryGetPlatformHandle()!.Handle);
            Check("floating-focus-mode", layout.Maximize(DockShellState.InfoId));
            Check("floating-restore", layout.Restore());
            Check("same-floating-host", ReferenceEquals(floating, layout.Root.Windows!.Single().Host));
            Check("floating-reachable", screens.Any(s => s.WorkingArea.Contains(floating.Position)));
            using (service.EditorCoordinator.TryBegin())
            {
                Check("pending-reject-maximize", !layout.Maximize(DockShellState.DocumentId));
                Check("pending-reject-reset", !layout.Reset());
            }
            if (Mode == "layout-input")
            {
                shell.Activate(); layout.Show(DockShellState.DocumentId);
                shell.Title = "G1-07 INPUT: View > Maximize Active View";
                await Wait(() => layout.IsMaximized, 180);
                Check("external-main-maximize", layout.MaximizedPaneId == DockShellState.DocumentId);
                shell.Title = "G1-07 INPUT: View > Restore Layout";
                await Wait(() => !layout.IsMaximized, 180);
                Check("external-main-restore", !layout.IsMaximized);
                floating.Activate(); floating.Title = "G1-07 INPUT: Layout > Maximize Active View";
                await Wait(() => layout.IsMaximized, 180);
                Check("external-floating-maximize", layout.MaximizedPaneId == DockShellState.InfoId);
                floating.Title = "G1-07 INPUT: Layout > Restore Layout";
                await Wait(() => !layout.IsMaximized, 180);
                Check("external-floating-restore", !layout.IsMaximized);
                shell.Activate(); shell.Title = "G1-07 INPUT: View > Reset to Default Layout";
                await Wait(() => layout.Root.Windows?.Count == 0, 180);
                Check("external-reset", layout.Root.Windows?.Count == 0);
                layout.Float(DockShellState.InfoId);
            }
            shell.Width = 640; shell.Height = 480; await Task.Delay(200);
            Check("small-window-menu-visible", shell.FindControl<Menu>("ShellMenu")!.Bounds.Width > 100);
            shell.Width = 1000; shell.Height = 700; shell.Position = new(80, 90);
            await Wait(() => Math.Abs(shell.ClientSize.Width - 1000) < 1 && Math.Abs(shell.ClientSize.Height - 700) < 1);
            layout.Close(DockShellState.StatusId);
            DockShellLayout.VisibleTree(layout.Root, false).Single(p => p.Id == DockShellLayout.Tools).Proportion = .3;
            DockShellLayout.VisibleTree(layout.Root, false).Single(p => p.Id == DockShellLayout.Documents).Proportion = .48;
            DockShellLayout.VisibleTree(layout.Root, false).Single(p => p.Id == DockShellLayout.Diagnostics).Proportion = .22;
            await Task.Delay(200);
            layout.Maximize(DockShellState.DocumentId);
            var host = layout.Root.Windows!.Single().Host;
            shell.Close(); await Wait(() => !shell.IsVisible);
            Check("shutdown-closes-floating", host is Window { IsVisible: false });
            Check("shutdown-retires-editor", !service.HasRegisteredEditor);
            var store = new DockLayoutStore(Output + ".layout.json");
            var saved = store.Load();
            Check("shutdown-atomic-file", saved != null);
            Check("normal-layout-saved-during-focus", saved != null && DockShellLayout.VisibleTree(saved.Root).Count(p => p is IDocument or ITool) == 5);
            Check("closed-tool-saved", saved?.ClosedTools.SequenceEqual(new[] { DockShellState.StatusId }) == true);
            Check("floating-position-saved", saved?.Root.Windows?.Single().Width == 500 || Mode == "layout-input");
        }
        catch (Exception error) { checks.Add(new { name = "exception", passed = false, error = error.ToString() }); failures++; }
        finally
        {
            if (shell.IsVisible) { shell.Close(); await Task.Delay(300); }
            File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, input = Mode == "layout-input" ? "external checkpoints" : "programmatic",
                screens, passed = checks.Count - failures, failed = failures, checks }, new JsonSerializerOptions { WriteIndented = true }));
            desktop.Shutdown(failures == 0 ? 0 : 1);
        }
    }
}
