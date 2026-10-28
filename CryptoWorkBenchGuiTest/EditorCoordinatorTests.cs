using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using System.Reflection;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class EditorCoordinatorTests
{
    public sealed class TestApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
            Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://CryptoWorkBenchAvalonia"))
                { Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml") });
        }
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
    private static async Task Ui(Func<Task> test)
    {
        var session = GuiTestSession.Session;
        await session.Dispatch(async () => { await test(); return true; }, CancellationToken.None);
    }
    private sealed class Host : IAsyncDisposable
    {
        internal readonly WorkbenchAutomationService Service = new();
        internal readonly WorkbenchTextEditor Editor = new() { Text = "base" };
        internal readonly Window Window;
        internal readonly EditorBridge.Registration Registration;
        internal EditorCoordinator Coordinator => Service.EditorCoordinator;
        internal WorkbenchTextArea Area => (WorkbenchTextArea)Editor.TextArea;
        internal Host()
        {
            Registration = Service.AttachEditor(Editor, null!)!;
            Window = new() { Content = Editor, Width = 800, Height = 400 }; Window.Show();
            Editor.ApplyTemplate(); Window.UpdateLayout();
            Assert.That(Editor.TextArea.Focus(), Is.True, "fixture must deliver real routed input to the TextArea");
            Editor.CaretOffset = 4;
        }
        internal void Key(Key key)
        {
            var command = key switch
            {
                Avalonia.Input.Key.Z => ApplicationCommands.Undo,
                Avalonia.Input.Key.Y => ApplicationCommands.Redo,
                Avalonia.Input.Key.C => ApplicationCommands.Copy,
                Avalonia.Input.Key.X => ApplicationCommands.Cut,
                Avalonia.Input.Key.V => ApplicationCommands.Paste,
                _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported fixture command")
            };
            var gesture = command.Gesture;
            var modifiers = RawInputModifiers.None;
            if (gesture.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= RawInputModifiers.Control;
            if (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= RawInputModifiers.Meta;
            if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= RawInputModifiers.Shift;
            if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= RawInputModifiers.Alt;
            var physicalKey = Enum.Parse<PhysicalKey>(gesture.Key.ToString());
            var symbol = gesture.Key.ToString().ToLowerInvariant();
            Window.KeyPress(gesture.Key, modifiers, physicalKey, symbol);
            Window.KeyRelease(gesture.Key, modifiers, physicalKey, symbol);
        }
        public async ValueTask DisposeAsync() { await Service.DisposeAsync(); Window.Close(); }
    }

    [Test]
    public Task GuiUnicodeInputKeepsOriginalDocumentAndBridgeRevision() => Ui(async () =>
    {
        await using var h = new Host();
        var document = h.Editor.Document;
        var before = h.Registration.Snapshot();
        h.Window.KeyTextInput("ä€ e\u0301 日本語 😀");
        Assert.That(h.Editor.Text, Is.EqualTo("baseä€ e\u0301 日本語 😀"));
        Assert.That(h.Editor.Document, Is.SameAs(document));
        Assert.That(h.Registration.Snapshot().EditorId, Is.EqualTo(before.EditorId));
        Assert.That(h.Registration.Snapshot().Revision, Is.Not.EqualTo(before.Revision));
        Assert.That(h.Coordinator.LastReceipt!.TextEffect, Is.EqualTo(EditorTextEffect.Applied));
        Assert.That(h.Coordinator.Pending, Is.Zero);
        h.Key(Key.Z); Assert.That(h.Editor.Text, Is.EqualTo("base"));
        h.Key(Key.Y); Assert.That(h.Editor.Text, Does.EndWith("😀"));
    });

    [Test]
    public Task NativeUndoRedoRegistrationsGesturesAndTwoGroupsRemainExactlyOnce() => Ui(async () =>
    {
        await using var h = new Host();
        foreach (var command in new[] { ApplicationCommands.Undo, ApplicationCommands.Redo })
        {
            Assert.That(h.Area.DefaultInputHandler.CommandBindings.Count(b => b.Command == command), Is.EqualTo(1));
            Assert.That(h.Area.CommandBindings.Count(b => b.Command == command), Is.EqualTo(1));
        }
        h.Editor.Document.Insert(4, "A"); h.Editor.Document.Insert(5, "B");
        for (int i = 0; i < 3; i++)
        {
            h.Key(Key.Z); Assert.That(h.Editor.Text, Is.EqualTo("baseA"));
            h.Key(Key.Z); Assert.That(h.Editor.Text, Is.EqualTo("base"));
            h.Key(Key.Y); Assert.That(h.Editor.Text, Is.EqualTo("baseA"));
            h.Key(Key.Y); Assert.That(h.Editor.Text, Is.EqualTo("baseAB"));
        }
        Assert.That(h.Editor.Document.IsInUpdate, Is.False);
    });

    [Test]
    public Task DrainRejectsNativeTextAndBothHistoryDirectionsBeforeEffect() => Ui(async () =>
    {
        await using var h = new Host();
        h.Editor.Document.Insert(4, "A"); h.Editor.Document.Insert(5, "B"); h.Key(Key.Z);
        var snapshot = h.Registration.Snapshot();
        Assert.That(h.Editor.Document.UndoStack.CanUndo && h.Editor.Document.UndoStack.CanRedo, Is.True);
        var drain = h.Coordinator.BeginDrain();
        h.Window.KeyTextInput("BAD"); h.Key(Key.Z); h.Key(Key.Y);
        ApplicationCommands.Delete.Execute(null, h.Area);
        Assert.That(h.Registration.Snapshot(), Is.EqualTo(snapshot));
        Assert.That(h.Editor.Document.UndoStack.CanUndo && h.Editor.Document.UndoStack.CanRedo, Is.True);
        Assert.That(h.Coordinator.ResumeGui(drain), Is.True);
        Assert.That(h.Coordinator.ResumeGui(drain), Is.False);
        h.Key(Key.Y); Assert.That(h.Editor.Text, Is.EqualTo("baseAB"));
    });

    [TestCase("document")]
    [TestCase("caret")]
    [TestCase("selection")]
    public Task ControlledObserverRejectsReentrancyAndReportsCallbackFailure(string trigger) => Ui(async () =>
    {
        await using var h = new Host();
        h.Editor.Document.Insert(4, "A"); h.Editor.Document.Insert(5, "B"); h.Editor.CaretOffset = 6;
        int calls = 0, admitted = 0;
        bool armed = true;
        void Callback()
        {
            if (!armed) return;
            h.Coordinator.Observe(() =>
            {
                calls++;
                if (h.Coordinator.RunGui(() => h.Editor.AppendText("BAD"))) admitted++;
                ApplicationCommands.Undo.Execute(null, h.Area);
                ApplicationCommands.Redo.Execute(null, h.Area);
                throw new InvalidOperationException("must not escape or appear in receipt");
            });
        }
        if (trigger == "document") h.Editor.Document.Changed += (_, _) => Callback();
        if (trigger == "caret") h.Area.Caret.PositionChanged += (_, _) => Callback();
        if (trigger == "selection") { h.Editor.Select(4, 2); h.Area.SelectionChanged += (_, _) => Callback(); }
        h.Key(Key.Z); armed = false;
        Assert.That(calls, Is.GreaterThan(0)); Assert.That(admitted, Is.Zero);
        Assert.That(h.Editor.Text, Is.EqualTo("baseA"));
        var receipt = h.Coordinator.LastReceipt!;
        Assert.That(receipt.TextEffect, Is.EqualTo(EditorTextEffect.Applied));
        Assert.That(receipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.Applied));
        Assert.That(receipt.Finalization, Is.EqualTo(EditorFinalization.Complete));
        Assert.That(receipt.Error, Is.EqualTo(EditorOperationError.CallbackFailed));
        Assert.That(receipt.RetryPolicy, Is.EqualTo("noAutomaticRetry"));
        Assert.That(h.Coordinator.ObserverDepth, Is.Zero);
        h.Key(Key.Y); h.Window.KeyTextInput("X");
        Assert.That(h.Editor.Text, Does.Contain("X"));
        Assert.That(h.Coordinator.Phase, Is.EqualTo(EditorControlPhase.Gui));
    });

    [Test]
    public Task LateObserverWithoutAnActiveOperationStillCannotStartMutation() => Ui(async () =>
    {
        await using var h = new Host();
        bool allowed = true;
        h.Coordinator.Observe(() => allowed = h.Coordinator.RunGui(() => h.Editor.AppendText("BAD")));
        Assert.That(allowed, Is.False); Assert.That(h.Editor.Text, Is.EqualTo("base"));
        h.Window.KeyTextInput("X"); Assert.That(h.Editor.Text, Is.EqualTo("baseX"));
    });

    [Test]
    public Task DocumentSwapInvalidatesOriginalTicketWithoutTouchingSuccessor() => Ui(async () =>
    {
        await using var h = new Host();
        var ticket = h.Coordinator.TryBegin()!;
        var old = h.Editor.Document;
        var id = h.Registration.Id;
        long generation = h.Coordinator.Generation;
        h.Editor.Document = new TextDocument("next");
        Assert.That(h.Coordinator.Generation, Is.GreaterThan(generation));
        Assert.That(h.Coordinator.Commit(ticket, () => h.Editor.AppendText("BAD")), Is.False);
        var receipt = await ticket.Completion;
        Assert.That(receipt.EditorId, Is.EqualTo(id));
        Assert.That(receipt.ObservedRevision, Is.Null);
        Assert.That(h.Registration.Snapshot().EditorId, Is.Not.EqualTo(id));
        Assert.That(h.Editor.Text, Is.EqualTo("next")); Assert.That(old.Text, Is.EqualTo("base"));
        h.Editor.CaretOffset = 4; h.Window.KeyTextInput("X"); h.Key(Key.Z);
        Assert.That(h.Editor.Text, Is.EqualTo("next"));
    });

    [Test]
    public Task DocumentReplacementFromActiveObserverIsRejectedBeforeIdentityChange() => Ui(async () =>
    {
        await using var h = new Host();
        var original = h.Editor.Document;
        h.Editor.Document.Changed += (_, _) => h.Coordinator.Observe(() => h.Editor.Document = new TextDocument("BAD"));
        h.Window.KeyTextInput("X");
        Assert.That(h.Editor.Document, Is.SameAs(original));
        Assert.That(h.Editor.Text, Is.EqualTo("baseX"));
        Assert.That(h.Coordinator.LastReceipt!.Error, Is.EqualTo(EditorOperationError.CallbackFailed));
    });

    [Test]
    public Task DrainWaitsForAcceptedCallbacksAndSealedProducer() => Ui(async () =>
    {
        await using var h = new Host();
        var ticket = h.Coordinator.TryBegin()!;
        var callback = ticket.RegisterCallback();
        ticket.SealProducer();
        Assert.Throws<ExecutionBusyException>(() => ticket.RegisterCallback());
        ticket.Dispose();
        Assert.That(ticket.Completion.IsCompleted, Is.False);
        var drain = h.Coordinator.BeginDrain();
        Assert.That(h.Coordinator.BeginDrain(), Is.SameAs(drain));
        Assert.That(drain.Completion.IsCompleted, Is.False);
        int calls = 0; callback.Observe(() => calls++);
        Assert.That(calls, Is.Zero, "old generation callback cannot act");
        callback.Dispose(); callback.Dispose();
        await drain.Completion;
        Assert.That(h.Coordinator.Pending, Is.Zero);
        Assert.That(h.Coordinator.ResumeGui(drain), Is.True);
    });

    [Test]
    public Task CallbackErrorAfterBodyCompletionRemainsInFinalReceipt() => Ui(async () =>
    {
        await using var h = new Host();
        var ticket = h.Coordinator.TryBegin()!;
        var callback = ticket.RegisterCallback();
        ticket.Dispose();
        callback.Observe(() => throw new InvalidOperationException("late"));
        callback.Dispose();
        Assert.That((await ticket.Completion).Error, Is.EqualTo(EditorOperationError.CallbackFailed));
        Assert.That(h.Coordinator.RunGui(() => h.Editor.AppendText("X")), Is.True);
    });

    [Test]
    public Task UncontainedNativeFailureDoesNotClaimNoEffectOrRepairHistory() => Ui(async () =>
    {
        await using var h = new Host();
        var before = h.Registration.Snapshot();
        bool result = h.Coordinator.RunGui(() => { h.Editor.Document.Insert(4, "X"); throw new InvalidOperationException(); }, expectedText: "baseX");
        Assert.That(result, Is.False); Assert.That(h.Editor.Text, Is.EqualTo("baseX"));
        var receipt = h.Coordinator.LastReceipt!;
        Assert.That(receipt.EditorId, Is.EqualTo(before.EditorId));
        Assert.That(receipt.OriginalRevision, Is.EqualTo(before.Revision));
        Assert.That(receipt.TextEffect, Is.EqualTo(EditorTextEffect.Applied));
        Assert.That(receipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.Indeterminate));
        Assert.That(receipt.Finalization, Is.EqualTo(EditorFinalization.Faulted));
        Assert.That(h.Coordinator.RunGui(() => h.Editor.AppendText("BAD")), Is.False);
        Assert.That(h.Coordinator.BeginDrain().Completion.IsCompleted, Is.False);
    });

    [Test]
    public Task OpenNativeUpdateFaultsWithoutEndUpdateRepairLoop() => Ui(async () =>
    {
        await using var h = new Host();
        h.Coordinator.RunGui(() => h.Editor.Document.BeginUpdate());
        Assert.That(h.Editor.Document.IsInUpdate, Is.True);
        Assert.That(h.Coordinator.Phase, Is.EqualTo(EditorControlPhase.Faulted));
        Assert.That(h.Coordinator.LastReceipt!.Finalization, Is.EqualTo(EditorFinalization.Faulted));
        h.Editor.Document.EndUpdate(); // Only the test owner closes the update it deliberately opened.
        Assert.That(h.Coordinator.Phase, Is.EqualTo(EditorControlPhase.Faulted));
    });

    [Test]
    public Task ImeAndTextDragRemainDisabledBeforeFocusAndAfterOptionsReplacement() => Ui(async () =>
    {
        await using var h = new Host();
        var fresh = new WorkbenchTextEditor();
        Assert.That(InputMethod.GetIsInputMethodEnabled(fresh), Is.False);
        Assert.That(InputMethod.GetIsInputMethodEnabled(fresh.TextArea), Is.False);
        InputMethod.SetIsInputMethodEnabled(h.Editor, true); InputMethod.SetIsInputMethodEnabled(h.Area, true);
        h.Editor.Options = new TextEditorOptions { EnableTextDragDrop = true };
        h.Editor.Options.EnableTextDragDrop = true;
        h.Area.Options = new TextEditorOptions { EnableTextDragDrop = true };
        Assert.That(InputMethod.GetIsInputMethodEnabled(h.Editor), Is.False);
        Assert.That(InputMethod.GetIsInputMethodEnabled(h.Area), Is.False);
        Assert.That(h.Editor.Options.EnableTextDragDrop, Is.False);
        Assert.That(h.Area.Options.EnableTextDragDrop, Is.False);
    });

    [Test]
    public Task UnicodeClipboardPasteCutAndNativeUndoRedoRemainUsable() => Ui(async () =>
    {
        await using var h = new Host();
        await h.Window.Clipboard!.SetTextAsync("ä e\u0301 日本語 😀");
        h.Key(Key.V); await h.Area.PendingClipboard;
        Assert.That(h.Editor.Text, Is.EqualTo("baseä e\u0301 日本語 😀"));
        h.Key(Key.Z); Assert.That(h.Editor.Text, Is.EqualTo("base"));
        h.Key(Key.Y); h.Editor.Select(4, h.Editor.Text.Length - 4);
        h.Key(Key.X); await h.Area.PendingClipboard;
        Assert.That(h.Editor.Text, Is.EqualTo("base"));
        Assert.That(await h.Window.Clipboard!.TryGetTextAsync(), Is.EqualTo("ä e\u0301 日本語 😀"));
        h.Key(Key.Z); Assert.That(h.Editor.Text, Does.EndWith("😀"));
    });

    // A test-only platform adapter delays the real clipboard contract. No host test bypass.
    public class ClipboardProxy : DispatchProxy
    {
        public IClipboard Inner = null!;
        public Task<string?>? DelayedRead;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "TryGetDataAsync" && DelayedRead is not null) return ReadDelayedAsync(DelayedRead);
            try { return method.Invoke(Inner, args); }
            catch (TargetInvocationException error) { throw error.InnerException!; }
        }
        // Avalonia 12 retrieves a disposable transfer rather than calling GetTextAsync.
        // Keep the original delayed text source and all successor/drain assertions.
        private static async Task<IAsyncDataTransfer?> ReadDelayedAsync(Task<string?> read)
        {
            var text = await read;
            if (text is null) return null;
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.Create(DataFormat.Text, text));
            return transfer;
        }
        internal static void Bind(IClipboard value)
        {
            var locator = typeof(AvaloniaLocator).GetProperty("CurrentMutable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
            var registration = locator.GetType().GetMethod("Bind")!.MakeGenericMethod(typeof(IClipboard)).Invoke(locator, null)!;
            registration.GetType().GetMethod("ToConstant")!.MakeGenericMethod(typeof(IClipboard)).Invoke(registration, new object[] { value });
        }
    }

    [Test]
    public Task DelayedClipboardDoesNotWriteAfterDocumentReplacementOrDrain() => Ui(async () =>
    {
        await using var h = new Host();
        var originalClipboard = h.Window.Clipboard!;
        var proxy = DispatchProxy.Create<IClipboard, ClipboardProxy>();
        var adapter = (ClipboardProxy)(object)proxy; adapter.Inner = originalClipboard;
        var source = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.DelayedRead = source.Task; ClipboardProxy.Bind(proxy);
        try
        {
            h.Key(Key.V);
            Assert.That(h.Area.PendingClipboard.IsCompleted, Is.False);
            var original = h.Editor.Document;
            h.Editor.Document = new TextDocument("next");
            var drain = h.Coordinator.BeginDrain();
            Assert.That(drain.Completion.IsCompleted, Is.False);
            source.SetResult("BAD"); await h.Area.PendingClipboard; await drain.Completion;
            Assert.That(original.Text, Is.EqualTo("base")); Assert.That(h.Editor.Text, Is.EqualTo("next"));
            Assert.That(h.Coordinator.Pending, Is.Zero);
        }
        finally { source.TrySetResult(null); ClipboardProxy.Bind(originalClipboard); }
    });

    [Test]
    public Task DetachedRegistrationCannotRebindAcceptedTicketsToNewEditor() => Ui(async () =>
    {
        await using var h = new Host();
        var ticket = h.Coordinator.TryBegin()!;
        h.Service.DetachEditor(h.Registration);
        var next = new WorkbenchTextEditor { Text = "next" };
        Assert.Throws<ExecutionBusyException>(() => h.Service.AttachEditor(next, null!));
        Assert.That(h.Coordinator.Commit(ticket, () => next.AppendText("BAD")), Is.False);
        h.Service.AttachEditor(next, null!);
        Assert.That(next.Coordinator, Is.SameAs(h.Coordinator));
        Assert.That(next.Text, Is.EqualTo("next"));
        Assert.That(h.Coordinator.Commit(ticket, () => next.AppendText("BAD")), Is.False);
    });

    [Test]
    public Task DirectPerformTextInputUsesTheSameEarlyBoundaryExactlyOnce() => Ui(async () =>
    {
        await using var h = new Host();
        int entering = 0, entered = 0;
        h.Area.TextEntering += (_, _) => entering++;
        h.Area.TextEntered += (_, _) => entered++;
        h.Area.PerformTextInput("X");
        Assert.That(h.Editor.Text, Is.EqualTo("baseX"));
        Assert.That((entering, entered), Is.EqualTo((1, 1)));
        var drain = h.Coordinator.BeginDrain();
        h.Area.PerformTextInput("BAD");
        Assert.That((entering, entered), Is.EqualTo((1, 1)));
        Assert.That(h.Editor.Text, Is.EqualTo("baseX"));
        Assert.That(h.Coordinator.ResumeGui(drain), Is.True);
        h.Key(Key.Z); Assert.That(h.Editor.Text, Is.EqualTo("base"));
    });

    [Test]
    public Task HistoryOnlyEffectIsIndependentOfTextRevision() => Ui(async () =>
    {
        await using var h = new Host();
        h.Editor.Document.Insert(4, "X");
        var before = h.Registration.Snapshot();
        h.Coordinator.RunGui(() => h.Editor.Document.UndoStack.ClearAll());
        var receipt = h.Coordinator.LastReceipt!;
        Assert.That(receipt.TextEffect, Is.EqualTo(EditorTextEffect.None));
        Assert.That(receipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.Applied));
        Assert.That(receipt.ObservedRevision, Is.EqualTo(before.Revision));
        Assert.That(receipt.Finalization, Is.EqualTo(EditorFinalization.Complete));
    });

    [Test]
    public Task NativeUndoCallbackEscapeFaultsWithoutPretendingTheStackWasUntouched() => Ui(async () =>
    {
        await using var h = new Host();
        h.Editor.Document.Insert(4, "X");
        h.Editor.Document.Changed += (_, _) => throw new InvalidOperationException("unmediated callback");
        h.Key(Key.Z);
        Assert.That(h.Editor.Text, Is.EqualTo("base"));
        Assert.That(h.Coordinator.LastReceipt!.TextEffect, Is.EqualTo(EditorTextEffect.Indeterminate));
        Assert.That(h.Coordinator.LastReceipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.Indeterminate));
        Assert.That(h.Coordinator.Phase, Is.EqualTo(EditorControlPhase.Faulted));
        h.Key(Key.Y); Assert.That(h.Editor.Text, Is.EqualTo("base"));
    });

    [Test]
    public Task DetachDuringNativeOperationCompletesOnlyOriginalAndBlocksNewRegistration() => Ui(async () =>
    {
        await using var h = new Host();
        var original = h.Editor.Document;
        h.Editor.Document.Changed += (_, _) => h.Coordinator.Observe(() => h.Service.DetachEditor(h.Registration));
        h.Area.PerformTextInput("X");
        Assert.That(original.Text, Is.EqualTo("baseX"));
        Assert.That(h.Coordinator.Phase, Is.EqualTo(EditorControlPhase.Unavailable));
        Assert.That(h.Coordinator.Pending, Is.Zero);
        Assert.That(h.Coordinator.LastReceipt!.Finalization, Is.EqualTo(EditorFinalization.Complete));
        Assert.That(h.Coordinator.LastReceipt.EditorId, Is.EqualTo(h.Registration.Id));
    });

    [Test]
    public Task SecondServiceCannotStealRegisteredEditorSlot() => Ui(async () =>
    {
        await using var h = new Host();
        await using var other = new WorkbenchAutomationService();
        Assert.Throws<ExecutionBusyException>(() => other.AttachEditor(h.Editor, null!));
        Assert.That(h.Editor.Coordinator, Is.SameAs(h.Coordinator));
        Assert.That(h.Coordinator.Phase, Is.EqualTo(EditorControlPhase.Gui));
        h.Area.PerformTextInput("X"); Assert.That(h.Editor.Text, Is.EqualTo("baseX"));
    });

    [Test]
    public Task OutstandingTicketsAreBoundedAndCannotBeDisposedFromInsideTheirMutation() => Ui(async () =>
    {
        await using var h = new Host();
        var ticket = h.Coordinator.TryBegin()!;
        h.Coordinator.Commit(ticket, () =>
        {
            h.Coordinator.Observe(ticket.Dispose);
            h.Editor.Document.Insert(4, "X");
        });
        Assert.That((await ticket.Completion).TextEffect, Is.EqualTo(EditorTextEffect.Applied));
        Assert.That((await ticket.Completion).Error, Is.EqualTo(EditorOperationError.CallbackFailed));
        var pending = Enumerable.Range(0, 64).Select(_ => h.Coordinator.TryBegin()).ToArray();
        Assert.That(pending.All(t => t is not null), Is.True);
        Assert.That(h.Coordinator.TryBegin(), Is.Null);
        var drain = h.Coordinator.BeginDrain();
        Assert.That(drain.Completion.IsCompleted, Is.False);
        foreach (var item in pending) item!.Dispose();
        await drain.Completion;
        Assert.That(h.Coordinator.Pending, Is.Zero);
    });

    // AP2.1-F1: permanent regressions for the independently reproduced transitions.
    [Test]
    public Task StaleServiceCannotUnbindSuccessorOrEnableInputAndHistory() => Ui(async () =>
    {
        await using var h = new Host();
        await using var successor = new WorkbenchAutomationService();
        h.Editor.Document.Insert(4, "A"); h.Editor.Document.Insert(5, "B"); h.Key(Key.Z);
        var originalDocument = h.Editor.Document;
        h.Service.DetachEditor(h.Registration);
        var successorRegistration = successor.AttachEditor(h.Editor, null!)!;
        var owner = successor.EditorCoordinator;
        var drain = owner.BeginDrain();
        var snapshot = successorRegistration.Snapshot();
        var next = new WorkbenchTextEditor { Text = "next" };
        h.Service.AttachEditor(next, null!);
        h.Service.DetachEditor(h.Registration); // stale and already detached
        await h.Service.DisposeAsync();
        await h.Service.DisposeAsync();
        Assert.That(h.Editor.Coordinator, Is.SameAs(owner));
        Assert.That(owner.Phase, Is.EqualTo(EditorControlPhase.Releasing));
        h.Window.KeyTextInput("BAD"); h.Area.PerformTextInput("BAD");
        h.Key(Key.Z); h.Key(Key.Y);
        Assert.That(successorRegistration.Snapshot(), Is.EqualTo(snapshot));
        Assert.That(h.Editor.Document, Is.SameAs(originalDocument));
        Assert.That(originalDocument.UndoStack.CanUndo && originalDocument.UndoStack.CanRedo, Is.True);
        Assert.That(owner.ResumeGui(drain), Is.True);
        h.Key(Key.Y); Assert.That(h.Editor.Text, Is.EqualTo("baseAB"));
        h.Key(Key.Z); Assert.That(h.Editor.Text, Is.EqualTo("baseA"));
    });

    [Test]
    public Task FailedAttachAndDocumentChangePreserveCurrentBindingAndDeferredDetach() => Ui(async () =>
    {
        await using var h = new Host();
        var owner = h.Coordinator;
        var original = h.Editor.Document;
        var next = new WorkbenchTextEditor { Text = "next" };
        var ticket = owner.TryBegin()!;
        Assert.Throws<ExecutionBusyException>(() => h.Service.AttachEditor(next, null!));
        Assert.That(h.Editor.Coordinator, Is.SameAs(owner));
        Assert.That(next.Coordinator, Is.Null);
        ticket.Dispose();
        using (h.Editor.LockIdentity())
        {
            Assert.Throws<ExecutionBusyException>(() => h.Service.AttachEditor(next, null!));
            Assert.Throws<ExecutionBusyException>(() => h.Editor.Document = new TextDocument("BAD"));
            h.Service.DetachEditor(h.Registration);
            Assert.That(h.Registration.Attached, Is.True, "identity owner still holds deferred detach");
            Assert.That(h.Editor.Document, Is.SameAs(original));
        }
        Assert.That(h.Registration.Attached, Is.False);
        await using var successor = new WorkbenchAutomationService();
        successor.AttachEditor(h.Editor, null!);
        h.Service.AttachEditor(next, null!);
        Assert.That(h.Editor.Coordinator, Is.SameAs(successor.EditorCoordinator));
        h.Area.PerformTextInput("X");
        Assert.That(h.Editor.Text, Is.EqualTo("baseX"));
    });

    [TestCase(false)]
    [TestCase(true)]
    public Task CallbackSelfDisposeWaitsForExitAndPreservesActualEffects(bool applyText) => Ui(async () =>
    {
        await using var h = new Host();
        var ticket = h.Coordinator.TryBegin()!;
        var callback = ticket.RegisterCallback();
        if (applyText)
            Assert.That(h.Coordinator.Commit(ticket, () => h.Editor.Document.Insert(4, "X")), Is.True);
        else ticket.Dispose();
        bool completedInside = true, mutationAdmitted = true;
        int pendingInside = -1, nestedCalls = 0;
        callback.Observe(() =>
        {
            callback.Dispose(); callback.Dispose();
            completedInside = ticket.Completion.IsCompleted;
            pendingInside = h.Coordinator.Pending;
            mutationAdmitted = h.Coordinator.RunGui(() => h.Editor.AppendText("BAD"));
            callback.Observe(() => nestedCalls++);
            throw new InvalidOperationException("actual late callback failure");
        });
        callback.Dispose();
        var receipt = await ticket.Completion;
        Assert.That(completedInside, Is.False);
        Assert.That(pendingInside, Is.EqualTo(2), "operation and executing callback remain obligations");
        Assert.That(mutationAdmitted, Is.False);
        Assert.That(nestedCalls, Is.Zero, "dispose request must not accept another callback execution");
        Assert.That(receipt.Error, Is.EqualTo(EditorOperationError.CallbackFailed));
        Assert.That(receipt.TextEffect, Is.EqualTo(applyText ? EditorTextEffect.Applied : EditorTextEffect.None));
        Assert.That(receipt.HistoryEffect, Is.EqualTo(applyText ? EditorHistoryEffect.Applied : EditorHistoryEffect.None));
        Assert.That(receipt.Finalization, Is.EqualTo(EditorFinalization.Complete));
        Assert.That(h.Editor.Text, Is.EqualTo(applyText ? "baseX" : "base"));
        Assert.That(h.Coordinator.Pending, Is.Zero);
        Assert.That(h.Coordinator.ObserverDepth, Is.Zero);
        Assert.Throws<ExecutionBusyException>(() => ticket.RegisterCallback());
        if (applyText) { h.Key(Key.Z); Assert.That(h.Editor.Text, Is.EqualTo("base")); }
    });

    [Test]
    public Task NestedCallbackSelfDisposeCannotCompleteOuterExecution() => Ui(async () =>
    {
        await using var h = new Host();
        var ticket = h.Coordinator.TryBegin()!;
        var callback = ticket.RegisterCallback();
        ticket.Dispose();
        bool completedInOuter = true;
        callback.Observe(() =>
        {
            callback.Observe(callback.Dispose);
            completedInOuter = ticket.Completion.IsCompleted;
            throw new InvalidOperationException("outer failure after nested disposal");
        });
        Assert.That(completedInOuter, Is.False);
        Assert.That((await ticket.Completion).Error, Is.EqualTo(EditorOperationError.CallbackFailed));
        Assert.That(h.Coordinator.Pending, Is.Zero);
    });

    [TestCase("update")]
    [TestCase("identity")]
    [TestCase("mutation")]
    public Task CommitRechecksCurrentUpdateAndLocksBeforeAnyEffect(string barrier) => Ui(async () =>
    {
        // Native TextArea update subscribers can themselves affect optional history.
        // Compare to the same foreign group without any Coordinator.Commit call.
        bool expectedUndo = true, expectedRedo = true;
        string expectedRedoText = "baseAB", expectedUndoText = "baseA";
        if (barrier == "update")
        {
            await using var control = new Host();
            control.Editor.Document.Insert(4, "A"); control.Editor.Document.Insert(5, "B"); control.Key(Key.Z);
            control.Editor.Document.BeginUpdate();
            control.Editor.Document.EndUpdate();
            expectedUndo = control.Editor.Document.UndoStack.CanUndo;
            expectedRedo = control.Editor.Document.UndoStack.CanRedo;
            control.Key(Key.Y); expectedRedoText = control.Editor.Text;
            control.Key(Key.Z); expectedUndoText = control.Editor.Text;
        }
        await using var h = new Host();
        var document = h.Editor.Document;
        h.Editor.Document.Insert(4, "A"); h.Editor.Document.Insert(5, "B"); h.Key(Key.Z);
        var snapshot = h.Registration.Snapshot();
        var version = document.Version;
        var ticket = h.Coordinator.TryBegin()!;
        IDisposable? lease = null;
        if (barrier == "update") document.BeginUpdate();
        if (barrier == "identity") lease = h.Editor.LockIdentity();
        if (barrier == "mutation") lease = h.Editor.LockMutations();
        var historyAtCommit = (document.UndoStack.CanUndo, document.UndoStack.CanRedo);
        int historySignals = 0;
        System.ComponentModel.PropertyChangedEventHandler historyChanged = (_, _) => historySignals++;
        document.UndoStack.PropertyChanged += historyChanged;
        try
        {
            bool delegated = false;
            Assert.That(h.Coordinator.Commit(ticket, () => { delegated = true; document.Insert(5, "BAD"); }), Is.False);
            Assert.That(delegated, Is.False);
            Assert.That(document.Version, Is.SameAs(version));
            Assert.That((document.UndoStack.CanUndo, document.UndoStack.CanRedo), Is.EqualTo(historyAtCommit));
            Assert.That(historySignals, Is.Zero);
            Assert.That(h.Registration.Snapshot(), Is.EqualTo(snapshot));
            var receipt = await ticket.Completion;
            Assert.That(receipt.TextEffect, Is.EqualTo(EditorTextEffect.None));
            Assert.That(receipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.None));
            Assert.That(receipt.Finalization, Is.EqualTo(EditorFinalization.Complete));
            Assert.That(receipt.Error, Is.EqualTo(EditorOperationError.Stale));
            Assert.That(document.IsInUpdate, Is.EqualTo(barrier == "update"));
            Assert.That(h.Coordinator.Phase, Is.EqualTo(EditorControlPhase.Gui));
        }
        finally
        {
            document.UndoStack.PropertyChanged -= historyChanged;
            lease?.Dispose();
            if (barrier == "update") document.EndUpdate();
        }
        Assert.That((document.UndoStack.CanUndo, document.UndoStack.CanRedo), Is.EqualTo((expectedUndo, expectedRedo)));
        h.Key(Key.Y); Assert.That(h.Editor.Text, Is.EqualTo(expectedRedoText));
        h.Key(Key.Z); Assert.That(h.Editor.Text, Is.EqualTo(expectedUndoText));
    });

    [Test]
    public Task OwnAtomicEditCanAcquireIdentityAndUpdateAfterCommitAdmission() => Ui(async () =>
    {
        await using var h = new Host();
        var ticket = h.Coordinator.TryBegin()!;
        Assert.That(h.Coordinator.Commit(ticket, () =>
        {
            using var identity = h.Editor.LockIdentity();
            using (h.Editor.Document.RunUpdate())
            {
                h.Editor.AllowSingleEditChange();
                h.Editor.Document.Insert(4, "X");
            }
        }), Is.True);
        var receipt = await ticket.Completion;
        Assert.That(receipt.TextEffect, Is.EqualTo(EditorTextEffect.Applied));
        Assert.That(receipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.Applied));
        Assert.That(receipt.Error, Is.Null);
        Assert.That(h.Editor.Document.IsInUpdate, Is.False);
        h.Key(Key.Z); Assert.That(h.Editor.Text, Is.EqualTo("base"));
        h.Key(Key.Y); Assert.That(h.Editor.Text, Is.EqualTo("baseX"));
    });

    [TestCase(false)]
    [TestCase(true)]
    public Task ActualFocusLossPreservesOwnDrainIdentityButInvalidatesInput(bool pendingCallback) => Ui(async () =>
    {
        await using var h = new Host();
        var ticket = h.Coordinator.TryBegin()!;
        var callback = ticket.RegisterCallback();
        ticket.Dispose();
        if (!pendingCallback) callback.Dispose();
        var drain = h.Coordinator.BeginDrain();
        long generation = h.Coordinator.Generation;
        await using var second = new Host(); // actual headless focus transfer / LostFocus
        Assert.That(h.Coordinator.Generation, Is.GreaterThan(generation));
        Assert.That(h.Coordinator.BeginDrain(), Is.SameAs(drain));
        int staleCalls = 0;
        callback.Observe(() => staleCalls++);
        Assert.That(staleCalls, Is.Zero);
        if (pendingCallback)
        {
            Assert.That(drain.Completion.IsCompleted, Is.False);
            Assert.That(h.Coordinator.ResumeGui(drain), Is.False);
            callback.Dispose();
        }
        await drain.Completion;
        Assert.That(h.Coordinator.ResumeGui(drain), Is.True);
        Assert.That(h.Coordinator.ResumeGui(drain), Is.False);
        Assert.That(h.Coordinator.Commit(ticket, () => h.Editor.AppendText("BAD")), Is.False);
        Assert.That(h.Area.Focus(), Is.True);
        h.Area.PerformTextInput("X");
        Assert.That(h.Editor.Text, Is.EqualTo("baseX"));
    });

    [Test]
    public Task ForeignOldObserverAndFaultedDrainsCannotReleaseSuccessor() => Ui(async () =>
    {
        await using var h = new Host();
        await using var second = new Host();
        var own = h.Coordinator.BeginDrain();
        var foreign = second.Coordinator.BeginDrain();
        Assert.That(h.Coordinator.ResumeGui(foreign), Is.False);
        bool resumedInObserver = true;
        h.Coordinator.Observe(() => resumedInObserver = h.Coordinator.ResumeGui(own));
        Assert.That(resumedInObserver, Is.False);
        Assert.That(h.Coordinator.ResumeGui(own), Is.True);
        var newer = h.Coordinator.BeginDrain();
        Assert.That(newer, Is.Not.SameAs(own));
        Assert.That(h.Coordinator.ResumeGui(own), Is.False);
        Assert.That(h.Coordinator.ResumeGui(newer), Is.True);
        h.Coordinator.RunGui(() => throw new InvalidOperationException("fault"));
        var faulted = h.Coordinator.BeginDrain();
        Assert.That(faulted.Completion.IsCompleted, Is.False);
        Assert.That(h.Coordinator.ResumeGui(own), Is.False);
        Assert.That(h.Coordinator.ResumeGui(faulted), Is.False);
    });
}
