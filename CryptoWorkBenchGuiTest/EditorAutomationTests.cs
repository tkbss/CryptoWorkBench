using System.Collections.Immutable;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class EditorAutomationTests
{
    public sealed class TestApp
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    private sealed class Harness : IAsyncDisposable
    {
        internal readonly WorkbenchAutomationService Service = new();
        internal readonly HistoryService History = new();
        internal readonly StatusViewModel Status = new();
        internal readonly VariableViewModel Variables = new();
        internal readonly CryptoScriptEditViewModel Vm;
        internal readonly CryptoScriptEditView View;
        internal readonly Window Window;
        internal readonly WorkbenchTextEditor Editor;
        internal readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "MCP014-" + Guid.NewGuid());
        private readonly VariableDeclaration[] previous = VariableDictionary.Instance().GetVariables().ToArray();
        internal Harness(bool attach = true)
        {
            VariableDictionary.Instance().Clear();
            Directory.CreateDirectory(DirectoryPath);
            Vm = new(Status, Variables, new InfoViewModel(), new MainViewModel(), null!, History, Service);
            View = new(Vm);
            Editor = View.FindControl<WorkbenchTextEditor>("Editor")!;
            Window = new() { Content = View };
            if (attach) Window.Show();
        }
        internal string PathFor(string name = "book.sbk") => Path.Combine(DirectoryPath, name);
        internal Task<EditorSnapshot> Snapshot() => Read();
        private async Task<EditorSnapshot> Read() => (await Service.GetEditorAsync(Caller, default)).Data!;
        internal async Task<EditorEdit> Edit(EditorEditMode mode, string text, int? offset = null)
        {
            var snapshot = await Snapshot();
            return new(Service.InstanceId, snapshot.EditorId, snapshot.Revision, mode, text, offset);
        }
        internal async Task<EditorExecution> Execution()
        {
            var snapshot = await Snapshot();
            return new(Service.InstanceId, Guid.NewGuid(), snapshot.EditorId, snapshot.Revision,
                (await Service.GetStatusAsync(Caller, default)).Data!.StateVersion);
        }
        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync();
            Window.Close(); Vm.Dispose();
            VariableDictionary.Instance().Clear();
            foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
            Directory.Delete(DirectoryPath, true);
        }
    }

    private static AutomationCaller Caller => WorkbenchAutomationService.LocalCaller;
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task Headless(Func<Task> test)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
        await session.Dispatch(async () => { await test(); return true; }, CancellationToken.None);
    }
    private sealed class Approval(Func<EditorReplaceApproval, Task<bool?>> decide) : IEditorReplaceConfirmation
    {
        internal EditorReplaceApproval? Last;
        public Task<bool?> ConfirmAsync(TextEditor editor, EditorReplaceApproval approval, CancellationToken ct)
        { Last = approval; return decide(approval); }
    }

    [Test]
    public Task ActualReturnKeepsPendingExecutionAnchorOutputAndRevision() => Headless(async () =>
    {
        await using var h = new Harness();
        h.Editor.Text = "Print(0x(AB))"; h.Editor.CaretOffset = h.Editor.Document.TextLength;
        var before = await h.Snapshot();
        h.Editor.TextArea.PerformTextInput("\n");
        await h.View.PendingExecution;
        var after = await h.Snapshot();
        Assert.That(after.Text, Does.Contain("out: 0x(AB)"));
        Assert.That(after.Text.Split("out: 0x(AB)").Length, Is.EqualTo(2));
        Assert.That(long.Parse(after.Revision), Is.GreaterThan(long.Parse(before.Revision)));
    });

    [Test]
    public Task WholeEditorPrintIsInsertedOnceWithoutDuplicatingSource() => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "Print(0x(AB))";
        var before = await h.Snapshot();
        var request = await h.Execution();
        var result = await h.Service.ExecuteEditorAsync(Caller, request, default);
        Assert.That(result.Data!.State, Is.EqualTo(ExecutionState.Completed));
        Assert.That(h.Editor.Text, Is.EqualTo("Print(0x(AB))\nout: 0x(AB)"));
        Assert.That(long.Parse((await h.Snapshot()).Revision), Is.GreaterThan(long.Parse(before.Revision)));
        Assert.That(result.Data.Outputs.Length, Is.EqualTo(1));
        Assert.That(result.Data.Outputs[0].Redacted, Is.True);
    });

    [TestCase(false)] [TestCase(true)]
    public Task ActualReplaceDialogShowsExactBeforeAfterAndHonoursChoice(bool accept) => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "before secret";
        var work = h.Service.UpdateEditorAsync(Caller, await h.Edit(EditorEditMode.Replace, "after secret"), default);
        for (int i = 0; i < 100 && !h.Window.OwnedWindows.Any() && !work.IsCompleted; i++) await Task.Delay(10);
        var dialog = h.Window.OwnedWindows.Single();
        var grid = (Grid)dialog.Content!;
        Assert.That(grid.Children.OfType<TextBox>().Select(box => box.Text), Is.EqualTo(new[] { "before secret", "after secret" }));
        dialog.Close(accept);
        var result = await work;
        Assert.That(result.Ok, Is.EqualTo(accept));
        Assert.That(h.Editor.Text, Is.EqualTo(accept ? "after secret" : "before secret"));
        Assert.That(h.Service.IsBusy, Is.False);
    });

    [Test]
    public Task EarlierChangeSubscribersCannotObserveNewTextWithOldRevision() => Headless(async () =>
    {
        await using var h = new Harness(false);
        EditorSnapshot? duringText = null;
        EditorSnapshot? duringSwap = null;
        bool removeDuringChange = false;
        h.Editor.Document.Changed += (_, _) =>
        {
            duringText = h.Service.GetEditorAsync(Caller, default).GetAwaiter().GetResult().Data;
            if (removeDuringChange) h.Editor.Document = null!;
        };
        h.Editor.DocumentChanged += (_, _) => duringSwap = h.Service.GetEditorAsync(Caller, default).GetAwaiter().GetResult().Data;
        h.Window.Show();
        var before = await h.Snapshot();
        h.Editor.Document.Insert(0, "new");
        Assert.That(duringText, Is.EqualTo(await h.Snapshot()));
        Assert.That(duringText!.Revision, Is.Not.EqualTo(before.Revision));
        removeDuringChange = true;
        h.Editor.Document.Insert(0, "change then swap");
        Assert.That((await h.Service.GetEditorAsync(Caller, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.EditorUnavailable));
        h.Editor.Document = new TextDocument("swapped");
        Assert.That(duringSwap, Is.EqualTo(await h.Snapshot()));
        Assert.That(duringSwap!.EditorId, Is.Not.EqualTo(before.EditorId));
    });

    [Test]
    public Task EditsRejectWrongInstanceEditorStaleRevisionAndConcurrentGuiAdmission() => Headless(async () =>
    {
        await using var h = new Harness();
        var request = await h.Edit(EditorEditMode.Append, "edit");
        Assert.That((await h.Service.UpdateEditorAsync(Caller, request with { InstanceId = Guid.NewGuid() }, default)).Error!.Code,
            Is.EqualTo(AutomationErrorCode.InstanceMismatch));
        Assert.That((await h.Service.UpdateEditorAsync(Caller, request with { EditorId = Guid.NewGuid() }, default)).Error!.Code,
            Is.EqualTo(AutomationErrorCode.EditorUnavailable));
        using (InterpreterHostQueue.Reserve())
            Assert.That((await h.Service.UpdateEditorAsync(Caller, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Busy));
        h.Editor.Document.Insert(0, "typed");
        var before = await h.Snapshot();
        Assert.That((await h.Service.UpdateEditorAsync(Caller, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.RevisionConflict));
        Assert.That(await h.Snapshot(), Is.EqualTo(before));
    });

    [Test]
    public Task LateDetachOfOldViewCannotUnregisterSuccessorView() => Headless(async () =>
    {
        await using var h = new Harness();
        var successor = new CryptoScriptEditView(h.Vm);
        var window = new Window { Content = successor }; window.Show();
        try
        {
            var before = await h.Snapshot();
            h.Window.Content = null;
            Assert.That((await h.Snapshot()).EditorId, Is.EqualTo(before.EditorId));
            Assert.That(h.Vm.TextEditor, Is.SameAs(successor.FindControl<TextEditor>("Editor")));
        }
        finally { window.Close(); }
    });

    [Test]
    public Task MissingDocumentIsUnavailableAndServiceDisposalUnregisters() => Headless(async () =>
    {
        await using var h = new Harness();
        var before = await h.Snapshot();
        h.Editor.Document = null!;
        Assert.That((await h.Service.GetStatusAsync(Caller, default)).Data!.EditorAvailable, Is.False);
        Assert.That((await h.Service.GetEditorAsync(Caller, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.EditorUnavailable));
        h.Editor.Document = new TextDocument("restored");
        Assert.That((await h.Snapshot()).EditorId, Is.Not.EqualTo(before.EditorId));
        await h.Service.DisposeAsync();
        h.Editor.Document.Insert(0, "detached");
        Assert.That((await h.Service.GetStatusAsync(Caller, default)).Data!.EditorAvailable, Is.False);
    });

    [Test]
    public Task AttachDetachAndDocumentSwapHaveDistinctIdentities() => Headless(async () =>
    {
        await using var h = new Harness(false);
        Assert.That((await h.Service.GetEditorAsync(Caller, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.EditorUnavailable));
        h.Window.Show();
        var initial = await h.Snapshot();
        Assert.That((await h.Service.GetStatusAsync(Caller, default)).Data!.EditorAvailable, Is.True);
        h.Editor.Document = new TextDocument("new document");
        var swapped = await h.Snapshot();
        Assert.That(swapped.EditorId, Is.Not.EqualTo(initial.EditorId));
        Assert.That(long.Parse(swapped.Revision), Is.GreaterThan(long.Parse(initial.Revision)));
        h.Window.Content = null;
        var detachedDocument = h.Editor.Document;
        detachedDocument.Insert(0, "detached");
        Assert.That((await h.Service.GetEditorAsync(Caller, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.EditorUnavailable));
        h.Window.Content = h.View;
        Assert.That((await h.Snapshot()).EditorId, Is.Not.EqualTo(swapped.EditorId));
    });

    [Test]
    public Task RevisionsTrackTypingUndoRedoHistoryAndOutputsWithoutReadEffects() => Headless(async () =>
    {
        await using var h = new Harness();
        h.Editor.IsModified = false;
        var initial = await h.Snapshot();
        Assert.That(initial.DirtyState, Is.EqualTo(DirtyState.Unknown));
        Assert.That(await h.Snapshot(), Is.EqualTo(initial));
        h.Editor.Document.Insert(0, "one");
        var typed = await h.Snapshot();
        Assert.That(long.Parse(typed.Revision), Is.GreaterThan(long.Parse(initial.Revision)));
        h.Editor.Document.UndoStack.Undo(); var undone = await h.Snapshot();
        h.Editor.Document.UndoStack.Redo(); var redone = await h.Snapshot();
        Assert.That(long.Parse(undone.Revision), Is.GreaterThan(long.Parse(typed.Revision)));
        Assert.That(long.Parse(redone.Revision), Is.GreaterThan(long.Parse(undone.Revision)));
        h.History.AddLine("history"); h.History.AddLine("second"); h.History.LineBack();
        var history = await h.Snapshot();
        Assert.That(history.Text, Is.EqualTo("history"));
        h.Editor.Document.Insert(h.Editor.Document.TextLength, "\nout: generated");
        var output = await h.Snapshot();
        Assert.That(long.Parse(output.Revision), Is.GreaterThan(long.Parse(history.Revision)));
        Assert.That(output.DirtyState, Is.EqualTo(DirtyState.Dirty));
        Assert.That(await h.Snapshot(), Is.EqualTo(output));
    });

    [TestCase(EditorEditMode.Insert)]
    [TestCase(EditorEditMode.Append)]
    [TestCase(EditorEditMode.Replace)]
    public Task EditsAreExactSingleUndoGroupsAndNoOpsPreserveRevision(EditorEditMode mode) => Headless(async () =>
    {
        await using var h = new Harness();
        h.Service.ReplaceConfirmation = new Approval(_ => Task.FromResult<bool?>(true));
        h.Editor.Text = "ab"; h.Editor.Document.UndoStack.ClearAll();
        var request = await h.Edit(mode, "XY", mode == EditorEditMode.Insert ? 1 : null);
        var result = await h.Service.UpdateEditorAsync(Caller, request, default);
        Assert.That(result.Ok, Is.True);
        string expected = mode switch { EditorEditMode.Insert => "aXYb", EditorEditMode.Append => "abXY", _ => "XY" };
        Assert.That(h.Editor.Text, Is.EqualTo(expected));
        h.Editor.Document.UndoStack.Undo(); Assert.That(h.Editor.Text, Is.EqualTo("ab"));
        Assert.That(h.Editor.Document.UndoStack.CanUndo, Is.False);
        h.Editor.Document.UndoStack.Redo(); Assert.That(h.Editor.Text, Is.EqualTo(expected));
        var before = await h.Snapshot();
        var noop = await h.Edit(mode, mode == EditorEditMode.Replace ? expected : "", mode == EditorEditMode.Insert ? 0 : null);
        h.Service.ReplaceConfirmation = new Approval(_ => throw new AssertionException("No-op must not ask"));
        Assert.That((await h.Service.UpdateEditorAsync(Caller, noop, default)).Ok, Is.True);
        Assert.That(await h.Snapshot(), Is.EqualTo(before));
    });

    [TestCase(-1, false)] [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, false)]
    [TestCase(3, true)] [TestCase(4, false)] [TestCase(5, true)] [TestCase(6, true)] [TestCase(7, false)]
    public Task Utf16OffsetsDoNotSplitSurrogatesOrCrLf(int offset, bool valid) => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "a\uD83D\uDE00\r\nb";
        var before = await h.Snapshot();
        var request = new EditorEdit(h.Service.InstanceId, before.EditorId, before.Revision, EditorEditMode.Insert, "!", offset);
        var result = await h.Service.UpdateEditorAsync(Caller, request, default);
        Assert.That(result.Ok, Is.EqualTo(valid));
        if (valid) Assert.That(h.Editor.Text, Is.EqualTo(before.Text.Insert(offset, "!")));
        else Assert.That(await h.Snapshot(), Is.EqualTo(before));
        Assert.That((await h.Snapshot()).Length, Is.EqualTo(h.Editor.Text.Length));
    });

    [TestCase(false)] [TestCase(true)]
    public Task ReplaceApprovalBindsContentAndRechecksRevision(bool accept) => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "before";
        var entered = Signal(); var release = new TaskCompletionSource<bool?>();
        var approval = new Approval(_ => { entered.SetResult(); return release.Task; }); h.Service.ReplaceConfirmation = approval;
        var request = await h.Edit(EditorEditMode.Replace, "after");
        var work = h.Service.UpdateEditorAsync(Caller, request, default);
        await entered.Task;
        Assert.That(approval.Last!.Before.Text, Is.EqualTo("before"));
        Assert.That(approval.Last.Edit, Is.EqualTo(request));
        Assert.That(approval.Last.Principal, Is.EqualTo(Caller.PrincipalId));
        h.Editor.Document.Insert(0, "typed ");
        release.SetResult(accept);
        var result = await work;
        Assert.That(result.Error!.Code, Is.EqualTo(accept ? AutomationErrorCode.RevisionConflict : AutomationErrorCode.ConfirmationDenied));
        Assert.That(h.Editor.Text, Is.EqualTo("typed before"));
        Assert.That(h.Service.IsBusy, Is.False);
    });

    [Test]
    public Task ReplaceCannotApplyToSuccessorAndMissingOwnerFailsClosed() => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "old";
        var release = new TaskCompletionSource<bool?>();
        h.Service.ReplaceConfirmation = new Approval(_ => release.Task);
        var work = h.Service.UpdateEditorAsync(Caller, await h.Edit(EditorEditMode.Replace, "replacement"), default);
        h.Window.Content = null; h.Window.Content = h.View;
        release.SetResult(true);
        Assert.That((await work).Error!.Code, Is.EqualTo(AutomationErrorCode.EditorUnavailable));
        Assert.That(h.Editor.Text, Is.EqualTo("old"));
        h.Service.ReplaceConfirmation = new Approval(_ => Task.FromResult<bool?>(null));
        Assert.That((await h.Service.UpdateEditorAsync(Caller, await h.Edit(EditorEditMode.Replace, "new"), default)).Error!.Code,
            Is.EqualTo(AutomationErrorCode.ConfirmationUnavailable));
    });

    [Test]
    public Task SnapshotIsAtomicAcrossBackgroundReadsAndDispatcherWrites() => Headless(async () =>
    {
        await using var h = new Harness();
        var before = await h.Snapshot();
        var reads = Task.Run(async () =>
        {
            for (int i = 0; i < 40; i++)
            {
                var snapshot = (await h.Service.GetEditorAsync(Caller, default)).Data!;
                Assert.That(snapshot.Length, Is.EqualTo(snapshot.Text.Length));
                Assert.That(snapshot.EditorId, Is.EqualTo(before.EditorId));
                Assert.That(snapshot.Text, Is.EqualTo(new string('x', int.Parse(snapshot.Revision) - int.Parse(before.Revision))));
            }
        });
        for (int i = 0; i < 40; i++) { h.Editor.Document.Insert(0, "x"); await Task.Yield(); }
        await reads;
    });

    [Test]
    public Task ExecuteEditorUsesImmutableBookAndRegisterWithNoSourceReinsertion() => Headless(async () =>
    {
        await using var h = new Harness();
        h.Editor.Text = "VAR editorOne=0x(01)\nout: ignored\nVAR editorTwo=Compare(editorOne,editorOne)";
        var request = await h.Execution();
        var execution = h.Service.ExecuteEditorAsync(Caller, request, default);
        h.Editor.Text = "user text while running";
        var result = await execution;
        Assert.That(result.Data!.State, Is.EqualTo(ExecutionState.Completed));
        Assert.That(result.Data.Source, Is.EqualTo(ExecutionSource.Editor));
        Assert.That(result.Data.EditorId, Is.EqualTo(request.EditorId));
        Assert.That(result.Data.SourceRevision, Is.EqualTo(request.ExpectedRevision));
        Assert.That(result.Data.Variables.Items.Select(v => v.Identifier), Does.Contain("editorOne").And.Contain("editorTwo"));
        Assert.That(h.Editor.Text, Is.EqualTo("user text while running"));
        Assert.That(h.Variables.DataVariables.Select(v => v.Identifier), Does.Contain("editorTwo"));
        Assert.That((await h.Service.ExecuteEditorAsync(Caller, request, default)).Data, Is.EqualTo(result.Data));
        Assert.That((await h.Service.ExecuteEditorAsync(Caller, request with { ExpectedRevision = "999" }, default)).Error!.Code,
            Is.EqualTo(AutomationErrorCode.InvalidArgument));
    });

    [Test]
    public Task EditorExecutionRechecksStateAndRevisionAndRejectsExternalExecution() => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "VAR blocked=0x(01)";
        var request = await h.Execution();
        h.Service.Mutate(() => { });
        Assert.That((await h.Service.ExecuteEditorAsync(Caller, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.StateConflict));
        h.Editor.Text += " ";
        Assert.That((await h.Service.ExecuteEditorAsync(Caller, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.RevisionConflict));
        var epoch = Guid.NewGuid(); h.Service.ActivateMcp(epoch);
        var external = Caller with { Source = CallerSource.Mcp, ActivationEpoch = epoch };
        Assert.That((await h.Service.ExecuteEditorAsync(external, await h.Execution(), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        Assert.That(VariableDictionary.Instance().Contains("blocked"), Is.False);
        var noRead = Caller with { Permissions = ImmutableHashSet<string>.Empty };
        Assert.That((await h.Service.GetEditorAsync(noRead, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
    });

    [TestCase("picker")] [TestCase("overwrite")]
    public Task SaveIntentPrecedesDialogAndStaleRevisionNeverDeletes(string phase) => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "confirmed";
        string path = h.PathFor(); File.WriteAllText(path, "sentinel");
        var picker = Signal(); var overwrite = Signal();
        int deletes = 0;
        var work = h.Vm.SaveScriptBookAsync(async () =>
        {
            await picker.Task;
            await overwrite.Task;
            return new(path, true, () => { deletes++; File.Delete(path); return Task.CompletedTask; });
        });
        if (phase == "overwrite") { picker.SetResult(); await Task.Yield(); }
        h.Editor.Document.Insert(0, "changed ");
        if (phase == "picker") picker.SetResult();
        overwrite.SetResult();
        await work;
        Assert.That(h.Status.StatusString, Does.Contain("RevisionConflict"));
        Assert.That(deletes, Is.Zero); Assert.That(File.ReadAllText(path), Is.EqualTo("sentinel"));
        Assert.That(h.Service.IsBusy, Is.False);
    });

    [TestCase("typing")] [TestCase("paste")] [TestCase("undo")] [TestCase("redo")]
    [TestCase("history")] [TestCase("output")] [TestCase("swap")] [TestCase("vm-swap")]
    public Task SaveLockCoversActualMutationsDuringDelayedDelete(string mutation) => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "first"; h.Editor.Document.Insert(5, " last");
        if (mutation == "redo") h.Editor.Document.UndoStack.Undo();
        var before = await h.Snapshot();
        h.History.AddLine("history"); h.History.AddLine("second");
        string path = h.PathFor(); File.WriteAllText(path, "sentinel");
        var entered = Signal(); var release = Signal();
        var intent = h.Vm.CaptureSaveIntent()!;
        var work = h.Vm.CommitSaveAsync(intent, new(path, true, async () => { entered.SetResult(); await release.Task; File.Delete(path); }));
        await entered.Task;
        try
        {
            Assert.That(h.Editor.IsReadOnly, Is.True); Assert.That(h.Service.IsBusy, Is.True);
            Assert.That((await h.Service.GetStatusAsync(Caller, default)).Data!.MutationBusy, Is.True);
            Action change = mutation switch
            {
                "typing" => () => h.Editor.Document.Insert(0, "typed"),
                "paste" => () => h.Editor.SelectedText = "pasted",
                "undo" => () => { h.Editor.Undo(); h.Editor.Undo(); },
                "redo" => () => { h.Editor.Redo(); h.Editor.Redo(); },
                "history" => h.History.LineBack,
                "output" => () => h.Editor.Document.Insert(h.Editor.Document.TextLength, "\nout: late"),
                "swap" => () => h.Editor.Document = new TextDocument("swapped"),
                _ => () => h.Vm.TextEditor = new WorkbenchTextEditor()
            };
            try { change(); }
            catch (ExecutionBusyException) { }
            catch (InvalidOperationException) when (mutation is "undo" or "redo") { }
            Assert.That(await h.Snapshot(), Is.EqualTo(before));
            Assert.That((await h.Service.UpdateEditorAsync(Caller, await h.Edit(EditorEditMode.Append, "mcp"), default)).Error!.Code,
                Is.EqualTo(AutomationErrorCode.Busy));
            Assert.Throws<ExecutionBusyException>(() => h.Service.Mutate(() => { }));
            bool responsive = false; await Dispatcher.UIThread.InvokeAsync(() => responsive = true); Assert.That(responsive, Is.True);
        }
        finally { release.TrySetResult(); }
        Assert.That(await work, Is.Null);
        Assert.That(File.ReadAllText(path), Is.EqualTo(before.Text + "\n"));
        Assert.That(h.Editor.IsReadOnly, Is.False); Assert.That(h.Service.IsBusy, Is.False);
        Assert.That(await h.Snapshot(), Is.EqualTo(before));
        if (mutation == "undo") { h.Editor.Document.UndoStack.Undo(); Assert.That(h.Editor.Text, Is.EqualTo("first")); }
        if (mutation == "redo") { h.Editor.Document.UndoStack.Redo(); Assert.That(h.Editor.Text, Is.EqualTo("first last")); }
        h.Editor.Document.Insert(0, "after"); Assert.That(h.Editor.Text, Does.StartWith("after"));
    });

    [Test]
    public Task SavePreservesAsciiWhitespaceLfAppendAndConservativeDirty() => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "a\r\n \t\r\n\n\u00FC\uD83D\uDE00\nlast";
        string path = h.PathFor(); File.WriteAllText(path, "prefix\n");
        var before = await h.Snapshot();
        Assert.That(await h.Vm.CommitSaveAsync(h.Vm.CaptureSaveIntent()!, new(path, false, null)), Is.Null);
        byte[] expected = System.Text.Encoding.ASCII.GetBytes("prefix\na\n\u00FC\uD83D\uDE00\nlast\n");
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(expected));
        Assert.That(await h.Snapshot(), Is.EqualTo(before));
        h.Vm.SaveScriptBook(path);
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(expected.Concat(System.Text.Encoding.ASCII.GetBytes("a\n\u00FC\uD83D\uDE00\nlast\n"))));
    });

    [TestCase("delete")] [TestCase("write")]
    public Task SaveFailuresReleaseBothLocksWithoutRollbackOrCleanClaim(string failure) => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "confirmed";
        string path = h.PathFor(); File.WriteAllText(path, "sentinel");
        var before = await h.Snapshot();
        var intent = h.Vm.CaptureSaveIntent()!;
        var target = new WorkbenchAutomationService.SaveTarget(path, true, () =>
        {
            if (failure == "delete") throw new IOException("injected delete failure");
            File.Delete(path); Directory.CreateDirectory(path); // File.Open will fail after deletion.
            return Task.CompletedTask;
        });
        await Assert.ThatAsync(async () => await h.Vm.CommitSaveAsync(intent, target), Throws.Exception);
        Assert.That(h.Service.IsBusy, Is.False); Assert.That(h.Editor.IsReadOnly, Is.False);
        Assert.That(await h.Snapshot(), Is.EqualTo(before));
        Assert.That(File.Exists(path), Is.EqualTo(failure == "delete"));
        h.Editor.Document.Insert(0, "after");
    });

    [Test]
    public Task ClosingAndCancellationDrainBegunCommitIncludingDetach() => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "confirmed";
        string path = h.PathFor(); File.WriteAllText(path, "sentinel");
        var entered = Signal(); var release = Signal();
        using var cancellation = new CancellationTokenSource();
        var work = h.Service.CommitSaveAsync(h.Vm.CaptureSaveIntent()!, new(path, true, async () =>
            { entered.SetResult(); await release.Task; File.Delete(path); }), cancellation.Token);
        await entered.Task;
        h.Service.BeginShutdown(); cancellation.Cancel();
        var drain = h.Service.DrainAsync(); var disposal = h.Service.DisposeAsync().AsTask();
        h.Window.Content = null;
        try
        {
            Assert.That(drain.IsCompleted || disposal.IsCompleted, Is.False);
            Assert.Throws<ExecutionBusyException>(() => h.Editor.Document.Insert(0, "after detach"));
            Assert.That((await h.Service.GetEditorAsync(Caller, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.ShuttingDown));
        }
        finally { release.TrySetResult(); }
        Assert.That(await work, Is.Null); await drain; await disposal;
        Assert.That(File.ReadAllText(path), Is.EqualTo("confirmed\n"));
        Assert.That(h.Editor.IsReadOnly, Is.False); Assert.That(h.Service.IsBusy, Is.False);
    });

    [TestCase("denied")] [TestCase("busy")] [TestCase("cancel")] [TestCase("detach")] [TestCase("shutdown")]
    public Task SaveRejectedBeforeCommitHasNoTargetEffects(string reason) => Headless(async () =>
    {
        await using var h = new Harness(); h.Editor.Text = "confirmed";
        var intent = h.Vm.CaptureSaveIntent()!;
        string path = h.PathFor(); File.WriteAllText(path, "sentinel");
        using var cancellation = new CancellationTokenSource();
        using var busy = reason == "busy" ? InterpreterHostQueue.Reserve() : null;
        if (reason == "detach") h.Window.Content = null;
        if (reason == "shutdown") h.Service.BeginShutdown();
        if (reason == "cancel") cancellation.Cancel();
        int deletes = 0;
        var target = new WorkbenchAutomationService.SaveTarget(path, reason != "denied", () => { deletes++; File.Delete(path); return Task.CompletedTask; });
        if (reason == "cancel") await Assert.ThatAsync(async () => await h.Service.CommitSaveAsync(intent, target, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        else Assert.That(await h.Service.CommitSaveAsync(intent, target), Is.EqualTo(reason switch
        {
            "denied" => AutomationErrorCode.ConfirmationDenied, "busy" => AutomationErrorCode.Busy,
            "detach" => AutomationErrorCode.EditorUnavailable, _ => AutomationErrorCode.ShuttingDown
        }));
        Assert.That(deletes, Is.Zero); Assert.That(File.ReadAllText(path), Is.EqualTo("sentinel"));
    });
}
