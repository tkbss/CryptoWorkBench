using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockAssignmentIntegrationTests
{
    private static Task Run(Func<DockToolTests.Host, Task> test) => GuiTestSession.Session.Dispatch(async () =>
    {
        var dictionary = VariableDictionary.Instance();
        var previous = dictionary.GetVariables().ToArray();
        try
        {
            await using var host = new DockToolTests.Host();
            await test(host);
        }
        finally
        {
            dictionary.Clear();
            foreach (var variable in previous) dictionary.Add(variable);
        }
        Assert.That(dictionary.GetVariables(), Is.EquivalentTo(previous));
        return true;
    }, CancellationToken.None);

    private static void Load(DockToolTests.Host h, string script) =>
        Assert.That(h.Session.LoadText(h.Service.CaptureSaveIntent()!, script), Is.True);

    [TestCase(false)] [TestCase(true)]
    public Task AssignmentPublishesAtomicReplacementAndNavigationWithoutUiReplay(bool floating) => Run(async h =>
    {
        if (floating)
        {
            h.Float(DockShellState.DocumentId);
            h.Float(DockShellState.VariablesId);
        }
        await h.Session.ViewModel.ParseLineAsync("VAR integrated=0x(11)");
        var dictionary = VariableDictionary.Instance();
        var original = dictionary.Get("integrated");
        const string script = "integrated=0x(22)\nPrint(integrated)";
        Load(h, script);
        await h.Session.ViewModel.ParseLineAsync(script);
        var replacement = dictionary.Get("integrated");
        Assert.That(replacement, Is.Not.SameAs(original));
        Assert.That(original.Value, Is.EqualTo("0x(11)"));
        Assert.That(replacement.Value, Is.EqualTo("0x(22)"));
        Assert.That(h.Variables.DataVariables.Single(v => v.Identifier == "integrated").Value, Is.EqualTo("0x(22)"));
        var output = h.Session.Output.Entries.Single();
        Assert.That(output.Text, Is.EqualTo("out: 0x(22)"));
        Assert.That(output.SourceLine, Is.EqualTo(2));
        Assert.That(h.Session.NavigateOutput(output), Is.True);
        Assert.That(h.Session.Editor.CaretOffset, Is.EqualTo(script.IndexOf("Print", StringComparison.Ordinal)));

        const string invalid = "integrated=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F))";
        Load(h, invalid);
        await h.Session.ViewModel.ParseLineAsync(invalid);
        Assert.That(dictionary.Get("integrated"), Is.SameAs(replacement));
        var diagnostic = h.Status.Diagnostics.Single(d => d.Text.Contains("AssignmentTypeMismatch", StringComparison.Ordinal));
        // Direct submissions have no reliable semantic token position. Preserve the
        // existing Unknown contract instead of inventing a navigable source line.
        Assert.That(diagnostic.IsError, Is.True);
        Assert.That(diagnostic.SourceLine, Is.Null);
        Assert.That(h.Session.NavigateDiagnostic(diagnostic), Is.EqualTo(SourceNavigationResult.Unknown));
        Assert.That(h.Variables.DataVariables.Single(v => v.Identifier == "integrated").Value, Is.EqualTo("0x(22)"));

        var revision = InterpreterHostQueue.StateRevision;
        var editor = h.Session.Editor;
        editor.Document.Insert(0, " ");
        editor.Undo(); Assert.That(editor.Text, Is.EqualTo(invalid));
        editor.Redo(); Assert.That(editor.Text, Is.EqualTo(" " + invalid));
        const string history = "integrated=0x(33)";
        h.History.AddLine(history);
        h.History.AddLine(invalid);
        h.History.LineBack();
        Assert.That(editor.Text, Is.EqualTo(history));
        h.History.LineForward();
        Assert.That(editor.Text, Is.EqualTo(invalid));
        if (floating) { h.Layout.Dock(DockShellState.DocumentId); h.Layout.Dock(DockShellState.VariablesId); h.Pump(); }
        Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
        Assert.That(dictionary.Get("integrated"), Is.SameAs(replacement));
    });

    [TestCase(false)] [TestCase(true)]
    public Task DockMcpAssignmentsPreserveLeasePublicationAndPartialFailure(bool fail) => Run(async h =>
    {
        string script = "VAR integratedMcp=0x(11) integratedMcp=0x(22) Print(integratedMcp)";
        if (fail) script += " integratedMcp=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F))";
        Load(h, script);
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("g2-i01", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var lease = (await h.Service.AcquireEditorAsync(caller,
            new("2", h.Service.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
        h.Service.ExecutionConfirmation = (_, _, _) => Task.FromResult<bool?>(true);
        var state = (await h.Service.GetStatusV2Async(caller, new("2"), default)).Result!;
        var request = new EditorExecutionV2("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId,
            Guid.NewGuid(), snapshot.Revision, state.StateVersion);
        var reply = await h.Service.ExecuteEditorV2Async(caller, request, default);
        Assert.That(reply.Error, Is.Null);
        Assert.That(reply.Result!.State, Is.EqualTo(fail ? ExecutionState.Aborted : ExecutionState.Completed));
        Assert.That(VariableDictionary.Instance().Get("integratedMcp").Value, Is.EqualTo("0x(22)"));
        if (fail) Assert.That(AutomationJson.Serialize(reply.Result), Does.Contain("AssignmentTypeMismatch"));
        Assert.That(reply.Result.Variables.Items.Single(v => v.Identifier == "integratedMcp").ValueIncluded, Is.False);
        Assert.That((await h.Service.ExecuteEditorV2Async(caller, request, default)).Result!.ExecutionId, Is.EqualTo(reply.Result.ExecutionId));
        Assert.That(h.Session.Output.Entries, Is.Empty);
        var rows = h.Variables.DataVariables.ToArray();
        h.Variables.RefreshCommand.Execute();
        Assert.That(h.Variables.IsSnapshotStale, Is.True);
        Assert.That(h.Variables.DataVariables, Is.EqualTo(rows));
        Assert.That((await h.Service.ReleaseEditorAsync(caller,
            new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId), default)).Error, Is.Null);
        h.Variables.RefreshCommand.Execute();
        Assert.That(h.Variables.IsSnapshotStale, Is.False);
        Assert.That(h.Variables.DataVariables.Single(v => v.Identifier == "integratedMcp").Value, Is.EqualTo("0x(22)"));
        Load(h, "Print(integratedMcp)");
        await h.Session.ViewModel.ParseLineAsync(h.Session.Editor.Text);
        var output = h.Session.Output.Entries.Single();
        Assert.That(output.Text, Is.EqualTo("out: 0x(22)"));
        Assert.That(h.Session.NavigateOutput(output), Is.True);
    });
}
