using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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
public class EditorReviewCorrectionTests
{
    public sealed class TestApp
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    private static AutomationCaller Caller => WorkbenchAutomationService.LocalCaller;
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task Ui(Func<Task> test)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
        await session.Dispatch(async () => { await test(); return true; }, CancellationToken.None);
    }

    private sealed class H : IAsyncDisposable
    {
        internal readonly WorkbenchAutomationService Service = new();
        internal readonly StatusViewModel Status = new();
        internal readonly VariableViewModel Variables = new();
        internal readonly CryptoScriptEditViewModel Vm;
        internal readonly CryptoScriptEditView View;
        internal readonly Window Window;
        internal readonly WorkbenchTextEditor Editor;
        private readonly VariableDeclaration[] previous = VariableDictionary.Instance().GetVariables().ToArray();
        internal readonly string Folder = Path.Combine(Path.GetTempPath(), "MCP014-Fixes-" + Guid.NewGuid());
        internal H()
        {
            Directory.CreateDirectory(Folder);
            VariableDictionary.Instance().Clear();
            Vm = new(Status, Variables, new InfoViewModel(), new MainViewModel(), null!, new HistoryService(), Service);
            View = new(Vm); Editor = View.FindControl<WorkbenchTextEditor>("Editor")!;
            Window = new() { Content = View }; Window.Show();
        }
        internal string FilePath => Path.Combine(Folder, "book.sbk");
        internal async Task<EditorSnapshot> Read() => (await Service.GetEditorAsync(Caller, default)).Data!;
        internal async Task<EditorExecution> Request()
        {
            var snapshot = await Read();
            return new(Service.InstanceId, Guid.NewGuid(), snapshot.EditorId, snapshot.Revision,
                (await Service.GetStatusAsync(Caller, default)).Data!.StateVersion);
        }
        internal async Task<EditorEdit> Edit(string text)
        {
            var snapshot = await Read();
            return new(Service.InstanceId, snapshot.EditorId, snapshot.Revision, EditorEditMode.Append, text);
        }
        internal void Invalidate(bool swap)
        {
            if (swap) Editor.Document = new TextDocument("successor"); else Window.Content = null;
        }
        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync(); Window.Close(); Vm.Dispose();
            VariableDictionary.Instance().Clear();
            foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
            Directory.Delete(Folder, true);
        }
    }

    [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
    public Task R1InvalidIdentityStopsBeforeFirstOrNextRunnerAndRetryDoesNotRun(bool swap, bool afterFirst) => Ui(async () =>
    {
        await using var h = new H();
        h.Editor.Text = afterFirst ? "VAR first=0x(01)\nVAR reviewShouldNotRun=0x(02)" : "VAR reviewShouldNotRun=0x(01)";
        bool invalidated = false;
        NotifyCollectionChangedEventHandler handler = (_, _) =>
        {
            if (afterFirst && !invalidated && h.Variables.DataVariables.Any(v => v.Identifier == "first"))
            { invalidated = true; h.Invalidate(swap); }
        };
        h.Variables.DataVariables.CollectionChanged += handler;
        var request = await h.Request();
        var work = h.Service.ExecuteEditorAsync(Caller, request, default);
        if (!afterFirst) { invalidated = true; h.Invalidate(swap); }
        var result = (await work).Data!;
        h.Variables.DataVariables.CollectionChanged -= handler;
        Assert.That(invalidated, Is.True);
        Assert.That(result.State, Is.EqualTo(ExecutionState.Cancelled));
        Assert.That(result.Effects, Is.EqualTo(afterFirst ? Effects.Possible : Effects.None));
        Assert.That(result.StateVersion, Is.EqualTo(afterFirst ? "1" : "0"));
        Assert.That(result.CancellationReason, Is.EqualTo(CancellationReason.User));
        Assert.That(VariableDictionary.Instance().Contains("reviewShouldNotRun"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("first"), Is.EqualTo(afterFirst));
        Assert.That((await h.Service.ExecuteEditorAsync(Caller, request, default)).Data, Is.EqualTo(result));
        Assert.That(h.Service.IsBusy, Is.False);
        if (swap) Assert.That(h.Editor.Text, Is.EqualTo("successor"));
    });

    [TestCase(false)] [TestCase(true)]
    public Task R1DetachAfterCompletedFinalRunnerDoesNotRewriteSuccessfulOutcome(bool swap) => Ui(async () =>
    {
        await using var h = new H(); h.Editor.Text = "VAR final=0x(01)";
        bool once = false;
        h.Variables.DataVariables.CollectionChanged += (_, _) =>
        {
            if (!once && h.Variables.DataVariables.Any(v => v.Identifier == "final"))
            { once = true; h.Invalidate(swap); }
        };
        var result = (await h.Service.ExecuteEditorAsync(Caller, await h.Request(), default)).Data!;
        Assert.That(once, Is.True);
        Assert.That(result.State, Is.EqualTo(ExecutionState.Completed));
        Assert.That(result.Effects, Is.EqualTo(Effects.Possible));
        Assert.That(result.StateVersion, Is.EqualTo("1"));
        Assert.That(VariableDictionary.Instance().Contains("final"), Is.True);
    });

    [TestCase("undo", "concrete-text")] [TestCase("redo", "concrete-text")]
    [TestCase("undo", "base-text")] [TestCase("redo", "base-text")]
    [TestCase("undo", "concrete-clear")] [TestCase("redo", "concrete-clear")]
    [TestCase("undo", "base-clear")] [TestCase("redo", "base-clear")]
    [TestCase("undo", "document-text")] [TestCase("redo", "document-text")]
    [TestCase("undo", "selected-text")] [TestCase("redo", "selected-text")]
    public Task R2EmptySetterAndClearPreserveHistoryThroughConcreteAndBaseReferences(string history, string api) => Ui(async () =>
    {
        await using var h = new H();
        if (history == "undo") { h.Editor.Text = "x"; h.Editor.Document.Remove(0, 1); }
        else { h.Editor.Text = ""; h.Editor.Document.Insert(0, "x"); h.Editor.Undo(); }
        Assert.That(h.Editor.Text, Is.Empty);
        var document = h.Editor.Document;
        bool undo = document.UndoStack.CanUndo, redo = document.UndoStack.CanRedo;
        Assert.That(history == "undo" ? undo : redo, Is.True);
        var before = await h.Read();
        var entered = Signal(); var release = Signal();
        var save = h.Service.CommitSaveAsync(h.Vm.CaptureSaveIntent()!, new(h.FilePath, true,
            async () => { entered.SetResult(); await release.Task; }));
        await entered.Task;
        try
        {
            TextEditor baseReference = h.Editor;
            Action mutate = api switch
            {
                "concrete-text" => () => h.Editor.Text = "",
                "base-text" => () => baseReference.Text = "",
                "concrete-clear" => h.Editor.Clear,
                "base-clear" => baseReference.Clear,
                "document-text" => () => document.Text = "",
                _ => () => baseReference.SelectedText = ""
            };
            Assert.Throws<ExecutionBusyException>(() => mutate());
            Assert.That(document.IsInUpdate, Is.False);
            Assert.That(document.UndoStack.CanUndo, Is.EqualTo(undo));
            Assert.That(document.UndoStack.CanRedo, Is.EqualTo(redo));
            Assert.That(await h.Read(), Is.EqualTo(before));
        }
        finally { release.TrySetResult(); }
        Assert.That(await save, Is.Null);
        if (history == "undo") h.Editor.Undo(); else h.Editor.Redo();
        Assert.That(h.Editor.Text, Is.EqualTo("x"));
        Assert.That(h.Service.IsBusy || h.Editor.MutationLocked || document.IsInUpdate, Is.False);
    });

    [TestCase(false)] [TestCase(true)]
    public Task R2IdenticalNonemptyTextAssignmentDoesNotTouchTextCaretOrUndo(bool throughBase) => Ui(async () =>
    {
        await using var h = new H(); h.Editor.Text = "before"; h.Editor.Document.Insert(6, "!"); h.Editor.CaretOffset = 3;
        var before = await h.Read();
        var entered = Signal(); var release = Signal();
        var save = h.Service.CommitSaveAsync(h.Vm.CaptureSaveIntent()!, new(h.FilePath, true, async () => { entered.SetResult(); await release.Task; }));
        await entered.Task;
        try
        {
            Assert.Throws<ExecutionBusyException>(() =>
            {
                if (throughBase) ((TextEditor)h.Editor).Text = before.Text; else h.Editor.Text = before.Text;
            });
            Assert.That(h.Editor.CaretOffset, Is.EqualTo(3));
            Assert.That(await h.Read(), Is.EqualTo(before));
        }
        finally { release.TrySetResult(); }
        await save; h.Editor.Undo(); Assert.That(h.Editor.Text, Is.EqualTo("before"));
    });

    [TestCase("shutdown")] [TestCase("dispose")] [TestCase("detach")]
    [TestCase("cancel")] [TestCase("throw")] [TestCase("shutdown-throw")]
    public Task R3OwnershipPrecedesLeaseCallbacksAndEveryRejectedInitializationUnwinds(string action) => Ui(async () =>
    {
        await using var h = new H(); h.Editor.Text = "confirmed"; File.WriteAllText(h.FilePath, "sentinel");
        var intent = h.Vm.CaptureSaveIntent()!;
        using var cancellation = new CancellationTokenSource();
        int deletes = 0; bool called = false; Task? drain = null; Task? disposal = null;
        h.Editor.PropertyChanged += (_, args) =>
        {
            if (args.Property != TextEditor.IsReadOnlyProperty || !h.Editor.IsReadOnly || called) return;
            called = true;
            drain = h.Service.DrainAsync();
            Assert.That(drain.IsCompleted, Is.False);
            Assert.That(h.Service.IsBusy, Is.True);
            switch (action)
            {
                case "shutdown": h.Service.BeginShutdown(); break;
                case "dispose": disposal = h.Service.DisposeAsync().AsTask(); Assert.That(disposal.IsCompleted, Is.False); break;
                case "detach": h.Window.Content = null; break;
                case "cancel": cancellation.Cancel(); break;
                case "shutdown-throw": h.Service.BeginShutdown(); throw new IOException("lease callback");
                default: throw new IOException("lease callback");
            }
        };
        var save = h.Service.CommitSaveAsync(intent, new(h.FilePath, true,
            () => { deletes++; File.Delete(h.FilePath); return Task.CompletedTask; }), cancellation.Token);
        if (action is "throw" or "shutdown-throw") await Assert.ThatAsync(async () => await save, Throws.InstanceOf<IOException>());
        else if (action == "cancel") await Assert.ThatAsync(async () => await save, Throws.InstanceOf<OperationCanceledException>());
        else Assert.That(await save, Is.EqualTo(action == "detach" ? AutomationErrorCode.EditorUnavailable : AutomationErrorCode.ShuttingDown));
        Assert.That(called, Is.True); await drain!; if (disposal is not null) await disposal;
        Assert.That(deletes, Is.Zero); Assert.That(File.ReadAllText(h.FilePath), Is.EqualTo("sentinel"));
        Assert.That(h.Service.IsBusy || h.Editor.MutationLocked || h.Editor.IsReadOnly || h.Editor.Document.IsInUpdate, Is.False);
        h.Editor.Document.Insert(0, "after");
        if (action == "throw")
            Assert.That(await h.Service.CommitSaveAsync(h.Vm.CaptureSaveIntent()!, new(h.FilePath, false, null)), Is.Null);
    });

    [Test]
    public Task R3SynchronousAppendPublishesOwnershipBeforeReadOnlyNotification() => Ui(async () =>
    {
        await using var h = new H(); h.Editor.Text = "confirmed"; File.WriteAllText(h.FilePath, "sentinel");
        Task? drain = null;
        h.Editor.PropertyChanged += (_, args) =>
        {
            if (args.Property != TextEditor.IsReadOnlyProperty || !h.Editor.IsReadOnly) return;
            h.Service.BeginShutdown(); drain = h.Service.DrainAsync(); Assert.That(drain.IsCompleted, Is.False);
        };
        Assert.Throws<ExecutionBusyException>(() => h.Vm.SaveScriptBook(h.FilePath));
        await drain!;
        Assert.That(File.ReadAllText(h.FilePath), Is.EqualTo("sentinel"));
        Assert.That(h.Service.IsBusy || h.Editor.MutationLocked, Is.False);
    });

    [Test]
    public Task R3ShutdownCancellationDetachAfterActualDeleteStillFinalizeWrite() => Ui(async () =>
    {
        await using var h = new H(); h.Editor.Text = "confirmed"; File.WriteAllText(h.FilePath, "sentinel");
        var deleted = Signal(); var release = Signal(); using var cancellation = new CancellationTokenSource();
        var save = h.Service.CommitSaveAsync(h.Vm.CaptureSaveIntent()!, new(h.FilePath, true, async () =>
        { File.Delete(h.FilePath); deleted.SetResult(); await release.Task; }), cancellation.Token);
        await deleted.Task;
        h.Service.BeginShutdown(); cancellation.Cancel(); var drain = h.Service.DrainAsync(); h.Window.Content = null;
        try
        {
            Assert.That(drain.IsCompleted, Is.False); Assert.That(File.Exists(h.FilePath), Is.False);
            Assert.Throws<ExecutionBusyException>(() => h.Editor.Document.Insert(0, "late"));
        }
        finally { release.TrySetResult(); }
        Assert.That(await save, Is.Null); await drain;
        Assert.That(File.ReadAllText(h.FilePath), Is.EqualTo("confirmed\n"));
        Assert.That(h.Service.IsBusy || h.Editor.MutationLocked || h.Editor.IsReadOnly, Is.False);
    });

    [TestCase("changed", "document")] [TestCase("finished", "document")]
    [TestCase("changed", "editor")] [TestCase("finished", "editor")]
    [TestCase("changed", "detach")] [TestCase("finished", "detach")]
    [TestCase("changed", "view")] [TestCase("finished", "view")]
    [TestCase("changed", "same")] [TestCase("finished", "same")]
    [TestCase("changed", "nested-service")] [TestCase("finished", "nested-service")]
    [TestCase("changed", "throw")] [TestCase("finished", "throw")]
    public Task R4EditKeepsOriginalIdentityAcrossReentrantCallbacksAndExceptions(string phase, string mutation) => Ui(async () =>
    {
        await using var h = new H(); h.Editor.Text = "old";
        var oldDocument = h.Editor.Document;
        var successorDocument = new TextDocument("successor");
        var successorEditor = new WorkbenchTextEditor { Text = "successor", IsModified = false };
        bool called = false, rejected = false;
        void Callback()
        {
            if (called) return;
            called = true;
            try
            {
                switch (mutation)
                {
                    case "document": h.Editor.Document = successorDocument; h.Editor.IsModified = false; break;
                    case "editor": h.Vm.TextEditor = successorEditor; break;
                    case "detach": h.Window.Content = null; break;
                    case "view": _ = new CryptoScriptEditView(h.Vm); break;
                    case "same": oldDocument.Insert(0, "nested"); break;
                    case "nested-service":
                        var request = h.Edit("nested").GetAwaiter().GetResult();
                        var reply = h.Service.UpdateEditorAsync(Caller, request, default).GetAwaiter().GetResult();
                        Assert.That(reply.Error!.Code, Is.EqualTo(AutomationErrorCode.Busy)); rejected = true; break;
                    default: throw new IOException("changed callback");
                }
            }
            catch (ExecutionBusyException) { rejected = true; }
            catch (InvalidOperationException) when (mutation == "same") { rejected = true; }
        }
        EventHandler<DocumentChangeEventArgs> changed = (_, _) => Callback();
        EventHandler finished = (_, _) => Callback();
        if (phase == "changed") oldDocument.Changed += changed; else oldDocument.UpdateFinished += finished;
        var edit = await h.Edit(" edit");
        var result = await h.Service.UpdateEditorAsync(Caller, edit, default);
        oldDocument.Changed -= changed; oldDocument.UpdateFinished -= finished;
        Assert.That(called, Is.True);
        Assert.That(result.Ok, Is.True, "Applied text must not be reported as a side-effect-free failure");
        Assert.That(result.Data!.EditorId, Is.EqualTo(edit.EditorId));
        Assert.That(oldDocument.Text, Is.EqualTo("old edit"));
        Assert.That(successorDocument.Text, Is.EqualTo("successor"));
        Assert.That(successorEditor.Text, Is.EqualTo("successor")); Assert.That(successorEditor.IsModified, Is.False);
        if (mutation is not "detach" and not "throw") Assert.That(rejected, Is.True);
        Assert.That(oldDocument.IsInUpdate || h.Editor.IdentityLocked || h.Editor.MutationLocked || h.Service.IsBusy, Is.False);
        if (mutation == "detach") h.Window.Content = h.View;
        Assert.That(h.Vm.TextEditor, Is.SameAs(h.Editor)); Assert.That(h.Editor.Document, Is.SameAs(oldDocument));
        var next = await h.Service.UpdateEditorAsync(Caller, await h.Edit(" next"), default);
        Assert.That(next.Ok, Is.True); Assert.That(oldDocument.Text, Is.EqualTo("old edit next"));
    });

    [TestCase(false)] [TestCase(true)]
    public Task R4UpdateStartedCannotStealTheAuthorizedChange(bool swap) => Ui(async () =>
    {
        await using var h = new H(); h.Editor.Text = "old";
        var document = h.Editor.Document;
        bool called = false;
        EventHandler observer = (_, _) =>
        {
            if (called) return;
            called = true;
            Assert.Throws<ExecutionBusyException>(() =>
            {
                if (swap) h.Editor.Document = new TextDocument("successor");
                else document.Insert(0, "nested");
            });
        };
        document.UpdateStarted += observer;
        var request = await h.Edit(" edit");
        var result = await h.Service.UpdateEditorAsync(Caller, request, default);
        document.UpdateStarted -= observer;
        Assert.That(called && result.Ok, Is.True);
        Assert.That(result.Data!.EditorId, Is.EqualTo(request.EditorId));
        Assert.That(document.Text, Is.EqualTo("old edit"));
        Assert.That(document.IsInUpdate || h.Editor.IdentityLocked || h.Service.IsBusy, Is.False);
    });

    [TestCase(false)] [TestCase(true)]
    public Task R5NewEditorRunClearsPriorDiagnosticsAndKeepsPriorImmutableResult(bool syntax) => Ui(async () =>
    {
        await using var h = new H();
        if (syntax)
        {
            h.Editor.Text = "VAR =";
            await h.Service.ExecuteEditorAsync(Caller, await h.Request(), default);
        }
        else await h.Vm.ParseLineAsync("KEY missing=Check(0x(AA))");
        if (syntax) Assert.That(h.Status.StatusString, Does.Contain("SYNTAX ERROR"));
        else Assert.That(h.Status.Diagnostics, Is.Not.Empty);
        var priorId = (await h.Service.GetStatusAsync(Caller, default)).Data!.LastExecutionId!.Value;
        var prior = (await h.Service.GetExecutionAsync(Caller, priorId, default)).Data!;
        Assert.That(prior.Diagnostics, Is.Not.Empty);
        if (syntax) Assert.That(prior.State, Is.EqualTo(ExecutionState.SyntaxError));
        h.Editor.Text = "VAR cleanRun=0x(01)";
        var result = await h.Service.ExecuteEditorAsync(Caller, await h.Request(), default);
        Assert.That(result.Data!.State, Is.EqualTo(ExecutionState.Completed));
        Assert.That(result.Data.Diagnostics, Is.Empty); Assert.That(h.Status.Diagnostics, Is.Empty);
        Assert.That((await h.Service.GetExecutionAsync(Caller, priorId, default)).Data, Is.EqualTo(prior));
    });

    [Test]
    public Task R5BookKeepsAllCurrentLineDiagnosticsAndRetryDoesNotResetPresentation() => Ui(async () =>
    {
        await using var h = new H();
        await h.Vm.ParseLineAsync("KEY previous=Check(0x(AA))");
        h.Editor.Text = "KEY first=Check(0x(AA))\nKEY second=Check(0x(BB))";
        var request = await h.Request(); var result = await h.Service.ExecuteEditorAsync(Caller, request, default);
        Assert.That(result.Data!.State, Is.EqualTo(ExecutionState.Completed));
        Assert.That(result.Data.Diagnostics.Length, Is.EqualTo(2)); Assert.That(h.Status.Diagnostics.Count, Is.EqualTo(2));
        int changes = 0; ((INotifyCollectionChanged)h.Status.Diagnostics).CollectionChanged += (_, _) => changes++;
        h.Status.StatusString = "retry marker";
        Assert.That((await h.Service.ExecuteEditorAsync(Caller, request, default)).Data, Is.EqualTo(result.Data));
        Assert.That(changes, Is.Zero); Assert.That(h.Status.StatusString, Is.EqualTo("retry marker"));
    });

    [TestCase(false)] [TestCase(true)]
    public Task R5RejectedBusyOrRevisionConflictDoesNotChangeDiagnosticsOrStatus(bool busy) => Ui(async () =>
    {
        await using var h = new H(); await h.Vm.ParseLineAsync("KEY previous=Check(0x(AA))");
        h.Editor.Text = "VAR next=0x(01)"; var request = await h.Request();
        var diagnostics = h.Status.Diagnostics.ToArray(); string status = h.Status.StatusString;
        using var reservation = busy ? InterpreterHostQueue.Reserve() : null;
        if (!busy) h.Editor.Document.Insert(0, " ");
        var result = await h.Service.ExecuteEditorAsync(Caller, request, default);
        Assert.That(result.Error!.Code, Is.EqualTo(busy ? AutomationErrorCode.Busy : AutomationErrorCode.RevisionConflict));
        Assert.That(h.Status.Diagnostics.ToArray(), Is.EqualTo(diagnostics)); Assert.That(h.Status.StatusString, Is.EqualTo(status));
    });
}
