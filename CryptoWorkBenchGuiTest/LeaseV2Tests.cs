using Avalonia.Input;
using AvaloniaEdit;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class LeaseV2Tests
{
    private static AutomationCaller Activate(Host h)
    {
        h.Service.ActivateMcp();
        return h.Service.IssueCredential("test", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
    }
    private static AcquireEditorV2 Acquire(Host h) => new("2", h.Service.InstanceId, h.Registration.Id,
        Guid.NewGuid(), h.Registration.Snapshot().Revision);

    [Test]
    public Task RetainedRegistrationIsAddressedWithoutFallbackAndOtherPrincipalSeesNoLease() => Ui(async () =>
    {
        await using var h = new Host("original");
        var second = h.Service.AttachEditor(new WorkbenchTextEditor { Text = "selected" }, null!, retainCurrent: true)!;
        var c = Activate(h);
        var other = h.Service.IssueCredential("other", c.Permissions.Remove(AutomationPermissions.EditorRead));
        var request = Acquire(h);
        var acquired = (await h.Service.AcquireEditorAsync(c, request, default)).Result!;
        Assert.That(acquired.EditorId, Is.EqualTo(h.Registration.Id));
        Assert.That((await h.Service.GetEditorV2Async(c, new("2", request.InstanceId, second.Id), default)).Result!.Text, Is.EqualTo("selected"));
        Assert.That((await h.Service.GetEditorV2Async(other, new("2", request.InstanceId, request.EditorId), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        Assert.That((await h.Service.GetStatusV2Async(other, new("2"), default)).Result!.OwnLease, Is.Null);
        Assert.That((await h.Service.GetStatusV2Async(c with { }, new("2"), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        var release = new ReleaseEditorV2("2", request.InstanceId, request.EditorId, acquired.LeaseId);
        Assert.That((await h.Service.ReleaseEditorAsync(other, release, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseNotFound));
        h.Service.DetachEditor(h.Registration);
        Assert.That((await h.Service.ReleaseEditorAsync(c, release, default)).Result!.Reason, Is.EqualTo(ReleaseReason.Detached));
        h.Service.ActivateEditor(second);
        Assert.That((await h.Service.GetEditorV2Async(c, new("2", request.InstanceId, request.EditorId), default)).Error!.Code,
            Is.EqualTo(AutomationErrorCode.EditorUnavailable));
    });

    [Test]
    public Task AcquireCanAddressRetainedDocumentAfterSelectedViewClosed() => Ui(async () =>
    {
        await using var h = new Host("retained");
        var selected = h.Service.AttachEditor(new WorkbenchTextEditor(), null!, retainCurrent: true)!;
        h.Service.DetachEditor(selected);
        var c = Activate(h);
        Assert.That((await h.Service.GetStatusV2Async(c, new("2"), default)).Result!.EditorId, Is.Null);
        var receipt = await h.Service.AcquireEditorAsync(c, Acquire(h), default);
        Assert.That(receipt.Result!.EditorId, Is.EqualTo(h.Registration.Id));
    });

    [TestCase(false)] [TestCase(true)]
    public Task ClockRollbackOrOverflowClosesAdmission(bool rollback) => Ui(async () =>
    {
        ulong time = 100;
        await using var h = new Host("A", new WorkbenchAutomationService { LeaseMilliseconds = () => time });
        var c = Activate(h);
        var req = Acquire(h);
        var owned = (await h.Service.AcquireEditorAsync(c, req, default)).Result!;
        time = rollback ? 1 : ulong.MaxValue;
        Assert.That((await h.Service.RenewEditorAsync(c, new("2", req.InstanceId, req.EditorId, owned.LeaseId, "1"), default)).Result, Is.Null);
        time = 101;
        Assert.That((await h.Service.AcquireEditorAsync(c, Acquire(h), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.TimerUnavailable));
        Assert.That(h.Editor.Text, Is.EqualTo("A"));
    });

    [Test]
    public Task PublicMutationFaultPreservesAppliedTextAndRejectsNewWork() => Ui(async () =>
    {
        await using var h = new Host("A");
        var c = Activate(h);
        var req = Acquire(h);
        var lease = (await h.Service.AcquireEditorAsync(c, req, default)).Result!;
        h.Editor.Document.Changed += (_, _) => throw new InvalidOperationException("sensitive-marker");
        var edit = new EditorEditV2("2", req.InstanceId, req.EditorId, lease.LeaseId, req.ExpectedRevision, EditorEditMode.Append, "!");
        var receipt = await h.Service.UpdateEditorV2Async(c, edit, default);
        Assert.That(receipt.TextEffect, Is.EqualTo(TextEffect.Applied));
        Assert.That(receipt.HistoryEffect, Is.EqualTo(HistoryEffect.Indeterminate));
        Assert.That(receipt.Finalization, Is.EqualTo(Finalization.Faulted));
        Assert.That(AutomationJson.Serialize(receipt), Does.Not.Contain("sensitive-marker"));
        Assert.That(receipt.RetryPolicy, Is.EqualTo(RetryPolicy.NoAutomaticRetry));
        Assert.That((await h.Service.UpdateEditorV2Async(c, edit, default)).Error, Is.Not.Null);
        Assert.That(h.Editor.Text, Is.EqualTo("A!"));
    });

    [Test]
    public Task LostRepliesCasAndDelayedTimerCannotResurrectLease() => Ui(async () =>
    {
        ulong elapsed = 100;
        await using var h = new Host("A", new WorkbenchAutomationService { LeaseMilliseconds = () => elapsed });
        var c = Activate(h);
        var request = Acquire(h);
        var receipt = (await h.Service.AcquireEditorAsync(c, request, default)).Result!;
        Assert.That((await h.Service.AcquireEditorAsync(c, request, default)).Result, Is.EqualTo(receipt));
        Assert.That((await h.Service.AcquireEditorAsync(c, request with { ExpectedRevision = "23" }, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.IdempotencyConflict));
        var renew = new RenewEditorV2("2", request.InstanceId, request.EditorId, receipt.LeaseId, "1");
        elapsed += 599999;
        var races = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => h.Service.RenewEditorAsync(c, renew, default))));
        Assert.That(races.Count(r => !r.Replayed), Is.EqualTo(1));
        Assert.That(races.Select(r => r.Result).Distinct().Count(), Is.EqualTo(1));
        elapsed++;
        Assert.That((await h.Service.GetStatusV2Async(c, new("2"), default)).Result!.OwnLease!.LeaseVersion, Is.EqualTo("2"));
        Assert.That((await h.Service.RenewEditorAsync(c, renew with { ExpectedLeaseVersion = "2" }, default)).Result!.LeaseVersion, Is.EqualTo("3"));
        Assert.That((await h.Service.RenewEditorAsync(c, renew, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseVersionConflict));
        elapsed += 600000;
        Assert.That((await h.Service.RenewEditorAsync(c, renew with { ExpectedLeaseVersion = "3" }, default)).Result, Is.Null);
        var released = await h.Service.ReleaseEditorAsync(c, new("2", request.InstanceId, request.EditorId, receipt.LeaseId), default);
        Assert.That(released.Result!.Reason, Is.EqualTo(ReleaseReason.Expired));
        Assert.That(released.Replayed, Is.True);
        Assert.That((await h.Service.AcquireEditorAsync(c, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.AcquisitionRetired));
        Assert.That(h.Owner.CanStartGui, Is.True);
    });

    [Test]
    public Task L1InsideRegistryPreparationPreventsPublicationAndRunnerStart() => Ui(async () =>
    {
        await using var h = new Host("Print(0x(01))");
        var c = Activate(h);
        var req = Acquire(h);
        var owned = (await h.Service.AcquireEditorAsync(c, req, default)).Result!;
        int prompts = 0;
        h.Service.ExecutionConfirmation = (_, _, _) => { prompts++; return Task.FromResult<bool?>(true); };
        h.Service.AdmissionCheckpoint = point => { if (point == "registration") h.Service.DisableMcp(); };
        var execution = new EditorExecutionV2("2", req.InstanceId, req.EditorId, owned.LeaseId, Guid.NewGuid(), req.ExpectedRevision, "0");
        Assert.That((await h.Service.ExecuteEditorV2Async(c, execution, default)).Result, Is.Null);
        await h.Service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(prompts, Is.Zero);
        Assert.That(h.Service.IsBusy, Is.False);
        var status = await h.Service.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default);
        Assert.That(status.Data!.LastExecutionId, Is.Null);
    });

    [Test]
    public Task BlockedProviderPreventsReadmissionAcrossRepeatedCloseRequests() => Ui(async () =>
    {
        await using var h = new Host("save");
        var c = Activate(h);
        var intent = h.Service.CaptureSaveIntent()!;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string path = Path.GetTempFileName();
        try
        {
            var save = h.Service.CommitSaveAsync(intent, new(path, true, () => gate.Task));
            h.Service.DisableMcp();
            var close = h.Service.DrainAsync();
            for (int i = 0; i < 10; i++)
            {
                h.Service.DisableMcp();
                Assert.Throws<InvalidOperationException>(() => h.Service.ActivateMcp());
            }
            Assert.That(close.IsCompleted, Is.False);
            Assert.That(h.Owner.CanStartGui, Is.False);
            gate.SetResult();
            Assert.That(await save, Is.Null);
            await close.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(File.ReadAllText(path), Is.EqualTo("save\n"));
            Assert.That(h.Owner.CanStartGui, Is.True);
            Assert.That((await h.Service.GetStatusV2Async(c, new("2"), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        }
        finally { gate.TrySetResult(); File.Delete(path); }
    });

    private sealed class DeferredDialog : IEditorReplaceConfirmation
    {
        internal TaskCompletionSource<bool?> Answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool?> ConfirmAsync(TextEditor editor, EditorReplaceApproval approval, CancellationToken ct) => Answer.Task;
    }

    [TestCase(false)]
    [TestCase(true)]
    public Task AcceptedDialogKeepsGuiBlockedUntilOriginalReturns(bool revoke) => Ui(async () =>
    {
        await using var h = new Host("before");
        var c = Activate(h);
        var req = Acquire(h);
        var lease = (await h.Service.AcquireEditorAsync(c, req, default)).Result!;
        var dialog = new DeferredDialog();
        h.Service.ReplaceConfirmation = dialog;
        var edit = h.Service.UpdateEditorV2Async(c, new("2", req.InstanceId, req.EditorId, lease.LeaseId, req.ExpectedRevision, EditorEditMode.Replace, "after"), default);
        if (revoke) h.Service.DisableMcp();
        else
        {
            var releasing = await h.Service.ReleaseEditorAsync(c, new("2", req.InstanceId, req.EditorId, lease.LeaseId), default);
            Assert.That(releasing.Result!.Phase, Is.EqualTo(ReleasePhase.Releasing));
        }
        var drain = h.Service.DrainAsync();
        Assert.That(drain.IsCompleted, Is.False);
        h.Area.PerformTextInput("bad");
        Assert.That(h.Owner.CanStartGui, Is.False);
        dialog.Answer.SetResult(true);
        var receipt = await edit;
        await drain;
        Assert.That(receipt.Error, Is.Not.Null);
        Assert.That((receipt.TextEffect, receipt.HistoryEffect), Is.EqualTo((TextEffect.None, HistoryEffect.None)));
        Assert.That(h.Editor.Text, Is.EqualTo("before"));
        Assert.That(h.Owner.CanStartGui, Is.True);
        if (revoke)
        {
            Assert.Throws<InvalidOperationException>(() => h.Service.ActivateMcp());
            Assert.That((await h.Service.AcquireEditorAsync(c, req, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        }
    });

    [TestCase(false)]
    [TestCase(true)]
    public Task JobAdmissionUsesConcreteApprovalAndRegistryReplay(bool revoke) => Ui(async () =>
    {
        await using var h = new Host("Print(0x(01))");
        var c = Activate(h);
        var req = Acquire(h);
        var lease = (await h.Service.AcquireEditorAsync(c, req, default)).Result!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int prompts = 0;
        h.Service.ExecutionConfirmation = (_, approval, _) =>
        {
            prompts++;
            Assert.That(approval.Snapshot.Text, Is.EqualTo(h.Editor.Text));
            entered.TrySetResult();
            return answer.Task;
        };
        var status = (await h.Service.GetStatusV2Async(c, new("2"), default)).Result!;
        var request = new EditorExecutionV2("2", req.InstanceId, req.EditorId, lease.LeaseId, Guid.NewGuid(), req.ExpectedRevision, status.StateVersion);
        var execution = h.Service.ExecuteEditorV2Async(c, request, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That((await h.Service.ExecuteEditorV2Async(c, request, default)).Result!.State, Is.EqualTo(ExecutionState.AwaitingConfirmation));
        if (revoke) h.Service.DisableMcp();
        answer.SetResult(true);
        var result = await execution;
        if (revoke) Assert.That(result.Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        else
        {
            Assert.That(result.Result!.State, Is.EqualTo(ExecutionState.Completed));
            Assert.That(result.Result.Outputs.All(o => o.Redacted && o.Text == null), Is.True);
            await h.Service.ReleaseEditorAsync(c, new("2", req.InstanceId, req.EditorId, lease.LeaseId), default);
            Assert.That((await h.Service.ExecuteEditorV2Async(c, request, default)).Result, Is.EqualTo(result.Result));
        }
        Assert.That(prompts, Is.EqualTo(1));
        Assert.That(h.Editor.Text, Is.EqualTo("Print(0x(01))"));
        Assert.That(h.Registration.Snapshot().Revision, Is.EqualTo(req.ExpectedRevision));
    });

    [Test]
    public Task ApprovedOutputNeedsTheOriginalOptInAndAReadGrantOnEveryProjection() => Ui(async () =>
    {
        await using var h = new Host("Print(0x(01))");
        var c = Activate(h);
        var privileged = h.Service.IssueCredential(c.PrincipalId, c.Permissions.Add(AutomationPermissions.OutputRead));
        var req = Acquire(h);
        var owned = (await h.Service.AcquireEditorAsync(c, req, default)).Result!;
        var request = new EditorExecutionV2("2", req.InstanceId, req.EditorId, owned.LeaseId, Guid.NewGuid(),
            req.ExpectedRevision, "0", OutputPolicy: OutputPolicy.IncludeText);
        Assert.That((await h.Service.ExecuteEditorV2Async(c, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        h.Service.ExecutionConfirmation = (_, approval, _) => Task.FromResult<bool?>(approval.Request.OutputPolicy == OutputPolicy.IncludeText);
        var execution = (await h.Service.ExecuteEditorV2Async(privileged, request, default)).Result!;
        Assert.That(execution.Outputs.Any(o => o.Text != null && !o.Redacted), Is.True);
        var lookup = new JobRequestV2("2", req.InstanceId, request.ExecutionId);
        var redacted = (await h.Service.GetExecutionV2Async(c, lookup, default)).Result!;
        Assert.That(redacted.Outputs.All(o => o.Text == null && o.Redacted), Is.True);
        var wrong = h.Service.IssueCredential("other", privileged.Permissions);
        Assert.That((await h.Service.CancelExecutionV2Async(wrong, lookup, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
        h.Service.ReclaimEditor();
        Assert.That((await h.Service.ReleaseEditorAsync(c, new("2", req.InstanceId, req.EditorId, owned.LeaseId), default)).Result!.Reason,
            Is.EqualTo(ReleaseReason.Reclaimed));
    });

    [Test]
    public Task DisableDuringLocalPickerAndLateCallbackAwaitsBothOriginalProducers() => Ui(async () =>
    {
        await using var h = new Host("original");
        var c = Activate(h);
        var request = Acquire(h);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var picker = h.Service.WithFileDialogAsync(h.Registration, _ => release.Task);
        var ticket = h.Owner.TryBegin()!;
        var callback = ticket.RegisterCallback();
        ticket.Dispose();
        Assert.That((await h.Service.AcquireEditorAsync(c, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Busy));
        h.Service.DisableMcp();
        var drain = h.Service.DrainAsync();
        Assert.That((await h.Service.AcquireEditorAsync(c, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        release.SetResult();
        await picker;
        Assert.That(drain.IsCompleted, Is.False);
        Assert.Throws<CryptoScript.Model.ExecutionBusyException>(() => ticket.RegisterCallback());
        callback.Dispose();
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(h.Owner.CanStartGui, Is.True);
    });

    // Called by the separate STA native probe in a fresh process, never by the headless session.
    public static async Task NativeTransitionsAsync(bool manualExpiry)
    {
        await using var h = new Host("native");
        var handle = h.Window.TryGetPlatformHandle();
        Assert.That(handle?.HandleDescriptor, Is.EqualTo("HWND"));
        Assert.That(handle!.Handle, Is.Not.EqualTo(IntPtr.Zero));
        var caller = Activate(h);
        var request = Acquire(h);
        var owned = (await h.Service.AcquireEditorAsync(caller, request, default)).Result!;
        h.Area.PerformTextInput("blocked");
        Assert.That(h.Editor.Text, Is.EqualTo("native"));
        if (manualExpiry)
        {
            Console.WriteLine("Suspend this Windows machine for more than ten minutes. After resume, press Enter in this console.");
            await Task.Run(() => Console.ReadLine());
        }
        var released = (await h.Service.ReleaseEditorAsync(caller, new("2", request.InstanceId, request.EditorId, owned.LeaseId), default)).Result!;
        Assert.That(released.Reason, Is.EqualTo(manualExpiry ? ReleaseReason.Expired : ReleaseReason.Released));
        h.Area.PerformTextInput("!");
        Assert.That(h.Editor.Text, Is.EqualTo("native!"));
        Assert.That((await h.Service.AcquireEditorAsync(caller, Acquire(h), default)).Result, Is.Not.Null);
        h.Service.BeginShutdown();
        await h.Service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(h.Owner.CanStartGui, Is.False);
        Assert.That((await h.Service.GetStatusV2Async(caller, new("2"), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
    }

    [Test]
    public Task ActiveLeaseAndMaximumCredentialSetFitTheReservedHeadroom() => Ui(async () =>
    {
        await using var h = new Host("");
        var request = Acquire(h);
        long start = GC.GetAllocatedBytesForCurrentThread();
        var owner = Activate(h);
        for (int i = 1; i < 64; i++)
        {
            var grants = System.Collections.Immutable.ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            for (int j = 0; j < 16; j++) grants.Add(new string((char)('a' + j), 64));
            h.Service.IssueCredential(new string((char)(64 + i), 128), grants.ToImmutable());
        }
        var acquired = await h.Service.AcquireEditorAsync(owner, request, default);
        long total = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.That(acquired.Result, Is.Not.Null);
        TestContext.Out.WriteLine($"Credential and active lease allocation traffic: {total} bytes");
        Assert.That(total, Is.LessThan(1024 * 1024));
        Assert.Throws<InvalidOperationException>(() => h.Service.IssueCredential("overflow", owner.Permissions));
    });

    [Test]
    public void ProcessLedgerBoundsIncludeWorstCaseRetainedIdentityStrings()
    {
        var c = WorkbenchAutomationService.LocalCaller;
        var instance = Guid.NewGuid();
        var id = Guid.NewGuid();
        long initial = GC.GetAllocatedBytesForCurrentThread();
        var table = new ProcessAcquisitions();
        for (int principal = 0; principal < 16; principal++)
        {
            for (int j = 0; j < 256; j++)
            {
                // Distinct maximum-length strings exercise retained bytes even for equal principal names.
                c = c with { PrincipalId = new string((char)('a' + principal), 128) };
                var request = new AcquireEditorV2("2", instance, id, Guid.NewGuid(), new string('1', 20));
                if (table.Reserve(instance, c, request).Slot == null) Assert.Fail("premature capacity denial");
            }
            var extra = new AcquireEditorV2("2", instance, id, Guid.NewGuid(), "0");
            Assert.That(table.Reserve(instance, c, extra).Error, Is.EqualTo(AutomationErrorCode.CapacityExceeded));
        }
        var total = GC.GetAllocatedBytesForCurrentThread() - initial;
        TestContext.Out.WriteLine($"Ledger and allocation traffic: {total} bytes");
        Assert.That(total, Is.LessThan(3 * 1024 * 1024));
        Assert.That(table.Reserve(instance, c with { PrincipalId = "fresh" }, new("2", instance, id, Guid.NewGuid(), "0")).Error,
            Is.EqualTo(AutomationErrorCode.CapacityExceeded));
        GC.KeepAlive(table);
    }

}
