using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class HistoryCapabilityTests
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

    [Test]
    public Task ForeignGeneralTicketHasNoHistoryPermission() => Ui(async () =>
    {
        await using var h = new Host(); bool denied = false;
        h.C.RunGui(() => {
            using var identity = h.Editor.LockIdentity();
            try { h.Editor.ReplaceHistoryLine(h.Editor.Document, 0, 4, "FOREIGN"); }
            catch (ExecutionBusyException) { denied = true; }
        });
        Assert.Multiple(() => {
            Assert.That(denied, Is.True);
            Assert.That(h.Editor.Text, Is.EqualTo("abcd"));
            Assert.That(h.Editor.Document.UndoStack.CanUndo, Is.False);
            Assert.That(h.History.LineCanForward, Is.False);
            Assert.That(h.C.LastReceipt!.TextEffect, Is.EqualTo(EditorTextEffect.None));
            Assert.That(h.C.LastReceipt.HistoryEffect, Is.EqualTo(EditorHistoryEffect.None));
            Assert.That(h.C.Pending, Is.Zero);
        });
        h.History.LineBack(); Assert.That(h.Editor.Text, Is.EqualTo("WXYZ"));
    });

    [Test]
    public Task ForeignUpdateStartedCannotStealOriginalPermission() => Ui(async () =>
    {
        await using var h = new Host(); bool denied = false;
        h.Editor.Document.UpdateStarted += (_, _) => {
            try { h.Editor.ReplaceHistoryLine(h.Editor.Document, 0, 4, "STOLEN"); }
            catch (ExecutionBusyException) { denied = true; }
        };
        h.C.RunGui(() => {
            using var identity = h.Editor.LockIdentity();
            using (h.Editor.Document.RunUpdate()) {
                h.Editor.AllowSingleEditChange();
                h.Editor.Document.Replace(0, 4, "intended");
            }
        });
        Assert.Multiple(() => {
            Assert.That(denied, Is.True);
            Assert.That(h.Editor.Text, Is.EqualTo("intended"));
            Assert.That(h.History.LineCanForward, Is.False);
            Assert.That(h.C.LastReceipt!.Finalization, Is.EqualTo(EditorFinalization.Complete));
            Assert.That(h.C.Pending, Is.Zero);
        });
        h.Editor.Document.UndoStack.Undo();
        Assert.That(h.Editor.Text, Is.EqualTo("abcd"));
        Assert.That(h.Editor.Document.UndoStack.CanUndo, Is.False);
    });
}
