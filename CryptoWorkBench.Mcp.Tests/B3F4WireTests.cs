using System.Reflection;
using System.Text;
using System.Text.Json;
using CryptoWorkBench.Automation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

public class B3F4WireTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public class EndpointProbe : DispatchProxy
    {
        internal readonly Guid Marker = Guid.NewGuid();
        internal int Resolutions, Calls;
        internal McpServerTool? Selected;
        internal Func<Task>? FinalizeEdit;
        internal AutomationCaller Resolve(RequestContext<CallToolRequestParams> request)
        {
            Interlocked.Increment(ref Resolutions);
            Selected = (McpServerTool)request.MatchedPrimitive!;
            return FakeAutomationService.Caller;
        }
        protected override object Invoke(MethodInfo? method, object?[]? arguments)
        {
            Interlocked.Increment(ref Calls);
            return method!.Name switch
            {
                nameof(IWorkbenchAutomationV2.GetStatusV2Async) => Task.FromResult(new ResultV2<ControlStatus>("2", Marker, null, SafeError.From(AutomationErrorCode.Forbidden))),
                nameof(IWorkbenchAutomationV2.ExecuteEditorV2Async) => Task.FromResult(new ResultV2<ExecutionResult>("2", Marker, null, SafeError.From(AutomationErrorCode.Forbidden))),
                nameof(IWorkbenchAutomationV2.UpdateEditorV2Async) => Edit((EditorEditV2)arguments![1]!),
                _ => throw new InvalidOperationException()
            };
        }
        private async Task<EditorMutationReceiptV2> Edit(EditorEditV2 edit)
        {
            if (FinalizeEdit != null) await FinalizeEdit();
            return new("2", Marker, edit.EditorId, edit.ExpectedRevision, "1", TextEffect.Applied, HistoryEffect.Applied,
                Finalization.Complete, null, RetryPolicy.NoAutomaticRetry, false);
        }
    }

    private static string Body(string name)
    {
        var id = Guid.NewGuid();
        string arguments = name switch
        {
            "workbench_get_status" => """{"contractVersion":"2"}""",
            "workbench_update_editor" => AutomationJson.Serialize(new EditorEditV2("2", id, id, id, "0", EditorEditMode.Append, "!")),
            "workbench_execute_editor" => AutomationJson.Serialize(new EditorExecutionV2("2", id, id, id, id, "0", "0")),
            _ => throw new InvalidOperationException()
        };
        return $$"""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"{{name}}","arguments":{{arguments}} } }""";
    }

    private static async Task<JsonElement> Post(Uri endpoint, string body, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Accept", "application/json, text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(text.Split('\n').Single(line => line.StartsWith("data: ", StringComparison.Ordinal))[6..]);
        return json.RootElement.Clone();
    }

    private delegate McpServerTool SelectTool(Microsoft.AspNetCore.Http.HttpContext http, McpServerTool original);

    private static Task<TestListener> Start(IWorkbenchAutomationV2 service, SelectTool? select = null) =>
        TestListener.StartAsync(configureTools: services =>
        {
            services.AddWorkbenchV2(service, ((EndpointProbe)(object)service).Resolve);
            if (select == null) return;
            services.PostConfigure<HttpServerTransportOptions>(options =>
            {
                var configure = options.ConfigureSessionOptions!;
                options.ConfigureSessionOptions = async (http, server, token) =>
                {
                    await configure(http, server, token);
                    McpServerPrimitiveCollection<McpServerTool> tools = new();
                    foreach (var tool in server.ToolCollection!) tools.Add(select(http, tool));
                    server.ToolCollection = tools;
                };
            });
        });

    [TestCase("workbench_get_status"), TestCase("workbench_update_editor"), TestCase("workbench_execute_editor")]
    public async Task ValidatedHostRejectsAnotherCatalog(string name)
    {
        var a = DispatchProxy.Create<IWorkbenchAutomationV2, EndpointProbe>();
        var b = DispatchProxy.Create<IWorkbenchAutomationV2, EndpointProbe>();
        var first = (EndpointProbe)(object)a;
        var second = (EndpointProbe)(object)b;
        var request = Body(name);
        McpServerTool? transplant = null;
        await using var origin = await Start(a);
        await using var target = await Start(b, (_, tool) => tool.ProtocolTool.Name == name ? Volatile.Read(ref transplant) ?? tool : tool);
        static Guid Marker(JsonElement reply) => reply.GetProperty("result").GetProperty("structuredContent").GetProperty("instanceId").GetGuid();
        var normal = await Task.WhenAll(Post(origin.Endpoint, request), Post(target.Endpoint, request));
        Assert.That((Marker(normal[0]), Marker(normal[1])), Is.EqualTo((first.Marker, second.Marker)));
        Volatile.Write(ref transplant, first.Selected!);
        var denied = await Post(target.Endpoint, request);
        Assert.That(denied.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32600));
        Assert.That((first.Resolutions, first.Calls, second.Resolutions, second.Calls), Is.EqualTo((1, 1, 1, 1)));
        Volatile.Write(ref transplant, null);
        Assert.That(Marker(await Post(target.Endpoint, request)), Is.EqualTo(second.Marker));
        Assert.That((second.Resolutions, second.Calls), Is.EqualTo((2, 2)));
    }

    private sealed class Intercept(McpServerTool original, Func<RequestContext<CallToolRequestParams>, Task<CallToolResult>> handle) : McpServerTool
    {
        public override Tool ProtocolTool => original.ProtocolTool;
        public override IReadOnlyList<object> Metadata => original.Metadata;
        public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken ct = default) =>
            await handle(request);
    }

    [Test]
    public async Task DisconnectLeavesIdenticalConcurrentPostAdmissible()
    {
        const string name = "workbench_get_status";
        var service = DispatchProxy.Create<IWorkbenchAutomationV2, EndpointProbe>();
        var first = Signal(); var second = Signal(); var release = Signal(); var aborted = Signal();
        var denied = Signal();
        int requests = 0;
        await using var target = await Start(service, (http, tool) =>
        {
            if (tool.ProtocolTool.Name != name) return tool;
            bool isFirst = Interlocked.Increment(ref requests) == 1;
            return new Intercept(tool, async request =>
            {
                using var registration = isFirst ? http.RequestAborted.Register(() => aborted.TrySetResult()) : default;
                (isFirst ? first : second).TrySetResult();
                await release.Task.WaitAsync(Timeout);
                try { return await tool.InvokeAsync(request); }
                catch (McpProtocolException ex) when (isFirst && ex.ErrorCode == McpErrorCode.InvalidRequest)
                {
                    denied.TrySetResult();
                    throw;
                }
            });
        });
        var body = Body(name);
        using var cancel = new CancellationTokenSource();
        var dropped = Post(target.Endpoint, body, cancel.Token);
        await first.Task.WaitAsync(Timeout);
        var live = Post(target.Endpoint, body);
        try
        {
            await second.Task.WaitAsync(Timeout);
            cancel.Cancel();
            await aborted.Task.WaitAsync(Timeout);
        }
        finally { release.TrySetResult(); }
        try { await dropped.WaitAsync(Timeout); }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        await denied.Task.WaitAsync(Timeout);
        Assert.That((await live.WaitAsync(Timeout)).TryGetProperty("result", out _), Is.True);
        var probe = (EndpointProbe)(object)service;
        Assert.That((probe.Resolutions, probe.Calls), Is.EqualTo((1, 1)));
    }

    [TestCase("abort-first"), TestCase("dispatch-first"), TestCase("race"), TestCase("unused")]
    public async Task ReceiptBelongsToTheOriginalPostUntilItsFirstAdmission(string ordering)
    {
        const string name = "workbench_update_editor";
        var service = DispatchProxy.Create<IWorkbenchAutomationV2, EndpointProbe>();
        var probe = (EndpointProbe)(object)service;
        var selected = Signal(); var release = Signal(); var aborted = Signal(); var ended = Signal();
        var admitted = Signal(); var finalize = Signal();
        McpServerTool? original = null;
        RequestContext<CallToolRequestParams>? saved = null;
        int intercept = 1;
        probe.FinalizeEdit = async () => { admitted.TrySetResult(); await finalize.Task.WaitAsync(Timeout); };
        await using var target = await Start(service, (http, tool) =>
        {
            if (tool.ProtocolTool.Name != name || Interlocked.Exchange(ref intercept, 0) == 0) return tool;
            original = tool;
            http.Response.OnCompleted(() => { ended.TrySetResult(); return Task.CompletedTask; });
            return new Intercept(tool, async request =>
            {
                using var registration = http.RequestAborted.Register(() => aborted.TrySetResult());
                saved = request;
                selected.TrySetResult();
                await release.Task.WaitAsync(Timeout);
                return new CallToolResult { Content = [] };
            });
        });
        using var cancel = new CancellationTokenSource();
        var call = Post(target.Endpoint, Body(name), cancel.Token);
        try
        {
            await selected.Task.WaitAsync(Timeout);
            var other = await Post(target.Endpoint, Body("workbench_get_status"));
            Assert.That(other.TryGetProperty("result", out _), Is.True);
            if (ordering != "unused")
            {
                Task<CallToolResult> Dispatch() => original!.InvokeAsync(saved!, CancellationToken.None).AsTask();
                Task<CallToolResult> attempt;
                if (ordering == "dispatch-first")
                {
                    attempt = Dispatch();
                    await admitted.Task.WaitAsync(Timeout);
                    Assert.ThrowsAsync<McpProtocolException>(async () => await Dispatch());
                    cancel.Cancel();
                }
                else if (ordering == "abort-first")
                {
                    cancel.Cancel();
                    await aborted.Task.WaitAsync(Timeout);
                    attempt = Dispatch(); // Host passes None. The original POST still owns cancellation.
                }
                else
                {
                    attempt = Task.Run(Dispatch);
                    cancel.Cancel();
                }
                await aborted.Task.WaitAsync(Timeout);
                finalize.TrySetResult();
                try
                {
                    var result = await attempt.WaitAsync(Timeout);
                    Assert.That(ordering, Is.Not.EqualTo("abort-first"));
                    var receipt = AutomationJson.Deserialize<EditorMutationReceiptV2>(result.StructuredContent!.Value.GetRawText());
                    Assert.That(receipt.TextEffect, Is.EqualTo(TextEffect.Applied));
                    Assert.That((probe.Resolutions, probe.Calls), Is.EqualTo((2, 2)));
                }
                catch (McpProtocolException ex)
                {
                    Assert.That(ordering, Is.Not.EqualTo("dispatch-first"));
                    Assert.That(ex.ErrorCode, Is.EqualTo(McpErrorCode.InvalidRequest));
                    Assert.That((probe.Resolutions, probe.Calls), Is.EqualTo((1, 1)));
                }
            }
        }
        finally { release.TrySetResult(); finalize.TrySetResult(); }
        try { await call.WaitAsync(Timeout); }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        await ended.Task.WaitAsync(Timeout);
        int calls = probe.Calls;
        Assert.ThrowsAsync<McpProtocolException>(async () => await original!.InvokeAsync(saved!));
        Assert.That((probe.Resolutions, probe.Calls), Is.EqualTo((calls, calls)));
        Assert.That((await Post(target.Endpoint, Body(name))).TryGetProperty("result", out _), Is.True);
        Assert.That((probe.Resolutions, probe.Calls), Is.EqualTo((calls + 1, calls + 1)));
    }
}
