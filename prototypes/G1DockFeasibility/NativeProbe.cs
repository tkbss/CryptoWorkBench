using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using Dock.Model.Core;
using Prism.Ioc;
using Prism.Navigation.Regions;

namespace G1DockFeasibility;

// Runs the actual platform backend, never Avalonia.Headless. No input is sent to other applications.
public static class NativeProbe
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);

    public static async Task Run(ProbeWindow shell, IContainerProvider container, string output)
    {
        var results = new List<object>();
        var workspace = shell.Workspace;
        var session = workspace.Session;
        void Check(string name, bool condition, object? detail = null)
        {
            results.Add(new { name, passed = condition, detail });
            if (!condition) Environment.ExitCode = 1;
        }
        static EditorView Editor(Window window) => window.GetVisualDescendants().OfType<EditorView>().Single();
        static Task Settle() => Task.Delay(250);
        static async Task WaitForEditor(Window window)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!window.GetVisualDescendants().OfType<EditorView>().Any() && DateTime.UtcNow < deadline)
                await Task.Delay(50);
        }
        try
        {
            await Settle();
            await WaitForEditor(shell);
            shell.Activate();
            var mainHandle = shell.TryGetPlatformHandle()!.Handle;
            Check("native-main-handle", mainHandle != 0);
            if (OperatingSystem.IsWindows())
            {
                // Windows may deny activation to a background-launched process. Give the desktop
                // operator a bounded opportunity to activate this app before testing focus transfers.
                var deadline = DateTime.UtcNow.AddSeconds(30);
                shell.Title = "G1-01 native probe — activate this window to start";
                while (GetForegroundWindow() != mainHandle && DateTime.UtcNow < deadline) await Task.Delay(50);
                Check("native-main-foreground", GetForegroundWindow() == mainHandle);
            }
            Check("prism-region-host", container.Resolve<IRegionManager>().Regions["ProbeDockHost"].ActiveViews.Contains(shell.Host));
            var document = session.Document;
            var original = workspace.Find(DocumentSession.Id);
            document.Text = "// native probe\n0123456789\n";
            Editor(shell).Editor.CaretOffset = 21;
            for (var i = 0; i < 5; i++)
            {
                workspace.Float(DocumentSession.Id);
                await Settle();
                var floating = (Window)workspace.Root.Windows!.Single().Host!;
                await WaitForEditor(floating);
                var handle = floating.TryGetPlatformHandle()!.Handle;
                Check($"float-{i}-distinct-native-handle", handle != 0 && handle != mainHandle);
                Check($"float-{i}-avalonia-owner", ReferenceEquals(floating.Owner, shell));
                if (OperatingSystem.IsWindows()) Check($"float-{i}-win32-owner", GetWindow(handle, 4) == mainHandle);
                floating.Position = new PixelPoint(shell.Position.X + (int)(shell.Width * shell.RenderScaling) + 25, shell.Position.Y + 30);
                floating.Activate();
                Editor(floating).Editor.TextArea.Focus();
                await Settle();
                Check($"float-{i}-independent-position", floating.Position.X > shell.Position.X + shell.Width * shell.RenderScaling,
                    new { main = shell.Position.ToString(), child = floating.Position.ToString() });
                Check($"float-{i}-text-caret-identity", ReferenceEquals(Editor(floating).Editor.Document, document) && Editor(floating).Editor.CaretOffset == 21 && ReferenceEquals(original, workspace.Find(DocumentSession.Id)));
                Check($"float-{i}-focus", Editor(floating).Editor.IsKeyboardFocusWithin && floating.IsActive,
                    new { editorFocus = Editor(floating).Editor.IsKeyboardFocusWithin, windowActive = floating.IsActive });
                if (OperatingSystem.IsWindows()) Check($"float-{i}-foreground", GetForegroundWindow() == handle,
                    new { expected = handle.ToInt64(), actual = GetForegroundWindow().ToInt64() });
                workspace.Move(DocumentSession.Id, "documents");
                shell.Activate();
                await Settle();
                await WaitForEditor(shell);
                Check($"redock-{i}-closed-floating", !floating.IsVisible && workspace.Root.Windows!.Count == 0);
                Check($"redock-{i}-editor-state", ReferenceEquals(Editor(shell).Editor.Document, document) && Editor(shell).Editor.CaretOffset == 21 && document.Text == "// native probe\n0123456789\n" && session.AttachedViews == 1);
                Check($"redock-{i}-focus", Editor(shell).Editor.IsKeyboardFocusWithin && shell.IsActive);
            }
            var variables = container.Resolve<VariablesViewModel>();
            variables.State.ToolVisits = 42;
            workspace.Move(VariablesViewModel.Id, "diagnostics-dock");
            workspace.Float(VariablesViewModel.Id);
            await Settle();
            var toolWindow = (Window)workspace.Root.Windows!.Single().Host!;
            toolWindow.Close();
            await Settle();
            Check("floating-tool-close", !toolWindow.IsVisible && workspace.Root.Windows!.Count == 0);
            Check("singleton-survives-tool-close", ReferenceEquals(variables, container.Resolve<VariablesViewModel>()) && variables.State.ToolVisits == 42);
            // Restore a known layout after explicitly closing a tool.
            var fresh = workspace.Factory.CreateLayout();
            var serializer = new Dock.Serializer.SystemTextJson.DockSerializer();
            workspace.RestoreLayout(serializer.Serialize(fresh), shell.Host);
            await Settle();
            var layoutFile = output + ".layout.json";
            File.WriteAllText(layoutFile, workspace.SaveLayout());
            workspace.RestoreLayout(File.ReadAllText(layoutFile), shell.Host);
            await Settle();
            Check("layout-restores-context", ReferenceEquals(workspace.Find(DocumentSession.Id).Context, session) && ReferenceEquals(workspace.Find(VariablesViewModel.Id).Context, variables));
            workspace.Float(DocumentSession.Id);
            await Settle();
            var finalFloat = (Window)workspace.Root.Windows!.Single().Host!;
            var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            shell.Close();
            await Settle();
            Check("main-close-closes-native-floats", !finalFloat.IsVisible);
            Check("shutdown-detaches-editor", session.AttachedViews == 0 && session.AttachCount == session.DetachCount);
            Check("shutdown-unregisters-controls", workspace.Factory.DockControls.Count == 0 && workspace.Factory.HostWindows.Count == 0,
                new { controls = workspace.Factory.DockControls.Count, windows = workspace.Factory.HostWindows.Count });
            Check("document-disposed-once", session.DisposeCount == 1);
        }
        catch (Exception exception)
        {
            results.Add(new { name = "exception", passed = false, detail = exception.ToString() });
            Environment.ExitCode = 1;
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, JsonSerializer.Serialize(new
            {
                platform = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                headless = false, utc = DateTimeOffset.UtcNow, results,
                lifecycle = new { session.InstanceId, session.AttachCount, session.DetachCount, session.ViewCount, session.AttachedViews, session.DisposeCount }
            }, new JsonSerializerOptions { WriteIndented = true }));
            workspace.Dispose();
            ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).Shutdown(Environment.ExitCode);
        }
    }
}
