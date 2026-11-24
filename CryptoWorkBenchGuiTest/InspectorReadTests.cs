using System.Text;
using Avalonia.Controls;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Services.Inspector;
using NUnit.Framework;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBench.Automation.Abstractions;
using CryptoScript.SecretSharing;
using Avalonia.Threading;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class InspectorReadTests
{
    private sealed class NoTimerClock : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            throw new InvalidOperationException("Inspector must not create execution or retention timers");
    }

    [Test]
    public Task ReadsDoNotAllocateExecutionTimersOrConsumeExecutionQuota() => GuiTestSession.RunAsync(async () =>
    {
        await using var service = new WorkbenchAutomationService(new NoTimerClock());
        service.AttachEditor(new WorkbenchTextEditor(), null!);
        for (int i = 0; i <= AutomationContracts.MaxExecutionIds; i++)
            Assert.That(await service.RunInspectorReadAsync(_ => Task.FromResult(1), () => true), Is.EqualTo(1));
        Assert.That((await service.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default)).Data!.LastExecutionId, Is.Null);
        Assert.That(service.EditorCoordinator.Pending, Is.Zero);
    });

    private sealed class DelayedDestination : MemoryStream
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Continue = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(); await Continue.Task.WaitAsync(cancellationToken);
            await base.WriteAsync(buffer, cancellationToken);
        }
    }

    [Test]
    public async Task PayloadLeaseSurvivesOwnerDisposeUntilCopyDrains()
    {
        string directory = Path.Combine(Path.GetTempPath(), "cwb-inspector-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var payload = new InspectorPayload(0, directory, 0);
            await payload.AppendAsync(new byte[] { 1, 2, 3 }, default); await payload.SealAsync(default);
            using var destination = new DelayedDestination();
            var copy = payload.CopyToAsync(destination);
            await destination.Entered.Task;
            payload.Dispose();
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
            destination.Continue.SetResult(); await copy;
            Assert.That(destination.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        finally { Directory.Delete(directory); }
    }
    [Test, Platform("Win")]
    public async Task CleanupFailureStillDeletesOtherSegmentsAndCanBeRetried()
    {
        string directory = Path.Combine(Path.GetTempPath(), "cwb-inspector-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var payload = new InspectorPayload(0, directory, 0);
        try
        {
            for (int i = 0; i < 5; i++) await payload.AppendAsync(new byte[256 * 1024], default);
            await payload.SealAsync(default);
            var paths = Directory.GetFiles(directory);
            Assert.That(paths, Has.Length.EqualTo(2));
            using (var locked = new FileStream(paths[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Assert.Throws<AggregateException>(() => payload.Dispose());
                Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { paths[0] }));
                Assert.That(payload.IsComplete, Is.False);
            }
            payload.Dispose();
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        finally { payload.Dispose(); Directory.Delete(directory); }
    }

    private sealed class Host : IAsyncDisposable
    {
        internal readonly WorkbenchAutomationService Service = new() { LeaseMilliseconds = () => 100 };
        internal readonly WorkbenchTextEditor Editor = new() { Text = "source" };
        internal readonly VariableViewModel Variables;
        internal readonly EditorBridge.Registration Registration;
        internal Host()
        {
            VariableDictionary.Instance().Clear();
            Registration = Service.AttachEditor(Editor, null!)!;
            Variables = new() { Automation = Service };
        }
        internal SelectionHandle Select(VariableDeclaration value)
        {
            Service.Mutate(() => VariableDictionary.Instance().Add(value), Variables.ApplyVariables);
            Variables.SelectedVariable = Variables.DataVariables.Single();
            return Variables.InspectorSelection()!;
        }
        public async ValueTask DisposeAsync() { await Service.DisposeAsync(); VariableDictionary.Instance().Clear(); }
    }

    [Test]
    public Task LargeStringCrossesSegmentBoundariesWithoutNormalizingOrTruncating() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        string value = " \r\n" + new string('x', 9 * 1024 * 1024) + "Ω🔑\0\r\n";
        var handle = h.Select(new StringVariableDeclaration { Id = "large", Value = value });
        var part = await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Value), () => true, default);
        using (part.Payload)
        {
            using var bytes = new MemoryStream(); await part.Payload.CopyToAsync(bytes);
            Assert.That(part.Count, Is.EqualTo(value.Length));
            Assert.That(bytes.Length, Is.EqualTo(2L * value.Length));
            Assert.That(bytes.GetBuffer().AsSpan(0, (int)bytes.Length).SequenceEqual(Encoding.Unicode.GetBytes(value)), Is.True);
        }
    });

    [Test]
    public Task RangesMergeOnlyWithinOriginalRevisionAndPreserveUtf16() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        const string value = " A\r\n\ud800\udc00\ud800Z\n";
        var root = new StringVariableDeclaration { Id = "x", Value = value };
        var handle = h.Select(root);
        using var snapshot = new InspectorSnapshot(handle.Stamp);
        var first = await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Value, Offset: 0, Count: 3), () => h.Variables.InspectorSelectionCurrent(handle), default);
        snapshot.Add(first);
        Assert.Throws<InvalidOperationException>(() => snapshot.Add(first));
        Assert.Throws<InvalidOperationException>(() => snapshot.Add(first with { Stamp = handle.Stamp with { Revision = handle.Stamp.Revision + 1 } }));
        Assert.Throws<InvalidOperationException>(() => snapshot.Add(first with { Total = first.Total + 1 }));
        Assert.That(snapshot.Parts, Has.Length.EqualTo(1));
        Assert.That(snapshot.Complete(InspectorMember.Value), Is.False);
        var second = await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Value, Offset: 3), () => h.Variables.InspectorSelectionCurrent(handle), default);
        snapshot.Add(second);
        Assert.That(snapshot.Complete(InspectorMember.Value), Is.True);
        using var data = new MemoryStream();
        foreach (var part in snapshot.Parts) await part.Payload.CopyToAsync(data);
        byte[] expected = value.SelectMany(c => new[] { (byte)c, (byte)(c >> 8) }).ToArray();
        Assert.That(data.ToArray(), Is.EqualTo(expected));
        h.Service.Mutate(() => root.Value = "changed");
        try { await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Value), () => true, default); Assert.Fail("Revision must be stale"); }
        catch (InvalidOperationException) { }
        using var historical = new MemoryStream();
        foreach (var part in snapshot.Parts) await part.Payload.CopyToAsync(historical);
        Assert.That(historical.ToArray(), Is.EqualTo(expected));
    });

    [Test]
    public Task AssignmentIdentityIsCheckedEvenWithoutRevisionChange() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        var handle = h.Select(new StringVariableDeclaration { Id = "x", Value = "old" });
        // Deliberately bypass host revision to isolate the independent identity guard.
        VariableDictionary.Instance().Add(new StringVariableDeclaration { Id = "x", Value = "new" });
        try { await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Value), () => true, default); Assert.Fail("Identity must be stale"); }
        catch (InvalidOperationException) { }
        Assert.That(h.Service.IsBusy, Is.False);
    });

    [Test]
    public Task McpAcquireCannotOverlapAndDisableCancelsReadWithoutSession() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("inspector-test", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = h.Service.RunInspectorReadAsync(async token => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); return 1; }, () => true);
        await started.Task;
        var reply = await h.Service.AcquireEditorAsync(caller,
            new("2", h.Service.InstanceId, h.Registration.Id, Guid.NewGuid(), h.Registration.Snapshot().Revision), default);
        Assert.That(reply.Error!.Code, Is.EqualTo(AutomationErrorCode.Busy));
        var drain = h.Service.DisableMcpAndDrainAsync();
        try { await read; Assert.Fail("Drain must cancel read"); } catch (OperationCanceledException) { }
        await drain;
        Assert.That(h.Service.IsBusy, Is.False);
    });

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public async Task ChangedSegmentCannotCommit(int change)
    {
        string dir = Directory.CreateTempSubdirectory("cwb-integrity-").FullName;
        try
        {
            using var snapshot = new InspectorSnapshot(new("v", new(new VariableDeclaration()), new(), 1, 1, 1));
            using var payload = new InspectorPayload(0, dir, 0);
            for (int i = 0; i < 5; i++) await payload.AppendAsync(new byte[262144], default);
            await payload.SealAsync(default);
            snapshot.Add(new(snapshot.Stamp, InspectorMember.ShareValue, 0, 0, payload.Length, payload.Length, payload));
            var file = Directory.GetFiles(dir).Single(f => new FileInfo(f).Length == 1048576);
            if (change == 2) File.Delete(file);
            else using (var stream = File.OpenWrite(file))
            {
                if (change == 0) stream.WriteByte(1); else stream.SetLength(stream.Length + change);
            }
            bool committed = false;
            try { await snapshot.CopyAsync(InspectorMember.ShareValue, 0, Stream.Null, () => { committed = true; return Task.CompletedTask; }); Assert.Fail(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { }
            Assert.That(committed, Is.False);
        }
        finally { Directory.Delete(dir); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public Task FullCoverageSurvivesResetAndShutdownDrainsCopy(bool cancel) => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        h.Select(new StringVariableDeclaration { Id = "x", Value = "AB" });
        using var session = new InspectorSession(h.Variables);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await session.Pending;
        foreach (var (offset,count) in new[] {(1,0),(1,1),(0,0),(0,1),(1,0),(2,0)})
            await session.ReadAsync(new(InspectorMember.Value, Offset: offset, Count: count));
        var snapshot = session.Snapshot!;
        Assert.That(snapshot.Complete(InspectorMember.Value), Is.True);
        Assert.That(snapshot.Parts.Count(p => p.Member == InspectorMember.Value && p.Count == 0), Is.EqualTo(1));
        using var destination = new DelayedDestination();
        bool committed = false;
        using var source = new CancellationTokenSource();
        var copy = snapshot.CopyAsync(InspectorMember.Value, 0, destination, () => { committed = true; return Task.CompletedTask; }, source.Token);
        await destination.Entered.Task;
        session.Reset();
        h.Service.BeginShutdown();
        var drain = h.Service.DrainAsync();
        Assert.That(drain.IsCompleted, Is.False);
        if (cancel)
        {
            source.Cancel();
            try { await copy; Assert.Fail("Cancelled staging committed"); } catch (OperationCanceledException) { }
        }
        else { destination.Continue.SetResult(); await copy; }
        await drain;
        Assert.That(committed, Is.EqualTo(!cancel));
        Assert.That(Encoding.Unicode.GetString(destination.ToArray()), Is.EqualTo(cancel ? "" : "AB"));
    });

    [TestCase(true)]
    [TestCase(false)]
    public Task SessionRetiresStaleHandle(bool assignment) => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        var root = new StringVariableDeclaration {Id="x", Value="old"};
        h.Select(root);
        using var session = new InspectorSession(h.Variables);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await session.Pending;
        await session.ReadAsync(new(InspectorMember.Value));
        h.Service.Mutate(() => { if (assignment) VariableDictionary.Instance().Update(new StringVariableDeclaration {Id="x", Value="new"}); else root.Value="new"; });
        await session.ReadAsync(new(InspectorMember.Text));
        Assert.That(session.Historical, Is.True);
        h.Service.AdmissionCheckpoint = _ => Assert.Fail("Stale handle was readmitted");
        var previousError = session.Error;
        await session.ReadAsync(new(InspectorMember.Text));
        Assert.That(session.Error, Is.EqualTo(previousError));
        using var bytes = new MemoryStream();
        await session.Snapshot!.CopyAsync(InspectorMember.Value, 0, bytes, () => Task.CompletedTask);
        Assert.That(Encoding.Unicode.GetString(bytes.ToArray()), Is.EqualTo("old"));
    });

    [Test]
    public Task OtherEditorTypingLeavesSelectedSnapshotLive() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        var other = new AvaloniaEdit.TextEditor();
        h.Service.AttachEditor(other, null!, retainCurrent: true);
        h.Service.ActivateEditor(h.Registration);
        h.Select(new StringVariableDeclaration {Id="x", Value="source"});
        using var session = new InspectorSession(h.Variables);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await session.Pending;
        other.Text += "!";
        Assert.That(session.Historical, Is.False);
        await session.ReadAsync(new(InspectorMember.Value));
        Assert.That(session.Snapshot!.Complete(InspectorMember.Value), Is.True);
    });

    [Test]
    public Task DocumentRoundTripRetiresSnapshotBeforeDispatch() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        h.Select(new StringVariableDeclaration {Id="x", Value="old"});
        using var session = new InspectorSession(h.Variables);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await session.Pending;
        var original = h.Editor.Document;
        h.Editor.Document = new AvaloniaEdit.Document.TextDocument();
        h.Editor.Document = original;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.That(session.Snapshot, Is.Null);
    });

    [Test]
    public Task LateCancellationAtDispatcherCommitDiscardsStaging() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        using var source = new CancellationTokenSource();
        bool staged = false, cleaned = false;
        var work = h.Service.RunInspectorReadAsync(_ => { staged = true; return Task.FromResult(42); },
            () => { if (staged) source.Cancel(); return true; }, source.Token, () => cleaned = true);
        try { await work; Assert.Fail(); } catch (OperationCanceledException) { }
        Assert.That(cleaned, Is.True);
        Assert.That(h.Service.EditorCoordinator.Pending, Is.Zero);
    });

    [Test]
    public Task WorkerStartAndStagingFailureAllowNextRead() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        var handle = h.Select(new StringVariableDeclaration {Id="x", Value="value"});
        IDisposable? competing = null;
        h.Service.AdmissionCheckpoint = step => { if (step == "worker") competing = InterpreterExecution.Enter(); };
        try { await h.Service.RunInspectorReadAsync(_ => Task.FromResult(0), () => true); Assert.Fail(); }
        catch (ExecutionBusyException) { }
        finally { competing?.Dispose(); h.Service.AdmissionCheckpoint = null; }
        try { await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Value), () => true, default,
            () => new InspectorPayload(0, diskReserve: long.MaxValue / 2)); Assert.Fail(); }
        catch (IOException) { }
        var part = await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Value), () => true, default);
        using (part.Payload) Assert.That(part.Count, Is.EqualTo(5));
        Assert.That(h.Service.EditorCoordinator.Pending, Is.Zero);
    });

    [TestCase(false), TestCase(true), Platform("Win")]
    public Task SessionRetainsCleanupOwnerAfterClose(bool copyActive) => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        h.Select(new StringVariableDeclaration { Id="x" });
        using var session = new InspectorSession(h.Variables);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await session.Pending;
        var dir = Directory.CreateTempSubdirectory("cwb-cleanup-");
        try
        {
            var payload = new InspectorPayload(0, dir.FullName, 0);
            await payload.AppendAsync(new byte[2], default); await payload.SealAsync(default);
            session.Snapshot!.Add(new(session.Snapshot.Stamp, InspectorMember.Value, 0, 0, 1, 1, payload));
            using (File.OpenRead(dir.GetFiles().Single().FullName))
            {
                using var staging = new DelayedDestination();
                var copy = copyActive ? session.Snapshot.CopyAsync(InspectorMember.Value, 0, staging,
                    () => Task.CompletedTask) : Task.CompletedTask;
                if (copyActive) await staging.Entered.Task;
                session.Dispose();
                staging.Continue.SetResult();
                await copy;
                Assert.That(session.CleanupError, Is.Not.Null);
            }
            session.RetryCleanup();
            Assert.That(session.CleanupError, Is.Null);
            Assert.That(dir.GetFiles(), Is.Empty);
        }
        finally { dir.Delete(); }
    });

    [Test]
    public Task LargeEditedDocumentAdmissionDoesNotAllocateFullText() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        h.Editor.Text = new string('x', 4 * 1024 * 1024);
        var handle = h.Select(new StringVariableDeclaration {Id="x", Text="x"});
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var work = InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Text, Count:0), () => true, default);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        clock.Stop();
        var part = await work;
        using (part.Payload) Assert.That(allocated, Is.LessThan(4 * 1024 * 1024));
        TestContext.Out.WriteLine($"Dispatcher admission: {clock.Elapsed.TotalMilliseconds:F3} ms; {allocated} allocated bytes.");
    });

    [TestCase(true), TestCase(false), Platform("Win")]
    public Task LockedReturnedOrUnpublishedPartRetainsOwner(bool closeBeforePublish) => GuiTestSession.RunAsync(async () =>
    {
        await using var host = new Host();
        host.Select(new StringVariableDeclaration { Id = "x", Value = "original" });
        using var session = new InspectorSession(host.Variables);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await session.Pending;
        if (!closeBeforePublish)
        {
            await session.ReadAsync(new(InspectorMember.Value));
            Assert.That(session.Snapshot!.Complete(InspectorMember.Value), Is.True);
        }

        // Only the new staging file uses this isolated directory. Holding the UI
        // allows the worker to seal it before either final validation or Add.
        var dir = Directory.CreateTempSubdirectory("cwb-staging-");
        string? originalTemp = Environment.GetEnvironmentVariable("TMP");
        FileStream? locked = null;
        try
        {
            Environment.SetEnvironmentVariable("TMP", dir.FullName);
            var read = session.ReadAsync(new(InspectorMember.Value));
            Assert.That(SpinWait.SpinUntil(() =>
            {
                var file = dir.GetFiles("*.payload").SingleOrDefault();
                if (file == null) return false;
                try { locked = file.OpenRead(); return true; }
                catch (IOException) { return false; }
            }, TimeSpan.FromSeconds(5)), Is.True);
            if (closeBeforePublish) session.Dispose();
            await read;
            if (!closeBeforePublish) Assert.That(session.Error, Does.Contain("Overlapping"));
            session.RetryCleanup();
            Assert.That(session.CleanupError, Is.Not.Null);
            Assert.That(host.Service.IsBusy, Is.False);
            locked!.Dispose();
            if (closeBeforePublish) session.RetryCleanup();
            host.Service.BeginShutdown();
            await host.Service.DrainAsync();
            Assert.That(session.CleanupError, Is.Null);
            Assert.That(dir.GetFiles(), Is.Empty);
            Assert.That(host.Service.EditorCoordinator.Pending, Is.Zero);
        }
        finally
        {
            locked?.Dispose();
            Environment.SetEnvironmentVariable("TMP", originalTemp);
            session.RetryCleanup();
            dir.Delete();
        }
    });

    [Test, Platform("Win")]
    public Task RejectedFinalValidationKeepsCleanupAcrossNextRead() => GuiTestSession.RunAsync(async () =>
    {
        await using var host = new Host();
        var handle = host.Select(new StringVariableDeclaration { Id = "x", Value = "original" });
        using var session = new InspectorSession(host.Variables);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await session.Pending;
        var dir = Directory.CreateTempSubdirectory("cwb-rejected-");
        FileStream? locked = null;
        try
        {
            var work = InspectorCapture.ReadAsync(host.Service, handle, new(InspectorMember.Value), () =>
            {
                var file = dir.GetFiles().SingleOrDefault();
                if (file == null) return true;
                locked = file.OpenRead();
                return false;
            }, default, () => new InspectorPayload(0, dir.FullName, 0));
            try { await work; Assert.Fail("Rejected selection published"); } catch (OperationCanceledException) { }
            Assert.That(locked, Is.Not.Null);
            Assert.That(host.Service.IsBusy, Is.False);
            locked!.Dispose();
            await session.ReadAsync(new(InspectorMember.Value));
            Assert.That(session.Error, Is.Null);
            Assert.That(session.Snapshot!.Complete(InspectorMember.Value), Is.True);
            Assert.That(session.CleanupError, Is.Not.Null);
            session.RetryCleanup();
            Assert.That(session.CleanupError, Is.Null);
            host.Service.BeginShutdown(); await host.Service.DrainAsync();
            Assert.That(dir.GetFiles(), Is.Empty);
            Assert.That(host.Service.EditorCoordinator.Pending, Is.Zero);
        }
        finally { locked?.Dispose(); session.RetryCleanup(); dir.Delete(); }
    });

    private sealed class ThrowingType : VariableDeclaration
    {
        internal int Calls;
        public override string Value { get { Calls++; throw new InvalidOperationException("untrusted value"); } set { } }
        public override CryptoType? Type { get { Calls++; throw new InvalidOperationException("untrusted"); } set { } }
    }

    [Test]
    public Task UnknownRuntimeNeverDispatchesValueGetter() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        var unknown = new ThrowingType { Id = "unknown" };
        VariableDictionary.Instance().Add(unknown);
        var row = new CryptoWorkBenchAvalonia.Models.VariableModel();
        var handle = new SelectionHandle(new("unknown", new(unknown), row, 0,
            h.Service.EditorCoordinator.WorkspaceGeneration, InterpreterHostQueue.StateRevision));
        try { await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Value), () => true, default); Assert.Fail("Unknown accessor must fail"); }
        catch (NotSupportedException) { }
        Assert.That(unknown.Calls, Is.Zero);
    });

    [Test]
    public async Task ActualFileCreationFailureDoesNotPublishOrTouchForeignFile()
    {
        string occupied = Path.Combine(Path.GetTempPath(), "cwb-inspector-file-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(occupied, "keep");
        try
        {
            using var payload = new InspectorPayload(0, occupied, 0);
            try { await payload.AppendAsync(new byte[1], default); Assert.Fail("File cannot be used as a directory"); }
            catch (IOException) { }
            Assert.That(payload.IsComplete, Is.False);
            Assert.That(await File.ReadAllTextAsync(occupied), Is.EqualTo("keep"));
        }
        finally { File.Delete(occupied); }
    }

    [Test]
    public Task DisposeBeforeDispatcherCannotPublishOrAcquire() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        h.Select(new StringVariableDeclaration { Id = "value", Text = "data" });
        var session = new InspectorSession(h.Variables);
        session.Dispose();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await session.Pending;
        Assert.That(session.Snapshot, Is.Null);
        Assert.That(h.Service.IsBusy, Is.False);
        Assert.That(h.Service.EditorCoordinator.Pending, Is.Zero);
    });

    [Test]
    public Task FullLargeShareSetRemainsReadableDuringMcpLease() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        const int length = 131072;
        var set = new SecretSet(new(SecretDescriptor.Split, 255, 255, length * 8),
            Enumerable.Range(1, 255).Select(i => new SecretShare(i, Enumerable.Repeat((byte)i, length).ToArray())));
        var handle = h.Select(new SecretVariableDeclaration(set) { Id = "secret" });
        using var snapshot = new InspectorSnapshot(handle.Stamp);
        for (int i = 0; i < 255; i++)
            snapshot.Add(await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.ShareValue, i),
                () => h.Variables.InspectorSelectionCurrent(handle), default));
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("historical-copy", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var lease = await h.Service.AcquireEditorAsync(caller,
            new("2", h.Service.InstanceId, h.Registration.Id, Guid.NewGuid(), h.Registration.Snapshot().Revision), default);
        Assert.That(lease.Error, Is.Null);
        long read = 0;
        foreach (var part in snapshot.Parts)
        {
            Assert.That(snapshot.Complete(InspectorMember.ShareValue, part.ShareIndex), Is.True);
            using var bytes = new MemoryStream(); await part.Payload.CopyToAsync(bytes);
            Assert.That(bytes.ToArray(), Is.All.EqualTo((byte)(part.ShareIndex + 1))); read += bytes.Length;
        }
        Assert.That(read, Is.EqualTo(33423360L));
        await h.Service.ReleaseEditorAsync(caller, new("2", h.Service.InstanceId, h.Registration.Id, lease.Result!.LeaseId), default);
    });

    [Test]
    public Task ObserverCoalescesSelectionAndEditPreservesHistoryWhileResetClears() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        h.Select(new StringVariableDeclaration { Id = "a", Text = "first", Value = "value" });
        using var session = new InspectorSession(h.Variables);
        h.Variables.SelectedVariable = null;
        h.Variables.SelectedVariable = h.Variables.DataVariables[0];
        Assert.That(session.Snapshot, Is.Null, "Observer has not captured anything");
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await session.Pending;
        Assert.That(session.Error, Is.Null);
        Assert.That(session.Snapshot!.Complete(InspectorMember.Text), Is.False, "Selection must not eagerly copy the value");
        await session.ReadAsync(new(InspectorMember.Text));
        Assert.That(session.Snapshot!.Complete(InspectorMember.Text), Is.True);
        var snapshot = session.Snapshot;
        h.Editor.Text += "!";
        Assert.That(session.Historical, Is.True);
        Assert.That(session.Snapshot, Is.SameAs(snapshot));
        Assert.That(h.Variables.InspectorSelection(), Is.Null, "Typing requires a new Variables publication");
        using var text = new MemoryStream();
        foreach (var part in snapshot.Parts) await part.Payload.CopyToAsync(text);
        Assert.That(Encoding.Unicode.GetString(text.ToArray()), Is.EqualTo("first"));
        await h.Service.RunAsync(_ => Task.FromResult(h.Service.ClearAdmittedState()), default);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.That(session.Snapshot, Is.Null, "Semantic clear must reach the session");
        Assert.That(h.Service.IsBusy, Is.False);
    });

    [Test]
    public Task LateUiInvalidationDiscardsWorkerResult() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool current = true;
        var work = h.Service.RunInspectorReadAsync(async _ => { started.SetResult(); await finish.Task; return "late"; }, () => current);
        await started.Task; current = false; finish.SetResult();
        try { await work; Assert.Fail("Late result must not succeed"); } catch (OperationCanceledException) { }
        Assert.That(h.Service.EditorCoordinator.Pending, Is.Zero);
        Assert.That(h.Service.IsBusy, Is.False);
    });

    [Test]
    public Task NullAndEmptyAreDifferentCapturedValues() => GuiTestSession.RunAsync(async () =>
    {
        foreach (var value in new string?[] { null, "" })
        {
            await using var h = new Host();
            var handle = h.Select(new StringVariableDeclaration { Id = "value", Value = value! });
            var part = await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Value), () => true, default);
            using (part.Payload) { Assert.That(part.IsNull, Is.EqualTo(value == null)); Assert.That(part.Count, Is.Zero); }
        }
        Assert.That(new InspectorScalar(ulong.MaxValue).Value, Is.EqualTo(ulong.MaxValue));
        Assert.That(new InspectorScalar(long.MinValue).Value, Is.EqualTo(long.MinValue));
    });

    [Test]
    public Task RecoveryNeverReadsUnselectedVirtualGetters() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new Host();
        var handle = h.Select(new StringVariableDeclaration { Id = "safe", Value = "ok" });
        var other = new ThrowingType { Id = "other" };
        VariableDictionary.Instance().Add(other);
        Assert.Throws<InvalidOperationException>(() => h.Service.Refresh());
        int calls = other.Calls;
        var before = (await h.Service.GetVariablesAsync(WorkbenchAutomationService.LocalCaller, default)).Data;
        var part = await InspectorCapture.ReadAsync(h.Service, handle, new(InspectorMember.Value), () => true, default);
        using (part.Payload) { Assert.That(other.Calls, Is.EqualTo(calls)); }
        Assert.That((await h.Service.GetVariablesAsync(WorkbenchAutomationService.LocalCaller, default)).Data, Is.EqualTo(before));
    });

    [Test]
    public async Task IoFailureAndCancellationLeaveNoSegments()
    {
        string directory = Path.Combine(Path.GetTempPath(), "cwb-inspector-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using (var payload = new InspectorPayload(0, directory, long.MaxValue / 2))
            {
                try { await payload.AppendAsync(new byte[1], default); Assert.Fail("Reserve must fail"); } catch (IOException) { }
            }
            using (var payload = new InspectorPayload(0, directory, 0))
            {
                await payload.AppendAsync(new byte[65536], default);
                try { await payload.AppendAsync(new byte[1], new CancellationToken(true)); Assert.Fail("Must cancel"); } catch (OperationCanceledException) { }
            }
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        finally { Directory.Delete(directory); }
    }

    [Test]
    public Task ReadUsesExistingOwnerWithoutExecutionPublicationOrRevision() => GuiTestSession.RunAsync(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        var editor = new WorkbenchTextEditor { Text = "source" };
        service.AttachEditor(editor, null!);
        var before = await service.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default);
        var variables = await service.GetVariablesAsync(WorkbenchAutomationService.LocalCaller, default);
        long revision = InterpreterHostQueue.StateRevision;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = service.RunInspectorReadAsync(async token =>
        {
            service.AccessJobState(() => { started.SetResult(); return true; });
            await finish.Task.WaitAsync(token); return 42;
        }, () => true);
        await started.Task;
        Assert.That(service.IsBusy, Is.True);
        Assert.Throws<ExecutionBusyException>(() => service.RunInspectorReadAsync(_ => Task.FromResult(0), () => true));
        finish.SetResult();
        Assert.That(await read, Is.EqualTo(42));
        Assert.That(service.IsBusy, Is.False);
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
        var after = await service.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default);
        Assert.That(after.Data!.LastExecutionId, Is.EqualTo(before.Data!.LastExecutionId));
        Assert.That((await service.GetVariablesAsync(WorkbenchAutomationService.LocalCaller, default)).Data, Is.EqualTo(variables.Data));
        Assert.That(service.EditorCoordinator.Pending, Is.Zero);
    });

    [Test]
    public Task CancellationAndShutdownDrainAcceptedRead() => GuiTestSession.RunAsync(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        service.AttachEditor(new WorkbenchTextEditor(), null!);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = service.RunInspectorReadAsync(async token =>
        {
            started.SetResult(); await Task.Delay(Timeout.Infinite, token); return 1;
        }, () => true);
        await started.Task;
        service.BeginShutdown();
        try { await read; Assert.Fail("Read must cancel"); } catch (OperationCanceledException) { }
        await service.DrainAsync();
        Assert.That(service.IsBusy, Is.False);
        Assert.That(service.EditorCoordinator.Pending, Is.Zero);
    });

    [Test]
    public Task ObserverCannotAcquireAndFailureReleasesIntent() => GuiTestSession.RunAsync(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        service.AttachEditor(new WorkbenchTextEditor(), null!);
        service.EditorCoordinator.Observe(() => Assert.Throws<ExecutionBusyException>(() =>
            service.RunInspectorReadAsync(_ => Task.FromResult(1), () => true)));
        foreach (string checkpoint in new[] { "registration", "worker" })
        {
            service.AdmissionCheckpoint = step => { if (step == checkpoint) throw new IOException("injected acceptance failure"); };
            Assert.Throws<IOException>(() => service.RunInspectorReadAsync(_ => Task.FromResult(1), () => true));
            Assert.That(service.IsBusy, Is.False);
            Assert.That(service.EditorCoordinator.Pending, Is.Zero);
            service.AdmissionCheckpoint = null;
            Assert.That(await service.RunInspectorReadAsync(_ => Task.FromResult(1), () => true), Is.EqualTo(1));
        }
    });

    [Test]
    public async Task SpillPreservesAllBytesAndDeletesOwnedFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "cwb-inspector-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using (var payload = new InspectorPayload(0, directory, 0))
            {
                byte[] block = Enumerable.Range(0, 65536).Select(i => (byte)i).ToArray();
                for (int i = 0; i < 257; i++) await payload.AppendAsync(block, default);
                await payload.SealAsync(default);
                Assert.That(Directory.GetFiles(directory).Select(p => new FileInfo(p).Length), Is.All.LessThanOrEqualTo(1024L * 1024));
                using var actual = new MemoryStream();
                await payload.CopyToAsync(actual);
                Assert.That(actual.Length, Is.EqualTo(257L * block.Length));
                Assert.That(actual.GetBuffer().AsSpan(0, (int)actual.Length).ToArray(),
                    Is.EqualTo(Enumerable.Range(0, 257).SelectMany(_ => block).ToArray()));
            }
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        finally { Directory.Delete(directory); }
    }
}
