using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CryptoScript.Model;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockOutputTests
{
    private static Task Run(Func<DockToolTests.Host, Task> test) => GuiTestSession.Session.Dispatch(async () =>
    { await using var h = new DockToolTests.Host(); await test(h); return true; }, CancellationToken.None);
    private static void Load(DockToolTests.Host h, string text) =>
        Assert.That(h.Session.LoadText(h.Service.CaptureSaveIntent()!, text), Is.True);

    [Test] public Task ParserPositionsOrderIdentityAndTextArePreserved() => Run(async h =>
    {
        const string text = "  Print(0x(AB))\n    Print(0x(CD))";
        Load(h, text);
        var before = h.Service.CaptureSaveIntent()!.Snapshot;
        var version = h.Session.Editor.Document.Version; var undo = h.Session.Editor.Document.UndoStack;
        var caret = h.Session.Editor.CaretOffset;
        await h.Session.ViewModel.ParseLineAsync(text);
        var rows = h.Session.Output.Entries;
        Assert.That(rows.Select(e => e.Text), Is.EqualTo(new[] { "out: 0x(AB)", "out: 0x(CD)" }));
        Assert.That(rows.Select(e => e.SourceLine), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(rows.Select(e => e.SourceColumn), Is.EqualTo(new[] { 3, 5 }));
        Assert.That(rows.Select(e => e.Sequence), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(rows.All(e => e.DocumentId == before.EditorId && ReferenceEquals(e.SourceRevision, version)), Is.True);
        Assert.That(rows[0].ExecutionId, Is.EqualTo(rows[1].ExecutionId).And.Not.EqualTo(Guid.Empty));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(before));
        Assert.That(h.Session.Editor.Document.UndoStack, Is.SameAs(undo));
        Assert.That(h.Session.Editor.CaretOffset, Is.EqualTo(caret));
    });

    [TestCase("Info(HASH-SHA256)")]
    [TestCase("VAR x=0x(01)")]
    [TestCase("Print(")]
    [TestCase("KEY missing=Check(0x(AA))")]
    public Task NonPrintResultsNeverEnterOutput(string text) => Run(async h =>
    { await h.Session.ViewModel.ParseLineAsync(text); Assert.That(h.Session.Output.Entries, Is.Empty); });

    [Test] public Task PrintBeforeSemanticErrorSurvives() => Run(async h =>
    {
        await h.Session.ViewModel.ParseLineAsync("Print(0x(AB)) Print(missing)");
        Assert.That(h.Session.Output.Entries.Single().Text, Is.EqualTo("out: 0x(AB)"));
        Assert.That(h.Status.HasDiagnostics, Is.True);
        Assert.That(h.Status.StatusString, Does.Contain("aborted"));
    });

    [Test] public Task UnknownSourceAndMultilineUnicodeAreNotGuessed() => Run(async h =>
    {
        await h.Session.ViewModel.ExecuteLineAsync("", testStatements: [new AsyncInfrastructureStatement(_ =>
        { new OutputOperations().Print(["ä€😀\n0x(AB)\nYWJj=="]); return Task.CompletedTask; })]);
        var entry = h.Session.Output.Entries.Single();
        Assert.That(entry.Text, Is.EqualTo("out: ä€😀\n0x(AB)\nYWJj=="));
        Assert.That(entry.SourceLine, Is.Null); Assert.That(entry.SourceColumn, Is.Null);
        Assert.That(h.Session.NavigateOutput(entry), Is.False);
    });

    [TestCase(false)][TestCase(true)]
    public Task NativeEnterProducesOnlyNativeNewlineAndUndo(bool floating) => Run(async h =>
    {
        Load(h, "Print(0x(11))");
        if (floating) h.Float(DockShellState.DocumentId);
        var editor = h.Session.Editor; editor.CaretOffset = editor.Text.Length; editor.TextArea.Focus();
        editor.TextArea.PerformTextInput("\n"); await h.Session.View.PendingExecution;
        Assert.That(editor.Text, Is.EqualTo("Print(0x(11))" + Environment.NewLine));
        Assert.That(h.Session.Output.Entries.Single().SourceLine, Is.EqualTo(1));
        Assert.That(h.Session.Output.Entries.Single().SourceColumn, Is.EqualTo(1));
        Assert.That(h.Session.NavigateOutput(h.Session.Output.Entries.Single()), Is.True);
        ApplicationCommands.Undo.Execute(null, editor.TextArea); Assert.That(editor.Text, Is.EqualTo("Print(0x(11))"));
        ApplicationCommands.Redo.Execute(null, editor.TextArea); Assert.That(editor.Text, Is.EqualTo("Print(0x(11))" + Environment.NewLine));
        Assert.That(h.Session.Output.Entries.Count, Is.EqualTo(1));
    });

    [Test] public Task NavigationChecksRevisionIdentityBoundsAndHasNoExecutionEffects() => Run(async h =>
    {
        Load(h, "  Print(0x(AA))"); await h.Session.ViewModel.ParseLineAsync(h.Session.Editor.Text);
        var entry = h.Session.Output.Entries.Single(); var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var revision = InterpreterHostQueue.StateRevision;
        h.Show(DockShellState.OutputId);
        Assert.That(h.Session.NavigateOutput(entry), Is.True); Assert.That(h.Session.Editor.CaretOffset, Is.EqualTo(2));
        Assert.That(h.Session.Editor.IsKeyboardFocusWithin, Is.True);
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
        Assert.That(h.Session.NavigateOutput(entry with { DocumentId = Guid.NewGuid() }), Is.False);
        Assert.That(h.Session.NavigateOutput(entry with { SourceLine = 999 }), Is.False);
        Assert.That(h.Session.NavigateOutput(entry with { SourceColumn = 999 }), Is.False);
        Load(h, "  Print(0x(BB))"); Assert.That(h.Session.NavigateOutput(entry), Is.False);
    });

    [Test] public Task ClearAndCyclesKeepGlobalIdentityWithoutReplay() => Run(async h =>
    {
        await h.Session.ViewModel.ParseLineAsync("Print(0x(01))");
        await h.Session.ViewModel.ParseLineAsync("Print(0x(02))");
        var view = h.Presentation.Output!; var model = view.DataContext;
        Assert.That(h.Session.Output.Entries.Select(e => e.ExecutionId).Distinct().Count(), Is.EqualTo(2));
        for (int i = 0; i < 3; i++)
        {
            h.Float(DockShellState.OutputId); h.Layout.Dock(DockShellState.OutputId); h.Pump();
            h.Layout.Close(DockShellState.OutputId); h.Show(DockShellState.OutputId);
            Assert.That(view.DataContext, Is.SameAs(model)); Assert.That(h.Session.Output.Entries.Count, Is.EqualTo(2));
        }
        var before = h.Service.CaptureSaveIntent()!.Snapshot;
        h.Session.Output.Clear(); Assert.That(h.Session.Output.Entries, Is.Empty);
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(before));
        h.Layout.Close(DockShellState.OutputId); h.Show(DockShellState.OutputId);
        Assert.That(h.Session.Output.Entries, Is.Empty);
    });

    [TestCase(false)][TestCase(true)]
    public Task SelectionAndCtrlCCopyExactText(bool floating) => Run(async h =>
    {
        await h.Session.ViewModel.ExecuteLineAsync("", testStatements: [new AsyncInfrastructureStatement(_ =>
        { new OutputOperations().Print(["ä€😀\n0x(AB)\nYWJj=="]); return Task.CompletedTask; })]);
        h.Show(DockShellState.OutputId); var host = floating ? h.Float(DockShellState.OutputId) : h.Window;
        var text = h.Presentation.Output!.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "PrintText");
        var before = h.Service.CaptureSaveIntent()!.Snapshot;
        text.Focus(); text.SelectionStart = 0; text.SelectionEnd = text.Text!.Length;
        host.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c"); await Task.Delay(30);
        Assert.That(await host.Clipboard!.TryGetTextAsync(), Is.EqualTo("out: ä€😀\n0x(AB)\nYWJj=="));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(before));
    });

    [Test] public Task AsyncPartialPrintSurvivesCancellationWhileToolClosed() => Run(async h =>
    {
        var started = new TaskCompletionSource(); using var stop = new CancellationTokenSource();
        h.Layout.Close(DockShellState.OutputId);
        var work = h.Session.ViewModel.ExecuteLineAsync("", cancellationToken: stop.Token,
            testStatements: [new AsyncInfrastructureStatement(async context =>
            { new OutputOperations().Print(["partial"]); started.SetResult(); await Task.Delay(Timeout.Infinite, stop.Token); })]);
        await started.Task; Assert.That(h.Session.Output.Entries, Is.Empty);
        stop.Cancel(); await work;
        Assert.That(h.Session.Output.Entries.Single().Text, Is.EqualTo("out: partial"));
        h.Show(DockShellState.OutputId); Assert.That(h.Session.Output.Entries.Count, Is.EqualTo(1));
    });

    [Test] public Task DeduplicationClearRevocationAndDisposal() => Run(async h =>
    {
        Load(h, "Print(0x(AB))"); var source = h.Session.CaptureOutputSource(h.Session.Editor.Text)!;
        var result = await h.Service.ExecuteSubmissionAsync(h.Session.Editor.Text, (_, _) => throw new InvalidOperationException());
        h.Session.Output.Publish(result, source, () => true); h.Session.Output.Publish(result, source, () => true);
        Assert.That(h.Session.Output.Entries.Count, Is.EqualTo(1));
        h.Session.Output.Clear(); h.Session.Output.Publish(result, source, () => true); Assert.That(h.Session.Output.Entries, Is.Empty);
        var next = await h.Service.ExecuteSubmissionAsync(h.Session.Editor.Text, (_, _) => throw new InvalidOperationException());
        h.Session.Output.Publish(next, source, () => false); Assert.That(h.Session.Output.Entries, Is.Empty);
        h.Session.Output.Dispose(); h.Session.Output.Publish(next, source, () => true); Assert.That(h.Session.Output.Entries, Is.Empty);
    });

    [Test] public Task GuiBookHasPhysicalSourceLinesAndSkipsHistoricalOutput() => Run(async h =>
    {
        Load(h, "Print(0x(11))\nout: historical\n  Print(0x(22))");
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var result = await h.Service.ExecuteEditorAsync(WorkbenchAutomationService.LocalCaller,
            new(h.Service.InstanceId, Guid.NewGuid(), snapshot.EditorId, snapshot.Revision,
                (await h.Service.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default)).Data!.StateVersion), default);
        Assert.That(result.Error, Is.Null);
        Assert.That(h.Session.Output.Entries.Select(e => e.SourceLine), Is.EqualTo(new[] { 1, 3 }));
        Assert.That(h.Session.Output.Entries.Select(e => e.SourceColumn), Is.EqualTo(new[] { 1, 3 }));
        Assert.That(h.Session.Output.Entries.Select(e => e.Sequence), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(h.Session.Output.Entries.All(e => e.ExecutionId == result.Data!.ExecutionId), Is.True);
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
    });

    [Test] public Task McpV2ReceiptsAndLeaseNeverPublishToGui() => Run(async h =>
    {
        Load(h, "Print(0x(CAFE))"); h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("g105", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var lease = (await h.Service.AcquireEditorAsync(caller, new("2", h.Service.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
        h.Service.ExecutionConfirmation = (_, _, _) => Task.FromResult<bool?>(true);
        var state = (await h.Service.GetStatusV2Async(caller, new("2"), default)).Result!;
        var executed = await h.Service.ExecuteEditorV2Async(caller, new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId, Guid.NewGuid(), snapshot.Revision, state.StateVersion), default);
        Assert.That(executed.Error, Is.Null); Assert.That(executed.Result!.State, Is.EqualTo(ExecutionState.Completed));
        Assert.That(h.Session.Output.Entries, Is.Empty);
        await h.Session.ViewModel.ParseLineAsync("Print(0x(AB))"); Assert.That(h.Session.Output.Entries, Is.Empty);
        h.Layout.Float(DockShellState.OutputId); Assert.That(h.Layout.Root.Windows?.Count ?? 0, Is.Zero);
    });
    [Test] public Task ShutdownDrainsLateWorkWithoutPublishingIntoDisposedSession() => Run(async h =>
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = h.Float(DockShellState.OutputId);
        var work = h.Session.ViewModel.ExecuteLineAsync("", testStatements: [new AsyncInfrastructureStatement(async _ =>
        { new OutputOperations().Print(["before shutdown"]); started.SetResult(); await finish.Task; new OutputOperations().Print(["late"]); })]);
        await started.Task;
        h.Window.Close(); Assert.That(h.Service.IsStopping, Is.True);
        Assert.That(h.Window.IsVisible, Is.True, "Shutdown must drain the accepted operation.");
        finish.SetResult(); await work; await DockEditorTests.Wait(() => !h.Window.IsVisible);
        Assert.That(h.Session.Output.Entries, Is.Empty); Assert.That(host.IsVisible, Is.False);
        Assert.That(h.Service.HasRegisteredEditor, Is.False);
    });

    [Test] public Task CancellationBeforeAdmissionHasNoOutput() => Run(async h =>
    {
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await h.Session.ViewModel.ParseLineAsync("Print(0x(AB))", stop.Token);
        Assert.That(h.Session.Output.Entries, Is.Empty);
    });

    [Test] public Task LongOutputIsScrollableWithoutExpandingWindow() => Run(async h =>
    {
        string payload = new string('A', 20000);
        await h.Session.ViewModel.ExecuteLineAsync("", testStatements: [new AsyncInfrastructureStatement(_ =>
        { new OutputOperations().Print([payload]); return Task.CompletedTask; })]);
        h.Show(DockShellState.OutputId);
        Assert.That(h.Session.Output.Entries.Single().Text, Is.EqualTo("out: " + payload));
        Assert.That(h.Presentation.Output!.Bounds.Width, Is.LessThanOrEqualTo(h.Window.Bounds.Width));
        Assert.That(h.Presentation.Output.GetVisualDescendants().OfType<ScrollViewer>().Any(s => s.Extent.Width > s.Viewport.Width), Is.True);
    });

    [Test] public Task LocalBookExecutionPublishesOnUiAndPreservesOriginalText() => Run(async h =>
    {
        string[] lines = ["Print(0x(11))", "out: preserved", "Print(0x(22))"];
        Load(h, string.Join("\n", lines)); var original = h.Service.CaptureSaveIntent()!.Snapshot;
        var appended = new List<string>();
        await h.Session.ViewModel.ExecuteScriptBookAsync(lines, appended.Add);
        Assert.That(appended, Is.EqualTo(lines));
        Assert.That(h.Session.Output.Entries.Select(e => e.SourceLine), Is.EqualTo(new[] { 1, 3 }));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(original));
    });

}
