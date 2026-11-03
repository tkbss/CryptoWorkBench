using System.Collections.Immutable;
using System.Reflection;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using AvaloniaEdit;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class B3F2Tests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    [TestCase("delete", false), TestCase("delete", true), TestCase("deleteThrows", false)]
    [TestCase("folder", false), TestCase("picker", true), TestCase("append", false), TestCase("observer", true)]
    public Task SynchronousSaveProducerAllowsConcurrentL1(string step, bool shutdown) => Ui(async () =>
    {
        WorkbenchAutomationService? service = null;
        Task? drain = null;
        int starts = 0;
        void WaitForL1()
        {
            starts++;
            // The provider cannot return until the other thread has published the revocation.
            // The bounded wait also releases the dispatcher on regression.
            var revoked = Task.Run(() =>
            {
                if (shutdown) service!.BeginShutdown(); else service!.DisableMcp();
                drain = service.DrainAsync();
            });
            Assert.That(revoked.Wait(Limit), Is.True, "L1 waited for the synchronous producer");
            Assert.That(drain!.IsCompleted, Is.False);
        }
        service = new WorkbenchAutomationService
        {
            OpenSaveAppend = path => { if (step == "append") WaitForL1(); return File.Open(path, FileMode.Append); }
        };
        await using var h = new Host("snapshot", service);
        service.ActivateMcp();
        var path = Path.GetTempFileName();
        try
        {
            var intent = service.CaptureSaveIntent()!;
            if (step == "append") service.SaveScriptBook(h.Editor, path);
            else if (step is "folder" or "picker")
            {
                await service.WithFileDialogAsync(h.Registration, async origin =>
                {
                    var item = await service.StartFileIntentStep<string>(origin, () => { WaitForL1(); return Task.FromResult<string?>("original"); });
                    Assert.That(item, Is.EqualTo("original"));
                    Assert.That(await service.StartFileIntentStep<string>(origin, () => throw new AssertionException("late step")), Is.Null);
                });
            }
            else
            {
                if (step == "observer")
                    h.Editor.PropertyChanged += (_, e) => { if (e.Property == TextEditor.IsReadOnlyProperty && h.Editor.IsReadOnly) WaitForL1(); };
                Task Delete()
                {
                    Assert.That(step, Is.Not.EqualTo("observer"), "L1 won before the commit decision");
                    WaitForL1();
                    if (step == "deleteThrows") throw new IOException("start failed");
                    File.Delete(path);
                    return Task.CompletedTask;
                }
                var save = service.CommitSaveAsync(intent, new(path, true, Delete));
                if (step == "deleteThrows") Assert.ThrowsAsync<IOException>(async () => await save);
                else Assert.That(await save, Is.EqualTo(step == "observer" ? AutomationErrorCode.ShuttingDown : (AutomationErrorCode?)null));
            }
            await drain!.WaitAsync(Limit);
            Assert.That(starts, Is.EqualTo(1));
            Assert.That((h.Editor.MutationLocked, h.Owner.Pending, service.IsBusy), Is.EqualTo((false, 0, false)));
            Assert.That(File.ReadAllText(path), Is.EqualTo(step is "delete" or "append" ? "snapshot\n" : ""));
            if (!shutdown && step == "deleteThrows") { service.SaveScriptBook(h.Editor, path); Assert.That(File.ReadAllText(path), Is.EqualTo("snapshot\n")); }
        }
        finally { File.Delete(path); }
    });

    [Test]
    public Task DisposeCapturesCutStartedAfterTheEpochClosed() => Ui(async () =>
    {
        await using var h = new Host("original");
        h.Service.ActivateMcp();
        h.Service.DisableMcp();
        await h.Service.DrainAsync().WaitAsync(Limit);
        var original = h.Window.Clipboard!;
        var proxy = DispatchProxy.Create<IClipboard, EditorCoordinatorTests.ClipboardProxy>();
        var provider = (EditorCoordinatorTests.ClipboardProxy)(object)proxy;
        provider.Inner = original;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.DelayedWrite = gate.Task;
        EditorCoordinatorTests.ClipboardProxy.Bind(proxy);
        try
        {
            h.Editor.Select(0, 2);
            ApplicationCommands.Cut.Execute(null, h.Area);
            var cut = h.Area.PendingClipboard;
            h.Service.DetachEditor(h.Registration);
            var first = h.Service.DisposeAsync().AsTask();
            var second = h.Service.DisposeAsync().AsTask();
            var drain = h.Service.DrainAsync();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.That(first.IsCompleted || second.IsCompleted || drain.IsCompleted || cut.IsCompleted, Is.False);
            using var cts = new CancellationTokenSource();
            var waiter = h.Service.DrainAsync(cts.Token);
            cts.Cancel();
            Assert.That(waiter.IsCanceled, Is.True);
            Assert.That(h.Owner.Pending, Is.Positive);
            gate.SetResult();
            await Task.WhenAll(cut, first, second, drain).WaitAsync(Limit);
            Assert.That((h.Owner.Pending, h.Editor.Text), Is.EqualTo((0, "original")));
        }
        finally { gate.TrySetResult(); EditorCoordinatorTests.ClipboardProxy.Bind(original); }
    });

    [Test]
    public Task FailedNativeCertificateReleasesTimerAndLifetime() => Ui(async () =>
    {
        await using var h = new Host("fault");
        h.Service.ActivateMcp();
        var c = h.Service.IssueCredential("fault", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var original = h.Registration.Snapshot();
        var lease = (await h.Service.AcquireEditorAsync(c, new("2", h.Service.InstanceId, original.EditorId, Guid.NewGuid(), original.Revision), default)).Result!;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var timer = (DispatcherTimer)typeof(WorkbenchAutomationService).GetField("leaseTimer", flags)!.GetValue(h.Service)!;
        var binding = typeof(WorkbenchAutomationService).GetField("lease", flags)!.GetValue(h.Service)!;
        var lifetime = (CancellationTokenSource)binding.GetType().GetField("Lifetime", flags)!.GetValue(binding)!;
        h.Editor.Document.UpdateStarted += (_, _) => throw new InvalidOperationException();
        var receipt = await h.Service.UpdateEditorV2Async(c, new("2", h.Service.InstanceId, original.EditorId, lease.LeaseId,
            original.Revision, EditorEditMode.Append, "!"), default);
        Assert.That(receipt.Finalization, Is.EqualTo(Finalization.Faulted));
        h.Service.DisableMcp();
        await Assert.ThatAsync(async () => await h.Service.DrainAsync().WaitAsync(Limit), Throws.InstanceOf<InvalidOperationException>());
        await h.Service.DisposeAsync();
        await h.Service.DisposeAsync();
        Assert.That(timer.IsEnabled, Is.False);
        var tick = (Delegate?)typeof(DispatcherTimer).GetField("Tick", flags)!.GetValue(timer);
        Assert.That(tick?.GetInvocationList().Any(d => ReferenceEquals(d.Target, h.Service)) ?? false, Is.False);
        Assert.Throws<ObjectDisposedException>(() => _ = lifetime.Token);
        Assert.That((h.Editor.Document.IsInUpdate, h.Owner.CanStartGui), Is.EqualTo((true, false)));
        Assert.That((await h.Service.GetStatusV2Async(c, new("2"), default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        await Assert.ThatAsync(async () => await h.Service.DrainAsync().WaitAsync(Limit), Throws.InstanceOf<InvalidOperationException>());
    });

    [TestCase("read"), TestCase("run"), TestCase("full")]
    public Task JobIdsAreScopedToAuthenticatedHistory(string grant) => Ui(async () =>
    {
        var clock = new WorkbenchRegistryTests.ManualClock();
        await using var h = new Host("Print(0x(01))", new WorkbenchAutomationService(clock));
        var s = h.Service;
        s.ActivateMcp();
        var all = WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl);
        var a = s.IssueCredential("a", all);
        var b = s.IssueCredential("b", all);
        var caller = s.IssueCredential("b", grant switch
        {
            "read" => ImmutableHashSet.Create(AutomationPermissions.ExecutionRead),
            "run" => ImmutableHashSet.Create(AutomationPermissions.ExecutionRun, AutomationPermissions.EditorControl),
            _ => all
        });
        int prompts = 0;
        s.ExecutionConfirmation = (_, _, _) => { prompts++; return Task.FromResult<bool?>(false); };
        async Task<EditorExecutionV2> Acquire(AutomationCaller owner)
        {
            var snapshot = h.Registration.Snapshot();
            var binding = (await s.AcquireEditorAsync(owner, new("2", s.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
            var status = (await s.GetStatusV2Async(owner, new("2"), default)).Result!;
            return new("2", s.InstanceId, snapshot.EditorId, binding.LeaseId, Guid.NewGuid(), snapshot.Revision, status.StateVersion);
        }
        async Task Release(AutomationCaller owner, EditorExecutionV2 r) =>
            _ = await s.ReleaseEditorAsync(owner, new("2", s.InstanceId, r.EditorId, r.LeaseId), default);
        var expiredA = await Acquire(a);
        await s.ExecuteEditorV2Async(a, expiredA, default);
        await Release(a, expiredA);
        var expiredB = await Acquire(b);
        await s.ExecuteEditorV2Async(b, expiredB, default);
        await Release(b, expiredB);
        clock.Advance(AutomationContracts.ResultRetention);
        var liveA = await Acquire(a);
        await s.ExecuteEditorV2Async(a, liveA, default);
        await Release(a, liveA);
        var liveB = await Acquire(b);
        await s.ExecuteEditorV2Async(b, liveB, default);
        var before = prompts;
        foreach (var validLease in new[] { false, true })
        {
            var prototype = validLease ? liveB : liveB with { LeaseId = Guid.NewGuid() };
            foreach (var id in new[] { liveA.ExecutionId, expiredA.ExecutionId, Guid.NewGuid() })
            {
                var r = await s.ExecuteEditorV2Async(caller, prototype with { ExecutionId = id }, default);
                if (validLease && grant != "read") Assert.That(r.Result!.State, Is.EqualTo(ExecutionState.Rejected));
                else Assert.That(r.Error!.Code, Is.EqualTo(grant == "full" ? AutomationErrorCode.LeaseNotFound : AutomationErrorCode.Forbidden));
            }
            var existing = await s.ExecuteEditorV2Async(caller, prototype, default);
            if (grant == "run") Assert.That(existing.Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
            else if (validLease) Assert.That(existing.Result!.State, Is.EqualTo(ExecutionState.Rejected));
            else Assert.That(existing.Error!.Code, Is.EqualTo(AutomationErrorCode.IdempotencyConflict));
            Assert.That((await s.ExecuteEditorV2Async(caller, prototype with { ExecutionId = expiredB.ExecutionId }, default)).Error!.Code,
                Is.EqualTo(grant == "run" ? AutomationErrorCode.Forbidden : AutomationErrorCode.ResultExpired));
        }
        Assert.That(prompts - before, Is.EqualTo(grant == "read" ? 0 : 3));
        // B's admission using the same UUID never replaces A's job or its tombstone.
        Assert.That((await s.ExecuteEditorV2Async(a, liveA, default)).Result!.ExecutionId, Is.EqualTo(liveA.ExecutionId));
        Assert.That((await s.ExecuteEditorV2Async(a, expiredA, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired));
        await Release(b, liveB);
        if (grant != "run") Assert.That((await s.ExecuteEditorV2Async(caller, liveB, default)).Result!.ExecutionId, Is.EqualTo(liveB.ExecutionId));
        s.DisableMcp();
        foreach (var id in new[] { liveA.ExecutionId, expiredA.ExecutionId, liveB.ExecutionId, expiredB.ExecutionId, Guid.NewGuid() })
            Assert.That((await s.ExecuteEditorV2Async(caller, liveB with { ExecutionId = id }, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
    });
}
