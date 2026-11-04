using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class B3F1Tests
{
    private static AutomationCaller Activate(Host h)
    {
        h.Service.ActivateMcp();
        return h.Service.IssueCredential("f1-owner", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
    }

    private static async Task<EditorExecutionV2> Bind(Host h, AutomationCaller c)
    {
        var snapshot = h.Registration.Snapshot();
        var state = (await h.Service.GetStatusV2Async(c, new("2"), default)).Result!;
        var lease = (await h.Service.AcquireEditorAsync(c, new("2", h.Service.InstanceId, snapshot.EditorId,
            Guid.NewGuid(), snapshot.Revision), default)).Result!;
        return new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId, Guid.NewGuid(), snapshot.Revision, state.StateVersion);
    }

    [TestCase(false), TestCase(true)]
    public Task SaveRechecksL1AfterReadOnlyObservers(bool synchronous) => Ui(async () =>
    {
        await using var h = new Host("original");
        Activate(h);
        string path = Path.GetTempFileName();
        bool revoked = false, deleted = false, providerStarted = false;
        try
        {
            var intent = h.Service.CaptureSaveIntent()!;
            h.Editor.PropertyChanged += (_, args) =>
            {
                if (!revoked && args.Property == TextEditor.IsReadOnlyProperty && h.Editor.IsReadOnly)
                {
                    revoked = true;
                    h.Service.DisableMcp();
                    Assert.That(h.Service.IsFileIntentCurrent(intent), Is.False);
                    _ = h.Service.StartFileIntentStep<object>(intent, () =>
                    {
                        providerStarted = true;
                        return Task.FromResult<object?>(null);
                    });
                }
            };
            if (synchronous) Assert.Throws<ExecutionBusyException>(() => h.Service.SaveScriptBook(h.Editor, path));
            else Assert.That(await h.Service.CommitSaveAsync(intent, new(path, true, () =>
            { deleted = true; return Task.CompletedTask; })), Is.Not.Null);
            await h.Service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(providerStarted, Is.False);
            Assert.That((revoked, deleted, File.ReadAllText(path), h.Editor.IsReadOnly, h.Owner.Pending), Is.EqualTo((true, false, "", false, 0)));
            h.Service.SaveScriptBook(h.Editor, path);
            Assert.That(File.ReadAllText(path), Is.EqualTo("original\n"));
        }
        finally { File.Delete(path); }
    });

    [Test]
    public Task DeleteBeforeL1CompletesOriginalWriteAndUnlock() => Ui(async () =>
    {
        await using var h = new Host("saved");
        Activate(h);
        string path = Path.GetTempFileName();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var save = h.Service.CommitSaveAsync(h.Service.CaptureSaveIntent()!, new(path, true, () =>
            {
                File.Delete(path);
                h.Service.DisableMcp();
                return release.Task;
            }));
            var close = h.Service.DrainAsync();
            Assert.That(File.Exists(path) || close.IsCompleted || h.Owner.CanStartGui, Is.False);
            Assert.That(h.Editor.MutationLocked, Is.True);
            release.SetResult();
            Assert.That(await save, Is.Null);
            await close.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That((File.ReadAllText(path), h.Editor.MutationLocked, h.Owner.Pending, h.Owner.CanStartGui),
                Is.EqualTo(("saved\n", false, 0, true)));
        }
        finally { release.TrySetResult(); File.Delete(path); }
    });

    [TestCase(false), TestCase(true)]
    public Task DetachedCutWaitsForOriginalClipboardBeforeClose(bool dispose) => Ui(async () =>
    {
        await using var h = new Host("original");
        Activate(h);
        var clipboard = h.Window.Clipboard!;
        var proxy = DispatchProxy.Create<IClipboard, EditorCoordinatorTests.ClipboardProxy>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = (EditorCoordinatorTests.ClipboardProxy)(object)proxy;
        provider.Inner = clipboard;
        provider.DelayedWrite = gate.Task;
        EditorCoordinatorTests.ClipboardProxy.Bind(proxy);
        try
        {
            h.Editor.Select(0, 4);
            ApplicationCommands.Cut.Execute(null, h.Area);
            var cut = h.Area.PendingClipboard;
            Assert.That(cut.IsCompleted, Is.False);
            h.Service.DetachEditor(h.Registration);
            h.Service.DisableMcp();
            h.Service.DisableMcp();
            var drain = h.Service.DrainAsync();
            var second = dispose ? h.Service.DisposeAsync().AsTask() : h.Service.DrainAsync();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
            Assert.That(drain.IsCompleted || second.IsCompleted || h.Owner.CanStartGui, Is.False);
            var next = new WorkbenchTextEditor { Text = "next" };
            Assert.That(h.Service.AttachEditor(next, null!), Is.Null);
            gate.SetResult();
            await Task.WhenAll(cut, drain, second).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That((h.Editor.Text, next.Text, h.Owner.Pending), Is.EqualTo(("original", "next", 0)));
            if (!dispose)
            {
                Assert.That(h.Service.AttachEditor(next, null!), Is.Not.Null);
                next.CaretOffset = 4;
                next.TextArea.PerformTextInput("!");
                Assert.That(next.Text, Is.EqualTo("next!"));
            }
        }
        finally { gate.TrySetResult(); EditorCoordinatorTests.ClipboardProxy.Bind(clipboard); }
    });

    [Test]
    public Task DetachedSealedBodyStillOwnsItsCallback() => Ui(async () =>
    {
        await using var h = new Host("callback");
        Activate(h);
        var body = h.Owner.TryBegin()!;
        using var callback = body.RegisterCallback();
        body.Dispose();
        h.Service.DetachEditor(h.Registration);
        h.Service.DisableMcp();
        var drain = h.Service.DrainAsync();
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
        Assert.That(body.Completion.IsCompleted || drain.IsCompleted, Is.False);
        callback.Dispose();
        await Task.WhenAll(body.Completion, drain).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(h.Owner.Pending, Is.Zero);
    });

    [TestCase(true), TestCase(false)]
    public Task FaultedEpochDistinguishesOpenNativeGroupFromExitQuiescence(bool openGroup) => Ui(async () =>
    {
        await using var h = new Host("before");
        var caller = Activate(h);
        if (openGroup)
        {
            var r = await Bind(h, caller);
            h.Editor.Document.UpdateStarted += (_, _) => throw new InvalidOperationException();
            var receipt = await h.Service.UpdateEditorV2Async(caller, new("2", r.InstanceId, r.EditorId, r.LeaseId,
                r.ExpectedRevision, EditorEditMode.Append, "!"), default);
            Assert.That((receipt.TextEffect, receipt.HistoryEffect, receipt.Finalization), Is.EqualTo(
                (TextEffect.Indeterminate, HistoryEffect.Indeterminate, Finalization.Faulted)));
        }
        else h.Owner.RunGui(() => throw new InvalidOperationException());
        Assert.That(h.Editor.Document.IsInUpdate, Is.EqualTo(openGroup));
        h.Service.DisableMcp();
        if (openGroup)
            await Assert.ThatAsync(async () => await h.Service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(5)),
                Throws.InstanceOf<InvalidOperationException>());
        else await h.Service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(h.Owner.Phase, Is.EqualTo(EditorControlPhase.Faulted));
        Assert.That(h.Owner.CanStartGui, Is.False);
        Assert.Throws<InvalidOperationException>(() => h.Service.ActivateMcp());
        h.Service.BeginShutdown();
        await h.Service.DrainForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await h.Service.DisposeAsync();
        await h.Service.DisposeAsync();
        var original = h.Editor.Document;
        var handlers = (Delegate?)original.GetType().GetField("UpdateFinished", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!.GetValue(original);
        Assert.That(handlers?.GetInvocationList().Any(d => ReferenceEquals(d.Target, h.Owner)) ?? false, Is.False);
        Assert.That((original.IsInUpdate, h.Owner.CanStartGui), Is.EqualTo((openGroup, false)));
    });

    [Test]
    public Task JobReadsRespectGrantsFromFirstReplyThroughTombstone() => Ui(async () =>
    {
        var clock = new WorkbenchRegistryTests.ManualClock();
        await using var h = new Host("Print(0x(01))", new WorkbenchAutomationService(clock) { LeaseMilliseconds = () => 1_000UL });
        var owner = Activate(h);
        var limited = h.Service.IssueCredential(owner.PrincipalId, owner.Permissions.Remove(AutomationPermissions.ExecutionRead)
            .Remove(AutomationPermissions.VariablesMetadataRead));
        var read = h.Service.IssueCredential(owner.PrincipalId, limited.Permissions.Add(AutomationPermissions.ExecutionRead)
            .Remove(AutomationPermissions.ExecutionRun));
        var foreign = h.Service.IssueCredential("f1-other", owner.Permissions);
        var dictionary = VariableDictionary.Instance();
        var previous = dictionary.GetVariables().ToArray();
        var answer = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            const string marker = "F1LocalMarker";
            var seed = await h.Service.ExecuteTextAsync(WorkbenchAutomationService.LocalCaller, new(h.Service.InstanceId,
                Guid.NewGuid(), "0", "VAR F1LocalMarker=0x(01)", TextExecutionMode.Submission), default);
            Assert.That(seed.Data!.Variables.Items.Any(v => v.Identifier == marker), Is.True);
            var request = await Bind(h, owner);
            var query = new JobRequestV2("2", request.InstanceId, request.ExecutionId);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            h.Service.ExecutionConfirmation = (_, _, _) => { entered.SetResult(); return answer.Task; };
            var first = h.Service.ExecuteEditorV2Async(limited, request, default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            async Task CheckReads()
            {
                Assert.That((await h.Service.ExecuteEditorV2Async(limited, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
                Assert.That((await h.Service.GetExecutionV2Async(limited, query, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
                Assert.That((await h.Service.ExecuteEditorV2Async(foreign, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseNotFound));
                Assert.That((await h.Service.ExecuteEditorV2Async(read, request, default)).Result!.Variables.Items, Is.Empty);
                var projected = (await h.Service.GetExecutionV2Async(read, query, default)).Result!;
                Assert.That(projected.Variables.Items, Is.Empty);
                Assert.That((projected.Variables.StateVersion, projected.Variables.CapturedAt, projected.Variables.Stale, projected.Variables.Truncated),
                    Is.EqualTo((projected.StateVersion, projected.FinishedAt ?? projected.AcceptedAt, true, false)));
                Assert.That((await h.Service.ExecuteEditorV2Async(owner, request, default)).Result!.Variables.Items.Any(v => v.Identifier == marker), Is.True);
            }
            await CheckReads();
            answer.SetResult(false);
            var initial = (await first).Result!;
            Assert.That(initial.State, Is.EqualTo(ExecutionState.Rejected));
            Assert.That(initial.Variables.Items, Is.Empty);
            await h.Service.ReleaseEditorAsync(owner, new("2", request.InstanceId, request.EditorId, request.LeaseId), default);
            await CheckReads();
            clock.Advance(AutomationContracts.ResultRetention);
            Assert.That((await h.Service.ExecuteEditorV2Async(read, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.ResultExpired));
            Assert.That((await h.Service.ExecuteEditorV2Async(limited, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
            Assert.That((await h.Service.ExecuteEditorV2Async(foreign, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.LeaseNotFound));
            h.Service.DisableMcp();
            Assert.That((await h.Service.ExecuteEditorV2Async(owner, request, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        }
        finally { answer.TrySetResult(false); dictionary.Clear(); foreach (var v in previous) dictionary.Add(v); }
    });

    [TestCase("accept"), TestCase("reject"), TestCase("missing"), TestCase("ownerLost"), TestCase("revoke")]
    public Task LocalExecutionConsentRecordsActualDecision(string action) => Ui(async () =>
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        string script = $"PATH F1Target=\"{path.Replace('\\', '/')}\"\nVAR F1Public=0x(01)\nSave(F1Target,F1Public)";
        await using var h = new Host(script);
        var caller = Activate(h);
        var request = await Bind(h, caller);
        var dictionary = VariableDictionary.Instance();
        var previous = dictionary.GetVariables().ToArray();
        try
        {
            if (action == "missing") h.Window.Content = null;
            var result = h.Service.ExecuteEditorV2Async(caller, request, default);
            if (action != "missing")
            {
                for (int i = 0; i < 200 && !h.Window.OwnedWindows.Any() && !result.IsCompleted; i++) await Task.Delay(10);
                var dialog = h.Window.OwnedWindows.Single();
                var grid = (Grid)dialog.Content!;
                string labels = string.Join("\n", grid.Children.OfType<TextBlock>().Select(b => b.Text));
                Assert.That(labels, Does.Contain(caller.PrincipalId).And.Contain("desktop permissions").And.Contain("read or export files and cryptographic keys"));
                Assert.That(grid.Children.OfType<TextBox>().Select(b => b.Text), Is.EqualTo(new[] { script,
                    $"Working directory: {Environment.CurrentDirectory}\nState version: {request.ExpectedStateVersion}\nOutput: MetadataOnly" }));
                if (action == "ownerLost") h.Window.Close();
                else if (action == "revoke") h.Service.DisableMcp();
                else dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, action == "accept" ? "Execute" : "Cancel"))
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            var first = await result.WaitAsync(TimeSpan.FromSeconds(5));
            var replay = await h.Service.ExecuteEditorV2Async(caller, request, default);
            if (action == "revoke")
                Assert.That((first.Error?.Code, replay.Error?.Code), Is.EqualTo((AutomationErrorCode.Forbidden, AutomationErrorCode.Forbidden)));
            else
            {
                var expected = action switch { "accept" => ExecutionState.Completed, "reject" => ExecutionState.Rejected, _ => ExecutionState.Failed };
                Assert.That(first.Result!.State, Is.EqualTo(expected));
                Assert.That(first.Result.Error?.Code, Is.EqualTo(expected == ExecutionState.Failed ? (AutomationErrorCode?)AutomationErrorCode.ConfirmationUnavailable : null));
                Assert.That(replay.Result, Is.EqualTo(first.Result));
            }
            Assert.That(File.Exists(path), Is.EqualTo(action == "accept"));
            Assert.That(h.Editor.Text, Is.EqualTo(script));
        }
        finally { File.Delete(path); dictionary.Clear(); foreach (var v in previous) dictionary.Add(v); }
    });
}
