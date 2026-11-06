using AvaloniaEdit.Document;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class HistoryCoordinatorTests
{
    [TestCase("started")][TestCase("changing")][TestCase("changed")][TestCase("finished")][TestCase("detach")][TestCase("swap")][TestCase("property")]
    public Task FailuresAndLifecycleRemainBoundToOriginal(string trigger) => Run(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        var history = new HistoryService(); var main = new MainViewModel(history);
        using var vm = new CryptoScriptEditViewModel(new(), new(), new(), main, null!, history, service);
        var editor = new WorkbenchTextEditor { Text = "base" };
        vm.TextEditor = editor; vm.AttachEditor();
        var original = editor.Document;
        history.AddLine("target"); history.AddLine("last");
        if (trigger == "started") original.UpdateStarted += (_, _) => throw new Exception();
        if (trigger == "changing") original.Changing += (_, _) => throw new Exception();
        if (trigger == "finished") original.UpdateFinished += (_, _) => throw new Exception();
        if (trigger == "property") editor.IsModified = false;
        if (trigger == "property") editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == AvaloniaEdit.TextEditor.IsModifiedProperty) throw new Exception();
        };
        original.Changed += (_, _) =>
        {
            if (trigger == "changed") throw new Exception();
            if (trigger == "detach") vm.DetachEditor();
            if (trigger == "swap") editor.Document = new TextDocument("successor");
        };
        main.LineHistoryBackCommand.Execute();
        Assert.That(history.LineCanBack, Is.False, "index is not native undo state");
        Assert.That(editor.Document, Is.SameAs(original));
        var receipt = service.EditorCoordinator.LastReceipt!;
        if (trigger == "detach")
        {
            Assert.That(receipt.TextEffect, Is.EqualTo(EditorTextEffect.Applied));
            var successor = new WorkbenchTextEditor { Text = "successor" };
            vm.TextEditor = successor; vm.AttachEditor();
            Assert.That(successor.Text, Is.EqualTo("successor"));
        }
        else
        {
            Assert.That(receipt.Finalization, Is.EqualTo(EditorFinalization.Faulted));
            Assert.That(receipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.Indeterminate));
            Assert.That(receipt.Error, Is.Not.Null);
            Assert.That(service.EditorCoordinator.CanStartGui, Is.False);
        }
    });

    [Test]
    public void MissingBoundEditorNeverMovesIndex()
    {
        var history = new HistoryService();
        history.AddLine("first"); history.AddLine("second");
        new MainViewModel(history).LineHistoryBackCommand.Execute();
        history.LineBack();
        Assert.That(history.LineCanForward, Is.False);
    }

    private static Task Run(Func<Task> test) => GuiTestSession.Session.Dispatch(async () => { await test(); return true; }, CancellationToken.None);

    [TestCase("base", "ä€😀")]
    [TestCase("", "")]
    [TestCase("", "new")]
    [TestCase("prefix\n", "")]
    [TestCase("prefix\nlast", "x\ny")]
    [TestCase("same", "same")]
    public Task CommandsKeepOneUndoGroupAndSeparateNoOp(string original, string target) => Run(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        var history = new HistoryService();
        var main = new MainViewModel(history);
        using var vm = new CryptoScriptEditViewModel(new(), new(), new(), main, null!, history, service);
        var editor = new WorkbenchTextEditor { Text = original };
        vm.TextEditor = editor; vm.AttachEditor();
        history.AddLine(target); history.AddLine("forward");
        main.LineHistoryBackCommand.Execute();
        string expected = original[..(original.LastIndexOf('\n') + 1)] + target;
        Assert.That(editor.Text, Is.EqualTo(expected));
        var receipt = service.EditorCoordinator.LastReceipt!;
        Assert.That(receipt.TextEffect, Is.EqualTo(expected == original ? EditorTextEffect.None : EditorTextEffect.Applied));
        Assert.That(receipt.HistoryEffect, Is.EqualTo(expected == original ? EditorHistoryEffect.None : EditorHistoryEffect.Applied));
        if (expected != original)
        {
            editor.Document.UndoStack.Undo(); Assert.That(editor.Text, Is.EqualTo(original));
            Assert.That(editor.Document.UndoStack.CanUndo, Is.False);
            editor.Document.UndoStack.Redo(); Assert.That(editor.Text, Is.EqualTo(expected));
        }
        main.LineHistoryForwardCommand.Execute();
        Assert.That(editor.Text, Does.EndWith("forward"));
        editor.Document.UndoStack.Undo(); Assert.That(editor.Text, Is.EqualTo(expected));
    });

    [TestCase("drain")][TestCase("observer")][TestCase("active")][TestCase("missing")][TestCase("busy")][TestCase("fault")]
    public Task RejectedCommandsAndDirectCallsPreserveIndexAndStacks(string mode) => Run(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        var history = new HistoryService(); var main = new MainViewModel(history);
        using var vm = new CryptoScriptEditViewModel(new(), new(), new(), main, null!, history, service);
        var editor = new WorkbenchTextEditor { Text = "base" };
        vm.TextEditor = editor; vm.AttachEditor();
        history.AddLine("old"); history.AddLine("new");
        var c = service.EditorCoordinator;
        void Attempt() { main.LineHistoryBackCommand.Execute(); history.LineBack(); }
        IDisposable? busy = null;
        try
        {
            switch (mode)
            {
                case "observer": c.Observe(Attempt); break;
                case "active": c.RunGui(Attempt); break;
                case "missing": vm.DetachEditor(); Attempt(); break;
                case "busy": busy = InterpreterHostQueue.Reserve(); Attempt(); break;
                case "fault": c.RunGui(() => throw new Exception()); Attempt(); break;
                default: c.BeginDrain(); Attempt(); break;
            }
            Assert.That(history.LineCanForward, Is.False);
            Assert.That(editor.Text, Is.EqualTo("base"));
            Assert.That(editor.Document.UndoStack.CanUndo, Is.False);
        }
        finally { busy?.Dispose(); }
    });

    [TestCase(false)][TestCase(true)]
    public Task HistoryObserversCannotReenterAndTheirFailureKeepsEffects(bool noOp) => Run(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        var history = new HistoryService(); var main = new MainViewModel(history);
        using var vm = new CryptoScriptEditViewModel(new(), new(), new(), main, null!, history, service);
        var editor = new WorkbenchTextEditor { Text = "base" };
        vm.TextEditor = editor; vm.AttachEditor();
        history.AddLine(noOp ? "base" : "target"); history.AddLine("last");
        history.LineHistoryChanged += (_, _) => { history.LineForward(); throw new Exception("observer"); };
        main.LineHistoryBackCommand.Execute();
        Assert.That(history.LineCanBack, Is.False);
        Assert.That(editor.Text, Is.EqualTo(noOp ? "base" : "target"));
        var receipt = service.EditorCoordinator.LastReceipt!;
        Assert.That(receipt.Error, Is.EqualTo(EditorOperationError.CallbackFailed));
        Assert.That(receipt.Finalization, Is.EqualTo(EditorFinalization.Complete));
        Assert.That(receipt.HistoryEffect, Is.EqualTo(noOp ? EditorHistoryEffect.None : EditorHistoryEffect.Applied));
    });

    [Test]
    public Task DocumentAndPropertySubscribersCannotStartAnotherHistoryOrServiceWrite() => Run(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        var history = new HistoryService(); var main = new MainViewModel(history);
        using var vm = new CryptoScriptEditViewModel(new(), new(), new(), main, null!, history, service);
        var editor = new WorkbenchTextEditor { Text = "base" };
        editor.IsModified = false;
        vm.TextEditor = editor; vm.AttachEditor();
        history.AddLine("target"); history.AddLine("last");
        int callbacks = 0, rejected = 0;
        void Reenter()
        {
            callbacks++;
            main.LineHistoryForwardCommand.Execute(); history.LineForward();
            try { service.Mutate(() => throw new InvalidOperationException("must not run")); }
            catch (CryptoScript.Model.ExecutionBusyException) { rejected++; }
        }
        editor.Document.Changed += (_, _) => Reenter();
        editor.PropertyChanged += (_, e) => { if (e.Property == AvaloniaEdit.TextEditor.IsModifiedProperty) Reenter(); };
        main.LineHistoryBackCommand.Execute();
        Assert.That(callbacks, Is.GreaterThanOrEqualTo(2));
        Assert.That(rejected, Is.EqualTo(callbacks));
        Assert.That(editor.Text, Is.EqualTo("target"));
        Assert.That(history.LineCanBack, Is.False);
        Assert.That(service.EditorCoordinator.LastReceipt!.Finalization, Is.EqualTo(EditorFinalization.Complete));
    });
}
