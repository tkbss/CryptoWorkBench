using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoWorkBench.Automation.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

[TestFixture]
public class ReviewRegressionTests
{
    [Test, Combinatorial]
    public async Task RoutedAliasesProtectErrorsAndAllowStatus(
        [Values("/mcp", "/MCP", "/mcp/", "/MCP/", "/mCp", "/%6dcp")] string path,
        [Values("2025-11-25", "2026-07-28")] string revision)
    {
        const string marker = "H1_SECRET_71bc9432";
        await using var host = await TestListener.StartAsync();
        var endpoint = new Uri(host.Endpoint, path);
        using var http = new HttpClient();
        foreach (var body in new[]
        {
            "{\"" + marker + "\":",
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":{\"" + marker + "\":true}}",
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":Q" + marker + "}"
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add("Accept", "application/json, text/event-stream");
            request.Headers.Add("MCP-Protocol-Version", revision);
            if (revision == "2026-07-28") request.Headers.Add("Mcp-Method", "tools/list");
            using var response = await http.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            using var json = JsonDocument.Parse(text);
            Assert.That(json.RootElement.GetProperty("error").GetProperty("message").GetString(),
                Is.EqualTo("Invalid JSON-RPC request."));
            Assert.That(text, Does.Not.Contain(marker));
            Assert.That(string.Join("\n", host.Logs.Entries), Does.Not.Contain(marker));
        }
        await using var transport = new HttpClientTransport(new() { Endpoint = endpoint, TransportMode = HttpTransportMode.StreamableHttp });
        await using var client = await McpClient.CreateAsync(transport, new() { ProtocolVersion = revision });
        Assert.That(client.NegotiatedProtocolVersion, Is.EqualTo(revision));
        Assert.That((await client.ListToolsAsync()).Single().Name, Is.EqualTo("workbench_get_status"));
        var status = await client.CallToolAsync("workbench_get_status");
        Assert.That(status.IsError, Is.Not.True);
        Assert.That(AutomationJson.Deserialize<Reply<WorkbenchStatus>>(status.StructuredContent!.Value.GetRawText()).Ok, Is.True);
    }

    [TestCase("application/json", "{\"ok\":true}")]
    [TestCase("text/event-stream", "event: message\ndata: {\"ok\":true}\n\n")]
    public async Task SuccessfulWritesReachOriginalStreamBeforeHandlerReturns(string contentType, string chunk)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        using var original = new MemoryStream();
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new McpParseErrorProtectionMetadata()), "probe"));
        context.Response.Body = original;
        app.UseSafeMcpParseErrors();
        app.Run(async ctx =>
        {
            ctx.Response.ContentType = contentType;
            await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(chunk));
            await ctx.Response.Body.FlushAsync();
            Assert.That(Encoding.UTF8.GetString(original.ToArray()), Is.EqualTo(chunk), "First chunk must be visible before the handler completes.");
            await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(chunk));
        });
        await app.Build()(context);
        Assert.That(Encoding.UTF8.GetString(original.ToArray()), Is.EqualTo(chunk + chunk));
        Assert.That(context.Response.Body, Is.SameAs(original));
    }

    [Test]
    public async Task OversizedParseErrorIsReplacedByFixedSmallResponse()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        using var original = new MemoryStream();
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new McpParseErrorProtectionMetadata()), "probe"));
        context.Response.Body = original;
        app.UseSafeMcpParseErrors();
        app.Run(async ctx =>
        {
            ctx.Response.StatusCode = 400;
            var chunk = Encoding.UTF8.GetBytes(new string('Q', 8192));
            for (var i = 0; i < 10; i++) await ctx.Response.Body.WriteAsync(chunk);
            Assert.That(original.Length, Is.Zero);
        });
        await app.Build()(context);
        Assert.That(original.Length, Is.LessThan(256));
        using var json = JsonDocument.Parse(original.ToArray());
        Assert.That(json.RootElement.GetProperty("error").GetProperty("message").GetString(), Is.EqualTo("Invalid JSON-RPC request."));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UnknownOrForeignExecutionCannotBeReadOrCancelled(bool hasJob)
    {
        var service = new FakeAutomationService();
        var id = Guid.NewGuid();
        Task<Reply<ExecutionResult>>? job = hasJob ? service.ExecuteTextAsync(FakeAutomationService.Caller,
            new(service.InstanceId, id, "0", "", TextExecutionMode.Submission), CancellationToken.None) : null;
        var foreign = Guid.NewGuid();
        var read = await service.GetExecutionAsync(FakeAutomationService.Caller, foreign, CancellationToken.None);
        var cancel = await service.CancelExecutionAsync(FakeAutomationService.Caller, foreign, CancellationToken.None);
        Assert.That(read.Ok, Is.False);
        Assert.That(read.Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
        Assert.That(cancel.Ok, Is.False);
        Assert.That(cancel.Error!.Code, Is.EqualTo(AutomationErrorCode.NotFound));
        if (job is not null)
        {
            var current = await service.GetExecutionAsync(FakeAutomationService.Caller, id, CancellationToken.None);
            Assert.That(current.Data!.ExecutionId, Is.EqualTo(id));
            Assert.That(current.Data.State, Is.EqualTo(ExecutionState.Running));
            Assert.That(current.Data.CancellationReason, Is.Null);
            service.FinishExecution.TrySetResult();
            Assert.That((await job).Data!.State, Is.EqualTo(ExecutionState.Completed));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TerminalAndRepeatedCancellationPreserveActualState(bool cancelFirst)
    {
        var service = new FakeAutomationService();
        var id = Guid.NewGuid();
        var job = service.ExecuteTextAsync(FakeAutomationService.Caller,
            new(service.InstanceId, id, "0", "", TextExecutionMode.Submission), CancellationToken.None);
        if (cancelFirst)
        {
            var first = await service.CancelExecutionAsync(FakeAutomationService.Caller, id, CancellationToken.None);
            Assert.That(first.Data!.Accepted, Is.True);
            // The job may finish before the second request; either receipt must match that snapshot.
            var second = await service.CancelExecutionAsync(FakeAutomationService.Caller, id, CancellationToken.None);
            Assert.That(second.Data!.State, Is.EqualTo(second.Data.Accepted ? ExecutionState.CancellationRequested : ExecutionState.Cancelled));
        }
        else service.FinishExecution.TrySetResult();
        var terminal = (await job).Data!;
        Assert.That(terminal.State, Is.EqualTo(cancelFirst ? ExecutionState.Cancelled : ExecutionState.Completed));
        for (var i = 0; i < 2; i++)
        {
            var receipt = await service.CancelExecutionAsync(FakeAutomationService.Caller, id, CancellationToken.None);
            Assert.That(receipt.Data!.Accepted, Is.False);
            Assert.That(receipt.Data.State, Is.EqualTo(terminal.State));
            Assert.That((await service.GetExecutionAsync(FakeAutomationService.Caller, id, CancellationToken.None)).Data, Is.EqualTo(terminal));
        }
    }

    [Test]
    public async Task CancelledCancelRequestCannotCancelAcceptedJob()
    {
        var service = new FakeAutomationService();
        var id = Guid.NewGuid();
        var job = service.ExecuteTextAsync(FakeAutomationService.Caller,
            new(service.InstanceId, id, "0", "", TextExecutionMode.Submission), CancellationToken.None);
        Assert.Throws<OperationCanceledException>(() => service.CancelExecutionAsync(FakeAutomationService.Caller, id, new CancellationToken(true)));
        Assert.That(service.Result!.State, Is.EqualTo(ExecutionState.Running));
        Assert.That(service.Result.CancellationReason, Is.Null);
        service.FinishExecution.TrySetResult();
        Assert.That((await job).Data!.State, Is.EqualTo(ExecutionState.Completed));
    }

    [Test]
    public async Task SecondAdmissionCannotReplaceAcceptedIdentity()
    {
        var service = new FakeAutomationService();
        var id = Guid.NewGuid();
        var job = service.ExecuteTextAsync(FakeAutomationService.Caller,
            new(service.InstanceId, id, "0", "", TextExecutionMode.Submission), CancellationToken.None);
        var second = await service.ExecuteTextAsync(FakeAutomationService.Caller,
            new(service.InstanceId, Guid.NewGuid(), "0", "", TextExecutionMode.Submission), CancellationToken.None);
        Assert.That(second.Error!.Code, Is.EqualTo(AutomationErrorCode.Busy));
        Assert.That(service.Result!.ExecutionId, Is.EqualTo(id));
        service.FinishExecution.TrySetResult();
        await job;
    }

    [TestCase("outputs")]
    [TestCase("diagnostics")]
    public void NullCollectionElementsDeserializeAsJsonException(string property)
    {
        var node = JsonNode.Parse(AutomationJson.Serialize(FakeAutomationService.Completed(Guid.NewGuid())))!;
        node[property] = new JsonArray((JsonNode?)null);
        Assert.Throws<JsonException>(() => AutomationJson.Deserialize<ExecutionResult>(node.ToJsonString()));
    }

    [Test]
    public void NullAndDefaultNestedSnapshotsSerializeAsJsonException()
    {
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(new EditorSnapshot(Guid.NewGuid(), "0", null!, 0, false, DirtyState.Clean)));
        var valid = FakeAutomationService.Completed(Guid.NewGuid());
        foreach (var invalid in new[]
        {
            valid with { Outputs = [null!] }, valid with { Outputs = default },
            valid with { Diagnostics = [null!] }, valid with { Variables = null! },
            valid with { Variables = valid.Variables with { Items = [null!] } }
        })
            Assert.Throws<JsonException>(() => AutomationJson.Serialize(invalid));
    }
}
