using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class ManualLeaseClockPowerTests
{
    private sealed class HeldConfirmation : IEditorReplaceConfirmation
    {
        internal readonly TaskCompletionSource<bool?> Answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<bool?> ConfirmAsync(AvaloniaEdit.TextEditor editor, EditorReplaceApproval approval, CancellationToken ct)
        {
            using var registration = ct.Register(() => Cancelled.TrySetResult());
            return await Answer.Task;
        }
    }

    [Test, Explicit("Requires an operator, real suspend/hibernate for over eleven minutes, and matching OS power-event evidence. See G1-P02 report.")]
    public Task RealPowerTransitionExpiresBeforeDispatcherCanTick() => Ui(async () =>
    {
        string? mode = Environment.GetEnvironmentVariable("CWB_LEASE_POWER_MODE");
        Assert.That(mode, Is.AnyOf("suspend", "hibernate"), "Set CWB_LEASE_POWER_MODE deliberately before selecting this manual test.");
        await using var h = new Host("before", new WorkbenchAutomationService());
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("manual-power-test", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var request = new AcquireEditorV2("2", h.Service.InstanceId, h.Registration.Id, Guid.NewGuid(), h.Registration.Snapshot().Revision);
        var reply = await h.Service.AcquireEditorAsync(caller, request, default);
        Assert.That(reply.Error, Is.Null);
        var acquired = reply.Result!;
        var dialog = new HeldConfirmation();
        h.Service.ReplaceConfirmation = dialog;
        var edit = h.Service.UpdateEditorV2Async(caller, new("2", request.InstanceId, request.EditorId,
            acquired.LeaseId, request.ExpectedRevision, EditorEditMode.Replace, "after"), default);
        Assert.That(edit.IsCompleted, Is.False);
        ulong start = h.Service.LeaseMilliseconds(), previous = start, resumed = start;
        TestContext.Progress.WriteLine($"READY: {mode} within 90 seconds; resume after >11 minutes. " +
            $"OS={System.Runtime.InteropServices.RuntimeInformation.OSDescription}; " +
            $"OSArch={System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}; " +
            $"ProcessArch={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}; " +
            $"Runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; lease={acquired.LeaseId}; ms={start}.");
        try
        {
            // Deliberately occupy THIS dispatcher turn. No timer callback can process expiry first.
            // A jump is necessary, but only external power-event records establish real OS sleep.
            for (int poll = 0; poll < 900; poll++)
            {
                Thread.Sleep(100);
                resumed = h.Service.LeaseMilliseconds();
                Assert.That(resumed, Is.GreaterThanOrEqualTo(previous));
                if (resumed - previous > 660_000) break;
                previous = resumed;
            }
            Assert.That(resumed - previous, Is.GreaterThan(660_000UL), "No >11 minute clock jump; real suspend inclusion NOT VERIFIED.");
            var renewal = h.Service.RenewEditorAsync(caller, new("2", request.InstanceId, request.EditorId, acquired.LeaseId, "1"), default);
            Assert.That(renewal.IsCompletedSuccessfully, Is.True);
            Assert.That(renewal.Result.Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseExpired));
            Assert.That(h.Owner.Phase, Is.EqualTo(EditorControlPhase.Releasing));
            Assert.That(h.Owner.CanStartGui, Is.False);
            TestContext.Progress.WriteLine($"Resume ms={resumed}; prior sample={previous}; old lease rejected before dispatcher yield.");
            await dialog.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(h.Owner.CanStartGui, Is.False);
            var gui = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Changed() { if (h.Owner.Phase == EditorControlPhase.Gui) gui.TrySetResult(); }
            h.Owner.WorkspaceChanged += Changed;
            try
            {
                dialog.Answer.SetResult(true);
                await Task.WhenAll(edit, h.Service.DrainAsync(), gui.Task).WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { h.Owner.WorkspaceChanged -= Changed; }
            Assert.That((await edit).Error, Is.Not.Null);
            Assert.That(((await edit).TextEffect, (await edit).HistoryEffect), Is.EqualTo((TextEffect.None, HistoryEffect.None)));
            Assert.That(h.Editor.Text, Is.EqualTo("before"));
            Assert.That(h.Owner.CanStartGui, Is.True);
            h.Area.PerformTextInput("!");
            Assert.That(h.Editor.Text, Is.EqualTo("before!"));
        }
        finally { dialog.Answer.TrySetResult(false); }
    });
}
