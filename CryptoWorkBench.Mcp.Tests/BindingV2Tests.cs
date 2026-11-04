using System.Reflection;
using System.Text.Json;
using CryptoWorkBench.Automation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

[TestFixture]
public class BindingV2Tests
{
    public class Probe : DispatchProxy
    {
        internal int Calls;
        protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls++;
            Assert.That(args![0], Is.SameAs(FakeAutomationService.Caller));
            Assert.That(targetMethod!.Name, Is.EqualTo(nameof(IWorkbenchAutomationV2.GetStatusV2Async)));
            return Task.FromResult(new ResultV2<ControlStatus>("2", Guid.NewGuid(), null, SafeError.From(AutomationErrorCode.Forbidden)));
        }
    }

    [Test]
    public async Task ToolDiscoveryAndStrictRejectionPrecedeTrustedDispatch()
    {
        var service = DispatchProxy.Create<IWorkbenchAutomationV2, Probe>();
        int authenticated = 0;
        await using var host = await TestListener.StartAsync(configureTools: services => services.AddWorkbenchV2(service, _ => { authenticated++; return FakeAutomationService.Caller; }));
        await using var transport = new HttpClientTransport(new() { Endpoint = host.Endpoint, TransportMode = HttpTransportMode.StreamableHttp });
        await using var client = await McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();
        Assert.That(tools, Has.Count.EqualTo(11));
        foreach (var tool in tools)
        {
            Assert.That(tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("contractVersion").GetProperty("const").GetString(), Is.EqualTo("2"));
            Assert.That(tool.ProtocolTool.OutputSchema!.Value.GetProperty("additionalProperties").GetBoolean(), Is.False);
            foreach (var args in new Dictionary<string, object?>[] { new(), new() { ["contractVersion"] = "1" },
                new() { ["contractVersion"] = "2", ["activationEpoch"] = "untrusted-marker" } })
                Assert.ThrowsAsync<McpProtocolException>(async () => await client.CallToolAsync(tool.Name, args));
        }
        Assert.That(authenticated, Is.Zero);
        var valid = await client.CallToolAsync("workbench_get_status", new Dictionary<string, object?> { ["contractVersion"] = "2" });
        Assert.That(valid.IsError, Is.True);
        Assert.That(AutomationJson.Deserialize<ResultV2<ControlStatus>>(valid.StructuredContent!.Value.GetRawText()).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        Assert.That(((Probe)(object)service).Calls, Is.EqualTo(1));
        Assert.That(string.Join("\n", host.Logs.Entries), Does.Not.Contain("untrusted-marker"));
    }

    [TestCase("01")] [TestCase("18446744073709551616")] [TestCase("-1")] [TestCase("0")]
    public void LeaseVersionsArePositiveCanonicalUInt64(string version) =>
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(new RenewEditorV2("2", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), version)));
}
