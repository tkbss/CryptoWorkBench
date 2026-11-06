using System.Reflection;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class B3F3Tests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);
    private sealed class Confirmation(Func<CancellationToken, Task<bool?>> answer) : IEditorReplaceConfirmation
    {
        public Task<bool?> ConfirmAsync(TextEditor editor, EditorReplaceApproval approval, CancellationToken ct) => answer(ct);
    }

    private static async Task<(AutomationCaller, EditorEditV2)> Bind(Host h, EditorEditMode mode)
    {
        var s = h.Service;
        s.ActivateMcp();
        var c = s.IssueCredential("f3", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var original = h.Registration.Snapshot();
        var l = (await s.AcquireEditorAsync(c, new("2", s.InstanceId, original.EditorId, Guid.NewGuid(), original.Revision), default)).Result!;
        return (c, new("2", s.InstanceId, l.EditorId, l.LeaseId, original.Revision, mode, "!", mode == EditorEditMode.Insert ? 0 : null));
    }

    [TestCase(EditorEditMode.Insert, "accepted", false)]
    [TestCase(EditorEditMode.Append, "started", false)]
    [TestCase(EditorEditMode.Replace, "started", true)]
    [TestCase(EditorEditMode.Insert, "finished", true)]
    [TestCase(EditorEditMode.Replace, "finished", false)]
    public Task AcceptedOriginalCanFinishWhileL1Returns(EditorEditMode mode, string phase, bool shutdown) => Ui(async () =>
    {
        await using var h = new Host("A");
        var (caller, edit) = await Bind(h, mode);
        h.Service.ReplaceConfirmation = new Confirmation(_ => Task.FromResult<bool?>(true));
        Task? drain = null;
        bool returned = false, pending = false;
        string? atFence = null;
        void Fence()
        {
            atFence = h.Editor.Text;
            var worker = Task.Run(() =>
            {
                if (shutdown) h.Service.BeginShutdown(); else h.Service.DisableMcp();
                drain = h.Service.DrainAsync();
            });
            returned = worker.Wait(Limit);
            pending = drain is { IsCompleted: false };
        }
        EventHandler start = (_, _) => { if (phase == "started") Fence(); };
        EventHandler end = (_, _) => { if (phase == "finished") Fence(); };
        h.Editor.Document.UpdateStarted += start;
        h.Editor.Document.UpdateFinished += end;
        h.Service.AdmissionCheckpoint = stage => { if (stage == "edit.accepted" && phase == "accepted") Fence(); };
        EditorMutationReceiptV2 result;
        try { result = await h.Service.UpdateEditorV2Async(caller, edit, default); }
        finally
        {
            h.Service.AdmissionCheckpoint = null;
            h.Editor.Document.UpdateStarted -= start;
            h.Editor.Document.UpdateFinished -= end;
        }
        var expected = mode switch { EditorEditMode.Insert => "!A", EditorEditMode.Append => "A!", _ => "!" };
        Assert.That((returned, pending, atFence), Is.EqualTo((true, true, phase == "finished" ? expected : "A")));
        await drain!.WaitAsync(Limit);
        Assert.That((result.EditorId, result.ExpectedRevision, result.ObservedRevision),
            Is.EqualTo((edit.EditorId, edit.ExpectedRevision, h.Registration.Snapshot().Revision)));
        Assert.That(result.ObservedRevision, Is.Not.EqualTo(result.ExpectedRevision));
        Assert.That((result.TextEffect, result.HistoryEffect, result.Finalization, result.Error),
            Is.EqualTo((TextEffect.Applied, HistoryEffect.Applied, Finalization.Complete, (SafeError?)null)));
        Assert.That((h.Editor.Text, h.Editor.Document.IsInUpdate, h.Owner.Pending), Is.EqualTo((expected, false, 0)));
        Assert.That((await h.Service.UpdateEditorV2Async(caller, edit, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        if (!shutdown)
        {
            ApplicationCommands.Undo.Execute(null, h.Area);
            Assert.That(h.Editor.Text, Is.EqualTo("A"));
            ApplicationCommands.Redo.Execute(null, h.Area);
            Assert.That(h.Editor.Text, Is.EqualTo(expected));
            h.Editor.CaretOffset = expected.Length;
            h.Area.PerformTextInput("x");
            Assert.That(h.Editor.Text, Is.EqualTo(expected + "x"));
        }
    });

    [Test]
    public Task RegisteredOriginalCallbackCanFinishOnDispatcherAfterNativeEdit() => Ui(async () =>
    {
        await using var h = new Host("A");
        var (c, edit) = await Bind(h, EditorEditMode.Append);
        EditorCoordinator.CallbackTicket? callback = null;
        h.Service.AdmissionCheckpoint = _ =>
        {
            var ticket = (EditorCoordinator.OperationTicket)typeof(EditorCoordinator)
                .GetField("active", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(h.Owner)!;
            callback = ticket.RegisterCallback();
        };
        try
        {
            var reply = h.Service.UpdateEditorV2Async(c, edit, default);
            Assert.That(callback, Is.Not.Null);
            h.Service.DisableMcp();
            var drain = h.Service.DrainAsync();
            Assert.That((h.Editor.Text, reply.IsCompleted, drain.IsCompleted), Is.EqualTo(("A!", false, false)));
            callback!.Dispose();
            Assert.That((await reply.WaitAsync(Limit)).TextEffect, Is.EqualTo(TextEffect.Applied));
            await drain.WaitAsync(Limit);
            Assert.That(h.Owner.Pending, Is.Zero);
        }
        finally { callback?.Dispose(); h.Service.AdmissionCheckpoint = null; }
    });

    [Test]
    public Task ReplaceCannotCommitAfterL1WinsDuringConsent() => Ui(async () =>
    {
        await using var h = new Host("A");
        var (c, edit) = await Bind(h, EditorEditMode.Replace);
        var gate = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Service.ReplaceConfirmation = new Confirmation(_ => gate.Task);
        var work = h.Service.UpdateEditorV2Async(c, edit, default);
        await Task.Run(h.Service.DisableMcp);
        var drain = h.Service.DrainAsync();
        Assert.That(drain.IsCompleted, Is.False);
        gate.SetResult(true);
        var result = await work;
        Assert.That((result.TextEffect, result.HistoryEffect, result.Error!.Code),
            Is.EqualTo((TextEffect.None, HistoryEffect.None, AutomationErrorCode.Forbidden)));
        await drain.WaitAsync(Limit);
        Assert.That((h.Editor.Text, h.Registration.Snapshot().Revision), Is.EqualTo(("A", edit.ExpectedRevision)));
    });

    [Test]
    public Task LegacyAndV2StatusProjectActiveAndLastIndependentlyOnUuidCollision() => Ui(async () =>
    {
        await using var h = new Host("A");
        var s = h.Service;
        var (a, edit) = await Bind(h, EditorEditMode.Append);
        var b = s.IssueCredential("b", a.Permissions);
        var c = s.IssueCredential("c", a.Permissions.Clear().Add(AutomationPermissions.StatusRead));
        var aStatus = s.IssueCredential(a.PrincipalId, c.Permissions);
        var id = Guid.NewGuid();
        var version = (await s.GetStatusV2Async(a, new("2"), default)).Result!.StateVersion;
        var request = new EditorExecutionV2("2", s.InstanceId, edit.EditorId, edit.LeaseId, id, edit.ExpectedRevision, version);
        s.ExecutionConfirmation = (_, _, _) => Task.FromResult<bool?>(false);
        Assert.That((await s.ExecuteEditorV2Async(a, request, default)).Result!.State, Is.EqualTo(ExecutionState.Rejected));
        await s.ReleaseEditorAsync(a, new("2", s.InstanceId, edit.EditorId, edit.LeaseId), default);
        var lease = (await s.AcquireEditorAsync(b, new("2", s.InstanceId, edit.EditorId, Guid.NewGuid(), edit.ExpectedRevision), default)).Result!;
        var answer = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        s.ExecutionConfirmation = (_, _, _) => { entered.SetResult(); return answer.Task; };
        var second = s.ExecuteEditorV2Async(b, request with { LeaseId = lease.LeaseId }, default);
        async Task Check(AutomationCaller who, Guid? active, Guid? last)
        {
            var legacy = (await s.GetStatusAsync(who, default)).Data!;
            var current = (await s.GetStatusV2Async(who, new("2"), default)).Result!;
            Assert.That((legacy.ActiveExecutionId, legacy.LastExecutionId), Is.EqualTo((active, last)));
            Assert.That((current.ActiveExecutionId, current.LastExecutionId), Is.EqualTo((active, last)));
        }
        try
        {
            await entered.Task.WaitAsync(Limit);
            await Check(a, null, id);
            await Check(b, id, null);
            await Check(c, null, null);
            await Check(aStatus, null, null);
            var local = (await s.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default)).Data!;
            Assert.That((local.ActiveExecutionId, local.LastExecutionId), Is.EqualTo((id, id)));
        }
        finally { answer.TrySetResult(false); await second.WaitAsync(Limit); }
        await Check(a, null, null);
        await Check(b, null, id);
    });

    [Test]
    public Task WorkspaceAndConfirmationObserversCanWaitForRevocation() => Ui(async () =>
    {
        foreach (bool onAcquire in new[] { true, false })
        {
            await using var h = new Host("A");
            Task? drain = null;
            bool returned = false;
            void Revoke()
            {
                var worker = Task.Run(() => { h.Service.DisableMcp(); drain = h.Service.DrainAsync(); });
                returned = worker.Wait(Limit);
            }
            void Changed() { if (h.Owner.Phase == EditorControlPhase.Mcp && onAcquire) Revoke(); }
            h.Owner.WorkspaceChanged += Changed;
            try
            {
                var (c, edit) = await Bind(h, EditorEditMode.Append);
                if (!onAcquire)
                {
                    h.Service.ExecutionConfirmation = (_, _, _) => { Revoke(); return Task.FromResult<bool?>(true); };
                    var state = (await h.Service.GetStatusV2Async(c, new("2"), default)).Result!;
                    var reply = await h.Service.ExecuteEditorV2Async(c, new("2", h.Service.InstanceId, edit.EditorId,
                        edit.LeaseId, Guid.NewGuid(), edit.ExpectedRevision, state.StateVersion), default);
                    Assert.That(reply.Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
                }
                Assert.That(returned, Is.True);
                await drain!.WaitAsync(Limit);
                Assert.That(h.Editor.Text, Is.EqualTo("A"));
            }
            finally { h.Owner.WorkspaceChanged -= Changed; }
        }
    });

    [Test]
    public Task ShellExitAndDisposeAwaitOriginalDespiteThrowingCancellation() => Ui(async () =>
    {
        await using var h = new Host("A");
        var (c, edit) = await Bind(h, EditorEditMode.Replace);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var original = typeof(WorkbenchAutomationService).GetField("lease", flags)!.GetValue(h.Service)!;
        var lifetime = (CancellationTokenSource)original.GetType().GetField("Lifetime", flags)!.GetValue(original)!;
        var timer = (DispatcherTimer)typeof(WorkbenchAutomationService).GetField("leaseTimer", flags)!.GetValue(h.Service)!;
        var callback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Service.ReplaceConfirmation = new Confirmation(async token =>
        {
            using var registration = token.Register(() => { callback.SetResult(); throw new IOException("injected"); });
            return await answer.Task;
        });
        var ticket = h.Owner.TryBeginMcp()!;
        var held = ticket.RegisterCallback();
        ticket.Dispose();
        var work = h.Service.UpdateEditorV2Async(c, edit, default);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Window.Closed += (_, _) => closed.TrySetResult();
        CryptoWorkBenchAvalonia.App.CoordinateClosing(h.Window, h.Service);
        try
        {
            h.Window.Close();
            await callback.Task.WaitAsync(Limit);
            var first = h.Service.DisposeAsync().AsTask();
            var second = h.Service.DisposeAsync().AsTask();
            var strict = h.Service.DrainAsync();
            using var cancel = new CancellationTokenSource();
            var waiter = h.Service.DrainAsync(cancel.Token);
            cancel.Cancel();
            answer.SetResult(false);
            await work.WaitAsync(Limit);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.That(waiter.IsCanceled, Is.True);
            Assert.That(closed.Task.IsCompleted || first.IsCompleted || second.IsCompleted || strict.IsCompleted, Is.False);
            Assert.That(h.Owner.Pending, Is.Positive);
            held.Dispose();
            await Task.WhenAll(closed.Task, first, second).WaitAsync(Limit);
            await h.Service.DisposeAsync();
            Assert.That(strict.IsFaulted, Is.True);
            await Assert.ThatAsync(async () => await h.Service.DrainAsync(), Throws.InstanceOf<InvalidOperationException>());
            Assert.Throws<ObjectDisposedException>(() => _ = lifetime.Token);
            Assert.That((timer.IsEnabled, h.Owner.CanStartGui, h.Owner.Pending), Is.EqualTo((false, false, 0)));
            Assert.That((await h.Service.GetStatusV2Async(c, new("2"), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        }
        finally { answer.TrySetResult(false); held.Dispose(); }
    });
}
