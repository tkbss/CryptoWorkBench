using AvaloniaEdit.Document;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class HistoryCapabilityAdversarialTests
{
    private static Task Ui(Func<Task> body) => GuiTestSession.Session.Dispatch(async () =>
    { await body(); return true; }, CancellationToken.None);

    private sealed class Host : IAsyncDisposable
    {
        internal readonly WorkbenchAutomationService Service = new();
        internal readonly HistoryService History = new();
        internal readonly WorkbenchTextEditor Editor = new() { Text = "abcd" };
        internal readonly CryptoScriptEditViewModel Vm;
        internal EditorCoordinator C => Service.EditorCoordinator;
        internal Host()
        {
            Vm = new(new(), new(), new(), new(History), null!, History, Service);
            Vm.TextEditor = Editor; Vm.AttachEditor();
            History.AddLine("WXYZ"); History.AddLine("last");
        }
        public async ValueTask DisposeAsync() { Vm.Dispose(); await Service.DisposeAsync(); }
    }

    [TestCase(false)] [TestCase(true)]
    public Task ExpiredCapabilityCannotBeUsedByNextTicket(bool used) => Ui(async () =>
    {
        await using var h = new Host(); EditorCoordinator.HistoryCapability? expired = null;
        h.C.RunHistory(cap => {
            expired = cap;
            using var identity = h.Editor.LockIdentity();
            if (used) h.Editor.ReplaceHistoryLine(cap.Document, 0, 4, "WXYZ", cap);
        });
        string text = h.Editor.Text; var version = h.Editor.Document.Version;
        bool denied = false, nested = true;
        h.C.RunGui(() => {
            nested = h.C.RunHistory(_ => throw new Exception("must not enter"));
            using var identity = h.Editor.LockIdentity();
            try { h.Editor.ReplaceHistoryLine(h.Editor.Document, 0, 4, "STALE", expired); }
            catch (ExecutionBusyException) { denied = true; }
        });
        Assert.Multiple(() => {
            Assert.That(denied, Is.True); Assert.That(nested, Is.False);
            Assert.That(h.Editor.Text, Is.EqualTo(text));
            Assert.That(h.Editor.Document.Version, Is.SameAs(version));
            Assert.That(h.C.LastReceipt!.TextEffect, Is.EqualTo(EditorTextEffect.None));
            Assert.That(h.C.LastReceipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.None));
        });
    });

    [Test]
    public Task ConsumedCapabilityCannotBeBorrowedAtAnyNativeBoundary() => Ui(async () =>
    {
        await using var h = new Host(); EditorCoordinator.HistoryCapability? current = null;
        int denied = 0, nested = 0; var boundaries = new List<string>();
        void Attack(string boundary) {
            boundaries.Add(boundary);
            if (!h.C.RunHistory(_ => throw new Exception("nested"))) nested++;
            try { h.Editor.ReplaceHistoryLine(h.Editor.Document, 0, 0, "BAD", current); }
            catch (ExecutionBusyException) { denied++; }
        }
        h.Editor.Document.UpdateStarted += (_, _) => Attack("start");
        h.Editor.Document.Changing += (_, a) => Attack(a.RemovalLength > 0 ? "remove-before" : "insert-before");
        h.Editor.Document.Changed += (_, a) => Attack(a.RemovalLength > 0 ? "remove-after" : "insert-after");
        h.Editor.Document.UpdateFinished += (_, _) => Attack("finish");
        h.C.RunHistory(cap => {
            current = cap;
            using var identity = h.Editor.LockIdentity();
            h.Editor.ReplaceHistoryLine(cap.Document, 0, 4, "WXYZ", cap);
            Assert.Throws<ExecutionBusyException>(() => h.Editor.ReplaceHistoryLine(cap.Document, 0, 4, "AGAIN", cap));
        });
        Assert.Multiple(() => {
            Assert.That(boundaries, Is.EqualTo(new[]{"start","remove-before","remove-after","insert-before","insert-after","finish"}));
            Assert.That(denied, Is.EqualTo(6)); Assert.That(nested, Is.EqualTo(6));
            Assert.That(h.Editor.Text, Is.EqualTo("WXYZ"));
            Assert.That(h.C.LastReceipt!.Finalization, Is.EqualTo(EditorFinalization.Complete));
        });
    });

    [TestCase("before")] [TestCase("remove")] [TestCase("insert")]
    public Task FaultPermanentlyInvalidatesCapability(string point) => Ui(async () =>
    {
        await using var h = new Host(); EditorCoordinator.HistoryCapability? saved = null;
        int changes = 0;
        h.Editor.Document.Changed += (_, _) => {
            changes++; if (point == "remove" && changes == 1 || point == "insert" && changes == 2) throw new Exception("native");
        };
        h.C.RunHistory(cap => {
            saved = cap;
            if (point == "before") throw new Exception("before use");
            using var identity = h.Editor.LockIdentity();
            h.Editor.ReplaceHistoryLine(cap.Document, 0, 4, "WXYZ", cap);
        });
        var version = h.Editor.Document.Version;
        Assert.Throws<ExecutionBusyException>(() => saved!.Consume(h.Editor, h.Editor.Document));
        Assert.Multiple(() => {
            Assert.That(h.Editor.Text, Is.EqualTo(point == "before" ? "abcd" : point == "remove" ? "" : "WXYZ"));
            Assert.That(h.Editor.Document.Version, Is.SameAs(version));
            Assert.That(h.C.Phase, Is.EqualTo(EditorControlPhase.Faulted));
            Assert.That(h.C.LastReceipt!.TextEffect, Is.EqualTo(EditorTextEffect.Indeterminate));
            Assert.That(h.C.LastReceipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.Indeterminate));
            Assert.That(h.C.LastReceipt.Finalization, Is.EqualTo(EditorFinalization.Faulted));
            Assert.That(h.C.Pending, Is.Zero); Assert.That(h.C.Active, Is.False);
        });
    });

    [Test]
    public Task FailedQueueReservationNeverPublishesCapability() => Ui(async () =>
    {
        await using var h = new Host(); bool entered = false;
        using (InterpreterHostQueue.Reserve()) {
            Assert.That(h.C.RunHistory(_ => entered = true), Is.False);
            Assert.That(entered, Is.False); Assert.That(h.C.Pending, Is.Zero);
            Assert.That(h.Editor.Text, Is.EqualTo("abcd"));
            Assert.That(h.Editor.Document.UndoStack.CanUndo, Is.False);
            Assert.That(h.C.LastReceipt!.TextEffect, Is.EqualTo(EditorTextEffect.None));
        }
        h.History.LineBack(); Assert.That(h.Editor.Text, Is.EqualTo("WXYZ"));
    });

    [Test]
    public Task CapabilityRejectsForeignEditorAndDocument() => Ui(async () =>
    {
        await using var h = new Host(); var other = new WorkbenchTextEditor { Text = "other" };
        h.C.RunHistory(cap => {
            Assert.Throws<ExecutionBusyException>(() => cap.Consume(other, h.Editor.Document));
            Assert.Throws<ExecutionBusyException>(() => cap.Consume(h.Editor, new TextDocument("foreign")));
            using var identity = h.Editor.LockIdentity();
            h.Editor.ReplaceHistoryLine(cap.Document, 0, 4, "WXYZ", cap);
        });
        Assert.That(h.Editor.Text, Is.EqualTo("WXYZ")); Assert.That(other.Text, Is.EqualTo("other"));
    });

    [TestCase(false)] [TestCase(true)]
    public Task OriginalLifecycleRetainsQueueAndTicket(bool dispose) => Ui(async () =>
    {
        await using var h = new Host(); bool retained = false;
        h.Editor.Document.Changed += (_, _) => {
            if (dispose) h.Vm.Dispose(); else h.Vm.DetachEditor();
            retained = h.C.Active && h.C.Pending == 1 && InterpreterHostQueue.IsBusy && h.Editor.Document.IsInUpdate;
        };
        h.History.LineBack();
        Assert.That(retained, Is.True); Assert.That(h.Editor.Text, Is.EqualTo("WXYZ"));
        Assert.That(h.C.Pending, Is.Zero); Assert.That(InterpreterHostQueue.IsBusy, Is.False);
        Assert.That(h.C.LastReceipt!.Finalization, Is.EqualTo(EditorFinalization.Complete));
    });

    [Test]
    public Task EscapingUnauthorizedCallbackFaultsWithoutStolenText() => Ui(async () =>
    {
        await using var h = new Host();
        var originalVersion = h.Editor.Document.Version;
        h.Editor.Document.UpdateStarted += (_, _) => h.Editor.ReplaceHistoryLine(h.Editor.Document, 0, 4, "STOLEN");
        h.C.RunGui(() => {
            using var identity = h.Editor.LockIdentity();
            using (h.Editor.Document.RunUpdate()) {
                h.Editor.AllowSingleEditChange(); h.Editor.Document.Replace(0, 4, "intended");
            }
        });
        Assert.That(h.Editor.Text, Is.EqualTo("abcd"));
        Assert.That(h.Editor.Document.Version, Is.SameAs(originalVersion));
        // The outer BeginUpdate has already notified TextArea's native history
        // listener. Its escaped callback is not a no-history-effect rejection.
        Assert.That(h.Editor.Document.UndoStack.CanUndo, Is.True);
        Assert.That(h.Editor.Document.IsInUpdate, Is.True);
        Assert.That(h.C.LastReceipt!.Finalization, Is.EqualTo(EditorFinalization.Faulted));
        Assert.That(h.C.LastReceipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.Indeterminate));
        Assert.That(h.C.CanStartGui, Is.False);
    });
}
