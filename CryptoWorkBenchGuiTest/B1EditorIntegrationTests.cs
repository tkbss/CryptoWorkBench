using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Search;
using AvaloniaEdit.Utils;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class B1EditorIntegrationTests
{
    internal static Task Ui(Func<Task> test) => GuiTestSession.Session.Dispatch(async () =>
    {
        await test();
        return true;
    }, CancellationToken.None);

    internal sealed class Host : IAsyncDisposable
    {
        internal readonly WorkbenchAutomationService Service;
        internal readonly WorkbenchTextEditor Editor = new();
        internal readonly Window Window;
        internal readonly EditorBridge.Registration Registration;
        internal WorkbenchTextArea Area => (WorkbenchTextArea)Editor.TextArea;
        internal EditorCoordinator Owner => Service.EditorCoordinator;

        internal Host(string text, WorkbenchAutomationService? service = null)
        {
            Service = service ?? new();
            Editor.Text = text;
            Registration = Service.AttachEditor(Editor, null!)!;
            Window = new Window { Content = Editor, Width = 800, Height = 400 };
            Window.Show();
            Window.UpdateLayout();
            Area.Focus();
            Editor.CaretOffset = text.Length;
        }

        public async ValueTask DisposeAsync()
        {
            Window.Close();
            await Service.DisposeAsync();
        }
    }

    [Test]
    public Task ActiveViewDoesNotOwnAnotherDocumentsIdentityOrHistory() => Ui(async () =>
    {
        await using var h = new Host("A");
        var b = new WorkbenchTextEditor { Text = "B" };
        h.Area.PerformTextInput("1");
        var aId = h.Registration.Id;
        var bRegistration = h.Service.AttachEditor(b, null!, retainCurrent: true)!;
        Assert.That(h.Registration.Attached, Is.True);
        Assert.That(bRegistration.Id, Is.Not.EqualTo(aId));
        h.Area.PerformTextInput("wrong");
        Assert.That(h.Editor.Text, Is.EqualTo("A1"));
        b.CaretOffset = 1;
        b.TextArea.PerformTextInput("2");
        using var forB = h.Owner.TryBegin()!;
        Assert.That(h.Owner.Commit(forB, h.Editor, d => d.Insert(0, "wrong")), Is.False);
        h.Service.ActivateEditor(h.Registration);
        Assert.That(h.Owner.Commit(forB, b, d => d.Insert(0, "stale")), Is.False);
        Assert.That(h.Registration.Id, Is.EqualTo(aId));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("A"));
        Assert.That(b.Text, Is.EqualTo("B2"));
        var drain = h.Owner.BeginDrain();
        Assert.Throws<ExecutionBusyException>(() => h.Service.ActivateEditor(bRegistration));
        b.TextArea.PerformTextInput("wrong");
        h.Area.PerformTextInput("wrong");
        Assert.That(h.Owner.ResumeGui(drain), Is.True);
        ApplicationCommands.Redo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("A1"));
        using var beforeClose = h.Owner.TryBegin()!;
        h.Service.DetachEditor(h.Registration);
        Assert.That(h.Owner.Commit(beforeClose, h.Editor, d => d.Insert(0, "stale")), Is.False);
        var reopened = h.Service.AttachEditor(h.Editor, null!, retainCurrent: true)!;
        Assert.That(reopened.Id, Is.Not.EqualTo(aId));
        Assert.That(h.Owner.Commit(beforeClose, h.Editor, d => d.Insert(0, "replay")), Is.False);
        Assert.That(h.Owner.Pending, Is.Zero);
        Assert.That(b.Text, Is.EqualTo("B2"));
    });

    [Test]
    public Task ExistingMcpWriteAndGuiCannotOverlapAcrossViews() => Ui(async () =>
    {
        await using var h = new Host("A");
        var b = new WorkbenchTextEditor { Text = "B" };
        var second = h.Service.AttachEditor(b, null!, retainCurrent: true)!;
        h.Service.ActivateEditor(h.Registration);
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential(WorkbenchAutomationService.LocalCaller.PrincipalId, WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var snapshot = h.Registration.Snapshot();
        var request = new EditorEdit(h.Service.InstanceId, snapshot.EditorId, snapshot.Revision, EditorEditMode.Append, "!");
        Assert.That((await h.Service.UpdateEditorAsync(caller, request, default)).Error?.Code, Is.EqualTo(AutomationErrorCode.LeaseRequired));
        AcquireEditorV2 Acquire() => new("2", request.InstanceId, request.EditorId, Guid.NewGuid(), request.ExpectedRevision);
        AutomationErrorCode? denied = null;
        h.Owner.RunGui(() => denied = h.Service.AcquireEditorAsync(caller, Acquire(), default).GetAwaiter().GetResult().Error?.Code);
        Assert.That(denied, Is.EqualTo(AutomationErrorCode.Busy));
        var drain = h.Owner.BeginDrain();
        Assert.That((await h.Service.AcquireEditorAsync(caller, Acquire(), default)).Error?.Code, Is.EqualTo(AutomationErrorCode.Busy));
        Assert.That(h.Owner.ResumeGui(drain), Is.True);
        var lease = (await h.Service.AcquireEditorAsync(caller, Acquire(), default)).Result!;
        int attempts = 0;
        bool switchRejected = false;
        h.Editor.Document.Changed += (_, _) =>
        {
            attempts++;
            try { h.Service.ActivateEditor(second); }
            catch (ExecutionBusyException) { switchRejected = true; }
            b.TextArea.PerformTextInput("wrong");
            h.Area.PerformTextInput("wrong");
        };
        var receipt = await h.Service.UpdateEditorV2Async(caller, new("2", request.InstanceId, request.EditorId,
            lease.LeaseId, request.ExpectedRevision, request.Mode, request.Text), default);
        Assert.That(receipt.Error, Is.Null);
        Assert.That(receipt.HistoryEffect, Is.EqualTo(HistoryEffect.Applied));
        await h.Service.ReleaseEditorAsync(caller, new("2", request.InstanceId, request.EditorId, lease.LeaseId), default);
        Assert.That(attempts, Is.EqualTo(1));
        Assert.That(switchRejected, Is.True);
        Assert.That(h.Editor.Text, Is.EqualTo("A!"));
        Assert.That(b.Text, Is.EqualTo("B"));
        Assert.That(h.Owner.CanStartGui, Is.True);
    });

    [TestCase(true, true)] [TestCase(false, true)]
    [TestCase(true, false)] [TestCase(false, false)]
    public Task SearchCommandsUseOriginalPanelAndRespectDrain(bool all, bool fromPanel) => Ui(async () =>
    {
        await using var h = new Host("one\none");
        var panel = h.Editor.SearchPanel!;
        panel.Open();
        panel.IsReplaceMode = true;
        panel.SearchPattern = "on[e]";
        panel.UseRegex = true;
        panel.ReplacePattern = "two";
        IInputElement target = fromPanel ? panel : h.Area;
        var command = all ? SearchCommands.ReplaceAll : SearchCommands.ReplaceNext;
        var drain = h.Owner.BeginDrain();
        SearchCommands.FindNext.Execute(null, target);
        var before = h.Registration.Snapshot();
        command.Execute(null, target);
        Assert.That(h.Registration.Snapshot(), Is.EqualTo(before));
        Assert.That(h.Owner.ResumeGui(drain), Is.True);
        command.Execute(null, target);
        Assert.That(h.Editor.Text.Split("two").Length - 1, Is.EqualTo(all ? 2 : 1));
        Assert.That(h.Owner.LastReceipt!.TextEffect, Is.EqualTo(EditorTextEffect.Applied));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo(before.Text));
        panel.SearchPattern = "[";
        h.Editor.Select(0, 3);
        command.Execute(null, target);
        Assert.That(h.Editor.Text, Is.EqualTo(before.Text));
        Assert.That(h.Owner.Pending, Is.Zero);
    });

    [Test]
    public Task CopyDuringDrainPublishesUnderObservationWithoutWriteTicket() => Ui(async () =>
    {
        await using var h = new Host("ä €");
        h.Editor.SelectAll();
        var drain = h.Owner.BeginDrain();
        int copies = 0;
        h.Area.TextCopied += (_, _) =>
        {
            copies++;
            Assert.That(h.Owner.ObserverDepth, Is.Positive);
            Assert.Throws<ExecutionBusyException>(() => h.Editor.Document.Insert(0, "wrong"));
        };
        ApplicationCommands.Copy.Execute(null, h.Area);
        Assert.That(copies, Is.EqualTo(1));
        Assert.That(h.Owner.ObserverFailures, Is.Zero);
        Assert.That(await h.Window.Clipboard!.TryGetTextAsync(), Is.EqualTo("ä €"));
        Assert.That(h.Owner.Pending, Is.Zero);
        Assert.That(h.Owner.LastReceipt, Is.Null);
        Assert.That(h.Owner.ResumeGui(drain), Is.True);
    });

    [TestCase(false)] [TestCase(true)]
    public Task RejectedUpdateDoesNotRecursivelyDeliverFinished(bool fromHistory) => Ui(async () =>
    {
        await using var h = new Host("abc");
        int deliveries = 0;
        h.Editor.Document.UpdateFinished += (_, _) =>
        {
            deliveries++;
            // Bound even a regressing implementation inside the test process.
            if (deliveries < 8)
                Assert.Throws<ExecutionBusyException>(() => h.Editor.Document.Insert(0, "wrong"));
        };
        if (fromHistory)
            h.Owner.RunHistory(cap =>
            {
                using var identity = h.Editor.LockIdentity();
                h.Editor.ReplaceHistoryLine(cap.Document, 0, 3, "def", cap);
            });
        else
            h.Owner.Observe(() => Assert.Throws<ExecutionBusyException>(() => h.Editor.Document.Insert(0, "wrong")));
        Assert.That(deliveries, Is.EqualTo(fromHistory ? 1 : 0));
        Assert.That(h.Owner.ObserverFailures, Is.Zero);
        Assert.That(h.Editor.Text, Is.EqualTo(fromHistory ? "def" : "abc"));
        Assert.That(h.Editor.Document.IsInUpdate || h.Owner.Active, Is.False);
        Assert.That(h.Owner.Pending, Is.Zero);
    });

    [TestCase("accept")] [TestCase("dispose")] [TestCase("swap")] [TestCase("generation")]
    [TestCase("late")] [TestCase("tab")] [TestCase("enter")] [TestCase("close-swap")] [TestCase("close-dispose")]
    public Task CompletionRequestCanOnlyCommitItsLiveSession(string scenario) => Ui(async () =>
    {
        await using var h = new Host("Info(AES");
        using var completion = new InfoCompletionController(h.Editor, new InfoCompletionProvider());
        Assert.That(completion.OpenForTypedParenthesis(), Is.True);
        var window = (CompletionWindow)typeof(InfoCompletionController)
            .GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(completion)!;
        var original = h.Editor.Document;
        var selected = window.CompletionList.SelectedItem!;
        var foreign = new TextEditor { Text = "foreign" };
        selected.Complete(foreign.TextArea, new SimpleSegment(0, 3), EventArgs.Empty);
        Assert.That(foreign.Text, Is.EqualTo("foreign"));
        if (scenario == "close-swap") window.Closed += (_, _) => h.Editor.Document = new TextDocument("x");
        if (scenario == "close-dispose") window.Closed += (_, _) => completion.Dispose();
        if (scenario == "dispose") completion.Dispose();
        if (scenario == "swap") h.Editor.Document = new TextDocument("new");
        if (scenario == "generation") h.Owner.InvalidateInputIntents();
        if (scenario == "late") window.Hide();
        if (scenario == "tab" || scenario == "enter")
        {
            var key = scenario == "tab" ? Key.Tab : Key.Enter;
            h.Window.KeyPress(key, RawInputModifiers.None, scenario == "tab" ? PhysicalKey.Tab : PhysicalKey.Enter, null);
        }
        else window.CompletionList.RequestInsertion(EventArgs.Empty);
        if (scenario is "accept" or "tab" or "enter")
        {
            Assert.That(original.Text, Is.EqualTo("Info(" + selected.Text));
            ApplicationCommands.Undo.Execute(null, h.Area);
        }
        Assert.That(original.Text, Is.EqualTo("Info(AES"));
        if (scenario == "swap") Assert.That(h.Editor.Text, Is.EqualTo("new"));
        if (scenario == "close-swap") Assert.That(h.Editor.Text, Is.EqualTo("x"));
        Assert.That(h.Owner.Pending, Is.Zero);
    });

    [TestCase("selection")] [TestCase("caret")] [TestCase("failure")] [TestCase("view")]
    public Task DelayedCutNeverDeletesAfterInvalidationOrExportFailure(string scenario) => Ui(async () =>
    {
        await using var h = new Host("original");
        var b = new WorkbenchTextEditor { Text = "second" };
        var view = h.Service.AttachEditor(b, null!, retainCurrent: true)!;
        h.Service.ActivateEditor(h.Registration);
        h.Editor.Select(0, 4);
        var original = h.Window.Clipboard!;
        var proxy = DispatchProxy.Create<IClipboard, EditorCoordinatorTests.ClipboardProxy>();
        var adapter = (EditorCoordinatorTests.ClipboardProxy)(object)proxy;
        adapter.Inner = original;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.DelayedWrite = completion.Task;
        EditorCoordinatorTests.ClipboardProxy.Bind(proxy);
        try
        {
            ApplicationCommands.Cut.Execute(null, h.Area);
            Assert.That(h.Area.PendingClipboard.IsCompleted, Is.False);
            Assert.That(h.Editor.Document.IsInUpdate || h.Owner.Active, Is.False);
            if (scenario == "selection") h.Editor.Select(2, 3);
            if (scenario == "caret") h.Editor.CaretOffset = 7;
            if (scenario == "view") h.Service.ActivateEditor(view);
            if (scenario == "failure") completion.SetException(new IOException());
            else completion.SetResult();
            await h.Area.PendingClipboard;
            Assert.That(h.Editor.Text, Is.EqualTo("original"));
            Assert.That(b.Text, Is.EqualTo("second"));
            Assert.That(h.Owner.Pending, Is.Zero);
            Assert.That(h.Owner.LastReceipt!.TextEffect, Is.EqualTo(EditorTextEffect.None));
            Assert.That(h.Owner.CanStartGui, Is.True);
        }
        finally
        {
            completion.TrySetResult();
            EditorCoordinatorTests.ClipboardProxy.Bind(original);
        }
    });

    [TestCase(false)] [TestCase(true)]
    public Task ClosingSelectedViewCannotReleaseGlobalDrain(bool retain) => Ui(async () =>
    {
        await using var h = new Host("first");
        var b = new WorkbenchTextEditor { Text = "second" };
        var selected = h.Service.AttachEditor(b, null!, retainCurrent: retain)!;
        var drain = h.Owner.BeginDrain();
        h.Service.DetachEditor(selected);
        Assert.Throws<ExecutionBusyException>(() => h.Service.AttachEditor(h.Editor, null!));
        Assert.That(h.Owner.Phase, Is.EqualTo(EditorControlPhase.Releasing));
        Assert.That(h.Owner.ResumeGui(drain), Is.True);
        h.Service.AttachEditor(h.Editor, null!);
        Assert.That(h.Owner.CanStartGui, Is.True);
    });

    [Test]
    public Task ParameterCommitChecksSelectedSnapshotVersionAndWorkspace() => Ui(async () =>
    {
        await using var h = new Host("script");
        var state = VariableDictionary.Instance();
        var previous = state.GetVariables().ToArray();
        try
        {
            var a = new ParameterVariableDeclaration { Id = "a" };
            var b = new ParameterVariableDeclaration { Id = "b" };
            a.SetParameter("PAD", "NONE");
            b.SetParameter("PAD", "NONE");
            h.Service.Mutate(() => { state.Clear(); state.Add(a); state.Add(b); });
            var variables = new VariableViewModel { Automation = h.Service };
            variables.SetupVariables();
            var first = variables.DataVariables.Single(v => v.Identifier == "a");
            var item = first.Parameters.Single(p => p.Parameter == "#PAD");
            item.Value = "PKCS-7";
            variables.SelectedVariable = variables.DataVariables.Single(v => v.Identifier == "b");
            variables.ModifyVariableCommand.Execute(item);
            variables.ModifyValueCommand.Execute(first);
            Assert.That(b.GetParameters()["#PAD"], Is.EqualTo("NONE"));
            variables.SelectedVariable = first;
            var drain = h.Owner.BeginDrain();
            variables.ModifyVariableCommand.Execute(item);
            Assert.That(a.GetParameters()["#PAD"], Is.EqualTo("NONE"));
            Assert.That(h.Owner.ResumeGui(drain), Is.True);
            variables.ModifyVariableCommand.Execute(item);
            Assert.That(variables.IsSnapshotStale, Is.True);
            variables.SetupVariables();
            variables.SelectedVariable = variables.DataVariables.Single(v => v.Identifier == "a");
            variables.ModifyVariableCommand.Execute(item);
            Assert.That(a.GetParameters()["#PAD"], Is.EqualTo("NONE"));
            item = variables.SelectedVariable.Parameters.Single(p => p.Parameter == "#PAD");
            item.Value = "PKCS-7";
            h.Service.Mutate(() => b.SetParameter("PAD", "PKCS-7"));
            variables.ModifyVariableCommand.Execute(item);
            Assert.That(variables.IsSnapshotStale, Is.True);
            Assert.That(a.GetParameters()["#PAD"], Is.EqualTo("NONE"));
            variables.SetupVariables();
            variables.SelectedVariable = variables.DataVariables.Single(v => v.Identifier == "a");
            item = variables.SelectedVariable.Parameters.Single(p => p.Parameter == "#PAD");
            item.Value = "PKCS-7";
            variables.ModifyVariableCommand.Execute(item);
            Assert.That(a.GetParameters()["#PAD"], Is.EqualTo("PKCS-7"));
            Assert.That(h.Registration.Revision, Is.Zero);
            Assert.That(h.Owner.Pending, Is.Zero);
        }
        finally
        {
            state.Clear();
            foreach (var variable in previous) state.Add(variable);
        }
    });

    [TestCase(false)] [TestCase(true)]
    public Task TextEnteringAndEnteredCannotBorrowNativeWrite(bool enter) => Ui(async () =>
    {
        await using var h = new Host("original");
        int calls = 0;
        void Attack(object? sender, TextInputEventArgs args)
        {
            calls++;
            Assert.That(h.Owner.ObserverDepth, Is.Positive);
            Assert.Throws<ExecutionBusyException>(() => h.Editor.Document.Insert(0, "wrong"));
        }
        h.Area.TextEntering += Attack;
        h.Area.TextEntered += Attack;
        if (enter) EditingCommands.EnterLineBreak.Execute(null, h.Area);
        else h.Area.PerformTextInput("€");
        Assert.That(calls, Is.EqualTo(2));
        Assert.That(h.Owner.ObserverFailures, Is.Zero);
        Assert.That(h.Editor.Text, Is.EqualTo(enter ? "original" + Environment.NewLine : "original€"));
        Assert.That(h.Owner.LastReceipt!.Finalization, Is.EqualTo(EditorFinalization.Complete));
    });

    [Test]
    public Task EveryInstalledEditingBindingRejectsWritesDuringDrain() => Ui(async () =>
    {
        await using var h = new Host("first\nsecond");
        h.Editor.SelectAll();
        var original = h.Registration.Snapshot();
        var drain = h.Owner.BeginDrain();
        var commands = h.Area.DefaultInputHandler.Editing.CommandBindings.Select(binding => binding.Command).Distinct().ToArray();
        Assert.That(commands, Does.Contain(AvaloniaEditCommands.ConvertToTitleCase));
        foreach (var command in commands.Where(command => command != ApplicationCommands.Copy))
        {
            command.Execute(null, h.Area);
            Assert.That(h.Registration.Snapshot(), Is.EqualTo(original), command.Name);
        }
        h.Window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
        Assert.That(h.Registration.Snapshot(), Is.EqualTo(original));
        Assert.That(h.Owner.Pending, Is.Zero);
        Assert.That(h.Owner.ResumeGui(drain), Is.True);
        AvaloniaEditCommands.ConvertToUppercase.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("FIRST\nSECOND"));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo(original.Text));
    });

    [Test]
    public Task RetemplatingRetiresTheOldSearchPanelBeforeFallback() => Ui(async () =>
    {
        await using var h = new Host("before");
        var first = h.Editor.SearchPanel!;
        first.Open(); first.IsReplaceMode = true; first.SearchPattern = "before"; first.ReplacePattern = "first";
        var template = h.Editor.Template;
        h.Editor.Template = null;
        h.Editor.ApplyTemplate();
        h.Editor.Template = template;
        h.Editor.ApplyTemplate();
        var next = h.Editor.SearchPanel!;
        Assert.That(next, Is.Not.SameAs(first));
        next.Open(); next.IsReplaceMode = true; next.SearchPattern = "before"; next.ReplacePattern = "next";
        SearchCommands.ReplaceAll.Execute(null, first);
        Assert.That(h.Editor.Text, Is.EqualTo("before"));
        SearchCommands.ReplaceAll.Execute(null, next);
        Assert.That(h.Editor.Text, Is.EqualTo("next"));
        h.Editor.Document = new TextDocument("new document");
        Assert.That(next.IsClosed, Is.True);
        SearchCommands.ReplaceAll.Execute(null, next);
        Assert.That(h.Editor.Text, Is.EqualTo("new document"));
    });

    private sealed class ProtectedPrefix(int length) : IReadOnlySectionProvider
    {
        public bool CanInsert(int offset) => offset >= length;
        public IEnumerable<ISegment> GetDeletableSegments(ISegment segment)
        {
            int start = Math.Max(length, segment.Offset);
            if (start < segment.EndOffset) yield return new SimpleSegment(start, segment.EndOffset - start);
        }
    }

    [Test]
    public Task WholeLineCutHonoursProtectedPrefixAndOneUndo() => Ui(async () =>
    {
        await using var h = new Host("abc\ndef");
        h.Editor.Options.CutCopyWholeLine = true;
        h.Editor.CaretOffset = 2;
        h.Area.ReadOnlySectionProvider = new ProtectedPrefix(1);
        ApplicationCommands.Cut.Execute(null, h.Area);
        await h.Area.PendingClipboard;
        Assert.That(h.Editor.Text, Is.EqualTo("adef"));
        Assert.That(await h.Window.Clipboard!.TryGetTextAsync(), Is.EqualTo("abc" + Environment.NewLine));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("abc\ndef"));
        var panel = h.Editor.SearchPanel!;
        panel.Open(); panel.IsReplaceMode = true; panel.SearchPattern = "abc"; panel.ReplacePattern = "xyz";
        SearchCommands.ReplaceAll.Execute(null, panel);
        Assert.That(h.Editor.Text, Is.EqualTo("abc\ndef"));
    });

    [Test]
    public Task RectangleCutAndNormalizedPasteKeepNativeSelectionSemantics() => Ui(async () =>
    {
        await using var h = new Host("abc\ndef");
        h.Area.Selection = new RectangleSelection(h.Area, new TextViewPosition(1, 2), new TextViewPosition(2, 3));
        ApplicationCommands.Cut.Execute(null, h.Area);
        await h.Area.PendingClipboard;
        Assert.That(h.Editor.Text, Is.EqualTo("ac\ndf"));
        ApplicationCommands.Undo.Execute(null, h.Area);
        h.Editor.Select(0, 0);
        h.Editor.Options.ConvertTabsToSpaces = true;
        h.Editor.Options.IndentationSize = 2;
        await h.Window.Clipboard!.SetTextAsync("ä\t€\r\n");
        ApplicationCommands.Paste.Execute(null, h.Area);
        await h.Area.PendingClipboard;
        Assert.That(h.Editor.Text, Is.EqualTo("ä  €\nabc\ndef"));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("abc\ndef"));
    });

    [TestCase("no matches")] [TestCase("identical")] [TestCase("readonly")]
    public Task NoOpReplacePreservesExistingRedo(string scenario) => Ui(async () =>
    {
        await using var h = new Host("text");
        h.Area.PerformTextInput("!");
        ApplicationCommands.Undo.Execute(null, h.Area);
        var panel = h.Editor.SearchPanel!;
        panel.Open(); panel.IsReplaceMode = true;
        panel.SearchPattern = scenario == "no matches" ? "missing" : "text";
        panel.ReplacePattern = scenario == "identical" ? "text" : "changed";
        h.Editor.SelectAll();
        if (scenario == "readonly") h.Editor.IsReadOnly = true;
        var before = h.Editor.Document.Version;
        SearchCommands.ReplaceNext.Execute(null, panel);
        SearchCommands.ReplaceAll.Execute(null, h.Area);
        Assert.That(h.Editor.Document.Version, Is.SameAs(before));
        Assert.That(h.Editor.Document.UndoStack.CanRedo, Is.True);
    });

    [Test]
    public Task PropertyObserverCannotEditOrRunAndErrorReleasesDepth() => Ui(async () =>
    {
        await using var h = new Host("unchanged");
        var status = new StatusViewModel { Automation = h.Service };
        int calls = 0;
        status.PropertyChanged += (_, _) =>
        {
            calls++;
            Assert.Throws<ExecutionBusyException>(() => h.Editor.Document.Insert(0, "wrong"));
            Assert.That(h.Owner.TryBegin(), Is.Null);
            ApplicationCommands.Delete.Execute(null, h.Area);
            throw new InvalidOperationException();
        };
        h.Owner.RunGui(() => h.Owner.Observe(() => status.StatusString = "updated"));
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(h.Owner.LastReceipt!.Error, Is.EqualTo(EditorOperationError.CallbackFailed));
        Assert.That(h.Editor.Text, Is.EqualTo("unchanged"));
        Assert.That(h.Owner.ObserverDepth, Is.Zero);
        Assert.That(h.Owner.CanStartGui, Is.True);
    });
}
