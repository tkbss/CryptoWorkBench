using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task RunPartialFailure(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This probe uses native Windows byte locks.");
        var shell = (DockShellWindow)desktop.MainWindow!;
        var layout = shell.Layout;
        var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        var checks = new List<object>(); int failed = 0;
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failed++; }
        async Task Render()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            foreach (var window in desktop.Windows) window.UpdateLayout();
        }
        try
        {
            await Render();
            const string script = "Print(0x(AA)) // R4 native: preserve without execution";
            service.Mutate(() => session.Editor.Text = script);
            var editor = session.Editor; var text = editor.Document; var undo = text.UndoStack;
            var save = service.CaptureSaveIntent()!.Snapshot;
            var generation = service.EditorCoordinator.WorkspaceGeneration;
            var stateRevision = InterpreterHostQueue.StateRevision;
            // Reflect the actual Desktop recorder, without adding a Desktop reference
            // (and new dependency/lock graph) to the GUI test assembly.
            var type = Assembly.Load("CryptoWorkBenchAvalonia.Desktop")
                .GetType("CryptoWorkBenchAvalonia.Desktop.NativeDockWindowTrace", throwOnError: true)!;
            const BindingFlags member = BindingFlags.Instance | BindingFlags.NonPublic;
            var tracePath = Output + ".native-trace.jsonl";
            using var trace = (IDisposable)Activator.CreateInstance(type, member, null, [tracePath], null)!;
            using var blocker = new FileStream(tracePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long? lockedPosition = null; bool armed = false;
            EventHandler<DockableRemovedEventArgs> lockFile = (_, _) =>
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                if (!armed || lockedPosition != null) return;
                lockedPosition = blocker.Length;
                blocker.Lock(lockedPosition.Value, 1);
            };
            // Register the OS lock first so the real product Removed observer is
            // the next subscriber. This callback injects no exception or mutation.
            layout.Factory.DockableRemoved += lockFile;
            try
            {
                type.GetMethod("Attach", member)!.Invoke(trace, [shell]);
                layout.Float(DockShellState.VariablesId);
                await Render();
                var pane = layout.Pane(DockShellState.VariablesId);
                var source = (Window)layout.Factory.FindRoot(pane)!.Window!.Host!;
                var sourceHwnd = source.TryGetPlatformHandle()!.Handle;
                bool removedRecord = false;
                using (var reader = new StreamReader(new FileStream(tracePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                    while (reader.ReadLine() is { } line)
                    {
                        using var record = JsonDocument.Parse(line);
                        removedRecord |= record.RootElement.GetProperty("reason").GetString() == "factory-dockable-removed";
                    }
                Check("normal-trace-records", removedRecord);
                armed = true;
                layout.Factory.MoveDockable((IDock)pane.Owner!, (IDock)layout.Pane(DockShellState.InfoId).Owner!, pane, null);
                Check("real-post-removal-file-lock-reached", lockedPosition != null);
                var error = (DockTraceFailure?)type.GetProperty("Failure", member)!.GetValue(trace);
                Check("trace-io-status", error?.ErrorType == typeof(IOException).FullName);
                Check("trace-does-not-fault-layout", !layout.LayoutFaulted);
                Check("move-completes", ReferenceEquals(pane.Owner, layout.Pane(DockShellState.InfoId).Owner));
                Check("source-HWND-cleanup", !IsWindow(sourceHwnd));
                Check("post-io-layout-valid", DockLayoutStore.Decode(layout.Capture(DockPersistenceCleanupTests.Bounds)) != null);
                var length = blocker.Length;
                type.GetMethod("Record", member)!.Invoke(trace, ["no-retry", null]);
                Check("trace-stays-disabled", blocker.Length == length && ReferenceEquals(error, type.GetProperty("Failure", member)!.GetValue(trace)));
            }
            finally
            {
                layout.Factory.DockableRemoved -= lockFile;
                if (lockedPosition is { } position) blocker.Unlock(position, 1);
            }
            Check("trace-failure-preserves-editor", ReferenceEquals(editor, session.Editor) && ReferenceEquals(text, editor.Document) && ReferenceEquals(undo, text.UndoStack));
            Check("trace-failure-preserves-save", service.HasRegisteredEditor && service.CaptureSaveIntent()!.Snapshot == save);

            layout.Float(DockShellState.DiagnosticsId);
            await Render();
            var retained = (Window)layout.Factory.FindRoot(layout.Pane(DockShellState.DiagnosticsId))!.Window!.Host!;
            var retainedHwnd = retained.TryGetPlatformHandle()!.Handle;
            Check("retained-host-native-owner", IsWindow(retainedHwnd) && GetWindow(retainedHwnd, 4) == shell.TryGetPlatformHandle()!.Handle);
            var locator = layout.Factory.HostWindowLocator!;
            var create = locator[nameof(IDockWindow)];
            var original = new Win32Exception(8, "R4 controlled native host construction failure");
            Exception? observed = null; int calls = 0;
            locator[nameof(IDockWindow)] = () => { calls++; throw original; };
            try { layout.Float(DockShellState.DocumentId); }
            catch (Exception error) { observed = error; }
            finally { locator[nameof(IDockWindow)] = create; }
            Check("host-boundary-original-error", ReferenceEquals(observed, original) && calls == 1);
            Check("partial-host-model", layout.Root.Windows!.Any(w => w.Host == null && w.Owner == null && w.Factory == null));
            Check("host-failure-latches-A02", layout.LayoutFaulted);
            bool captureRejected = false;
            try { layout.Capture(DockPersistenceCleanupTests.Bounds); }
            catch (InvalidDataException) { captureRejected = true; }
            Check("partial-layout-not-persisted", captureRejected);
            var tree = DockShellLayout.VisibleTree(layout.Root).ToArray();
            var info = layout.Pane(DockShellState.InfoId); var infoOwner = info.Owner;
            layout.Float(DockShellState.InfoId);
            layout.Factory.MoveDockable((IDock)info.Owner!, (IDock)layout.Pane(DockShellState.DiagnosticsId).Owner!, info, null);
            Check("fault-blocks-mutations", !layout.Reset() && ReferenceEquals(info.Owner, infoOwner) && tree.SequenceEqual(DockShellLayout.VisibleTree(layout.Root)));
            Check("host-failure-preserves-editor", ReferenceEquals(editor, session.Editor) && ReferenceEquals(text, editor.Document) && ReferenceEquals(undo, text.UndoStack));
            Check("host-failure-preserves-business-save", service.HasRegisteredEditor && service.CaptureSaveIntent()!.Snapshot == save);
            Check("no-execution-or-new-session", service.EditorCoordinator.WorkspaceGeneration == generation && InterpreterHostQueue.StateRevision == stateRevision);
            Check("observer-cleaned", service.EditorCoordinator.ObserverDepth == 0);
            var savePath = Output + ".business-save.sbk";
            await service.CommitSaveAsync(service.CaptureSaveIntent()!, new(savePath, false, null, PreserveText: true));
            Check("business-save-committed-after-fault", File.ReadAllText(savePath) == script);
            var hwnds = VisibleProcessWindows().ToArray();
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            shell.Closed += (_, _) => closed.TrySetResult();
            shell.Close();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(8));
            await service.DrainAsync(); await Render();
            Check("controlled-shutdown", !service.HasRegisteredEditor && hwnds.All(h => !IsWindow(h)) &&
                layout.Factory.HostWindows.Count == 0 && layout.Factory.DockControls.Count == 0);
        }
        catch (Exception error) { failed++; checks.Add(new { name = "exception", passed = false, detail = error.ToString() }); }
        File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, processId = Environment.ProcessId,
            input = "native Windows; real trace byte-lock failure; controlled host-locator exception; no OS resource exhaustion",
            passed = checks.Count - failed, failed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        desktop.Shutdown(failed == 0 ? 0 : 1);
    }
}
