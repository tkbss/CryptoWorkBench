using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoWorkBench.Automation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

public class B3F1WireTests
{
    public class ReplyProbe : DispatchProxy
    {
        internal int Calls;
        internal Func<object>? Reply;
        protected override object Invoke(MethodInfo? method, object?[]? arguments)
        {
            Calls++;
            Assert.That(arguments![0], Is.SameAs(FakeAutomationService.Caller));
            return Reply!();
        }
    }

    private readonly Guid id = Guid.NewGuid();
    private readonly HttpClient http = new();
    private TestListener host = null!;
    private ReplyProbe probe = null!;
    private int resolutions;
    private string Edit => AutomationJson.Serialize(new EditorEditV2("2", id, id, id, "0", EditorEditMode.Append, "public"));
    private string Execute => AutomationJson.Serialize(new EditorExecutionV2("2", id, id, id, id, "0", "0"));

    [SetUp]
    public async Task Open()
    {
        var service = DispatchProxy.Create<IWorkbenchAutomationV2, ReplyProbe>();
        probe = (ReplyProbe)(object)service;
        resolutions = 0;
        host = await TestListener.StartAsync(configureTools: services => services.AddWorkbenchV2(service, _ => { resolutions++; return FakeAutomationService.Caller; }));
    }

    [TearDown] public async Task Close() => await host.DisposeAsync();
    [OneTimeTearDown] public void DisposeClient() => http.Dispose();

    private async Task<JsonElement> Call(string tool, string arguments)
    {
        string body = $$"""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"workbench_{{tool}}","arguments":{{arguments}}} }""";
        return await Raw(body);
    }

    private async Task<JsonElement> Raw(string body, Uri? endpoint = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint ?? host.Endpoint) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Accept", "application/json, text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        using var response = await http.SendAsync(request);
        string text = await response.Content.ReadAsStringAsync();
        string json = text.StartsWith("event:", StringComparison.Ordinal)
            ? text.Split('\n').Single(line => line.StartsWith("data: ", StringComparison.Ordinal))[6..] : text;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [TestCase("contractVersion"), TestCase("editorId"), TestCase("leaseId"), TestCase("text")]
    public async Task RawDuplicateNeverReachesResolverEvenIfLastValueIsValid(string field)
    {
        string first = JsonNode.Parse(Edit)![field]!.ToJsonString();
        string second = JsonSerializer.Serialize(field switch { "contractVersion" => "1", "text" => "second", _ => Guid.NewGuid().ToString() });
        string key = JsonSerializer.Serialize(field) + ":";
        foreach (var (left, right) in new[] { (first, second), (second, first) })
        {
            string raw = Edit.Replace(key + first, key + left + "," + key + right, StringComparison.Ordinal);
            var envelope = await Call("update_editor", raw);
            Assert.That(envelope.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32602));
        }
        Assert.That((resolutions, probe.Calls), Is.EqualTo((0, 0)));
        await AppliedMutation();
    }

    [TestCase(false), TestCase(true)]
    public async Task StableMigrationErrorsAreDistinctFromMalformedInput(bool execute)
    {
        var args = JsonNode.Parse(execute ? Execute : Edit)!.AsObject();
        string tool = execute ? "execute_editor" : "update_editor";
        args.Remove("leaseId");
        async Task Reason(string expected)
        {
            var response = await Call(tool, args.ToJsonString());
            var error = response.GetProperty("error");
            Assert.That(error.GetProperty("message").GetString(), Is.EqualTo(expected));
            Assert.That(error.GetProperty("code").GetInt32(), Is.EqualTo(-32602));
        }
        await Reason("LeaseRequired");
        args["expectedRevision"] = "00";
        await Reason("Invalid v2 arguments.");
        args["expectedRevision"] = "0";
        args["editorId"] = Guid.Empty.ToString();
        await Reason("Invalid v2 arguments.");
        args["editorId"] = "invalid-uuid";
        await Reason("Invalid v2 arguments.");
        args["editorId"] = id.ToString();
        args.Remove("contractVersion");
        await Reason("UnsupportedContractVersion");
        args["contractVersion"] = "1";
        await Reason("UnsupportedContractVersion");
        args["editorId"] = "sensitive-malformed-marker";
        await Reason("Invalid v2 arguments.");
        Assert.That((resolutions, probe.Calls), Is.EqualTo((0, 0)));
    }

    [Test]
    public async Task AmbiguousEnvelopeNeverSelectsATool()
    {
        const string status = """{"name":"workbench_get_status","arguments":{"contractVersion":"2"}}""";
        var edit = $$"""{"name":"workbench_update_editor","arguments":{{Edit}}}""";
        const string head = """{"jsonrpc":"2.0","id":1,"method":"tools/call",""";
        foreach (var (left, right) in new[] { (status, edit), (edit, status) })
        {
            var body = head + "\"params\":" + left + ",\"p\\u0061rams\":" + right + "}";
            Assert.That((await Raw(body)).GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32600));
        }
        foreach (var names in new[] { "\"name\":\"workbench_get_status\",\"name\":\"workbench_update_editor\"",
                                      "\"name\":\"workbench_update_editor\",\"name\":\"workbench_get_status\"" })
            Assert.That((await Raw(head + "\"params\":{" + names + ",\"arguments\":" + Edit + "}}")).GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32600));
        foreach (var (first, second) in new[] { ("tools/list", "tools/call"), ("tools/call", "tools/list") })
            Assert.That((await Raw(head.Replace("tools/call", first) + "\"m\\u0065thod\":\"" + second + "\",\"params\":" + edit + "}"))
                .GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32600));
        Assert.That((resolutions, probe.Calls), Is.EqualTo((0, 0)));
        probe.Reply = () => Task.FromResult(new EditorMutationReceiptV2("2", id, id, "0", "1", TextEffect.Applied,
            HistoryEffect.Applied, Finalization.Complete, null, RetryPolicy.NoAutomaticRetry, false));
        var good = edit.Insert(1, "\"_meta\":{\"a\":{\"label\":1},\"b\":{\"label\":2}},");
        Assert.That((await Raw(head + "\"params\":" + good + "}")).TryGetProperty("result", out _), Is.True);
        Assert.That((resolutions, probe.Calls), Is.EqualTo((1, 1)));
    }

    [TestCase(true, false), TestCase(false, true), TestCase(false, false)]
    public async Task IncompleteHostRegistrationNeverResolvesCaller(bool guarded, bool supported)
    {
        var service = DispatchProxy.Create<IWorkbenchAutomationV2, ReplyProbe>();
        int resolved = 0;
        // Deliberately reuse an authentic SDK primitive captured on another, validated POST.
        ModelContextProtocol.Server.IMcpServerPrimitive? captured = null;
        await using (var origin = await TestListener.StartAsync(configureTools: services => services.AddWorkbenchV2(service, request =>
        {
            resolved++;
            captured = request.MatchedPrimitive;
            return FakeAutomationService.Caller;
        })))
        {
            ((ReplyProbe)(object)service).Reply = () => Task.FromResult(new ResultV2<ControlStatus>("2", id, null, SafeError.From(AutomationErrorCode.Forbidden)));
            await Raw("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"workbench_get_status","arguments":{"contractVersion":"2"}}}""", origin.Endpoint);
        }
        var replayed = (ModelContextProtocol.Server.McpServerTool)captured!;
        Assert.That(resolved, Is.EqualTo(1));
        resolved = 0;
        ((ReplyProbe)(object)service).Calls = 0;
        AutomationCaller Resolve(ModelContextProtocol.Server.RequestContext<ModelContextProtocol.Protocol.CallToolRequestParams> _)
        { resolved++; return FakeAutomationService.Caller; }
        await using var partial = await TestListener.StartAsync(normalizeParseErrors: guarded, configureTools: services =>
        {
            if (supported) services.AddWorkbenchV2(service, Resolve);
            else services.AddMcpServer().WithHttpTransport(o => o.SessionMode = ModelContextProtocol.AspNetCore.HttpServerSessionMode.Stateless)
                .WithTools([replayed]);
        });
        const string head = """{"jsonrpc":"2.0","id":1,"method":"tools/call",""";
        const string status = "\"params\":" + """{"name":"workbench_get_status","arguments":{"contractVersion":"2"}}""";
        foreach (var fields in new[] { status, status + "," + status })
        {
            var response = await Raw(head + fields + "}", partial.Endpoint);
            Assert.That(response.TryGetProperty("error", out _), Is.True);
        }
        Assert.That((resolved, ((ReplyProbe)(object)service).Calls), Is.EqualTo((0, 0)));
        await AppliedMutation();
    }

    [Test]
    public async Task InvalidEnvelopeTakesPrecedenceOverDuplicateArguments()
    {
        const string fields = """{"name":"workbench_get_status","arguments":{"contractVersion":"2","contractVersion":"2"}}""";
        foreach (string prefix in new[] { """{"id":7,"method":"tools/call",""", """{"jsonrpc":"2.0","id":7,""",
            """{"jsonrpc":"2.0","method":"tools/call",""", """{"jsonrpc":"2.0","method":"tools/call","id":null,""",
            """{"jsonrpc":"2.0","method":"tools/call","id":{},""", """{"jsonrpc":"2.0","method":"tools/call","id":0.5,""",
            """{"jsonrpc":"2.0","method":"ping","id":1,""", """{"jsonrpc":"2.0","method":"tools/call","id":1,"id":"two",""" })
        {
            var response = await Raw(prefix + "\"params\":" + fields + "}");
            Assert.That(response.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32600));
            Assert.That(response.GetProperty("id").ValueKind, Is.EqualTo(JsonValueKind.Null));
        }
        var valid = await Raw("""{"jsonrpc":"2.0","method":"tools/call","id":"seven","params":""" + fields + "}");
        Assert.That(valid.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32602));
        Assert.That(valid.GetProperty("id").GetString(), Is.EqualTo("seven"));
        Assert.That((resolutions, probe.Calls), Is.EqualTo((0, 0)));
        await AppliedMutation();
    }

    [Test]
    public async Task ExecuteDiscoveryExposesDesktopFileEffects()
    {
        await using var transport = new HttpClientTransport(new() { Endpoint = host.Endpoint, TransportMode = HttpTransportMode.StreamableHttp });
        await using var client = await McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();
        var execute = tools.Single(t => t.Name == "workbench_execute_editor").ProtocolTool.Annotations!;
        Assert.That((execute.OpenWorldHint, execute.IdempotentHint, execute.ReadOnlyHint, execute.DestructiveHint),
            Is.EqualTo((true, false, false, true)));
        Assert.That(tools.Where(t => t.Name != "workbench_execute_editor").All(t => t.ProtocolTool.Annotations!.OpenWorldHint == false), Is.True);
    }

    [TestCase(ExecutionState.Rejected), TestCase(ExecutionState.Failed), TestCase(ExecutionState.Cancelled), TestCase(ExecutionState.Completed)]
    public async Task ExecutionStateOnlyMarksTheExecutionCall(ExecutionState state)
    {
        var result = FakeAutomationService.Completed(id) with { State = state, HasErrors = true,
            Diagnostics = [new(DiagnosticKind.Semantic, DiagnosticSeverity.Error, "Check", "Check failed.", null, null, 1, null, null, null, null, null)] };
        probe.Reply = () => Task.FromResult(new ResultV2<ExecutionResult>("2", id, result, null));
        foreach (bool run in new[] { true, false })
        {
            var wire = (await Call(run ? "execute_editor" : "get_execution", run ? Execute :
                AutomationJson.Serialize(new JobRequestV2("2", id, id)))).GetProperty("result");
            Assert.That(wire.TryGetProperty("isError", out var flag) && flag.GetBoolean(), Is.EqualTo(run && state != ExecutionState.Completed));
            var data = AutomationJson.Deserialize<ResultV2<ExecutionResult>>(wire.GetProperty("structuredContent").GetRawText());
            Assert.That((data.Error, data.Result!.State, data.Result.HasErrors), Is.EqualTo(((SafeError?)null, state, true)));
        }
    }

    [TestCase(AutomationErrorCode.Forbidden), TestCase(AutomationErrorCode.NotFound)]
    public async Task DeniedLookupKeepsStructuredError(AutomationErrorCode code)
    {
        probe.Reply = () => Task.FromResult(new ResultV2<ExecutionResult>("2", id, null, SafeError.From(code)));
        var wire = (await Call("get_execution", AutomationJson.Serialize(new JobRequestV2("2", id, id)))).GetProperty("result");
        Assert.That(wire.GetProperty("isError").GetBoolean(), Is.True);
        var reply = AutomationJson.Deserialize<ResultV2<ExecutionResult>>(wire.GetProperty("structuredContent").GetRawText());
        Assert.That((reply.Result, reply.Error!.Code), Is.EqualTo(((ExecutionResult?)null, code)));
    }

    [Test]
    public async Task AppliedMutation()
    {
        var receipt = new EditorMutationReceiptV2("2", id, id, "0", "1", TextEffect.Applied, HistoryEffect.Applied,
            Finalization.Complete, SafeError.From(AutomationErrorCode.CallbackFailed), RetryPolicy.NoAutomaticRetry, true);
        probe.Reply = () => Task.FromResult(receipt);
        var wire = (await Call("update_editor", Edit)).GetProperty("result");
        Assert.That(wire.GetProperty("isError").GetBoolean(), Is.True);
        Assert.That(wire.GetProperty("content")[0].GetProperty("text").GetString(),
            Is.EqualTo("Editor operation failed after an applied change; refresh state."));
        Assert.That(AutomationJson.Deserialize<EditorMutationReceiptV2>(wire.GetProperty("structuredContent").GetRawText()), Is.EqualTo(receipt));
        Assert.That((resolutions, probe.Calls), Is.EqualTo((1, 1)));
    }
}
