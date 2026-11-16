using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockDiagnosticsTests
{
    private static Task Run(Func<DockToolTests.Host, Task> test) => GuiTestSession.Session.Dispatch(async () =>
    { await using var h = new DockToolTests.Host(); await test(h); return true; }, CancellationToken.None);
    private static void Load(DockToolTests.Host h, string text) =>
        Assert.That(h.Session.LoadText(h.Service.CaptureSaveIntent()!, text), Is.True);
    private static ExecutionPresentation Result(params SemanticError[] errors) => new(true, "Line successfull parsed", null,
        errors, null, [], [], [], false, false) { ExecutionId = Guid.NewGuid(), Outcome = ExecutionState.Completed };
    private static SemanticError Warning(string text = "warning ä€😀") => new() { Severity = SemanticDiagnosticSeverity.Warning, Message = text };

    [TestCase("  @", 1, 3)]
    [TestCase("\n    @", 2, 5)]
    public Task SyntaxCoordinatesAreParserCoordinates(string script, int line, int column) => Run(async h =>
    {
        Load(h, script); var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        await h.Session.ViewModel.ParseLineAsync(script);
        var item = h.Status.Diagnostics.First();
        Assert.That(item.Category, Is.EqualTo(DiagnosticCategory.SyntaxError));
        Assert.That(item.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Error));
        Assert.That(item.SourceLine, Is.EqualTo(line)); Assert.That(item.SourceColumn, Is.EqualTo(column));
        Assert.That(item.DocumentId, Is.EqualTo(snapshot.EditorId)); Assert.That(item.ExecutionId, Is.Not.Null.And.Not.EqualTo(Guid.Empty));
        Assert.That(item.SourceRevision, Is.SameAs(h.Session.Editor.Document.Version));
        Assert.That(h.Session.NavigateDiagnostic(item), Is.EqualTo(SourceNavigationResult.Success));
        Assert.That(h.Session.Editor.TextArea.Caret.Line, Is.EqualTo(line));
        Assert.That(h.Session.Editor.TextArea.Caret.Column, Is.EqualTo(column));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
    });

    [Test] public Task MultipleSyntaxErrorsOnSameLineRemainDistinctAndOrdered() => Run(async h =>
    {
        Load(h, "@ @"); await h.Session.ViewModel.ParseLineAsync(h.Session.Editor.Text);
        Assert.That(h.Status.Diagnostics.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(h.Status.Diagnostics.Take(2).Select(x => x.SourceColumn), Is.EqualTo(new[] { 1, 3 }));
        Assert.That(h.Status.Diagnostics.Select(x => x.Sequence), Is.EqualTo(Enumerable.Range(1, h.Status.Diagnostics.Count)));
        Assert.That(h.Status.Diagnostics.Select(x => x.ExecutionId).Distinct().Count(), Is.EqualTo(1));
    });

    [Test] public Task SemanticErrorDoesNotChangeS01ContinuationOrInventCoordinates() => Run(async h =>
    {
        Load(h, "KEY missing=Check(0x(AA)) VAR continued=0x(01)");
        await h.Session.ViewModel.ParseLineAsync(h.Session.Editor.Text);
        var item = h.Status.Diagnostics.Single();
        Assert.That(item.Category, Is.EqualTo(DiagnosticCategory.SemanticError));
        Assert.That(item.Text, Does.Contain("KeyCheckValueNotFound"));
        Assert.That(item.SourceLine, Is.Null); Assert.That(item.SourceColumn, Is.Null);
        Assert.That(h.Variables.DataVariables.Select(v => v.Identifier), Does.Contain("continued"));
        Assert.That(h.Status.StatusString, Is.EqualTo("Line successfull parsed"));
    });

    [Test] public Task WarningsErrorsPrintAndPartialVariablesRetainOriginalMeaning() => Run(async h =>
    {
        var warning = Warning(); var error = new SemanticError { Message = "reported error" };
        var runner = CryptoScriptExecutionPresentationTests.ControlledRunner((_, sink) => { sink.Report(warning); sink.Report(error); });
        bool success = await h.Session.ViewModel.ExecuteLineAsync("VAR retained=0x(01) Print(0x(02))", runner: runner);
        Assert.That(success, Is.True);
        Assert.That(h.Status.Diagnostics.Select(x => x.Category), Is.EqualTo(new[] { DiagnosticCategory.SemanticWarning, DiagnosticCategory.SemanticError }));
        Assert.That(h.Status.Diagnostics[0].Text, Is.EqualTo(SemanticDiagnosticFormatter.Format(warning)));
        Assert.That(warning.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
        Assert.That(h.Session.Output.Entries.Single().Text, Is.EqualTo("out: 0x(02)"));
        Assert.That(h.Variables.DataVariables.Select(v => v.Identifier), Does.Contain("retained"));
    });

    [Test] public Task PartialPrintBeforeAbortAndSafeInfrastructureFailure() => Run(async h =>
    {
        await h.Session.ViewModel.ParseLineAsync("VAR retained=0x(01) Print(0x(AA)) Print(missing)");
        Assert.That(h.Status.Diagnostics.All(x => x.Category == DiagnosticCategory.SemanticError), Is.True);
        Assert.That(h.Status.HasDiagnostics, Is.True);
        Assert.That(h.Session.Output.Entries.Single().Text, Is.EqualTo("out: 0x(AA)"));
        Assert.That(h.Variables.DataVariables.Select(v => v.Identifier), Does.Contain("retained"));
        h.Status.ClearDiagnostics();
        await h.Session.ViewModel.ExecuteLineAsync("", testStatements: [new AsyncInfrastructureStatement(_ => throw new InvalidOperationException("private exception"))]);
        var failure = h.Status.Diagnostics.Single();
        Assert.That(failure.Category, Is.EqualTo(DiagnosticCategory.ExecutionError));
        Assert.That(failure.Text, Is.EqualTo("Script execution failed."));
        Assert.That(failure.SourceLine, Is.Null); Assert.That(failure.SourceColumn, Is.Null);
    });

    [Test] public Task CaptureFailureIsSeparateFromScriptOutcomeAndPreservesRows() => Run(h =>
    {
        var result = Result(Warning()) with { CaptureFailed = true };
        h.Status.AppendExecution(result, null, null, () => true);
        Assert.That(h.Status.Diagnostics.Select(x => x.Category), Is.EqualTo(new[] { DiagnosticCategory.SemanticWarning, DiagnosticCategory.ExecutionError }));
        Assert.That(h.Status.Diagnostics[1].Text, Does.Contain("capture failed"));
        Assert.That(result.Succeeded, Is.True); return Task.CompletedTask;
    });

    [Test] public Task FilterAndStatusNeverDeleteDiagnosticsOrIncludePrint() => Run(async h =>
    {
        h.Status.AppendExecution(Result(Warning(), new SemanticError { Message = "error" }), null, null, () => true);
        var original = h.Status.Diagnostics.ToArray(); var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var revision = InterpreterHostQueue.StateRevision;
        h.Status.Filter = DiagnosticFilter.Errors;
        Assert.That(h.Status.VisibleDiagnostics.Single(), Is.SameAs(original[1]));
        h.Status.Filter = DiagnosticFilter.Warnings;
        Assert.That(h.Status.VisibleDiagnostics.Single(), Is.SameAs(original[0]));
        h.Status.Filter = DiagnosticFilter.All; Assert.That(h.Status.VisibleDiagnostics, Is.EqualTo(original));
        h.Status.StatusString = "Script book saved in: fixture";
        Assert.That(h.Status.Diagnostics, Is.EqualTo(original));
        h.Show(DockShellState.StatusId);
        Assert.That(h.Presentation.Status!.GetVisualDescendants().OfType<SelectableTextBlock>().Single().Text, Is.EqualTo(h.Status.StatusString));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
        await h.Session.ViewModel.ParseLineAsync("Print(0x(AA))");
        Assert.That(h.Status.Diagnostics, Is.Empty); Assert.That(h.Session.Output.Entries.Count, Is.EqualTo(1));
    });

    [TestCase(DockShellState.DiagnosticsId)] [TestCase(DockShellState.StatusId)]
    public Task CloseFloatRedockKeepOneGlobalViewAndState(string id) => Run(h =>
    {
        h.Status.AppendExecution(Result(Warning()), null, null, () => true);
        Control view = id == DockShellState.DiagnosticsId ? h.Presentation.Diagnostics! : h.Presentation.Status!;
        for (int i = 0; i < 4; i++)
        {
            h.Float(id); h.Layout.Dock(id); h.Pump(); h.Layout.Close(id);
            h.Status.StatusString = "status " + i; h.Show(id);
            Assert.That(view.DataContext, Is.SameAs(h.Status));
            Assert.That(h.Status.Diagnostics.Count, Is.EqualTo(1));
        }
        h.Status.AppendExecution(Result(Warning("second")), null, null, () => true);
        Assert.That(h.Status.VisibleDiagnostics.Count, Is.EqualTo(2)); return Task.CompletedTask;
    });

    [TestCase(false)] [TestCase(true)]
    public Task TextSelectionCtrlCCopiesExactMessageWithoutEffects(bool floating) => Run(async h =>
    {
        h.Status.AppendExecution(Result(Warning("ä€😀\nline two")), null, null, () => true);
        h.Show(DockShellState.DiagnosticsId); var top = floating ? h.Float(DockShellState.DiagnosticsId) : h.Window;
        var text = h.Presentation.Diagnostics!.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot; var revision = InterpreterHostQueue.StateRevision;
        text.Focus(); text.SelectionStart = 0; text.SelectionEnd = text.Text!.Length;
        top.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c"); await Task.Delay(30);
        Assert.That(await top.Clipboard!.TryGetTextAsync(), Is.EqualTo(h.Status.Diagnostics[0].Text));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
    });

    [TestCase(false)] [TestCase(true)]
    public Task SourceButtonActivatesDockedOrFloatingEditorAndReportsStale(bool floating) => Run(async h =>
    {
        Load(h, "  @"); await h.Session.ViewModel.ParseLineAsync(h.Session.Editor.Text);
        if (floating) h.Float(DockShellState.DocumentId);
        h.Show(DockShellState.DiagnosticsId);
        var button = h.Presentation.Diagnostics!.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "DiagnosticSource");
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot; var undo = h.Session.Editor.Document.UndoStack;
        var revision = InterpreterHostQueue.StateRevision;
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.That(h.Session.Editor.CaretOffset, Is.EqualTo(2)); Assert.That(h.Session.Editor.IsKeyboardFocusWithin, Is.True);
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
        Assert.That(h.Session.Editor.Document.UndoStack, Is.SameAs(undo)); Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
        Load(h, "changed"); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.That(h.Presentation.Diagnostics!.Feedback.Text, Does.Contain("outdated"));
        Assert.That(h.Session.Editor.Text, Is.EqualTo("changed"));
    });

    [TestCase(0, 1)] [TestCase(99, 1)] [TestCase(1, 0)] [TestCase(1, 99)]
    public Task InvalidBoundsAreRejected(int line, int column) => Run(h =>
    {
        Load(h, "abc"); var source = h.Session.CaptureOutputSource("abc")!;
        Assert.That(h.Session.NavigateSource(source.DocumentId, source.Version, line, column), Is.EqualTo(SourceNavigationResult.Invalid));
        return Task.CompletedTask;
    });

    [Test] public Task IdentityUnknownRevisionAndLineOnlyNavigation() => Run(h =>
    {
        Load(h, "abc\ndef"); var source = h.Session.CaptureOutputSource(h.Session.Editor.Text)!;
        Assert.That(h.Session.NavigateSource(source.DocumentId, source.Version, 2, null), Is.EqualTo(SourceNavigationResult.Success));
        Assert.That(h.Session.Editor.CaretOffset, Is.EqualTo(4));
        Assert.That(h.Session.NavigateSource(Guid.NewGuid(), source.Version, 1, 1), Is.EqualTo(SourceNavigationResult.Stale));
        Assert.That(h.Session.NavigateSource(source.DocumentId, null, 1, 1), Is.EqualTo(SourceNavigationResult.Unknown));
        Assert.That(h.Session.NavigateSource(source.DocumentId, source.Version, null, 1), Is.EqualTo(SourceNavigationResult.Unknown));
        h.Session.Editor.Document.Insert(0, "x"); h.Session.Editor.Undo();
        Assert.That(h.Session.Editor.Text, Is.EqualTo("abc\ndef"));
        Assert.That(h.Session.NavigateSource(source.DocumentId, source.Version, 2, 1), Is.EqualTo(SourceNavigationResult.Stale));
        return Task.CompletedTask;
    });

    [TestCase(false)] [TestCase(true)]
    public Task BookUsesPhysicalOffsetAndReliableSemanticLineOnly(bool wholeEditor) => Run(async h =>
    {
        string[] lines = ["Print(0x(01))", "out: history", "KEY missing=Check(0x(AA))", "  @"];
        Load(h, string.Join("\n", lines));
        if (wholeEditor)
        {
            var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
            var result = await h.Service.ExecuteEditorAsync(WorkbenchAutomationService.LocalCaller,
                new(h.Service.InstanceId, Guid.NewGuid(), snapshot.EditorId, snapshot.Revision,
                    (await h.Service.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default)).Data!.StateVersion), default);
            Assert.That(result.Error, Is.Null);
        }
        else await h.Session.ViewModel.ExecuteScriptBookAsync(lines, _ => { });
        Assert.That(h.Status.Diagnostics.Select(x => x.SourceLine), Is.EqualTo(new[] { 3, 4 }));
        Assert.That(h.Status.Diagnostics.Select(x => x.SourceColumn), Is.EqualTo(new int?[] { null, 3 }));
        Assert.That(h.Status.Diagnostics.Select(x => x.Sequence), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(h.Status.Diagnostics.Select(x => x.ExecutionId).Distinct().Count(), Is.EqualTo(1));
        Assert.That(h.Session.NavigateDiagnostic(h.Status.Diagnostics[0]), Is.EqualTo(SourceNavigationResult.Success));
        Assert.That(h.Session.Editor.TextArea.Caret.Line, Is.EqualTo(3));
    });

    [Test] public Task ForeignBookCannotBorrowCurrentDocumentPosition() => Run(async h =>
    {
        Load(h, "unrelated"); await h.Session.ViewModel.ExecuteScriptBookAsync(["  @"], _ => { });
        Assert.That(h.Status.Diagnostics.Single().SourceLine, Is.Null);
    });

    [Test] public Task RevocationDeduplicationAndLateBatchStopAtOriginalDestination() => Run(h =>
    {
        var result = Result(Warning("one"), Warning("two"));
        h.Status.AppendExecution(result, null, null, () => false); Assert.That(h.Status.Diagnostics, Is.Empty);
        h.Status.AppendExecution(result, null, null, () => true); h.Status.AppendExecution(result, null, null, () => true);
        Assert.That(h.Status.Diagnostics.Count, Is.EqualTo(2));
        h.Status.ClearDiagnostics();
        bool current = true;
        NotifyCollectionChangedEventHandler clear = (_, e) => { if (e.Action == NotifyCollectionChangedAction.Add) current = false; };
        ((INotifyCollectionChanged)h.Status.Diagnostics).CollectionChanged += clear;
        h.Status.AppendExecution(Result(Warning(), Warning()), null, null, () => current);
        ((INotifyCollectionChanged)h.Status.Diagnostics).CollectionChanged -= clear;
        Assert.That(h.Status.Diagnostics.Count, Is.EqualTo(1));
        h.Status.ClearDiagnostics(); Assert.That(h.Status.VisibleDiagnostics, Is.Empty); return Task.CompletedTask;
    });

    [Test] public Task CancelledWorkRetainsEarlierDiagnosticsWhileClosed() => Run(async h =>
    {
        var started = new TaskCompletionSource(); using var stop = new CancellationTokenSource();
        h.Layout.Close(DockShellState.DiagnosticsId); h.Layout.Close(DockShellState.StatusId);
        var task = h.Session.ViewModel.ExecuteLineAsync("", cancellationToken: stop.Token,
            testStatements: [new AsyncInfrastructureStatement(async context =>
            {
                context.Diagnostics.Report(Warning()); new OutputOperations().Print(["partial"]);
                started.SetResult(); await Task.Delay(Timeout.Infinite, context.CancellationToken);
            })]);
        await started.Task; stop.Cancel(); await task;
        Assert.That(h.Status.StatusString, Does.Contain("cancelled"));
        Assert.That(h.Session.Output.Entries.Single().Text, Is.EqualTo("out: partial"));
        Assert.That(h.Status.Diagnostics.All(x => x.Category == DiagnosticCategory.SemanticWarning), Is.True);
        h.Show(DockShellState.DiagnosticsId); Assert.That(h.Status.HasDiagnostics, Is.True);
    });

    [Test] public Task BusyPendingObserverAndMcpLeaseBlockNavigationWithoutPublishingV2() => Run(async h =>
    {
        Load(h, "@"); await h.Session.ViewModel.ParseLineAsync("@"); var item = h.Status.Diagnostics.Single();
        h.Service.EditorCoordinator.Observe(() => Assert.That(h.Session.NavigateDiagnostic(item), Is.EqualTo(SourceNavigationResult.Busy)), propagate: true);
        using (h.Service.EditorCoordinator.TryBegin())
            Assert.That(h.Session.NavigateDiagnostic(item), Is.EqualTo(SourceNavigationResult.Busy));
        var started = new TaskCompletionSource(); var finish = new TaskCompletionSource();
        var task = h.Session.ViewModel.ExecuteLineAsync("", testStatements: [new AsyncInfrastructureStatement(async _ => { started.SetResult(); await finish.Task; })]);
        await started.Task; Assert.That(h.Session.NavigateDiagnostic(item), Is.EqualTo(SourceNavigationResult.Busy));
        finish.SetResult(); await task;
        h.Service.ActivateMcp(); var caller = h.Service.IssueCredential("g106", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var lease = (await h.Service.AcquireEditorAsync(caller, new("2", h.Service.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
        Assert.That(h.Session.NavigateDiagnostic(item), Is.EqualTo(SourceNavigationResult.Busy));
        h.Service.ExecutionConfirmation = (_, _, _) => Task.FromResult<bool?>(true);
        var state = (await h.Service.GetStatusV2Async(caller, new("2"), default)).Result!;
        var result = await h.Service.ExecuteEditorV2Async(caller, new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId, Guid.NewGuid(), snapshot.Revision, state.StateVersion), default);
        Assert.That(result.Error, Is.Null); Assert.That(result.Result!.State, Is.EqualTo(ExecutionState.SyntaxError));
        Assert.That(h.Status.Diagnostics.Single(), Is.SameAs(item));
        var changed = await h.Service.UpdateEditorV2Async(caller,
            new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId, snapshot.Revision, EditorEditMode.Append, " changed"), default);
        Assert.That(changed.Error, Is.Null);
        Assert.That(h.Status.Diagnostics.Single(), Is.SameAs(item));
        await h.Service.ReleaseEditorAsync(caller, new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId), default);
        Assert.That(h.Session.NavigateDiagnostic(item), Is.EqualTo(SourceNavigationResult.Stale));
    });

    [Test] public Task ShutdownRevokesLateDiagnosticsAndRetainsNoSuccessorPublication() => Run(async h =>
    {
        var started = new TaskCompletionSource(); var finish = new TaskCompletionSource();
        var top = h.Float(DockShellState.DiagnosticsId);
        var task = h.Session.ViewModel.ExecuteLineAsync("", testStatements: [new AsyncInfrastructureStatement(async _ =>
        { started.SetResult(); await finish.Task; throw new InvalidOperationException(); })]);
        await started.Task; h.Window.Close(); Assert.That(h.Service.IsStopping, Is.True);
        finish.SetResult(); await task; await DockEditorTests.Wait(() => !h.Window.IsVisible);
        Assert.That(h.Status.Diagnostics, Is.Empty); Assert.That(top.IsVisible, Is.False);
        Assert.That(h.Service.HasRegisteredEditor, Is.False);
    });
}
