using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using CryptoWorkBenchAvalonia.ViewModels;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;
using System.Reflection;
using Avalonia.Input.Platform;
using AvaloniaEdit.CodeCompletion;
using CryptoScript.Model;
using Avalonia.Platform.Storage;
using System.Text;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockEditorTests
{
    internal sealed class Host : IAsyncDisposable
    {
        internal readonly WorkbenchAutomationService Service = new() { LeaseMilliseconds = () => 1_000UL };
        internal readonly HistoryService History = new();
        internal readonly MainViewModel Main;
        internal readonly DockEditorSession Session;
        internal readonly DockShellLayout Layout = new(new DockShellState());
        internal readonly DockShellWindow Window;
        private readonly DockShellPresentation _presentation;
        internal WorkbenchTextEditor Editor => Session.Editor;
        internal EditorCoordinator Coordinator => Service.EditorCoordinator;
        internal Host()
        {
            Main = new(History);
            var vm = new CryptoScriptEditViewModel(new(), new(), new(), Main, null!, History, Service);
            Session = new(vm, Main, Service);
            _presentation = new(Application.Current!, Session);
            Window = new(Layout);
            Session.Start(Window); Window.BindEditor(Session);
            App.CoordinateClosing(Window, Service);
            Window.Closed += (_, _) => Session.Dispose();
            Window.Show(); Pump(Window);
            Editor.TextArea.Focus();
        }
        internal EditorSnapshot Snapshot => Service.CaptureSaveIntent()!.Snapshot;
        internal void Float() { Layout.Float(DockShellState.DocumentId); Pump((Window)Layout.Root.Windows!.Single().Host!); }
        internal void Dock() { Layout.Dock(DockShellState.DocumentId); Pump(Window); }
        public async ValueTask DisposeAsync()
        {
            Window.Close();
            await Wait(() => !Window.IsVisible);
            Session.Dispose(); Layout.Dispose(); _presentation.Dispose();
            await Service.DisposeAsync();
        }
    }
    internal static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    internal static async Task Wait(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(20);
        Assert.That(condition(), Is.True, "UI/lifecycle did not settle");
    }
    private static Task Run(Func<Host, Task> test) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var host = new Host(); await test(host); return true;
    }, CancellationToken.None);

    [Test] public Task RepeatedFloatRedockRetainsEditorDocumentRegistrationHistoryAndDirtyState() => Run(async h =>
    {
        h.Editor.TextArea.PerformTextInput("ä€😀");
        var editor = h.Editor; var document = editor.Document; var history = document.UndoStack;
        var id = h.Snapshot.EditorId; var generation = h.Coordinator.WorkspaceGeneration;
        var lifecycle = h.Session.ViewModel.EditorLifecycle;
        editor.Select(1, 2);
        for (int i = 0; i < 12; i++)
        {
            h.Float(); h.Dock();
            Assert.That(h.Editor, Is.SameAs(editor));
            Assert.That(h.Editor.Document, Is.SameAs(document));
            Assert.That(h.Snapshot.EditorId, Is.EqualTo(id));
            Assert.That(h.Coordinator.WorkspaceGeneration, Is.EqualTo(generation));
            Assert.That(h.Session.ViewModel.EditorLifecycle, Is.EqualTo(lifecycle));
            Assert.That(h.Editor.SelectionStart, Is.EqualTo(1)); Assert.That(h.Editor.SelectionLength, Is.EqualTo(2));
            Assert.That(h.Window.GetVisualDescendants().OfType<WorkbenchTextEditor>().Count(), Is.EqualTo(1));
        }
        Assert.That(document.UndoStack, Is.SameAs(history));
        Assert.That(h.Snapshot.DirtyState, Is.EqualTo(DirtyState.Dirty));
        ApplicationCommands.Undo.Execute(null, editor.TextArea); Assert.That(editor.Text, Is.Empty);
        ApplicationCommands.Redo.Execute(null, editor.TextArea); Assert.That(editor.Text, Is.EqualTo("ä€😀"));
        await Task.CompletedTask;
    });

    [Test] public Task FloatingDocumentCloseIsVetoedAndShutdownRetiresExactlyOnce() => Run(async h =>
    {
        h.Float(); var host = (Window)h.Layout.Root.Windows!.Single().Host!;
        var generation = h.Coordinator.WorkspaceGeneration;
        host.Close(); Assert.That(host.IsVisible, Is.True);
        Assert.That(h.Coordinator.WorkspaceGeneration, Is.EqualTo(generation));
        h.Window.Close(); await Wait(() => !h.Window.IsVisible);
        Assert.That(host.IsVisible, Is.False); Assert.That(h.Service.HasRegisteredEditor, Is.False);
        Assert.That(h.Layout.Factory.DockControls, Is.Empty);
        var lifecycle = h.Session.ViewModel.EditorLifecycle;
        h.Session.Dispose(); Assert.That(h.Session.ViewModel.EditorLifecycle, Is.EqualTo(lifecycle));
    });

    private static void Blocked(Host h)
    {
        var pane = h.Layout.Pane(DockShellState.DocumentId);
        var before = DockShellLayout.VisibleTree(h.Layout.Root).ToArray();
        var generation = h.Coordinator.WorkspaceGeneration;
        h.Layout.Float(pane.Id);
        h.Layout.Factory.FloatDockable(pane);
        h.Layout.Factory.SplitToWindow((IDock)pane.Owner!, pane, 10, 10, 500, 400);
        h.Layout.Factory.NewHorizontalDocumentDock(pane);
        var manager = new DockManager(new DockService());
        Assert.That(manager.ValidateDockable(pane, h.Layout.Pane(DockShellState.OutputId), DragAction.Move, DockOperation.Left, true), Is.False);
        Assert.That(DockCapabilityResolver.IsEnabled(pane, DockCapability.Drag), Is.False);
        Assert.That(DockShellLayout.VisibleTree(h.Layout.Root), Is.EqualTo(before));
        Assert.That(h.Coordinator.WorkspaceGeneration, Is.EqualTo(generation));
    }

    [TestCase("pending")][TestCase("observer")][TestCase("active")][TestCase("host-busy")][TestCase("modal")][TestCase("identity")][TestCase("mutation")]
    public Task AllDockEntryPointsRejectBusyEditorWithoutPartialMutation(string mode) => Run(async h =>
    {
        switch (mode)
        {
            case "pending": using (h.Coordinator.TryBegin()) Blocked(h); break;
            case "observer": h.Coordinator.Observe(() => Blocked(h)); break;
            case "active": h.Coordinator.RunGui(() => Blocked(h)); break;
            case "host-busy": using (InterpreterHostQueue.Reserve()) Blocked(h); break;
            case "modal": h.Window.IsEnabled = false; try { Blocked(h); } finally { h.Window.IsEnabled = true; } break;
            case "identity": using (h.Editor.LockIdentity()) Blocked(h); break;
            case "mutation": using (h.Editor.LockMutations()) Blocked(h); break;
        }
        h.Float(); h.Dock(); await Task.CompletedTask;
    });

    [Test] public Task McpLeaseSurvivesRejectedDockAndTargetsOnlyRegisteredEditor() => Run(async h =>
    {
        h.Float(); h.Dock();
        var before = h.Snapshot;
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("dock-test", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var request = new AcquireEditorV2("2", h.Service.InstanceId, before.EditorId, Guid.NewGuid(), before.Revision);
        var lease = (await h.Service.AcquireEditorAsync(caller, request, default)).Result!;
        Assert.That(lease, Is.Not.Null);
        Blocked(h);
        h.Editor.TextArea.PerformTextInput("rejected GUI"); Assert.That(h.Editor.Text, Is.Empty);
        var edit = new EditorEditV2("2", request.InstanceId, request.EditorId, lease.LeaseId, before.Revision, EditorEditMode.Append, "MCP");
        var receipt = await h.Service.UpdateEditorV2Async(caller, edit, default);
        Assert.That(receipt.Error, Is.Null); Assert.That(h.Editor.Text, Is.EqualTo("MCP"));
        var rejected = await h.Service.UpdateEditorV2Async(caller, edit, default);
        Assert.That(rejected.Error, Is.Not.Null); Assert.That(h.Editor.Text, Is.EqualTo("MCP"));
        Blocked(h);
        await h.Service.ReleaseEditorAsync(caller, new("2", request.InstanceId, request.EditorId, lease.LeaseId), default);
        h.Float(); h.Dock(); Assert.That(h.Snapshot.EditorId, Is.EqualTo(before.EditorId));
        ApplicationCommands.Undo.Execute(null, h.Editor.TextArea); Assert.That(h.Editor.Text, Is.Empty);
    });

    [Test] public Task FileDialogPendingBlocksDockAndShutdownWaitsForOriginalCompletion() => Run(async h =>
    {
        h.Float();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = h.Session.ViewModel.WithFileDialogAsync(async _ => await gate.Task);
        var host = (Window)h.Layout.Root.Windows!.Single().Host!;
        h.Layout.Dock(DockShellState.DocumentId); Assert.That(host.IsVisible, Is.True);
        h.Window.Close(); await Task.Delay(50);
        Assert.That(h.Window.IsVisible, Is.True); Assert.That(h.Service.IsStopping, Is.True);
        Assert.That(h.Service.HasRegisteredEditor, Is.True);
        gate.SetResult(); await work; await Wait(() => !h.Window.IsVisible);
        Assert.That(h.Service.HasRegisteredEditor, Is.False); Assert.That(host.IsVisible, Is.False);
    });

    [Test] public Task LoadingIsTextOnlyAndStaleLoadCannotOverwriteNewerText() => Run(async h =>
    {
        long revision = InterpreterHostQueue.StateRevision;
        var intent = h.Service.CaptureSaveIntent()!;
        Assert.That(h.Session.LoadText(intent, "Print(0x(11))\nUnicode ä€😀\n"), Is.True);
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
        Assert.That(h.History.LineCanBack, Is.False);
        var text = h.Editor.Text;
        Assert.That(h.Session.LoadText(intent, "stale"), Is.False);
        Assert.That(h.Editor.Text, Is.EqualTo(text));
        h.Float(); h.Dock(); Assert.That(h.Editor.Text, Is.EqualTo(text));
        await Task.CompletedTask;
    });

    [Test] public Task ExactTextSaveRetainsUnicodeBlankLinesAndSaveIntentSafety() => Run(async h =>
    {
        Assert.That(h.Session.LoadText(h.Service.CaptureSaveIntent()!, "ä€😀\n\n"), Is.True);
        var intent = h.Service.CaptureSaveIntent()!;
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dock-save-" + Guid.NewGuid() + ".sbk");
        try
        {
            h.Float(); h.Dock();
            Assert.That(await h.Service.CommitSaveAsync(intent, new(path, false, null, PreserveText: true)), Is.Null);
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(h.Editor.Text));
            h.Editor.TextArea.PerformTextInput("new");
            Assert.That(await h.Service.CommitSaveAsync(intent, new(path, false, null, PreserveText: true)), Is.EqualTo(AutomationErrorCode.RevisionConflict));
        }
        finally { File.Delete(path); }
    });

    [Test] public Task HistoryNavigationAndRoutedReturnStillWorkAfterRelocation() => Run(async h =>
    {
        h.History.AddLine("first"); h.History.AddLine("last");
        h.Float(); h.Main.LineHistoryBackCommand.Execute(); Assert.That(h.Editor.Text, Is.EqualTo("first"));
        h.Dock(); h.Main.LineHistoryForwardCommand.Execute(); Assert.That(h.Editor.Text, Is.EqualTo("last"));
        h.Editor.Text = "Print(0x(11))"; h.Editor.CaretOffset = h.Editor.Text.Length;
        h.Float(); var window = (Window)h.Layout.Root.Windows!.Single().Host!;
        h.Editor.TextArea.Focus(); window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
        await h.Session.View.PendingExecution;
        Assert.That(h.Editor.Text, Does.Contain("11"));
        Assert.That(h.Session.ViewModel.PrintMessage, Is.Not.Empty);
    });

    [Test] public Task HostChangeRetiresPopupAndItsLateInsertionWithoutLosingCompletion() => Run(async h =>
    {
        for (int i = 0; i < 3; i++)
        {
            h.Float(); h.Editor.Text = "Info"; h.Editor.CaretOffset = 4;
            h.Editor.TextArea.PerformTextInput("(");
            var controller = typeof(CryptoScriptEditView).GetField("_infoCompletionController", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(h.Session.View)!;
            var popup = (CompletionWindow)typeof(InfoCompletionController).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
            Assert.That(popup.IsOpen, Is.True);
            popup.Closed += (_, _) => h.Editor.TextArea.PerformTextInput("REENTRANT");
            h.Dock();
            Assert.That(popup.IsOpen, Is.False);
            popup.CompletionList.RequestInsertion(EventArgs.Empty);
            Assert.That(h.Editor.Text, Is.EqualTo("Info("));
            Assert.That(h.Coordinator.Pending, Is.Zero);
        }
        await Task.CompletedTask;
    });

    [Test] public Task DelayedClipboardCannotDockOrWriteAfterShutdown() => Run(async h =>
    {
        h.Float(); h.Editor.Text = "original"; h.Editor.CaretOffset = h.Editor.Text.Length;
        var original = ((Window)TopLevel.GetTopLevel(h.Editor)!).Clipboard!;
        var proxy = DispatchProxy.Create<IClipboard, EditorCoordinatorTests.ClipboardProxy>();
        var adapter = (EditorCoordinatorTests.ClipboardProxy)(object)proxy;
        adapter.Inner = original;
        var response = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.DelayedRead = response.Task; EditorCoordinatorTests.ClipboardProxy.Bind(proxy);
        var area = (WorkbenchTextArea)h.Editor.TextArea;
        try
        {
            ApplicationCommands.Paste.Execute(null, area);
            Assert.That(area.PendingClipboard.IsCompleted, Is.False);
            var host = TopLevel.GetTopLevel(h.Editor);
            h.Layout.Dock(DockShellState.DocumentId);
            Assert.That(TopLevel.GetTopLevel(h.Editor), Is.SameAs(host));
            h.Window.Close(); Assert.That(h.Window.IsVisible, Is.True);
            response.SetResult("STALE"); await area.PendingClipboard;
            await Wait(() => !h.Window.IsVisible);
            Assert.That(h.Editor.Text, Is.EqualTo("original"));
            Assert.That(h.Service.HasRegisteredEditor, Is.False);
        }
        finally { response.TrySetResult(null); EditorCoordinatorTests.ClipboardProxy.Bind(original); }
    });

    [Test] public Task GuiPasswordRequestKeepsOriginalExecutionAndBlocksDockUntilSettled() => Run(async h =>
    {
        h.Float();
        var requested = new TaskCompletionSource<PasswordRequestEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<PasswordResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var execution = h.Session.ViewModel.ExecuteLineAsync("Print(0x(01))",
            passwordHandler: (_, request) => { requested.SetResult(request); return response.Task; },
            testStatements: [new AsyncInfrastructureStatement(async context =>
                { using var answer = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad); })]);
        var request = await requested.Task;
        try
        {
            Assert.That(h.Session.ViewModel.IsExecuting, Is.True);
            Blocked(h);
            var host = TopLevel.GetTopLevel(h.Editor);
            h.Layout.Dock(DockShellState.DocumentId);
            Assert.That(TopLevel.GetTopLevel(h.Editor), Is.SameAs(host));
        }
        finally { response.TrySetResult(PasswordResponse.UserCancelled(request.RequestId)); await execution; }
        h.Dock(); Assert.That(h.Service.HasRegisteredEditor, Is.True);
    });

    [Test] public Task FailedVisualRelocationKeepsRegistrationAndReleasesObservationGate() => Run(async h =>
    {
        var id = h.Snapshot.EditorId; var generation = h.Coordinator.WorkspaceGeneration;
        Assert.Throws<InvalidOperationException>(() => h.Session.Relocate(() => throw new InvalidOperationException("injected visual failure")));
        Assert.That(h.Coordinator.ObserverDepth, Is.Zero);
        Assert.That(h.Service.HasRegisteredEditor, Is.True);
        Assert.That(h.Snapshot.EditorId, Is.EqualTo(id));
        Assert.That(h.Coordinator.WorkspaceGeneration, Is.EqualTo(generation));
        h.Float(); h.Dock(); await Task.CompletedTask;
    });

    [TestCase(false)][TestCase(true)]
    public Task FileCommandsUseFloatingOwnerAndRejectStaleReadWithoutExecution(bool stale) => Run(async h =>
    {
        h.Float(); var owner = (Window)TopLevel.GetTopLevel(h.Editor)!;
        var field = typeof(TopLevel).GetField("_storageProvider", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var original = field.GetValue(owner);
        var file = DispatchProxy.Create<IStorageFile, FileProbe>();
        var probe = (FileProbe)(object)file;
        probe.Path = System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, "dock-command-" + Guid.NewGuid() + ".sbk");
        var provider = DispatchProxy.Create<IStorageProvider, FileProbe>();
        ((FileProbe)(object)provider).File = file;
        field.SetValue(owner, provider);
        const string text = "Print(0x(11))\r\n\r\nä€😀\n";
        long state = InterpreterHostQueue.StateRevision;
        try
        {
            var open = ((Prism.Commands.IAsyncCommand)h.Session.OpenCommand).ExecuteAsync(null);
            Assert.That(probe.ReadRequested, Is.True);
            h.Layout.Dock(DockShellState.DocumentId);
            Assert.That(TopLevel.GetTopLevel(h.Editor), Is.SameAs(owner));
            if (stale) h.Editor.TextArea.PerformTextInput("newer");
            probe.Read.SetResult(new MemoryStream(Encoding.UTF8.GetBytes(text)));
            await open;
            Assert.That(h.Editor.Text, Is.EqualTo(stale ? "newer" : text));
            Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(state));
            Assert.That(h.History.LineCanBack, Is.False);
            if (!stale)
            {
                await ((Prism.Commands.IAsyncCommand)h.Session.SaveCommand).ExecuteAsync(null);
                Assert.That(await System.IO.File.ReadAllTextAsync(probe.Path), Is.EqualTo(text));
            }
        }
        finally
        {
            probe.Read.TrySetResult(Stream.Null);
            field.SetValue(owner, original);
            System.IO.File.Delete(probe.Path);
        }
    });

    public class FileProbe : DispatchProxy
    {
        internal string Path = "";
        internal IStorageFile? File;
        internal bool ReadRequested;
        internal readonly TaskCompletionSource<Stream> Read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "OpenFilePickerAsync": return Task.FromResult<IReadOnlyList<IStorageFile>>([File!]);
                case "SaveFilePickerAsync": return Task.FromResult(File);
                case "OpenReadAsync": ReadRequested = true; return Read.Task;
                case "get_Path": return new Uri(Path);
                case "get_Name": return System.IO.Path.GetFileName(Path);
                case "get_CanOpen": case "get_CanSave": return true;
                case "Dispose": return null;
                default: throw new NotSupportedException(method.Name);
            }
        }
    }
}
