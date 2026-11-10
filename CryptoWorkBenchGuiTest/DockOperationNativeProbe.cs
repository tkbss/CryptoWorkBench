using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    // A04 probe after R2: the separate partial-failure criterion remains active.
    // Existing native infrastructure, journal and shutdown are reused.
    private async Task RunOperationContract(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var shell = (DockShellWindow)desktop.MainWindow!;
        var layout = shell.Layout;
        var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        var checks = new List<object>(); int failed = 0;
        using var journal = new RedockJournal(layout, session, desktop, Output + ".windows.jsonl");
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failed++; }
        async Task Render()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            foreach (var window in desktop.Windows) window.UpdateLayout();
        }
        try
        {
            await Render();
            var main = shell.TryGetPlatformHandle()!.Handle;
            var editor = session.Editor; var text = editor.Document; var undo = text.UndoStack;
            var snapshot = service.CaptureSaveIntent()!.Snapshot;
            var generation = service.EditorCoordinator.WorkspaceGeneration;
            Check("main-HWND", IsWindow(main));
            layout.Float(DockShellState.DiagnosticsId);
            layout.Float(DockShellState.VariablesId);
            await Render();
            var pane = layout.Pane(DockShellState.VariablesId);
            var model = layout.Factory.FindRoot(pane)!.Window!;
            var source = (Window)model.Host!; var sourceHwnd = source.TryGetPlatformHandle()!.Handle;
            var retainedModel = layout.Factory.FindRoot(layout.Pane(DockShellState.DiagnosticsId))!.Window!;
            var retained = (Window)retainedModel.Host!; var retainedHwnd = retained.TryGetPlatformHandle()!.Handle;
            Check("source-native-owner", IsWindow(sourceHwnd) && GetWindow(sourceHwnd, 4) == main);
            Check("retained-native-owner", IsWindow(retainedHwnd) && GetWindow(retainedHwnd, 4) == main);
            Check("source-model-ownership", ReferenceEquals(model.Factory, layout.Factory) && ReferenceEquals(model.Owner, layout.Root) &&
                ReferenceEquals(model.Layout!.Window, model) && ReferenceEquals(((IHostWindow)source).Window, model));
            var info = layout.Pane(DockShellState.InfoId); var infoOwner = info.Owner;
            layout.Factory.MoveDockable((IDock)pane.Owner!, (IDock)infoOwner!, pane, null);
            await Render();
            Check("legitimate-source-HWND-destroyed", !IsWindow(sourceHwnd) && !VisibleProcessWindows().Contains(sourceHwnd));
            Check("source-model-released", model.Owner == null && model.Layout == null && !layout.Root.Windows!.Contains(model));
            Check("other-host-survives", IsWindow(retainedHwnd) && retained.IsVisible && ReferenceEquals(retainedModel.Factory, layout.Factory));
            journal.Record("A04-after-source-cleanup");
            var normal = layout.Capture(DockPersistenceCleanupTests.Bounds);
            Check("capture-decode", DockLayoutStore.Decode(normal) != null);
            Check("maximize", layout.Maximize(DockShellState.DocumentId));
            Check("restore", layout.Restore());
            Check("restore-capture-equal", layout.Capture(DockPersistenceCleanupTests.Bounds) == normal);
            Check("visual-relocation-keeps-generation", service.EditorCoordinator.WorkspaceGeneration == generation);

            service.ActivateMcp();
            var caller = service.IssueCredential("a04-native", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
            var lease = (await service.AcquireEditorAsync(caller, new("2", service.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
            Check("lease-active", lease != null && !session.CanRelocate);
            layout.Factory.FloatDockable(pane);
            var accepted = new DockManager(new DockService()).ValidateDockable(pane, info, DragAction.Move, DockOperation.Fill, true);
            Check("lease-rejects-before-mutation", !accepted && layout.Capture(DockPersistenceCleanupTests.Bounds) == normal);
            await service.ReleaseEditorAsync(caller, new("2", service.InstanceId, snapshot.EditorId, lease!.LeaseId), default);
            // TakeControl/BeginDrain/ResumeGui intentionally advance workspace
            // generation. Visual relocation was checked before that transition.
            Check("identity-preserved-through-lease", ReferenceEquals(session.Editor, editor) &&
                ReferenceEquals(editor.Document, text) && ReferenceEquals(text.UndoStack, undo));

            // Deterministic post-removal exception: missing mandatory document is
            // invalid, rather than an ambiguous closed-tool state.
            var document = layout.Pane(DockShellState.DocumentId);
            var original = new InvalidOperationException("A04 native original move failure");
            EventHandler<DockableRemovedEventArgs> fail = (_, e) => { if (e.Dockable == document) throw original; };
            Exception? observed = null;
            layout.Factory.DockableRemoved += fail;
            try { layout.Factory.MoveDockable((IDock)document.Owner!, (IDock)pane.Owner!, document, null); }
            catch (Exception error) { observed = error; }
            finally { layout.Factory.DockableRemoved -= fail; }
            Check("original-error-preserved", ReferenceEquals(original, observed));
            bool rejectedCapture = false;
            try { layout.Capture(DockPersistenceCleanupTests.Bounds); }
            catch (InvalidDataException) { rejectedCapture = true; }
            Check("invalid-document-tree-not-persistable", rejectedCapture);
            Check("partial-failure-latches-A02-fault", layout.LayoutFaulted);
            Check("observer-cleaned", service.EditorCoordinator.ObserverDepth == 0);
            Check("business-save-intent-preserved", service.CaptureSaveIntent()!.Snapshot == snapshot);
            journal.Record("A04-after-partial-failure");
            var hwnds = VisibleProcessWindows().ToArray();
            shell.Close(); await service.DrainAsync(); await Render();
            Check("final-shutdown", !service.HasRegisteredEditor && hwnds.All(h => !IsWindow(h)) &&
                layout.Factory.HostWindows.Count == 0 && layout.Factory.DockControls.Count == 0);
        }
        catch (Exception error) { failed++; checks.Add(new { name = "exception", passed = false, detail = error.ToString() }); }
        File.WriteAllText(Output, JsonSerializer.Serialize(new { mode = Mode, processId = Environment.ProcessId,
            input = "programmatic Factory and DockManager on native Windows; no external mouse/keyboard",
            passed = checks.Count - failed, failed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        desktop.Shutdown(failed == 0 ? 0 : 1);
    }
}

