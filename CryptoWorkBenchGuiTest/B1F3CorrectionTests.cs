using Avalonia;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Utils;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class B1F3CorrectionTests
{
    private class Policy(Func<int, bool> insert, Func<ISegment, IEnumerable<ISegment>> delete) : IReadOnlySectionProvider
    {
        public bool CanInsert(int offset) => insert(offset);
        public IEnumerable<ISegment> GetDeletableSegments(ISegment segment) => delete(segment);
    }

    private sealed class VersionedPolicy(Func<int, bool> insert, Func<ISegment, IEnumerable<ISegment>> delete,
        Func<long> revision) : Policy(insert, delete), IRevisionedSectionProvider
    {
        public long PermissionRevision => revision();
    }

    private static void SeedRedo(Host h)
    {
        h.Area.PerformTextInput("!");
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Document.UndoStack.CanRedo, Is.True);
    }

    private static CompletionWindow Open(InfoCompletionController controller)
    {
        Assert.That(controller.OpenForTypedParenthesis(), Is.True);
        return (CompletionWindow)typeof(InfoCompletionController).GetField("_window",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(controller)!;
    }

    [TestCase(true, true)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(false, false)]
    public Task RectangularClipboardRequiresCoherentPermission(bool versioned, bool paste) => Ui(async () =>
    {
        await using var h = new Host("abc\ndef");
        SeedRedo(h);
        var original = h.Area.ReadOnlySectionProvider;
        var visits = new List<int>();
        long revision = 0;
        IEnumerable<ISegment> Delete(ISegment part)
        {
            visits.Add(part.Offset);
            if (part.Offset == 1) revision++;
            if (part.Offset != 5 || revision == 0) yield return part;
        }
        h.Area.ReadOnlySectionProvider = versioned
            ? new VersionedPolicy(_ => true, Delete, () => revision)
            : new Policy(_ => true, Delete);
        h.Area.Selection = new RectangleSelection(h.Area, new TextViewPosition(1, 2), new TextViewPosition(2, 3));
        var before = h.Registration.Snapshot();
        await h.Window.Clipboard!.SetTextAsync("Z");
        (paste ? ApplicationCommands.Paste : ApplicationCommands.Cut).Execute(null, h.Area);
        await h.Area.PendingClipboard;
        if (versioned) Assert.That(visits, Is.EqualTo(new[] { 5, 1 }));
        Assert.That(h.Registration.Snapshot(), Is.EqualTo(before));
        Assert.That(h.Editor.Document.IsInUpdate || h.Owner.Pending != 0, Is.False);
        ApplicationCommands.Redo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("abc\ndef!"));
        h.Area.ReadOnlySectionProvider = original;
        h.Area.Selection = new RectangleSelection(h.Area, new TextViewPosition(1, 2), new TextViewPosition(2, 3));
        await h.Window.Clipboard!.SetTextAsync("Z");
        ApplicationCommands.Paste.Execute(null, h.Area);
        await h.Area.PendingClipboard;
        Assert.That((h.Editor.Text, h.Owner.CanStartGui), Is.EqualTo(("aZc\ndZf!", true)));
    });

    [Test]
    public Task PasteAvailabilityCannotStartForeignInterpreterJob() => Ui(async () =>
    {
        await using var h = new Host("query");
        await using var foreign = new WorkbenchAutomationService();
        var dict = VariableDictionary.Instance();
        var old = dict.GetVariables().ToArray();
        var caller = WorkbenchAutomationService.LocalCaller;
        var request = new TextExecution(foreign.InstanceId, Guid.NewGuid(), "0", "VAR queryF3=0x(33)", TextExecutionMode.Submission);
        var original = h.Area.ReadOnlySectionProvider;
        try
        {
            long revision = InterpreterHostQueue.StateRevision;
            Task<Reply<ExecutionResult>>? attempt = null;
            h.Area.ReadOnlySectionProvider = new Policy(_ =>
            {
                attempt = foreign.ExecuteTextAsync(caller, request, default);
                return false;
            }, _ => []);
            Assert.That(ApplicationCommands.Paste.CanExecute(null, h.Area), Is.False);
            Assert.That((await attempt!).Error?.Code, Is.EqualTo(AutomationErrorCode.Busy));
            Assert.That((await foreign.GetExecutionAsync(caller, request.ExecutionId, default)).Error?.Code, Is.EqualTo(AutomationErrorCode.NotFound));
            Assert.That((dict.Contains("queryF3"), foreign.PresentationVersion, InterpreterHostQueue.StateRevision),
                Is.EqualTo((false, 0L, revision)));
            h.Area.ReadOnlySectionProvider = original;
            Assert.That(ApplicationCommands.Paste.CanExecute(null, h.Area), Is.True);
            Assert.That((await foreign.ExecuteTextAsync(caller, request, default)).Data?.ExecutionId, Is.EqualTo(request.ExecutionId));
            Assert.That(dict.Contains("queryF3"), Is.True);
        }
        finally { h.Area.ReadOnlySectionProvider = original; dict.Clear(); foreach (var item in old) dict.Add(item); }
    });

    [TestCase(true)] [TestCase(false)]
    public Task WholeLineCutSkipsEmptyRectangleFinalization(bool backwards) => Ui(async () =>
    {
        await using var h = new Host("abc\ndef");
        h.Editor.Options.CutCopyWholeLine = true;
        var start = new TextViewPosition(backwards ? 2 : 1, 2);
        var end = new TextViewPosition(backwards ? 1 : 2, 2);
        h.Area.Caret.Position = end;
        h.Area.Selection = new RectangleSelection(h.Area, start, end);
        Assert.That(h.Area.Selection.IsEmpty, Is.True);
        ApplicationCommands.Cut.Execute(null, h.Area);
        await h.Area.PendingClipboard;
        var expected = backwards ? "def" : "abc\n";
        Assert.That(await h.Window.Clipboard!.TryGetTextAsync(), Is.EqualTo(backwards ? "abc" + Environment.NewLine : "def"));
        Assert.That(h.Editor.Text, Is.EqualTo(expected));
        Assert.That(h.Owner.CanStartGui && !h.Editor.Document.IsInUpdate, Is.True);
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("abc\ndef"));
        ApplicationCommands.Redo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo(expected));
        h.Area.ClearSelection();
        h.Editor.CaretOffset = h.Editor.Text.Length;
        h.Area.PerformTextInput("!");
        Assert.That(h.Editor.Text, Is.EqualTo(expected + "!"));
    });

    [TestCase(false)] [TestCase(true)]
    public Task DisjointWholeLineCutRequiresStablePolicyAndIsAtomic(bool versioned) => Ui(async () =>
    {
        await using var h = new Host("abc\ndef");
        h.Editor.Options.CutCopyWholeLine = true;
        h.Editor.CaretOffset = 2;
        static IEnumerable<ISegment> Separate(ISegment _) => [new SimpleSegment(0, 1), new SimpleSegment(2, 1)];
        h.Area.ReadOnlySectionProvider = versioned ? new VersionedPolicy(_ => true, Separate, () => 0) : new Policy(_ => true, Separate);
        var before = h.Registration.Snapshot();
        ApplicationCommands.Cut.Execute(null, h.Area);
        await h.Area.PendingClipboard;
        Assert.That(await h.Window.Clipboard!.TryGetTextAsync(), Is.EqualTo("abc" + Environment.NewLine));
        if (!versioned)
        {
            Assert.That(h.Registration.Snapshot(), Is.EqualTo(before));
            Assert.That(h.Owner.CanStartGui, Is.True);
            return;
        }
        Assert.That(h.Editor.Text, Is.EqualTo("b\ndef"));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("abc\ndef"));
        ApplicationCommands.Redo.Execute(null, h.Area);
        Assert.That((h.Editor.Text, h.Owner.CanStartGui), Is.EqualTo(("b\ndef", true)));
    });

    [TestCase("protect")] [TestCase("stop")] [TestCase("dispose")] [TestCase("accept")]
    public Task CompletionAcceptsOnlyTheOriginalLiveGrant(string mode) => Ui(async () =>
    {
        await using var h = new Host("Info(AES");
        h.Editor.Document.UndoStack.SizeLimit = 2;
        SeedRedo(h);
        var before = h.Registration.Snapshot();
        long policyRevision = 0;
        h.Area.ReadOnlySectionProvider = new VersionedPolicy(_ => policyRevision == 0, s => policyRevision == 0 ? [s] : [], () => policyRevision);
        using var completion = new InfoCompletionController(h.Editor, new());
        var window = Open(completion);
        int updates = 0;
        void Revoke(object? sender, EventArgs args)
        {
            updates++;
            if (mode == "protect") policyRevision++;
            if (mode == "stop") h.Service.StopAccepting();
            if (mode == "dispose") completion.Dispose();
        }
        h.Editor.Document.UpdateStarted += Revoke;
        try { window.CompletionList.RequestInsertion(EventArgs.Empty); }
        finally { h.Editor.Document.UpdateStarted -= Revoke; }
        Assert.That(updates, Is.EqualTo(1));
        Assert.That((h.Editor.Document.IsInUpdate, h.Owner.Pending, h.Editor.Document.UndoStack.SizeLimit), Is.EqualTo((false, 0, 2)));
        if (mode == "accept")
        {
            var inserted = h.Editor.Text;
            Assert.That(inserted, Does.StartWith("Info(AES-"));
            Assert.That(h.Owner.LastReceipt?.HistoryEffect, Is.EqualTo(EditorHistoryEffect.Applied));
            ApplicationCommands.Undo.Execute(null, h.Area);
            Assert.That(h.Editor.Text, Is.EqualTo(before.Text));
            ApplicationCommands.Redo.Execute(null, h.Area);
            Assert.That(h.Editor.Text, Is.EqualTo(inserted));
            return;
        }
        Assert.That(h.Registration.Snapshot(), Is.EqualTo(before));
        Assert.That(h.Owner.LastReceipt?.HistoryEffect, Is.EqualTo(EditorHistoryEffect.None));
        if (mode == "stop")
        {
            h.Area.PerformTextInput("bad");
            Assert.That(h.Editor.Text, Is.EqualTo(before.Text));
            await h.Service.DisposeAsync();
        }
        await using var fresh = new WorkbenchAutomationService();
        if (mode == "stop") fresh.AttachEditor(h.Editor, null!);
        ApplicationCommands.Redo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("Info(AES!"));
    });

    [Test]
    public Task KnownAttachCannotReturnRetiredRegistration() => Ui(async () =>
    {
        await using var h = new Host("A");
        var b = new WorkbenchTextEditor { Text = "B" };
        var rb = h.Service.AttachEditor(b, null!, true)!;
        Assert.That(h.Service.AttachEditor(h.Editor, null!), Is.SameAs(h.Registration));
        h.Service.ActivateEditor(rb);
        bool disposed = false;
        void Stop()
        {
            if (disposed || !h.Owner.Targets(h.Editor)) return;
            disposed = true;
            h.Service.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        h.Owner.WorkspaceChanged += Stop;
        try { Assert.Throws<ExecutionBusyException>(() => h.Service.AttachEditor(h.Editor, null!)); }
        finally { h.Owner.WorkspaceChanged -= Stop; }
        Assert.That(disposed && !rb.Attached && !h.Registration.Attached, Is.True);
        Assert.That(h.Editor.Coordinator, Is.Null);
        h.Area.PerformTextInput("bad");
        Assert.That(h.Editor.Text, Is.EqualTo("A"));
        await using var fresh = new WorkbenchAutomationService();
        fresh.AttachEditor(h.Editor, null!);
        h.Area.PerformTextInput("z");
        Assert.That(h.Editor.Text, Is.EqualTo("Az"));
    });

    private sealed class PrivateTimer : ITimer
    {
        internal bool Disposed { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class DeadlineClock : TimeProvider
    {
        internal bool Expire = true;
        internal readonly PrivateTimer Timer = new();
        internal Guid AttemptId;
        internal TimeSpan? Due;
        private int samples;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => samples++ == 0 ? 0 : TimeSpan.FromSeconds(31).Ticks;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (!Expire) return base.CreateTimer(callback, state, dueTime, period);
            Due = dueTime;
            AttemptId = (Guid)state!.GetType().GetProperty("Id")!.GetValue(state)!;
            if (dueTime == TimeSpan.Zero) callback(state);
            return Timer;
        }
    }

    [Test]
    public Task PrivateReturnDeadlineRejectsBothJobAndNewline() => Ui(async () =>
    {
        await using var h = new Host("host");
        var clock = new DeadlineClock();
        await using var service = new WorkbenchAutomationService(clock);
        var history = new HistoryService();
        using var vm = new CryptoScriptEditViewModel(new(), new(), new(), new(history), null!, history, service);
        var view = new CryptoScriptEditView(vm);
        h.Window.Content = view;
        h.Window.UpdateLayout();
        var ed = view.FindControl<WorkbenchTextEditor>("Editor")!;
        ed.TextArea.Focus();
        var caller = WorkbenchAutomationService.LocalCaller;
        var dict = VariableDictionary.Instance();
        var old = dict.GetVariables().ToArray();
        try
        {
            const string line = "VAR privateF3=0x(42)";
            ed.Text = line;
            ed.CaretOffset = line.Length;
            long revision = InterpreterHostQueue.StateRevision;
            ed.TextArea.PerformTextInput("\n");
            await view.PendingExecution;
            var status = (await service.GetStatusAsync(caller, default)).Data!;
            Assert.That(clock.Due, Is.EqualTo(TimeSpan.Zero));
            Assert.That(clock.AttemptId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(clock.Timer.Disposed, Is.True);
            Assert.That((await service.GetExecutionAsync(caller, clock.AttemptId, default)).Error?.Code, Is.EqualTo(AutomationErrorCode.NotFound));
            Assert.That(status.ActiveExecutionId ?? status.LastExecutionId, Is.Null);
            Assert.That((ed.Text, dict.Contains("privateF3"), service.IsBusy, status.StateVersion),
                Is.EqualTo((line, false, false, "0")));
            Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
            clock.Expire = false;
            ed.TextArea.PerformTextInput("\n");
            await view.PendingExecution;
            Assert.That(dict.Contains("privateF3"), Is.True);
            Assert.That(ed.Text, Is.EqualTo(line + Environment.NewLine));
            Assert.That(service.PresentationVersion, Is.EqualTo(1));
        }
        finally { h.Window.Content = null; dict.Clear(); foreach (var item in old) dict.Add(item); }
    });

    [Test]
    public Task InvalidBooleanKeepsOtherDraftWritable() => Ui(async () =>
    {
        await using var h = new Host("A");
        var dict = VariableDictionary.Instance();
        var old = dict.GetVariables().ToArray();
        try
        {
            var flag = new ValVariableDeclaration(true) { Id = "flagF3" };
            var value = new StringVariableDeclaration { Id = "otherF3", Value = "0x(01)" };
            h.Service.Mutate(() => { dict.Add(flag); dict.Add(value); });
            var flags = new VariableViewModel { Automation = h.Service };
            var others = new VariableViewModel { Automation = h.Service };
            flags.SetupVariables(); others.SetupVariables();
            var row = flags.SelectedVariable = flags.DataVariables.Single(v => v.Identifier == flag.Id);
            var other = others.SelectedVariable = others.DataVariables.Single(v => v.Identifier == value.Id);
            other.Value = "0x(02)";
            row.Value = "invalid";
            var versions = (h.Service.PresentationVersion, InterpreterHostQueue.StateRevision);
            flags.ModifyValueCommand.Execute(row);
            Assert.That((h.Service.PresentationVersion, InterpreterHostQueue.StateRevision), Is.EqualTo(versions));
            Assert.That((flag.Value, flags.DataVariables.Single(v => v.Identifier == flag.Id).Value), Is.EqualTo(("true","true")));
            others.ModifyValueCommand.Execute(other);
            Assert.That(value.Value, Is.EqualTo("0x(02)"));
            flags.SetupVariables();
            row = flags.SelectedVariable = flags.DataVariables.Single(v => v.Identifier == flag.Id);
            row.Value = "false";
            flags.ModifyValueCommand.Execute(row);
            Assert.That(flag.Value, Is.EqualTo("false"));
        }
        finally { dict.Clear(); foreach (var item in old) dict.Add(item); }
    });

    [Test]
    public Task CompletionFinishObserverFailureAllowsNativeDrainToComplete() => Ui(async () =>
    {
        await using var h = new Host("Info(AES");
        using var completion = new InfoCompletionController(h.Editor, new());
        var window = Open(completion);
        var list = window.CompletionList.ListBox;
        var doc = h.Editor.Document;
        int failures = 0;
        void Fail(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property != ItemsControl.ItemsSourceProperty) return;
            failures++;
            throw new InvalidOperationException("completion list refresh failed");
        }
        doc.BeginUpdate();
        doc.Insert(doc.TextLength, "-");
        var certificate = h.Owner.BeginDrain();
        Assert.That(certificate.Completion.IsCompleted, Is.False);
        var before = h.Owner.ObserverFailures;
        list.PropertyChanged += Fail;
        try { doc.EndUpdate(); }
        finally { list.PropertyChanged -= Fail; }
        await certificate.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(failures, Is.GreaterThan(0));
        Assert.That(h.Owner.ObserverFailures, Is.GreaterThan(before));
        Assert.That(doc.IsInUpdate || h.Owner.Pending != 0, Is.False);
        Assert.That(h.Owner.ResumeGui(certificate), Is.True);
        completion.Dispose();
        h.Area.PerformTextInput("z");
        Assert.That(h.Editor.Text, Does.Contain("z"));
    });
}
