using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class NativeEnterContinuationTests
{
    private sealed class Host : IAsyncDisposable
    {
        internal readonly WorkbenchAutomationService Service = new();
        internal readonly HistoryService History = new();
        internal readonly StatusViewModel Status = new();
        internal readonly CryptoScriptEditViewModel Vm;
        internal readonly CryptoScriptEditView? View;
        internal readonly WorkbenchTextEditor Editor;
        internal readonly Window Window;
        internal WorkbenchTextArea Area => (WorkbenchTextArea)Editor.TextArea;
        internal EditorCoordinator Coordinator => Service.EditorCoordinator;
        internal int Entering, Entered, Changes;
        internal readonly List<Task> ExecutionRequests = new();
        internal int HistoryCount => ((System.Collections.ICollection)typeof(HistoryService)
            .GetField("_lineHistory", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(History)!).Count;

        internal Host(bool withView = true)
        {
            Vm = new(Status, new(), new(), new(History), null!, History, Service);
            if (withView) { View = new(Vm); Editor = View.FindControl<WorkbenchTextEditor>("Editor")!; }
            else { Editor = new(); Vm.TextEditor = Editor; Vm.AttachEditor(); }
            Window = new() { Content = (Control?)View ?? Editor, Width = 800, Height = 400 };
            Window.Show(); Editor.ApplyTemplate(); Window.UpdateLayout();
            Editor.Text = "Print(0x(11))"; Editor.CaretOffset = Editor.Text.Length;
            Assert.That(Area.Focus(), Is.True, "native key fixture needs actual TextArea focus");
            var previous = View?.PendingExecution;
            Area.TextEntering += (_, _) => Entering++;
            Area.TextEntered += (_, _) =>
            {
                Entered++;
                if (View is not null && !ReferenceEquals(previous, View.PendingExecution))
                { ExecutionRequests.Add(View.PendingExecution); previous = View.PendingExecution; }
            };
            Editor.Document.Changed += (_, _) => Changes++;
        }

        internal void Key(PhysicalKey physical = PhysicalKey.Enter, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPress(Avalonia.Input.Key.Return, modifiers, physical, null);
            Window.KeyRelease(Avalonia.Input.Key.Return, modifiers, physical, null);
        }
        internal async Task Complete() { if (View is not null) await View.PendingExecution; }
        internal void AssertClosed()
        {
            Assert.That(Coordinator.Active, Is.False);
            Assert.That(Coordinator.Pending, Is.Zero);
            Assert.That(Coordinator.ObserverDepth, Is.Zero);
            Assert.That(Editor.Document.IsInUpdate, Is.False);
            Assert.That(Editor.IdentityLocked || Editor.MutationLocked, Is.False);
            Assert.That(InterpreterHostQueue.IsBusy, Is.False);
        }
        public async ValueTask DisposeAsync() { await Service.DisposeAsync(); Window.Close(); Vm.Dispose(); }
    }

    private static async Task Ui(Func<Task> test) =>
        await GuiTestSession.Session.Dispatch(async () => { await test(); return true; }, CancellationToken.None);

    [TestCase("\n")]
    [TestCase("\r\n")]
    public Task NativeEnterPreservesExistingDocumentNewLine(string newLine) => Ui(async () =>
    {
        await using var h = new Host(false);
        string seed = "Print(0x(22))" + newLine + "Print(0x(11))";
        h.Editor.Text = seed;
        h.Editor.CaretOffset = seed.Length;
        h.Key();
        Assert.That(h.Editor.Text, Is.EqualTo(seed + newLine));
        Assert.That(h.Coordinator.LastReceipt!.Error, Is.Null);
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo(seed));
        ApplicationCommands.Redo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo(seed + newLine));
        h.AssertClosed();
    });

    [TestCase(false)]
    [TestCase(true)]
    public Task DiscardedReturnDoesNotPublishOrExecute(bool changeText) => Ui(async () =>
    {
        await using var h = new Host();
        h.Area.TextEntering += (_, args) =>
        {
            if (changeText) args.Text = "FOREIGN";
            else args.Handled = true;
        };
        h.Key();
        await h.Complete();
        Assert.That(h.Editor.Text, Is.EqualTo("Print(0x(11))"));
        Assert.That(h.HistoryCount, Is.Zero);
        Assert.That(h.Vm.PrintMessage, Is.Empty);
        h.AssertClosed();
    });

    [Test]
    public Task RegistrationExceptionRejectsNewlineAndAllowsNextReturn() => Ui(async () =>
    {
        await using var h = new Host();
        h.Service.AdmissionCheckpoint = stage => { if (stage == "worker") throw new IOException(); };
        h.Key();
        await h.Complete();
        var status = (await h.Service.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default)).Data!;
        Assert.That((h.Editor.Text, h.HistoryCount, status.LastExecutionId), Is.EqualTo(("Print(0x(11))", 0, (Guid?)null)));
        h.AssertClosed();
        h.Service.AdmissionCheckpoint = null;
        h.Key();
        await h.Complete();
        Assert.That(h.Editor.Text, Does.Contain("out: 0x(11)"));
    });

    [Test]
    public Task NativeReturnExecutesExactlyOnceThroughTheCompleteView() => Ui(async () =>
    {
        await using var h = new Host();
        h.Key(); await h.Complete();
        Assert.That(h.Entering, Is.EqualTo(1));
        Assert.That(h.Entered, Is.EqualTo(1));
        Assert.That(h.ExecutionRequests, Has.Count.EqualTo(1));
        Assert.That(h.HistoryCount, Is.EqualTo(1));
        Assert.That(h.Editor.Text.Split("out: 0x(11)").Length - 1, Is.EqualTo(1));
        Assert.That(h.Vm.PrintMessage, Is.EqualTo("out: 0x(11)"));
        Assert.That(h.Status.StatusString, Is.EqualTo("Line successfull parsed"));
        h.AssertClosed();
    });

    [TestCase("key-numpad")]
    [TestCase("key-shift")]
    [TestCase("paragraph-command")]
    [TestCase("line-command")]
    [TestCase("perform")]
    [TestCase("routed")]
    public Task SupportedNewlineRoutesKeepOneExecution(string route) => Ui(async () =>
    {
        await using var h = new Host();
        switch (route)
        {
            case "key-numpad": h.Key(PhysicalKey.NumPadEnter); break;
            case "key-shift": h.Key(modifiers: RawInputModifiers.Shift); break;
            case "paragraph-command": EditingCommands.EnterParagraphBreak.Execute(null, h.Area); break;
            case "line-command": EditingCommands.EnterLineBreak.Execute(null, h.Area); break;
            case "perform": h.Area.PerformTextInput("\n"); break;
            case "routed": h.Area.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "\n" }); break;
        }
        await h.Complete();
        Assert.That((h.Entering, h.Entered, h.ExecutionRequests.Count, h.HistoryCount), Is.EqualTo((1, 1, 1, 1)));
        Assert.That(h.Editor.Text.Split("out: 0x(11)").Length - 1, Is.EqualTo(1));
        Assert.That(h.Vm.PrintMessage, Is.EqualTo("out: 0x(11)"));
        h.AssertClosed();
    });

    [Test]
    public Task UnicodeStillUsesItsOwnAdmission() => Ui(async () =>
    {
        await using var h = new Host(false);
        h.Area.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "äöü € ê" });
        Assert.That(h.Editor.Text, Is.EqualTo("Print(0x(11))äöü € ê"));
        Assert.That((h.Entering, h.Entered, h.Changes, h.HistoryCount), Is.EqualTo((1, 1, 1, 0)));
        h.AssertClosed();
    });

    private static EditorCoordinator.OperationTicket Active(Host h) => (EditorCoordinator.OperationTicket)
        typeof(EditorCoordinator).GetField("active", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(h.Coordinator)!;

    [Test]
    public Task DelegationIsConsumedBeforeFirstObserverAndCannotFollowANewTicket() => Ui(async () =>
    {
        await using var h = new Host(false);
        EditorCoordinator.OperationTicket? original = null;
        h.Area.TextEntering += (_, _) =>
        {
            original = Active(h);
            Assert.That(h.Coordinator.IsCurrent(original), Is.True);
            Assert.That(h.Coordinator.ObserverDepth, Is.Positive);
            Assert.That(h.Coordinator.IsActiveGuiOperation(original), Is.False, "input observers cannot use the original operation");
            Assert.That(typeof(WorkbenchTextArea).GetField("enterContinuation", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(h.Area), Is.Null);
            h.Area.PerformTextInput("FOREIGN");
        };
        h.Key();
        Assert.That(original, Is.Not.Null);
        Assert.That(h.Coordinator.IsActiveGuiOperation(original!), Is.False);
        h.Coordinator.RunGui(() =>
        {
            Assert.That(h.Coordinator.IsActiveGuiOperation(original!), Is.False);
            h.Area.PerformTextInput("\n");
        });
        Assert.That(h.Editor.Text, Is.EqualTo(("Print(0x(11))" + Environment.NewLine)));
        Assert.That((h.Entering, h.Entered, h.Changes, h.HistoryCount), Is.EqualTo((1, 1, 1, 0)));
        h.AssertClosed();
    });

    [Test]
    public Task NativeEnterHasOneCommandOneOriginalTicketAndOneUndoGroup() => Ui(async () =>
    {
        await using var h = new Host(false);
        int commands = 0;
        var commandEvent = (RoutedEvent<ExecutedRoutedEventArgs>)typeof(RoutedCommand)
            .GetProperty("ExecutedEvent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        h.Area.AddHandler(commandEvent, (_, e) => { if (e.Command == EditingCommands.EnterParagraphBreak) commands++; }, RoutingStrategies.Bubble, true);
        var tickets = new List<EditorCoordinator.OperationTicket>();
        void Capture() { tickets.Add(Active(h)); Assert.That(h.Coordinator.Pending, Is.EqualTo(1)); }
        h.Area.TextEntering += (_, _) => Capture();
        h.Editor.Document.Changing += (_, _) => Capture();
        h.Area.TextEntered += (_, _) => Capture();
        h.Key();
        Assert.That(commands, Is.EqualTo(1), "no nested routed Command.Execute with an unconsumed permission");
        Assert.That(tickets, Has.Count.EqualTo(3));
        Assert.That(tickets.All(t => ReferenceEquals(t, tickets[0])), Is.True);
        Assert.That((h.Entering, h.Entered, h.Changes), Is.EqualTo((1, 1, 1)));
        Assert.That(h.Editor.Text, Is.EqualTo(("Print(0x(11))" + Environment.NewLine)));
        var receipt = await tickets[0].Completion;
        Assert.That((receipt.TextEffect, receipt.HistoryEffect, receipt.Finalization, receipt.Error),
            Is.EqualTo((EditorTextEffect.Applied, EditorHistoryEffect.Applied, EditorFinalization.Complete, (EditorOperationError?)null)));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("Print(0x(11))"));
        Assert.That(h.Editor.Document.UndoStack.CanUndo, Is.False);
        ApplicationCommands.Redo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo(("Print(0x(11))" + Environment.NewLine)));
        h.AssertClosed();
    });

    [TestCase("entering")]
    [TestCase("entered")]
    [TestCase("update-start")]
    [TestCase("changing")]
    [TestCase("changed")]
    [TestCase("update-finish")]
    [TestCase("caret")]
    [TestCase("property")]
    public Task ForeignTextInputFromCallbacksCannotInheritEnterPermission(string point) => Ui(async () =>
    {
        await using var h = new Host(false);
        int attempts = 0;
        void Attack()
        {
            attempts++;
            h.Area.PerformTextInput("FOREIGN");
            h.Area.PerformTextInput("\n");
            EditingCommands.EnterLineBreak.Execute(null, h.Area);
            Assert.That(h.Coordinator.TryBegin(), Is.Null);
        }
        h.Editor.IsModified = false;
        switch (point)
        {
            case "entering": h.Area.TextEntering += (_, _) => Attack(); break;
            case "entered": h.Area.TextEntered += (_, _) => Attack(); break;
            case "update-start": h.Editor.Document.UpdateStarted += (_, _) => Attack(); break;
            case "changing": h.Editor.Document.Changing += (_, _) => Attack(); break;
            case "changed": h.Editor.Document.Changed += (_, _) => Attack(); break;
            case "update-finish": h.Editor.Document.UpdateFinished += (_, _) => Attack(); break;
            case "caret": h.Area.Caret.PositionChanged += (_, _) => Attack(); break;
            case "property": h.Editor.PropertyChanged += (_, e) => { if (e.Property == TextEditor.IsModifiedProperty) Attack(); }; break;
        }
        h.Key();
        Assert.That(attempts, Is.GreaterThan(0));
        Assert.That(h.Editor.Text, Is.EqualTo(("Print(0x(11))" + Environment.NewLine)));
        Assert.That((h.Entering, h.Entered, h.Changes, h.HistoryCount), Is.EqualTo((1, 1, 1, 0)));
        h.AssertClosed();
    });

    [Test]
    public Task SameEventReplayIsDeniedAndCancelsOnlyTheOriginalNativeInsertion() => Ui(async () =>
    {
        await using var h = new Host(false);
        h.Area.TextEntering += (_, e) => h.Area.PerformTextInput(e);
        h.Key();
        Assert.That(h.Editor.Text, Is.EqualTo("Print(0x(11))"));
        Assert.That((h.Entering, h.Entered, h.Changes, h.HistoryCount), Is.EqualTo((1, 0, 0, 0)));
        Assert.That(h.Coordinator.LastReceipt!.TextEffect, Is.EqualTo(EditorTextEffect.None));
        h.AssertClosed();
    });

    [Test]
    public Task ObserverCannotRepurposeTheDelegatedNewlineEvent() => Ui(async () =>
    {
        await using var h = new Host(false);
        h.Area.TextEntering += (_, e) => e.Text = "FOREIGN";
        h.Key();
        Assert.That(h.Editor.Text, Is.EqualTo("Print(0x(11))"));
        Assert.That((h.Entering, h.Entered, h.Changes, h.HistoryCount), Is.EqualTo((1, 0, 0, 0)));
        h.AssertClosed();
    });

    [Test]
    public Task NativeCanExecuteExceptionBeforeAdmissionCannotLeaveEnterPermission() => Ui(async () =>
    {
        await using var h = new Host(false);
        var binding = h.Area.CommandBindings.Single(b => b.Command == EditingCommands.EnterParagraphBreak);
        EventHandler<CanExecuteRoutedEventArgs> fail = (_, _) => throw new InvalidOperationException("early callback");
        binding.CanExecute += fail;
        try { Assert.Throws<InvalidOperationException>(() => h.Key()); }
        finally { binding.CanExecute -= fail; }
        Assert.That(h.Editor.Text, Is.EqualTo("Print(0x(11))"));
        Assert.That((h.Entering, h.Entered, h.Changes, h.HistoryCount), Is.EqualTo((0, 0, 0, 0)));
        Assert.That(h.Coordinator.LastReceipt, Is.Null);
        h.AssertClosed();
        h.Key();
        Assert.That(h.Editor.Text, Is.EqualTo(("Print(0x(11))" + Environment.NewLine)));
        Assert.That((h.Entering, h.Entered, h.Changes, h.HistoryCount), Is.EqualTo((1, 1, 1, 0)));
        h.AssertClosed();
    });

    [TestCase("generation")]
    [TestCase("detach")]
    [TestCase("release")]
    public Task InvalidatedOriginalCannotContinueNativeInsertion(string kind) => Ui(async () =>
    {
        await using var h = new Host(false);
        h.Area.TextEntering += (_, _) =>
        {
            if (kind == "generation") h.Coordinator.InvalidateInputIntents();
            else if (kind == "detach") h.Vm.DetachEditor(h.Editor);
            else h.Coordinator.BeginDrain();
        };
        h.Key();
        Assert.That(h.Editor.Text, Is.EqualTo("Print(0x(11))"));
        Assert.That((h.Entering, h.Entered, h.Changes, h.HistoryCount), Is.EqualTo((1, 0, 0, 0)));
        Assert.That(h.Coordinator.LastReceipt!.TextEffect, Is.EqualTo(EditorTextEffect.None));
        h.AssertClosed();
    });

    [TestCase("document")]
    [TestCase("editor")]
    [TestCase("observer-write")]
    public Task ObserverCannotReplaceOriginalIdentityOrWriteDuringEnter(string kind) => Ui(async () =>
    {
        await using var h = new Host(false);
        var original = h.Editor.Document;
        var next = new WorkbenchTextEditor { Text = "successor" };
        h.Area.TextEntering += (_, _) => h.Coordinator.Observe(() =>
        {
            if (kind == "document") h.Editor.Document = new TextDocument("FOREIGN");
            else if (kind == "editor") h.Service.AttachEditor(next, null!);
            else original.Insert(0, "FOREIGN");
        });
        h.Key();
        Assert.That(h.Editor.Document, Is.SameAs(original));
        Assert.That(h.Editor.Text, Is.EqualTo(("Print(0x(11))" + Environment.NewLine)));
        Assert.That(next.Text, Is.EqualTo("successor"));
        Assert.That(h.HistoryCount, Is.Zero);
        Assert.That(h.Coordinator.LastReceipt!.Error, Is.EqualTo(EditorOperationError.CallbackFailed));
        h.AssertClosed();
    });

    [TestCase("active")]
    [TestCase("releasing")]
    [TestCase("faulted")]
    [TestCase("mutation-lock")]
    [TestCase("identity-lock")]
    public Task UnavailableOriginalAuthorityCannotEnter(string state) => Ui(async () =>
    {
        await using var h = new Host(false);
        if (state == "active") h.Coordinator.RunGui(() => h.Key());
        else if (state == "releasing") { h.Coordinator.BeginDrain(); h.Key(); }
        else if (state == "faulted") { h.Coordinator.RunGui(() => throw new InvalidOperationException()); h.Key(); }
        else if (state == "mutation-lock") { using var lease = h.Editor.LockMutations(); h.Key(); }
        else { using var lease = h.Editor.LockIdentity(); h.Key(); }
        Assert.That(h.Editor.Text, Is.EqualTo("Print(0x(11))"));
        Assert.That((h.Entering, h.Entered, h.Changes, h.HistoryCount), Is.EqualTo((0, 0, 0, 0)));
        h.AssertClosed();
    });

    [TestCase("entering", false)]
    [TestCase("changing", false)]
    [TestCase("changed", true)]
    [TestCase("entered", true)]
    public Task NativeCallbackExceptionsInvalidateDelegationAndFaultConservatively(string point, bool changed) => Ui(async () =>
    {
        await using var h = new Host(false);
        void Fail() => throw new InvalidOperationException("test callback");
        switch (point)
        {
            case "entering": h.Area.TextEntering += (_, _) => Fail(); break;
            case "changing": h.Editor.Document.Changing += (_, _) => Fail(); break;
            case "changed": h.Editor.Document.Changed += (_, _) => Fail(); break;
            case "entered": h.Area.TextEntered += (_, _) => Fail(); break;
        }
        h.Key();
        Assert.That(h.Editor.Text, Is.EqualTo(changed ? ("Print(0x(11))" + Environment.NewLine) : "Print(0x(11))"));
        var before = h.Editor.Text;
        h.Area.PerformTextInput("FOREIGN"); h.Key();
        Assert.That(h.Editor.Text, Is.EqualTo(before));
        Assert.That(h.HistoryCount, Is.Zero);
        var receipt = h.Coordinator.LastReceipt!;
        Assert.That(receipt.TextEffect, Is.EqualTo(EditorTextEffect.Indeterminate));
        Assert.That(receipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.Indeterminate));
        Assert.That(receipt.Finalization, Is.EqualTo(EditorFinalization.Faulted));
        Assert.That(h.Coordinator.Phase, Is.EqualTo(EditorControlPhase.Faulted));
        var drain = h.Coordinator.BeginDrain();
        Assert.That(drain.Quiescent.Task.IsCompletedSuccessfully, Is.True);
        Assert.That(h.Coordinator.ResumeGui(drain), Is.False);
        Assert.That(typeof(WorkbenchTextArea).GetField("enterContinuation", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(h.Area), Is.Null);
        h.AssertClosed();
    });
}
