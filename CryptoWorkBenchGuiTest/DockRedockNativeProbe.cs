using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model.Controls;
using Dock.Model.Core;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    // Read-only, process-local native census. No input injection and no manipulation of HWNDs.
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, nint state);
    private delegate bool EnumWindowCallback(nint hwnd, nint state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder text, int length);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);

    private static HashSet<nint> VisibleProcessWindows()
    {
        var result = new HashSet<nint>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == Environment.ProcessId && IsWindowVisible(hwnd)) result.Add(hwnd);
            return true;
        }, 0);
        return result;
    }

    private sealed class RedockJournal : IDisposable
    {
        private readonly DockShellLayout layout;
        private readonly DockEditorSession session;
        private readonly IClassicDesktopStyleApplicationLifetime desktop;
        private readonly Func<IHostWindow?> original;
        private readonly List<Window> created = new();
        private readonly DispatcherTimer timer;
        private readonly StreamWriter writer;
        private string? last;
        private bool disposed;
        private static int Identity(object? value) => value is null ? 0 : RuntimeHelpers.GetHashCode(value);
        internal RedockJournal(DockShellLayout layout, DockEditorSession session, IClassicDesktopStyleApplicationLifetime desktop, string path)
        {
            this.layout = layout; this.session = session; this.desktop = desktop;
            writer = new StreamWriter(path) { AutoFlush = true };
            original = layout.Factory.HostWindowLocator![nameof(IDockWindow)];
            layout.Factory.HostWindowLocator[nameof(IDockWindow)] = () =>
            {
                var host = original();
                if (host is Window window)
                {
                    created.Add(window);
                    window.Opened += (_, _) => Record("host-opened");
                    window.Closing += (_, e) => Record("host-closing:" + e.CloseReason + ":cancel=" + e.Cancel);
                    window.Closed += (_, _) => Record("host-closed");
                    Record("host-created");
                }
                return host;
            };
            layout.Factory.WindowAdded += (_, _) => Record("model-window-added");
            layout.Factory.WindowRemoved += (_, _) => Record("model-window-removed");
            layout.Factory.WindowClosing += (_, _) => Record("factory-window-closing");
            layout.Factory.WindowClosed += (_, _) => Record("factory-window-closed");
            layout.Factory.WindowMoveDragBegin += (_, _) => Record("mouse-window-drag-begin");
            layout.Factory.WindowMoveDrag += (_, _) => Record("mouse-window-drag-move");
            layout.Factory.WindowMoveDragEnd += (_, _) => Record("mouse-window-drag-end");
            layout.Factory.DockableRemoved += (_, _) => Record("dockable-removed");
            layout.Factory.DockableMoved += (_, _) => Record("dockable-moved");
            layout.Factory.DockableDocked += (_, _) => Record("dockable-docked");
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            timer.Tick += (_, _) => Record("sample", changedOnly: true); timer.Start();
            Record("journal-start");
        }
        internal void Record(string reason, bool changedOnly = false)
        {
            if (disposed) return;
            var windows = desktop.Windows.Concat(created).Distinct().ToArray();
            var natives = new List<object>();
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out uint pid); if (pid != Environment.ProcessId) return true;
                var title = new StringBuilder(512); var kind = new StringBuilder(128);
                GetWindowText(hwnd, title, title.Capacity); GetClassName(hwnd, kind, kind.Capacity);
                var managed = windows.FirstOrDefault(w => w.TryGetPlatformHandle()?.Handle == hwnd);
                var host = managed as IHostWindow;
                natives.Add(new { hwnd = hwnd.ToInt64(), owner = GetWindow(hwnd, 4).ToInt64(), title = title.ToString(), visible = IsWindowVisible(hwnd), nativeClass = kind.ToString(), extendedStyle = GetWindowLongPtr(hwnd, -20).ToInt64(), managedType = managed?.GetType().FullName, showInTaskbar = managed?.ShowInTaskbar, windowState = managed?.WindowState.ToString(), hostIdentity = Identity(host), modelIdentity = Identity(host?.Window), modelId = host?.Window?.Id, factoryRegistered = host != null && layout.Factory.HostWindows.Contains(host) });
                return true;
            }, 0);
            var snapshot = JsonSerializer.Serialize(new
            {
                nativeWindows = natives,
                hosts = created.Select(w => new { identity = Identity(w), w.IsVisible, hwnd = w.TryGetPlatformHandle()?.Handle.ToInt64(), closed = (w as IHostWindowCloseState)?.IsClosed, model = Identity((w as IHostWindow)?.Window) }).ToArray(),
                floating = (layout.Root.Windows ?? []).Select(w => new { identity = Identity(w), w.Id, host = Identity(w.Host), owner = Identity(w.Owner), layout = Identity(w.Layout), panes = w.Layout is null ? [] : DockShellLayout.VisibleTree(w.Layout).Where(d => d is ITool or IDocument).Select(d => d.Id).ToArray() }).ToArray(),
                registeredHosts = layout.Factory.HostWindows.Select(Identity).ToArray(),
                dockControls = layout.Factory.DockControls.Select(Identity).ToArray(),
                session.CanRelocate, observer = WorkbenchAutomationService.Shared.EditorCoordinator.ObserverDepth,
                currentDockable = layout.Factory.CurrentDockable?.Id,
                disposedLayout = layout.IsDisposed
            });
            if (changedOnly && snapshot == last) return;
            last = snapshot;
            writer.WriteLine(JsonSerializer.Serialize(new { utc = DateTime.UtcNow, reason, state = JsonSerializer.Deserialize<JsonElement>(snapshot) }));
        }
        public void Dispose()
        {
            Record("journal-dispose"); disposed = true; timer.Stop(); writer.Dispose();
            // The probe owns the complete application/factory lifetime. No user singleton is retained.
            if (layout.Factory.HostWindowLocator?.ContainsKey(nameof(IDockWindow)) == true)
                layout.Factory.HostWindowLocator[nameof(IDockWindow)] = original;
        }
    }

    private async Task RunRedock(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var shell = (DockShellWindow)desktop.MainWindow!;
        var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        var layout = shell.Layout;
        using var journal = new RedockJournal(layout, session, desktop, Output + ".windows.jsonl");
        var checks = new List<object>(); int failed = 0;
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failed++; }
        try
        {
            await Task.Delay(300); shell.Activate();
            if (Mode == "redock-manual")
            {
                shell.Title = "G1-N1-F01 MANUAL — mouse float/redock; close main window to finish recording";
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                shell.Closed += (_, _) => closed.TrySetResult();
                await closed.Task; await service.DrainAsync(); journal.Record("manual-main-closed");
                File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, acceptance = "NOT CERTIFIED: owner must inspect journal and confirm M2", input = "manual human mouse only", checks }));
                desktop.Shutdown(); return;
            }
            var before = service.CaptureSaveIntent()!.Snapshot; var document = session.Editor.Document;
            var generation = service.EditorCoordinator.WorkspaceGeneration;
            layout.Float(DockShellState.DiagnosticsId); await Task.Delay(150);
            var legitimate = (Window)layout.Factory.FindRoot(layout.Pane(DockShellState.DiagnosticsId))!.Window!.Host!;
            var legitimateHwnd = legitimate.TryGetPlatformHandle()!.Handle;
            var expectedNativeWindows = VisibleProcessWindows();
            foreach (bool group in new[] { false, true })
            for (int i = 0; i < 3; i++)
            {
                layout.Float(DockShellState.VariablesId); await Task.Delay(150);
                var pane = layout.Pane(DockShellState.VariablesId);
                var model = layout.Factory.FindRoot(pane)!.Window!;
                var host = (Window)model.Host!; var hwnd = host.TryGetPlatformHandle()!.Handle;
                if (group)
                {
                    var output = layout.Pane(DockShellState.OutputId);
                    layout.Factory.MoveDockable((IDock)output.Owner!, (IDock)pane.Owner!, output, null);
                }
                journal.Record($"before-drop-group={group}-cycle={i}");
                Check($"drop-accepted-{group}-{i}", DockMouseRedockTests.Drop(layout, group ? pane.Owner! : pane));
                await Task.Delay(150); journal.Record($"after-drop-group={group}-cycle={i}");
                Check($"native-source-destroyed-{group}-{i}", !IsWindow(hwnd) && !host.IsVisible);
                Check($"complete-native-window-census-{group}-{i}", VisibleProcessWindows().SetEquals(expectedNativeWindows));
                Check($"source-registrations-removed-{group}-{i}", !layout.Root.Windows!.Contains(model) && !layout.Factory.HostWindows.Contains((IHostWindow)host));
                Check($"other-window-retained-{group}-{i}", IsWindow(legitimateHwnd) && legitimate.IsVisible && layout.Factory.FindRoot(layout.Pane(DockShellState.DiagnosticsId)) != layout.Root);
                Check($"one-original-editor-{group}-{i}", ReferenceEquals(session.Editor.Document, document) && service.CaptureSaveIntent()!.Snapshot == before && service.EditorCoordinator.WorkspaceGeneration == generation);
            }
            Check("only-one-legitimate-floating-host", layout.Root.Windows!.Count == 1 && layout.Factory.HostWindows.Count == 1 && layout.Factory.DockControls.Count == 2);
            layout.Dock(DockShellState.DiagnosticsId); await Task.Delay(150);
            foreach (var scenario in new[] { "single", "group", "nested", "document-tab", "document-anchor" })
            {
                var retainedId = scenario == "group" ? DockShellState.DocumentId : DockShellState.DiagnosticsId;
                layout.Float(retainedId); await Task.Delay(150);
                var retained = (Window)layout.Factory.FindRoot(layout.Pane(retainedId))!.Window!.Host!;
                var retainedHwnd = retained.TryGetPlatformHandle()!.Handle;
                var expected = VisibleProcessWindows();
                foreach (var operation in new[] { DockOperation.Fill, DockOperation.Right, DockOperation.Bottom })
                {
                    var source = DockGroupDocumentRedockTests.FloatScenario(layout, scenario);
                    await Task.Delay(150);
                    var model = layout.Factory.FindRoot(source)!.Window!;
                    var host = (Window)model.Host!; var hwnd = host.TryGetPlatformHandle()!.Handle;
                    journal.Record($"F03-before-{scenario}-{operation}");
                    Check($"F03-drop-{scenario}-{operation}", new Dock.Model.DockManager(new Dock.Model.DockService()).ValidateDockable(source, DockGroupDocumentRedockTests.Target(layout, scenario), DragAction.Move, operation, true));
                    await Task.Delay(150); journal.Record($"F03-after-{scenario}-{operation}");
                    Check($"F03-native-source-destroyed-{scenario}-{operation}", !IsWindow(hwnd) && !host.IsVisible);
                    Check($"F03-complete-native-census-{scenario}-{operation}", VisibleProcessWindows().SetEquals(expected));
                    Check($"F03-source-model-removed-{scenario}-{operation}", !layout.Root.Windows!.Contains(model) && !layout.Factory.HostWindows.Contains((IHostWindow)host));
                    Check($"F03-other-host-preserved-{scenario}-{operation}", IsWindow(retainedHwnd) && retained.IsVisible);
                    Check($"F03-editor-identity-{scenario}-{operation}", ReferenceEquals(session.Editor.Document, document) && service.CaptureSaveIntent()!.Snapshot == before && service.EditorCoordinator.WorkspaceGeneration == generation);
                }
                layout.Dock(retainedId); await Task.Delay(150);
                Check($"F03-retained-host-return-{scenario}", !IsWindow(retainedHwnd));
            }
            layout.Float(DockShellState.DocumentId); await Task.Delay(150);
            var shutdownHwnd = ((Window)layout.Factory.FindRoot(layout.Pane(DockShellState.DocumentId))!.Window!.Host!).TryGetPlatformHandle()!.Handle;
            journal.Record("before-main-shutdown"); shell.Close(); await service.DrainAsync(); await Task.Delay(150);
            journal.Record("after-main-shutdown");
            Check("all-hosts-and-controls-released", layout.Factory.HostWindows.Count == 0 && layout.Factory.DockControls.Count == 0 && !service.HasRegisteredEditor && !IsWindow(legitimateHwnd));
            Check("F03-native-document-destroyed-on-shutdown", !IsWindow(shutdownHwnd) && VisibleProcessWindows().Count == 0);
        }
        catch (Exception error) { checks.Add(new { name = "exception", passed = false, detail = error.ToString() }); failed++; }
        File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, input = "programmatic DockManager entry used after mouse hit-testing; NOT a mouse-redock acceptance", passed = checks.Count - failed, failed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        desktop.Shutdown(failed == 0 ? 0 : 1);
    }
}
