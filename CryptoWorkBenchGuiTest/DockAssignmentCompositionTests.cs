using Avalonia.Headless;
using Avalonia.Input;
using AvaloniaEdit;
using System.Reflection;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockAssignmentCompositionTests
{
    private static Task Run(Func<DockToolTests.Host, Task> test) => GuiTestSession.Session.Dispatch(async () =>
    { await using var host = new DockToolTests.Host(); await test(host); return true; }, CancellationToken.None);

    private static void Load(DockToolTests.Host h, string text) =>
        Assert.That(h.Session.LoadText(h.Service.CaptureSaveIntent()!, text), Is.True);

    private static string[] History(DockToolTests.Host h) =>
        ((IEnumerable<string>)typeof(HistoryService).GetField("_lineHistory",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(h.History)!).ToArray();

    [TestCase(false)]
    [TestCase(true)]
    public Task EnterAssignmentKeepsDockIdentityHistoryAndPrintSource(bool floating) => Run(async h =>
    {
        const string script = "VAR composed=0x(11) composed=0x(22) Print(composed)";
        Load(h, script);
        var editor = h.Session.Editor; var document = editor.Document; var undo = document.UndoStack;
        var variables = h.Presentation.Variables!.DataContext;
        var editorId = h.Service.CaptureSaveIntent()!.Snapshot.EditorId;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
        if (floating) h.Float(DockShellState.DocumentId);
        editor.CaretOffset = editor.Text.Length; editor.TextArea.Focus();
        editor.TextArea.PerformTextInput("\n");
        await h.Session.View.PendingExecution;
        Assert.That(editor.Text, Is.EqualTo(script + Environment.NewLine));
        Assert.That(VariableDictionary.Instance().Get("composed").Value, Is.EqualTo("0x(22)"));
        Assert.That(h.Variables.DataVariables.Single(v => v.Identifier == "composed").Value, Is.EqualTo("0x(22)"));
        var output = h.Session.Output.Entries.Single();
        Assert.That(output.Text, Is.EqualTo("out: 0x(22)"));
        Assert.That(output.SourceLine, Is.EqualTo(1));
        Assert.That(output.SourceColumn, Is.EqualTo(37));
        Assert.That(h.Session.NavigateOutput(output), Is.True);
        Assert.That(editor.CaretOffset, Is.EqualTo(36));
        Assert.That(History(h), Is.EqualTo(new[] { script }));
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var stateRevision = InterpreterHostQueue.StateRevision;
        h.Float(DockShellState.VariablesId); h.Layout.Dock(DockShellState.VariablesId);
        h.Layout.Dock(DockShellState.DocumentId); h.Pump();
        Assert.That(h.Presentation.Variables.DataContext, Is.SameAs(variables));
        Assert.That(h.Session.Editor, Is.SameAs(editor)); Assert.That(editor.Document, Is.SameAs(document));
        Assert.That(document.UndoStack, Is.SameAs(undo));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
        Assert.That(snapshot.EditorId, Is.EqualTo(editorId));
        Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(stateRevision));
        ApplicationCommands.Undo.Execute(null, editor.TextArea);
        Assert.That(editor.Text, Is.EqualTo(script));
        ApplicationCommands.Redo.Execute(null, editor.TextArea);
        Assert.That(editor.Text, Is.EqualTo(script + Environment.NewLine));
        Assert.That(History(h), Is.EqualTo(new[] { script })); Assert.That(h.Session.Output.Entries.Count, Is.EqualTo(1));
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(stateRevision));
    });

    [TestCase(false, 12)]
    [TestCase(true, 28)]
    public Task AssignmentPrintHasParserSourceButCannotPublishOrMutate(bool nested, int column) => Run(async h =>
    {
        string expression = nested ? "Compare(0x(11), Print(\"secret-marker\"))" : "Print(\"secret-marker\")";
        string script = "\n  target = " + expression + " VAR unreachable=0x(FF)";
        var parser = ParserBuilder.StringBuild(script);
        var assignment = (AssignmentStatementNode)AntlrToStatement.Map(parser.program().statement(0))!;
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        var root = (FunctionCallExpressionNode)assignment.Initializer!;
        var print = nested ? ((NestedCallArgumentNode)root.Arguments[1]).Call : root;
        Assert.That(print.Name, Is.EqualTo("Print"));
        Assert.That(print.Source, Is.EqualTo(new SourcePosition(2, column)));
        await h.Session.ViewModel.ParseLineAsync("VAR target=0x(11)");
        var original = VariableDictionary.Instance().Get("target");
        Load(h, script); h.Float(DockShellState.DocumentId);
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        await h.Session.ViewModel.ParseLineAsync(script);
        Assert.That(VariableDictionary.Instance().Get("target"), Is.SameAs(original));
        Assert.That(original.Value, Is.EqualTo("0x(11)"));
        Assert.That(VariableDictionary.Instance().Get("unreachable"), Is.Null);
        Assert.That(h.Session.Output.Entries, Is.Empty);
        var diagnostic = h.Status.Diagnostics.Single();
        Assert.That(diagnostic.Category, Is.EqualTo(DiagnosticCategory.SemanticError));
        Assert.That(diagnostic.Text, Does.Contain("MissingReturnValue").And.Not.Contain("secret-marker"));
        // L1 records statement identity, not token coordinates; G1 must not invent a navigation target.
        Assert.That(diagnostic.SourceLine, Is.Null); Assert.That(diagnostic.SourceColumn, Is.Null);
        Assert.That(h.Session.NavigateDiagnostic(diagnostic), Is.EqualTo(SourceNavigationResult.Unknown));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
    });

    [Test]
    public Task DockAssignmentPreservesS01ReportContinuationAndThrowAbort() => Run(async h =>
    {
        const string script = "KEY missing=Check(0x(AA)) VAR continued=0x(11) continued=0x(22) Print(continued)";
        Load(h, script); await h.Session.ViewModel.ParseLineAsync(script);
        Assert.That(VariableDictionary.Instance().Get("continued").Value, Is.EqualTo("0x(22)"));
        Assert.That(h.Session.Output.Entries.Single().Text, Is.EqualTo("out: 0x(22)"));
        Assert.That(h.Status.Diagnostics.Single().Text, Does.Contain("KeyCheckValueNotFound"));
        Assert.That(h.Status.StatusString, Is.EqualTo("Line successfull parsed"));
        var original = VariableDictionary.Instance().Get("continued");
        h.Status.ClearDiagnostics();
        const string failed = "continued=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F)) VAR unreachable=0x(FF)";
        Load(h, failed); await h.Session.ViewModel.ParseLineAsync(failed);
        Assert.That(VariableDictionary.Instance().Get("continued"), Is.SameAs(original));
        Assert.That(original.Value, Is.EqualTo("0x(22)"));
        Assert.That(VariableDictionary.Instance().Get("unreachable"), Is.Null);
        var diagnostic = h.Status.Diagnostics.Single();
        Assert.That(diagnostic.Category, Is.EqualTo(DiagnosticCategory.SemanticError));
        Assert.That(diagnostic.Text, Does.Contain("AssignmentTypeMismatch").And.Not.Contain("000102030405060708090A0B0C0D0E0F"));
        Assert.That(h.Status.StatusString, Does.Contain("aborted"));
        Assert.That(h.Session.Output.Entries.Count, Is.EqualTo(1));
    });

    [TestCase(false)]
    [TestCase(true)]
    public Task AssignmentMcpLeaseUsesSameDockEditorAndNeverReplaysOnRedock(bool fail) => Run(async h =>
    {
        string script = "VAR leased=0x(11) leased=0x(22)";
        if (fail) script += " leased=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F))";
        Load(h, script); h.Float(DockShellState.DocumentId);
        var editor = h.Session.Editor; var document = editor.Document; var undo = document.UndoStack;
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("g1-i01", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var lease = (await h.Service.AcquireEditorAsync(caller,
            new("2", h.Service.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
        h.Service.ExecutionConfirmation = (_, approval, _) => Task.FromResult<bool?>(approval.Snapshot.Text == script);
        var state = (await h.Service.GetStatusV2Async(caller, new("2"), default)).Result!;
        var request = new EditorExecutionV2("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId,
            Guid.NewGuid(), snapshot.Revision, state.StateVersion);
        var reply = await h.Service.ExecuteEditorV2Async(caller, request, default);
        Assert.That(reply.Error, Is.Null);
        Assert.That(reply.Result!.State, Is.EqualTo(fail ? ExecutionState.Aborted : ExecutionState.Completed));
        Assert.That(VariableDictionary.Instance().Get("leased").Value, Is.EqualTo("0x(22)"));
        Assert.That(reply.Result.Variables.Items.Single(v => v.Identifier == "leased").ValueIncluded, Is.False);
        Assert.That(AutomationJson.Serialize(reply.Result), Does.Not.Contain("000102030405060708090A0B0C0D0E0F"));
        Assert.That((await h.Service.ExecuteEditorV2Async(caller, request, default)).Result!.ExecutionId, Is.EqualTo(reply.Result.ExecutionId));
        var root = h.Layout.Factory.FindRoot(h.Layout.Pane(DockShellState.DocumentId));
        h.Layout.Dock(DockShellState.DocumentId); h.Layout.Float(DockShellState.OutputId);
        Assert.That(h.Layout.Factory.FindRoot(h.Layout.Pane(DockShellState.DocumentId)), Is.SameAs(root));
        Assert.That(h.Layout.Root.Windows, Has.Count.EqualTo(1));
        Assert.That(h.Session.Output.Entries, Is.Empty);
        Assert.That((await h.Service.ReleaseEditorAsync(caller,
            new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId), default)).Error, Is.Null);
        // MCP activation/acquire/release advance epochs; only the following docking must preserve it.
        var releasedGeneration = h.Service.EditorCoordinator.WorkspaceGeneration;
        Assert.That(releasedGeneration, Is.GreaterThan(generation));
        var revision = InterpreterHostQueue.StateRevision;
        h.Layout.Dock(DockShellState.DocumentId); h.Pump();
        Assert.That(h.Layout.Factory.FindRoot(h.Layout.Pane(DockShellState.DocumentId)), Is.SameAs(h.Layout.Root));
        Assert.That(h.Session.Editor, Is.SameAs(editor)); Assert.That(editor.Document, Is.SameAs(document));
        Assert.That(document.UndoStack, Is.SameAs(undo));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
        Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(releasedGeneration));
        Assert.That(h.Service.EditorCoordinator.Targets(editor), Is.True);
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
    });
}
