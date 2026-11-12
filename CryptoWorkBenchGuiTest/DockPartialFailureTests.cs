using System.ComponentModel;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockPartialFailureTests
{
    private static Task Run(Action<DockToolTests.Host> test) => GuiTestSession.Session.Dispatch(async () =>
    { await using var host = new DockToolTests.Host(); test(host); return true; }, CancellationToken.None);
    private static Task RunAsync(Func<DockToolTests.Host, Task> test) => GuiTestSession.Session.Dispatch(async () =>
    { await using var host = new DockToolTests.Host(); await test(host); return true; }, CancellationToken.None);

    private static void Move(DockToolTests.Host h)
    {
        var pane = h.Layout.Pane(DockShellState.VariablesId);
        h.Layout.Factory.MoveDockable((IDock)pane.Owner!,
            (IDock)h.Layout.Pane(DockShellState.DiagnosticsId).Owner!, pane, null);
    }

    [TestCase(DockShellState.DocumentId)]
    [TestCase(DockShellState.VariablesId)]
    public Task HostCreationFailureLatchesFaultAndPreservesBusinessSession(string id) => RunAsync(async h =>
    {
        const string script = "Print(0x(AA)) // R4: preserve without execution";
        h.Service.Mutate(() => h.Session.Editor.Text = script);
        var locator = h.Layout.Factory.HostWindowLocator!;
        var create = locator[nameof(IDockWindow)];
        var original = new Win32Exception(8, "R4 controlled host-construction failure");
        var editor = h.Session.Editor; var text = editor.Document; var undo = text.UndoStack;
        var save = h.Service.CaptureSaveIntent()!.Snapshot;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
        var stateRevision = InterpreterHostQueue.StateRevision;
        int calls = 0;
        locator[nameof(IDockWindow)] = () => { calls++; throw original; };
        try { Assert.That(Assert.Catch(() => h.Layout.Float(id)), Is.SameAs(original)); }
        finally { locator[nameof(IDockWindow)] = create; }
        Assert.That(calls, Is.EqualTo(1));
        var partial = h.Layout.Root.Windows!.Single();
        Assert.That(partial.Owner, Is.Null); Assert.That(partial.Factory, Is.Null); Assert.That(partial.Host, Is.Null);
        Assert.That(h.Layout.LayoutFaulted, Is.True);
        Assert.Throws<InvalidDataException>(() => h.Layout.Capture(DockPersistenceCleanupTests.Bounds));
        var before = DockShellLayout.VisibleTree(h.Layout.Root).ToArray();
        var owner = h.Layout.Pane(DockShellState.InfoId).Owner;
        h.Layout.Float(DockShellState.InfoId); Move(h);
        Assert.That(h.Layout.Reset(), Is.False);
        Assert.That(DockShellLayout.VisibleTree(h.Layout.Root), Is.EqualTo(before));
        Assert.That(h.Layout.Pane(DockShellState.InfoId).Owner, Is.SameAs(owner));
        Assert.That(h.Session.Editor, Is.SameAs(editor)); Assert.That(editor.Document, Is.SameAs(text));
        Assert.That(text.UndoStack, Is.SameAs(undo));
        Assert.That(h.Service.HasRegisteredEditor, Is.True);
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(save));
        Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(stateRevision));
        Assert.That(h.Service.EditorCoordinator.ObserverDepth, Is.Zero);
        var savePath = Path.Combine(Path.GetTempPath(), "cwb-r4-save-" + Guid.NewGuid() + ".sbk");
        try
        {
            await h.Service.CommitSaveAsync(h.Service.CaptureSaveIntent()!, new(savePath, false, null, PreserveText: true));
            Assert.That(File.ReadAllText(savePath), Is.EqualTo(script));
        }
        finally { File.Delete(savePath); }
        h.Window.Close();
        await DockEditorTests.Wait(() => !h.Window.IsVisible);
        Assert.That(h.Window.IsVisible, Is.False);
        Assert.That(h.Service.HasRegisteredEditor, Is.False);
        Assert.That(h.Layout.Factory.HostWindows, Is.Empty);
    });

    [Test]
    public Task FailureBeforeMutationDoesNotFaultValidLayout() => Run(h =>
    {
        var before = h.Layout.Capture(DockPersistenceCleanupTests.Bounds);
        var original = new InvalidOperationException("R4 before mutation");
        DockLifecycleTrace.Sink = (reason, _) => { if (reason == "MoveDockable:before") throw original; };
        try { Assert.That(Assert.Catch(() => Move(h)), Is.SameAs(original)); }
        finally { DockLifecycleTrace.Sink = null; }
        Assert.That(h.Layout.LayoutFaulted, Is.False);
        Assert.That(h.Layout.Capture(DockPersistenceCleanupTests.Bounds), Is.EqualTo(before));
        Move(h);
        Assert.That(h.Layout.Pane(DockShellState.VariablesId).Owner,
            Is.SameAs(h.Layout.Pane(DockShellState.DiagnosticsId).Owner));
    });

    [Test]
    public Task FailureAfterValidMovePreservesErrorWithoutLatchingFault() => Run(h =>
    {
        var original = new InvalidOperationException("R4 after valid move");
        EventHandler<DockableMovedEventArgs> handler = (_, _) => throw original;
        h.Layout.Factory.DockableMoved += handler;
        try { Assert.That(Assert.Catch(() => Move(h)), Is.SameAs(original)); }
        finally { h.Layout.Factory.DockableMoved -= handler; }
        Assert.That(h.Layout.LayoutFaulted, Is.False);
        Assert.That(h.Layout.Pane(DockShellState.VariablesId).Owner,
            Is.SameAs(h.Layout.Pane(DockShellState.DiagnosticsId).Owner));
        Assert.That(DockLayoutStore.Decode(h.Layout.Capture(DockPersistenceCleanupTests.Bounds)), Is.Not.Null);
        h.Layout.Float(DockShellState.InfoId);
        Assert.That(h.Layout.Root.Windows, Has.Count.EqualTo(1));
    });

    [Test]
    public Task SecondaryDiagnosticFailureCannotReplaceOriginalPartialFailure() => Run(h =>
    {
        var pane = h.Layout.Pane(DockShellState.DocumentId);
        var original = new InvalidOperationException("R4 original");
        var secondary = new InvalidOperationException("R4 secondary diagnostic");
        EventHandler<DockableRemovedEventArgs> handler = (_, e) => { if (e.Dockable == pane) throw original; };
        h.Layout.Factory.DockableRemoved += handler;
        DockLifecycleTrace.Sink = (reason, _) => { if (reason == "MoveDockable:after") throw secondary; };
        try
        {
            Assert.That(Assert.Catch(() => h.Layout.Factory.MoveDockable((IDock)pane.Owner!,
                (IDock)h.Layout.Pane(DockShellState.DiagnosticsId).Owner!, pane, null)), Is.SameAs(original));
        }
        finally { h.Layout.Factory.DockableRemoved -= handler; DockLifecycleTrace.Sink = null; }
        Assert.That(h.Layout.LayoutFaulted, Is.True);
        Assert.That(original.Data["Dock layout validation"], Is.TypeOf<InvalidDataException>());
        Assert.That(original.Data["Dock failure trace"], Is.SameAs(secondary));
    });

    [TestCase(false)]
    [TestCase(true)]
    public Task RealTraceWriteFailureDoesNotInterruptMove(bool lifecycleSink) => Run(h =>
    {
        if (!OperatingSystem.IsWindows()) { Assert.Ignore("Windows mandatory file byte locks are required."); return; }
        h.Service.Mutate(() => h.Session.Editor.Text = "Print(0x(BB)) // R4: trace must not execute or erase this");
        var path = Path.Combine(Path.GetTempPath(), "cwb-r4-" + Guid.NewGuid() + ".jsonl");
        var save = h.Service.CaptureSaveIntent()!.Snapshot;
        var editor = h.Session.Editor; var text = editor.Document; var undo = text.UndoStack;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
        var stateRevision = InterpreterHostQueue.StateRevision;
        try
        {
            using var output = lifecycleSink ? null : new DockTraceOutput(path);
            using var raw = lifecycleSink ? new StreamWriter(new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.Read)) { AutoFlush = true } : null;
            if (raw != null) raw.WriteLine("normal"); else output!.Write(() => "normal");
            using (var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                Assert.That(reader.ReadToEnd(), Does.Contain("normal"));
            using var blocker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var position = blocker.Length;
            blocker.Lock(position, 1);
            int attempts = 0;
            if (raw != null) DockLifecycleTrace.Sink = (_, _) => { attempts++; raw.WriteLine("locked"); };
            EventHandler<DockableRemovedEventArgs> handler = (_, _) =>
            {
                if (raw != null) DockLifecycleTrace.Write("removed");
                else output!.Write(() => { attempts++; return "locked"; });
            };
            // The writer is invoked at the actual post-removal boundary. For the
            // lifecycle variant, install its sink only inside that callback.
            var sink = DockLifecycleTrace.Sink;
            DockLifecycleTrace.Sink = null;
            EventHandler<DockableRemovedEventArgs> invoke = (_, e) =>
            { if (raw != null) DockLifecycleTrace.Sink = sink; handler(null, e); };
            h.Layout.Factory.DockableRemoved += invoke;
            try { Assert.DoesNotThrow(() => Move(h)); }
            finally
            {
                h.Layout.Factory.DockableRemoved -= invoke;
                blocker.Unlock(position, 1);
            }
            var failure = raw != null ? DockLifecycleTrace.Failure : output!.Failure;
            Assert.That(failure, Is.Not.Null); Assert.That(failure!.ErrorType, Is.EqualTo(typeof(IOException).FullName));
            var size = blocker.Length;
            if (raw != null) DockLifecycleTrace.Write("no retry");
            else output!.Write(() => { attempts++; return "no retry"; });
            Assert.That(attempts, Is.EqualTo(1)); Assert.That(blocker.Length, Is.EqualTo(size));
            output?.Dispose();
            Assert.That(blocker.Length, Is.EqualTo(size), "Closing a failed output must not retry buffered records.");
            Assert.That(h.Layout.LayoutFaulted, Is.False);
            Assert.That(h.Layout.Pane(DockShellState.VariablesId).Owner,
                Is.SameAs(h.Layout.Pane(DockShellState.DiagnosticsId).Owner));
            Assert.That(DockLayoutStore.Decode(h.Layout.Capture(DockPersistenceCleanupTests.Bounds)), Is.Not.Null);
            Assert.That(h.Session.Editor, Is.SameAs(editor)); Assert.That(editor.Document, Is.SameAs(text));
            Assert.That(text.UndoStack, Is.SameAs(undo));
            Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(save));
            Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
            Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(stateRevision));
        }
        finally { DockLifecycleTrace.Sink = null; File.Delete(path); }
    });

    private sealed class UnreadableChildren(IEnumerable<IDockable> children) : List<IDockable>(children), IEnumerable<IDockable>
    {
        internal bool FailReads;
        internal readonly Exception Error = new InvalidOperationException("R4 model cannot be read");
        IEnumerator<IDockable> IEnumerable<IDockable>.GetEnumerator() => FailReads ? throw Error : base.GetEnumerator();
    }

    [Test]
    public Task UnverifiableEndStateLatchesFaultWithoutReplacingOriginal() => Run(h =>
    {
        var children = new UnreadableChildren(h.Layout.Root.VisibleDockables!);
        h.Layout.Root.VisibleDockables = children;
        var original = new InvalidOperationException("R4 original valid move but unreadable state");
        EventHandler<DockableMovedEventArgs> handler = (_, _) => { children.FailReads = true; throw original; };
        h.Layout.Factory.DockableMoved += handler;
        try { Assert.That(Assert.Catch(() => Move(h)), Is.SameAs(original)); }
        finally { children.FailReads = false; h.Layout.Factory.DockableMoved -= handler; }
        Assert.That(h.Layout.LayoutFaulted, Is.True);
        Assert.That(original.Data["Dock layout validation"], Is.SameAs(children.Error));
        Assert.Throws<InvalidOperationException>(() => h.Layout.Capture(DockPersistenceCleanupTests.Bounds));
    });

    [Test]
    public Task GroupFailureIsValidatedAfterItsExistingCompletionScope() => Run(h =>
    {
        var factory = (EditorDockFactory)h.Layout.Factory;
        var original = new InvalidOperationException("R4 group member move failure");
        var pane = h.Layout.Pane(DockShellState.DocumentId);
        EventHandler<DockableRemovedEventArgs> handler = (_, e) => { if (e.Dockable == pane) throw original; };
        using (factory.BeginDockOperation(h.Layout.Root.VisibleDockables![0], h.Layout.Pane(DockShellState.DiagnosticsId), DockOperation.Fill))
        {
            h.Layout.Factory.DockableRemoved += handler;
            try
            {
                Assert.That(Assert.Catch(() => factory.MoveDockable((IDock)pane.Owner!,
                    (IDock)h.Layout.Pane(DockShellState.DiagnosticsId).Owner!, pane, null)), Is.SameAs(original));
            }
            finally { h.Layout.Factory.DockableRemoved -= handler; }
            Assert.That(h.Layout.LayoutFaulted, Is.False, "The existing synchronous completion still owns cleanup.");
        }
        Assert.That(h.Layout.LayoutFaulted, Is.True);
        Assert.That(original.Data["Dock layout validation"], Is.TypeOf<InvalidDataException>());
    });
}
