using System.Net;
using System.Text;
using System.Text.Json;
using CryptoWorkBench.Automation.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

[TestFixture]
public class TransportTests
{
    [TestCase("2026-07-28")]
    [TestCase("2025-11-25")]
    public async Task DiscoveryAndStatus(string revision)
    {
        await using var host = await TestListener.StartAsync();
        await using var transport = new HttpClientTransport(new() { Endpoint = host.Endpoint, TransportMode = HttpTransportMode.StreamableHttp });
        await using var client = await McpClient.CreateAsync(transport, new() { ProtocolVersion = revision });
        TestContext.Out.WriteLine($"Revision {revision}; server info: {client.ServerInfo?.Name ?? "<absent>"}");
        var tools = await client.ListToolsAsync();
        Assert.That(client.NegotiatedProtocolVersion, Is.EqualTo(revision));
        Assert.That(tools.Select(t => t.Name), Is.EqualTo(new[] { "workbench_get_status" }));
        var result = await client.CallToolAsync("workbench_get_status");
        Assert.That(result.IsError, Is.Not.True);
        var json = result.StructuredContent!.Value.GetRawText();
        var reply = AutomationJson.Deserialize<Reply<WorkbenchStatus>>(json);
        Assert.That(reply.Data!.StateVersion, Is.EqualTo("9007199254740993"));
        Assert.That(reply.InstanceId, Is.EqualTo(host.Service.InstanceId));
        Assert.That(host.Service.LastCaller, Is.SameAs(FakeAutomationService.Caller));
        Assert.That(((TextContentBlock)result.Content.Single()).Text, Is.EqualTo(json));
        Assert.That(tools.Single().ProtocolTool.OutputSchema!.Value.GetProperty("oneOf").GetArrayLength(), Is.EqualTo(2));
    }

    private static HttpRequestMessage Request(Uri endpoint, string body, string revision = "2025-11-25")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Accept", "application/json, text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", revision);
        return request;
    }
    [TestCase("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"no_such_method\"}", "-32601")]
    [TestCase("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"workbench_get_status\",\"arguments\":{\"caller\":\"secret-marker\"}}}", "-32602")]
    public async Task ProtocolErrors(string body, string code)
    {
        await using var host = await TestListener.StartAsync();
        using var http = new HttpClient();
        using var request = Request(host.Endpoint, body);
        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.That(text, Does.Contain(code));
        Assert.That(text, Does.Not.Contain("secret-marker"));
        Assert.That(string.Join("\n", host.Logs.Entries), Does.Not.Contain("secret-marker"));
        Assert.That(host.Service.StatusCalls, Is.Zero);
    }
    [TestCase("{\"secret-marker\":")]
    [TestCase("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":{\"secret-marker\":true}}")]
    [TestCase("{\"jsonrpc\":\"secret-marker\",\"id\":1,\"method\":\"tools/list\"}")]
    [TestCase("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":{\"secret-marker\":invalid}}")]
    public async Task SafeParseErrors(string body)
    {
        await using var host = await TestListener.StartAsync();
        using var http = new HttpClient();
        using var request = Request(host.Endpoint, body);
        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.That(text, Does.Contain("Invalid JSON-RPC request.").And.Not.Contain("Path:").And.Not.Contain("invalid start"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(text, Does.Not.Contain("secret-marker"));
        Assert.That(string.Join("\n", host.Logs.Entries), Does.Not.Contain("secret-marker"));
    }
    [Test]
    public async Task StatelessHasNoLegacyListenerOrSession()
    {
        await using var host = await TestListener.StartAsync();
        using var http = new HttpClient();
        using var get = await http.GetAsync(host.Endpoint);
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
        using var delete = await http.DeleteAsync(host.Endpoint);
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
        using var sse = await http.GetAsync(new Uri(host.Endpoint, "/sse"));
        Assert.That(sse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var request = Request(host.Endpoint, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""");
        using var response = await http.SendAsync(request);
        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(response.Headers.Contains("Mcp-Session-Id"), Is.False);
    }
    [TestCase("2025-11-25")]
    [TestCase("2026-07-28")]
    public async Task SilentHandlerExceedsHttpWaitWithoutCancellingAdmittedJob(string revision)
    {
        var service = new FakeAutomationService { AdmitJobDuringStatus = true };
        await using var host = await TestListener.StartAsync(service);
        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(300) };
        using var request = Request(host.Endpoint, JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0", id = 1, method = "tools/call",
            @params = new
            {
                name = "workbench_get_status", arguments = new { },
                _meta = new Dictionary<string, object>
                {
                    ["io.modelcontextprotocol/protocolVersion"] = revision,
                    ["io.modelcontextprotocol/clientCapabilities"] = new { },
                    ["io.modelcontextprotocol/clientInfo"] = new { name = "disconnect-test", version = "1" }
                }
            }
        }), revision);
        if (revision == "2026-07-28")
        {
            request.Headers.Add("Mcp-Method", "tools/call");
            request.Headers.Add("Mcp-Name", "workbench_get_status");
        }
        var send = http.SendAsync(request);
        if (await Task.WhenAny(service.StatusEntered.Task, send) == send && send.IsCompletedSuccessfully)
        {
            using var early = await send;
            Assert.Fail($"Request rejected before admission: {early.StatusCode} {await early.Content.ReadAsStringAsync()}");
        }
        await service.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.ThrowsAsync<TaskCanceledException>(async () => await send);
        await service.Admitted.Task;
        Assert.That(service.Result!.State, Is.EqualTo(ExecutionState.Running));
        Assert.That(service.Result.CancellationReason, Is.Null);
        var id = service.Result.ExecutionId;
        Assert.That((await service.GetExecutionAsync(FakeAutomationService.Caller, id, CancellationToken.None)).Data!.State, Is.EqualTo(ExecutionState.Running));
        service.FinishExecution.TrySetResult();
        await service.JobFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(service.Result.State, Is.EqualTo(ExecutionState.Completed));
        Assert.That((await service.GetExecutionAsync(FakeAutomationService.Caller, id, CancellationToken.None)).Data!.State, Is.EqualTo(ExecutionState.Completed));
    }
    [Test]
    public async Task HttpBodyLimitAndListenerDisposal()
    {
        var host = await TestListener.StartAsync();
        var endpoint = host.Endpoint;
        try
        {
            using var http = new HttpClient();
            using var request = Request(endpoint, new string(' ', AutomationContracts.MaxHttpBodyBytes + 1));
            using var response = await http.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
            Assert.That(host.Service.StatusCalls, Is.Zero);
        }
        finally { await host.DisposeAsync(); }
        using var socket = new System.Net.Sockets.TcpClient();
        Assert.ThrowsAsync<System.Net.Sockets.SocketException>(async () => await socket.ConnectAsync(endpoint.Host, endpoint.Port));
    }
    [Test]
    public async Task Sdk230ParseDetailReproducerAndMitigation()
    {
        foreach (var normalize in new[] { false, true })
        {
            await using var host = await TestListener.StartAsync(normalizeParseErrors: normalize);
            using var http = new HttpClient();
            using var request = Request(host.Endpoint, """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":Q}""");
            using var response = await http.SendAsync(request);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var message = json.RootElement.GetProperty("error").GetProperty("message").GetString();
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            if (normalize)
            {
                Assert.That(message, Is.EqualTo("Invalid JSON-RPC request."));
                Assert.That(string.Join("\n", host.Logs.Entries), Does.Not.Contain("\u0027Q\u0027"));
            }
            else Assert.That(message, Does.Contain("'Q'"), "SDK 2.3.0 reflects an input character; keep normalization enabled.");
        }
    }
}
