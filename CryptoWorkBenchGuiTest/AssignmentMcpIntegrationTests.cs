using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class AssignmentMcpIntegrationTests
{
    [TestCase(false)] [TestCase(true)]
    public Task AssignmentUsesAuthenticatedMcpLeaseAndSharedRunner(bool fail) => Ui(async () =>
    {
        var dictionary = VariableDictionary.Instance();
        var previous = dictionary.GetVariables().ToArray();
        try
        {
            string script = "VAR assignmentMcp=0x(11) assignmentMcp=0x(22)";
            if (fail) script += " assignmentMcp=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F))";
            await using var h = new Host(script);
            h.Service.ActivateMcp();
            var caller = h.Service.IssueCredential("assignment", WorkbenchAutomationService.LocalCaller.Permissions
                .Add(AutomationPermissions.EditorControl));
            var snapshot = h.Registration.Snapshot();
            var state = (await h.Service.GetStatusV2Async(caller, new("2"), default)).Result!;
            var lease = (await h.Service.AcquireEditorAsync(caller,
                new("2", h.Service.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
            h.Service.ExecutionConfirmation = (_, approval, _) =>
                Task.FromResult<bool?>(approval.Snapshot.Text == script);
            var request = new EditorExecutionV2("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId,
                Guid.NewGuid(), snapshot.Revision, state.StateVersion);
            var reply = await h.Service.ExecuteEditorV2Async(caller, request, default);
            Assert.That(reply.Error, Is.Null);
            var result = reply.Result!;
            Assert.That(result.State, Is.EqualTo(fail ? ExecutionState.Aborted : ExecutionState.Completed));
            Assert.That(dictionary.Get("assignmentMcp").Value, Is.EqualTo("0x(22)"));
            Assert.That(result.Variables.Items.Single(v => v.Identifier == "assignmentMcp").ValueIncluded, Is.False);
            var json = AutomationJson.Serialize(result);
            Assert.That(json, Does.Not.Contain("000102030405060708090A0B0C0D0E0F"));
            if (fail) Assert.That(json, Does.Contain("AssignmentTypeMismatch"));
            Assert.That((await h.Service.ExecuteEditorV2Async(caller, request, default)).Result!.ExecutionId,
                Is.EqualTo(result.ExecutionId));
            Assert.That((await h.Service.ReleaseEditorAsync(caller,
                new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId), default)).Error, Is.Null);
            Assert.That(h.Owner.CanStartGui, Is.True);
        }
        finally
        {
            dictionary.Clear();
            foreach (var variable in previous) dictionary.Add(variable);
        }
    });
}
