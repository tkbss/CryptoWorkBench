using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class LeaseClockContractTests
{
    private sealed class DeferredDialog : IEditorReplaceConfirmation
    {
        internal readonly TaskCompletionSource<bool?> Answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<bool?> ConfirmAsync(AvaloniaEdit.TextEditor editor, EditorReplaceApproval approval, CancellationToken ct)
        {
            using var registration = ct.Register(() => Cancelled.TrySetResult());
            return await Answer.Task;
        }
    }

    private static AutomationCaller Activate(Host h)
    {
        h.Service.ActivateMcp();
        return h.Service.IssueCredential("clock-test", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
    }

    private static AcquireEditorV2 Acquire(Host h) => new("2", h.Service.InstanceId, h.Registration.Id,
        Guid.NewGuid(), h.Registration.Snapshot().Revision);

    [TestCase(0UL)]
    [TestCase(1000UL)]
    [TestCase(ulong.MaxValue - 600_000)]
    public Task EqualSamplesAndMaximumSafeDeadlineRemainValid(ulong time) => Ui(async () =>
    {
        await using var h = new Host("A", new WorkbenchAutomationService { LeaseMilliseconds = () => time });
        var caller = Activate(h);
        var request = Acquire(h);
        var first = await h.Service.AcquireEditorAsync(caller, request, default);
        Assert.That(first.Error, Is.Null);
        var replay = await h.Service.AcquireEditorAsync(caller, request, default);
        Assert.That(replay.Replayed, Is.True);
        Assert.That(replay.Result, Is.EqualTo(first.Result));
        var renew = new RenewEditorV2("2", request.InstanceId, request.EditorId, first.Result!.LeaseId, "1");
        var renewed = await h.Service.RenewEditorAsync(caller, renew, default);
        Assert.That(renewed.Result!.LeaseVersion, Is.EqualTo("2"));
        var repeated = await h.Service.RenewEditorAsync(caller, renew, default);
        Assert.That(repeated.Replayed, Is.True);
        Assert.That(repeated.Result, Is.EqualTo(renewed.Result));
        Assert.That(h.Owner.CanStartGui, Is.False);
    });

    [TestCase(599_999UL, false)]
    [TestCase(600_000UL, true)]
    [TestCase(600_001UL, true)]
    [TestCase(7_230_000UL, true)]
    public Task FirstAccessChecksDeadlineWithoutATimerTick(ulong elapsed, bool expired) => Ui(async () =>
    {
        ulong time = 1000;
        await using var h = new Host("A", new WorkbenchAutomationService { LeaseMilliseconds = () => time });
        var caller = Activate(h);
        var request = Acquire(h);
        var acquired = (await h.Service.AcquireEditorAsync(caller, request, default)).Result!;
        // Stay on this dispatcher turn: no await/yield/message pumping between jump and admission.
        time += elapsed;
        var pending = h.Service.RenewEditorAsync(caller, new("2", request.InstanceId, request.EditorId, acquired.LeaseId, "1"), default);
        Assert.That(pending.IsCompletedSuccessfully, Is.True);
        var reply = pending.GetAwaiter().GetResult();
        if (expired)
        {
            Assert.That(reply.Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseExpired));
            Assert.That(reply.Result, Is.Null);
            var edit = await h.Service.UpdateEditorV2Async(caller, new("2", request.InstanceId, request.EditorId,
                acquired.LeaseId, request.ExpectedRevision, EditorEditMode.Append, "bad"), default);
            Assert.That(edit.Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseExpired));
            Assert.That(h.Editor.Text, Is.EqualTo("A"));
            Assert.That(h.Owner.CanStartGui, Is.True);
        }
        else
        {
            Assert.That(reply.Error, Is.Null);
            Assert.That(reply.Result!.LeaseVersion, Is.EqualTo("2"));
            time += 600_000;
            Assert.That((await h.Service.RenewEditorAsync(caller, new("2", request.InstanceId, request.EditorId,
                acquired.LeaseId, "2"), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseExpired));
        }
    });

    private sealed class ReceiptClock : TimeProvider
    {
        internal DateTimeOffset Utc = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Utc;
    }

    [TestCase(-24)]
    [TestCase(24)]
    public Task UtcJumpAndReplaysCannotMoveTheAuthorizationDeadline(int hours) => Ui(async () =>
    {
        ulong time = 1000;
        var utc = new ReceiptClock();
        await using var h = new Host("A", new WorkbenchAutomationService(utc) { LeaseMilliseconds = () => time });
        var caller = Activate(h);
        var request = Acquire(h);
        var acquired = (await h.Service.AcquireEditorAsync(caller, request, default)).Result!;
        var renew = new RenewEditorV2("2", request.InstanceId, request.EditorId, acquired.LeaseId, "1");
        time += 100;
        var first = await h.Service.RenewEditorAsync(caller, renew, default);
        utc.Utc = utc.Utc.AddHours(hours);
        time += 599_999;
        Assert.That((await h.Service.AcquireEditorAsync(caller, request, default)).Result, Is.EqualTo(acquired));
        Assert.That((await h.Service.RenewEditorAsync(caller, renew, default)).Result, Is.EqualTo(first.Result));
        time++;
        Assert.That((await h.Service.RenewEditorAsync(caller, renew, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseExpired));
        Assert.That((await h.Service.AcquireEditorAsync(caller, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.AcquisitionRetired));
    });

    private static ulong Fault(string kind) => kind switch
    {
        "linux-error" => LeaseClock.LinuxMilliseconds(-1, 0, 0, 22),
        "linux-data" => LeaseClock.LinuxMilliseconds(0, 0, -1, 0),
        "mac-error" => LeaseClock.MacMilliseconds(0, 1, 1, 1),
        "mac-data" => LeaseClock.MacMilliseconds(0, 0, 1, 0),
        "overflow" => LeaseClock.MacMilliseconds(ulong.MaxValue, 0, uint.MaxValue, 1),
        "missing-library" => throw new DllNotFoundException(),
        "missing-symbol" => throw new EntryPointNotFoundException(),
        "rollback" => 999,
        "deadline-overflow" => ulong.MaxValue - 599_999,
        _ => throw new ArgumentException(nameof(kind))
    };

    [TestCase("linux-error")]
    [TestCase("linux-data")]
    [TestCase("mac-error")]
    [TestCase("mac-data")]
    [TestCase("overflow")]
    [TestCase("missing-library")]
    [TestCase("missing-symbol")]
    [TestCase("rollback")]
    [TestCase("deadline-overflow")]
    public Task FailedActiveClockRevokesAndRemainsLatchedAfterRecovery(string kind) => Ui(async () =>
    {
        bool fail = false;
        await using var h = new Host("A", new WorkbenchAutomationService { LeaseMilliseconds = () => fail ? Fault(kind) : 1000 });
        var caller = Activate(h);
        var request = Acquire(h);
        var acquired = (await h.Service.AcquireEditorAsync(caller, request, default)).Result!;
        fail = true;
        Assert.That((await h.Service.RenewEditorAsync(caller, new("2", request.InstanceId, request.EditorId,
            acquired.LeaseId, "1"), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseExpired));
        fail = false;
        for (int attempt = 0; attempt < 2; attempt++)
            Assert.That((await h.Service.AcquireEditorAsync(caller, Acquire(h), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.TimerUnavailable));
        Assert.That(h.Editor.Text, Is.EqualTo("A"));
        Assert.That(h.Owner.CanStartGui, Is.True);
    });

    [Test]
    public Task InitialNativeFailureDoesNotTakeControlAndOnlyANewServiceCanRecover() => Ui(async () =>
    {
        AutomationCaller oldCaller;
        Guid oldInstance;
        bool fail = true;
        await using (var h = new Host("A", new WorkbenchAutomationService { LeaseMilliseconds = () => fail ? Fault("missing-symbol") : 1000 }))
        {
            oldCaller = Activate(h);
            oldInstance = h.Service.InstanceId;
            var denied = await h.Service.AcquireEditorAsync(oldCaller, Acquire(h), default);
            Assert.That(denied.Error!.Code, Is.EqualTo(AutomationErrorCode.TimerUnavailable));
            Assert.That(denied.Result, Is.Null);
            Assert.That(h.Owner.CanStartGui, Is.True);
            Assert.That((await h.Service.GetStatusV2Async(oldCaller, new("2"), default)).Result!.OwnLease, Is.Null);
            fail = false;
            Assert.That((await h.Service.AcquireEditorAsync(oldCaller, Acquire(h), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.TimerUnavailable));
            h.Service.DisableMcp();
            await h.Service.DrainAsync();
            Assert.Throws<InvalidOperationException>(() => h.Service.ActivateMcp());
        }
        await using var fresh = new Host("A", new WorkbenchAutomationService { LeaseMilliseconds = () => 1000 });
        var caller = Activate(fresh);
        Assert.That(fresh.Service.InstanceId, Is.Not.EqualTo(oldInstance));
        Assert.That(caller.ActivationEpoch, Is.Not.EqualTo(oldCaller.ActivationEpoch));
        Assert.That((await fresh.Service.AcquireEditorAsync(oldCaller, Acquire(fresh), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        Assert.That((await fresh.Service.AcquireEditorAsync(caller, Acquire(fresh), default)).Result, Is.Not.Null);
    });

    [Test]
    public Task SuspendExpiryCancelsPendingEditAndWaitsForDrainBeforeGuiAdmission() => Ui(async () =>
    {
        ulong time = 1000;
        await using var h = new Host("before", new WorkbenchAutomationService { LeaseMilliseconds = () => time });
        var caller = Activate(h);
        var request = Acquire(h);
        var acquired = (await h.Service.AcquireEditorAsync(caller, request, default)).Result!;
        var dialog = new DeferredDialog();
        h.Service.ReplaceConfirmation = dialog;
        var edit = h.Service.UpdateEditorV2Async(caller, new("2", request.InstanceId, request.EditorId,
            acquired.LeaseId, request.ExpectedRevision, EditorEditMode.Replace, "after"), default);
        Assert.That(edit.IsCompleted, Is.False);
        time += 7_230_000;
        var expired = h.Service.RenewEditorAsync(caller, new("2", request.InstanceId, request.EditorId, acquired.LeaseId, "1"), default);
        Assert.That(expired.IsCompletedSuccessfully, Is.True);
        Assert.That(expired.Result.Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseExpired));
        Assert.That(h.Owner.Phase, Is.EqualTo(EditorControlPhase.Releasing));
        Assert.That(h.Owner.CanStartGui, Is.False);
        var drain = h.Service.DrainAsync();
        Assert.That(drain.IsCompleted, Is.False);
        h.Area.PerformTextInput("bad");
        Assert.That(h.Editor.Text, Is.EqualTo("before"));
        var gui = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed() { if (h.Owner.Phase == EditorControlPhase.Gui) gui.TrySetResult(); }
        h.Owner.WorkspaceChanged += Changed;
        try
        {
            await dialog.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(h.Owner.CanStartGui, Is.False);
            dialog.Answer.SetResult(true);
            await Task.WhenAll(edit, drain, gui.Task).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { h.Owner.WorkspaceChanged -= Changed; }
        Assert.That((await edit).Error, Is.Not.Null);
        Assert.That(((await edit).TextEffect, (await edit).HistoryEffect), Is.EqualTo((TextEffect.None, HistoryEffect.None)));
        Assert.That(h.Editor.Text, Is.EqualTo("before"));
        Assert.That(h.Owner.CanStartGui, Is.True);
        h.Area.PerformTextInput("!");
        Assert.That(h.Editor.Text, Is.EqualTo("before!"));
    });
}
