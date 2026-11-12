using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CryptoWorkBenchAvalonia.Shell;
using CryptoWorkBenchAvalonia.Services.Automation;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task RunIndependentWindows(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var shell = (DockShellWindow)desktop.MainWindow!;
        var layout = shell.Layout;
        var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        var checks = new List<object>(); var observations = new List<object>(); int failed = 0;
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failed++; }
        void Save() => File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, failed, checks, observations }, new JsonSerializerOptions { WriteIndented = true }));
        object[] Snapshot() => desktop.Windows.Select(w => new
        {
            w.Title, hwnd = w.TryGetPlatformHandle()?.Handle.ToInt64(),
            owner = w.TryGetPlatformHandle() is { } p ? GetWindow(p.Handle, 4).ToInt64() : 0,
            state = w.WindowState.ToString(), w.IsVisible, w.IsActive, w.ShowInTaskbar,
            x = w.Position.X, y = w.Position.Y, width = w.ClientSize.Width, height = w.ClientSize.Height,
            decorations = w.WindowDecorations.ToString()
        }).Cast<object>().ToArray();
        try
        {
            await Task.Delay(250);
            if (Mode == "independent-restart")
                Check("startup-restores-three-hosts", layout.Factory.HostWindows.Count == 3);
            else foreach (var id in DockIndependentWindowTests.Panes) layout.Float(id);
            await PrepareVisualExample();
            var hosts = DockIndependentWindowTests.Panes.Select(id => (Window)layout.Factory.FindRoot(layout.Pane(id))!.Window!.Host!).ToArray();
            Window[] windows = [shell, .. hosts];
            if (Mode != "independent-restart")
            {
                for (int i = 0; i < hosts.Length; i++)
                { hosts[i].Width = 620; hosts[i].Height = 450; hosts[i].Position = new PixelPoint(150 + i * 700, 180 + i * 100); }
            }
            await Task.Delay(300);
            foreach (var w in hosts)
            {
                var hwnd = w.TryGetPlatformHandle()!.Handle;
                long style = GetWindowLongPtr(hwnd, -16).ToInt64(), ex = GetWindowLongPtr(hwnd, -20).ToInt64();
                Check(w.Title + "-native-unowned", GetWindow(hwnd, 4) == 0 && w.Owner == null);
                Check(w.Title + "-caption-and-min-max", (style & 0x00C00000) == 0x00C00000 && (style & 0x00030000) == 0x00030000);
                Check(w.Title + "-taskbar-eligible", w.ShowInTaskbar && (ex & 0x80) == 0);
                Check(w.Title + "-full-chrome", DockNativeChromeTests.HasNativeChrome(w));
            }
            Check("recognizable-window-titles", hosts.Select(w => w.Title).Order().SequenceEqual(new[] { "CryptoScript", "Info", "Variables" }));
            observations.Add(new { stage = "initial", windows = Snapshot() });
            if (Mode == "independent-review")
            {
                // Observe real user/Computer Use actions without driving native input.
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                string previous = "";
                timer.Tick += (_, _) =>
                {
                    var snapshot = Snapshot(); var json = JsonSerializer.Serialize(snapshot);
                    if (json == previous) return;
                    previous = json; observations.Add(new { stage = "interactive", windows = snapshot }); Save();
                };
                shell.Closed += (_, _) => { timer.Stop(); Check("interactive-close-no-hosts", layout.Factory.HostWindows.Count == 0 && hosts.All(w => !w.IsVisible)); Save(); };
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                timer.Start(); Save(); return;
            }
            var editor = session.Editor; var document = editor.Document; var undo = document.UndoStack;
            // Windows restricts background processes from stealing foreground.
            // Require one real activation before testing in-process activation.
            var input = new TaskCompletionSource();
            void Begin(object? sender, Avalonia.Input.KeyEventArgs e)
            { if (e.Key == Avalonia.Input.Key.F8) { e.Handled = true; input.TrySetResult(); } }
            shell.KeyDown += Begin;
            File.WriteAllText(Output + ".ready", "Press F8 in the main probe window to begin.");
            try { await input.Task.WaitAsync(TimeSpan.FromSeconds(90)); }
            finally { shell.KeyDown -= Begin; }
            bool HasForeground() => windows.Any(w => w.TryGetPlatformHandle()?.Handle == GetForegroundWindow());
            Check("initial-user-activation", HasForeground());
            foreach (var selected in windows)
            {
                foreach (var state in new[] { WindowState.Minimized, WindowState.Normal, WindowState.Maximized, WindowState.Normal })
                {
                    selected.WindowState = state; await Task.Delay(180);
                    Check(selected.Title + "-" + state + "-independent", selected.WindowState == state && windows.Where(w => w != selected).All(w => w.WindowState == WindowState.Normal && w.IsVisible));
                }
                selected.Activate(); await Task.Delay(180);
                observations.Add(new { stage = selected.Title + "-activated", windows = Snapshot(), foreground = GetForegroundWindow().ToInt64() });
                Check(selected.Title + "-foreground", GetForegroundWindow() == selected.TryGetPlatformHandle()!.Handle);
                Check(selected.Title + "-activation-keeps-others", windows.Where(w => w != selected).All(w => w.WindowState == WindowState.Normal));
            }
            hosts[1].WindowState = WindowState.Minimized; await Task.Delay(100);
            layout.Show(DockShellState.VariablesId); await Task.Delay(200);
            observations.Add(new { stage = "show-variables", windows = Snapshot(), foreground = GetForegroundWindow().ToInt64() });
            Check("show-activates-only-variables", GetForegroundWindow() == hosts[1].TryGetPlatformHandle()!.Handle && hosts[1].WindowState != WindowState.Minimized && hosts[0].WindowState == WindowState.Normal && hosts[2].WindowState == WindowState.Normal);
            foreach (var id in DockIndependentWindowTests.Panes)
            {
                var old = (Window)layout.Factory.FindRoot(layout.Pane(id))!.Window!.Host!;
                var hwnd = old.TryGetPlatformHandle()!.Handle;
                layout.Dock(id); await Task.Delay(150);
                Check(id + "-command-redock-destroys-host", !old.IsVisible && !IsWindow(hwnd));
            }
            Check("redock-preserves-editor", ReferenceEquals(editor, session.Editor) && ReferenceEquals(document, editor.Document) && ReferenceEquals(undo, document.UndoStack));
            foreach (var id in DockIndependentWindowTests.Panes) layout.Float(id);
            Check("reset", layout.Reset()); await Task.Delay(150);
            Check("reset-no-hosts", layout.Factory.HostWindows.Count == 0);
            foreach (var id in DockIndependentWindowTests.Panes) layout.Float(id);
            await Task.Delay(200);
            observations.Add(new { stage = "before-close", windows = Snapshot() });
            var handles = desktop.Windows.Select(w => w.TryGetPlatformHandle()!.Handle).ToArray();
            shell.Close();
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (shell.IsVisible && DateTime.UtcNow < deadline) await Task.Delay(50);
            await Task.Delay(150);
            Check("main-close-destroys-all-hwnds", handles.All(h => !IsWindow(h)));
            Check("shutdown-releases-hosts-and-editor", layout.IsDisposed && layout.Factory.HostWindows.Count == 0 && !service.HasRegisteredEditor);
            Check("no-visible-process-windows", VisibleProcessWindows().Count == 0);
        }
        catch (Exception error) { checks.Add(new { error = error.ToString() }); failed++; }
        Save();
        desktop.Shutdown(failed == 0 ? 0 : 1);
    }
}
