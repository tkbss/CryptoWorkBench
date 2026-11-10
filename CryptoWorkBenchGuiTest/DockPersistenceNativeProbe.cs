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
    private async Task RunPersistenceCleanup(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var scenario = File.ReadAllText(Output + ".scenario.txt").Trim();
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
            await Wait("main HWND", () => shell.IsVisible && shell.TryGetPlatformHandle()?.Handle != 0);
            var mainHwnd = shell.TryGetPlatformHandle()!.Handle;
            if (Mode == "cleanup-save")
            {
                layout.Float(DockShellState.DiagnosticsId);
                DockPersistenceCleanupTests.FloatSource(layout, scenario);
                layout.Close(DockShellState.StatusId);
            }
            else if (scenario == "historical") layout.Float(DockShellState.DiagnosticsId);
            await Render();
            var pane = layout.Pane(scenario == "historical" ? DockShellState.InfoId :
                scenario.StartsWith("document", StringComparison.Ordinal) ? DockShellState.DocumentId : DockShellState.VariablesId);
            var sourceRoot = layout.Factory.FindRoot(pane)!;
            var sourceModel = sourceRoot.Window!;
            var sourceHost = (Window)sourceModel.Host!;
            var retainedModel = layout.Factory.FindRoot(layout.Pane(DockShellState.DiagnosticsId))!.Window!;
            var retained = (Window)retainedModel.Host!;
            await Wait("both floating HWNDs", () => sourceHost.IsVisible && retained.IsVisible);
            var sourceHwnd = sourceHost.TryGetPlatformHandle()!.Handle;
            var retainedHwnd = retained.TryGetPlatformHandle()!.Handle;
            Check("independent-native-census", VisibleProcessWindows().Contains(sourceHwnd) && VisibleProcessWindows().Contains(retainedHwnd));
            foreach (var model in new[] { sourceModel, retainedModel })
            {
                var host = (Window)model.Host!;
                var hwnd = host.TryGetPlatformHandle()!.Handle;
                Check("native-owner-" + hwnd, GetWindow(hwnd, 4) == mainHwnd);
                Check("native-top-level-" + hwnd, (GetWindowLongPtr(hwnd, -16).ToInt64() & 0x40000000L) == 0);
                Check("model-owner-" + hwnd, ReferenceEquals(model.Owner, layout.Root) && ReferenceEquals(model.Factory, layout.Factory) &&
                    ReferenceEquals(model.Layout!.Window, model) && ReferenceEquals(((IHostWindow)host).Window, model) && model.ParentWindow == null);
                Check("floating-collapse-roles-" + hwnd, DockShellLayout.VisibleTree(model.Layout!).OfType<IDock>().All(d => d.IsCollapsable));
            }
            Check("main-anchor-roles", DockContainerRoles.MainAnchors(layout.Root).Count() == 3 &&
                DockContainerRoles.ProtectedMainDocks(layout.Root).All(d => !d.IsCollapsable));
            var captured = layout.Capture(DockPersistenceCleanupTests.Bounds);
            Check("closed-tool-restored", DockLayoutStore.Decode(captured).ClosedTools.Contains(DockShellState.StatusId));
            var ids = DockShellLayout.VisibleTree(layout.Root).OfType<IDock>().Select(d => d.Id).ToArray();
            Check("ids-roundtrip", DockShellLayout.VisibleTree(DockLayoutStore.Decode(captured).Root).OfType<IDock>().Select(d => d.Id).SequenceEqual(ids));
            if (Mode == "cleanup-restore" && scenario != "historical")
            {
                var input = DockLayoutStore.Decode(File.ReadAllText(Output + ".input-layout.json"));
                Check("ids-from-previous-process", DockShellLayout.VisibleTree(input.Root).OfType<IDock>().Select(d => d.Id).SequenceEqual(ids));
            }
            if (Mode == "cleanup-restore")
            {
                var editor = session.Editor; var text = editor.Document; var undo = text.UndoStack;
                service.Mutate(() => editor.Text = "// A03 native retained");
                service.Mutate(() => text.Insert(text.TextLength, " edit"));
                var snapshot = service.CaptureSaveIntent()!.Snapshot;
                var generation = service.EditorCoordinator.WorkspaceGeneration;
                var viewModel = session.ViewModel; var output = session.Output; var entries = output.Entries;
                var diagnostics = viewModel.Status.VisibleDiagnostics;
                var panes = DockLayoutStore.PaneIds.Select(layout.Pane).ToArray();
                IDock Target(string id) => DockContainerRoles.MainAnchors(layout.Root).Single(d => d.Id ==
                    (id == DockShellState.DocumentId ? DockShellLayout.Documents : DockShellLayout.Tools));
                if (scenario is "multiple" or "nested")
                {
                    var children = DockShellLayout.VisibleTree(sourceRoot).Where(d => d is IDocument or ITool).ToArray();
                    for (int i = 0; i < children.Length; i++)
                    {
                        var child = children[i];
                        layout.Factory.MoveDockable((IDock)child.Owner!, Target(child.Id), child, null);
                        if (i < children.Length - 1)
                            Check("partial-group-host-preserved-" + i, IsWindow(sourceHwnd) && sourceHost.IsVisible);
                    }
                }
                else Check("accepted-manager-drop", new DockManager(new DockService()).ValidateDockable(
                    sourceRoot.ActiveDockable!, Target(pane.Id), DragAction.Move, DockOperation.Fill, true));
                await Wait("source HWND destroyed", () => !IsWindow(sourceHwnd));
                Check("source-native-destroyed", !IsWindow(sourceHwnd) && !VisibleProcessWindows().Contains(sourceHwnd));
                Check("source-registrations-released", !sourceHost.IsVisible && !layout.Factory.HostWindows.Contains((IHostWindow)sourceHost) &&
                    !layout.Root.Windows!.Contains(sourceModel) && sourceModel.Owner == null && sourceModel.Layout == null);
                Check("retained-host-unchanged", IsWindow(retainedHwnd) && retained.IsVisible && retained.TryGetPlatformHandle()!.Handle == retainedHwnd &&
                    layout.Root.Windows!.Single() == retainedModel && layout.Factory.HostWindows.Single() == retainedModel.Host);
                await Render();
                var normal = layout.Capture(DockPersistenceCleanupTests.Bounds);
                Check("maximize-after-redock", layout.Maximize(pane.Id)); await Render();
                Check("restore-after-redock", layout.Restore()); await Render();
                Check("exact-restored-layout", layout.Capture(DockPersistenceCleanupTests.Bounds) == normal);
                Check("reset-default", layout.Reset()); await Render();
                await Wait("reset destroys retained HWND", () => !IsWindow(retainedHwnd));
                Check("reset-main-anchors", DockContainerRoles.MainAnchors(layout.Root).Count() == 3 && layout.Factory.HostWindows.Count == 0);
                layout.Float(DockShellState.InfoId); await Render();
                var again = (Window)layout.Factory.FindRoot(layout.Pane(DockShellState.InfoId))!.Window!.Host!;
                var againHwnd = again.TryGetPlatformHandle()!.Handle;
                layout.Dock(DockShellState.InfoId);
                await Wait("post-reset redock HWND", () => !IsWindow(againHwnd));
                Check("post-reset-capture", DockLayoutStore.Decode(layout.Capture(DockPersistenceCleanupTests.Bounds)) != null);
                Check("same-business-session", ReferenceEquals(editor, session.Editor) && ReferenceEquals(text, editor.Document) && ReferenceEquals(undo, text.UndoStack) &&
                    service.CaptureSaveIntent()!.Snapshot == snapshot && service.EditorCoordinator.WorkspaceGeneration == generation && ReferenceEquals(session.ViewModel, viewModel));
                Check("same-output-diagnostics", ReferenceEquals(session.Output, output) && ReferenceEquals(output.Entries, entries) && ReferenceEquals(viewModel.Status.VisibleDiagnostics, diagnostics));
                Check("six-original-panes-once", DockLayoutStore.PaneIds.Select(layout.Pane).SequenceEqual(panes) && panes.All(p => DockShellLayout.VisibleTree(layout.Root).Count(d => ReferenceEquals(d, p)) == 1));
                service.Mutate(() => editor.Undo()); Check("undo-preserved", text.Text == "// A03 native retained");
                layout.Float(DockShellState.DocumentId); await Render();
            }
            var shutdownHwnds = layout.Factory.HostWindows.Cast<Window>().Select(w => w.TryGetPlatformHandle()!.Handle).Append(mainHwnd).ToArray();
            shell.Close(); await service.DrainAsync();
            await Wait("normal shutdown native destruction", () => shutdownHwnds.All(h => !IsWindow(h)));
            Check("shutdown-clean", !service.HasRegisteredEditor && layout.Factory.HostWindows.Count == 0 && layout.Factory.DockControls.Count == 0 && VisibleProcessWindows().Count == 0);
            Check("shutdown-saved-schema1", File.Exists(Output + ".layout.json") && DockLayoutStore.Decode(File.ReadAllText(Output + ".layout.json")) != null);
        }
        catch (Exception error) { failed++; checks.Add(new { name = "exception", passed = false, detail = error.ToString() }); }
        File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, scenario, processId = Environment.ProcessId,
            input = "programmatic DockManager/Factory; separate native save/restart processes; NOT mouse gestures",
            passed = checks.Count - failed, failed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        desktop.Shutdown(failed == 0 ? 0 : 1);
    }
}
