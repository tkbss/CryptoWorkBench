using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoWorkBench.Automation.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CryptoWorkBench.Mcp;

// Registration only. The host supplies a resolver for the authenticated request's
// original credential. No listener, pairing endpoint or default principal.
public static class WorkbenchToolsV2
{
    public static IMcpServerBuilder AddWorkbenchV2(this IServiceCollection services, IWorkbenchAutomationV2 service,
        Func<RequestContext<CallToolRequestParams>, AutomationCaller> authenticatedCaller)
    {
        var catalog = Create(service, authenticatedCaller);
        return services.AddMcpServer().WithHttpTransport(options =>
        {
            options.SessionMode = HttpServerSessionMode.Stateless;
            options.ConfigureSessionOptions = (http, server, _) =>
            {
                if (SafeMcpParseErrors.BindValidatedPost(http, server, catalog))
                {
                    // Never mutate a collection inherited from another server/session.
                    McpServerPrimitiveCollection<McpServerTool> tools = new();
                    if (server.ToolCollection is { } existing)
                        foreach (var tool in existing) tools.Add(tool);
                    foreach (var tool in catalog) tools.Add(tool);
                    server.ToolCollection = tools;
                }
                return Task.CompletedTask;
            };
#pragma warning disable MCP9004
            options.EnableLegacySse = false;
#pragma warning restore MCP9004
        });
    }

    // Call after UseRouting on the host. Omitting either half always denies v2 dispatch.
    public static void MapWorkbenchV2(this WebApplication app, string pattern)
    {
        app.UseSafeMcpParseErrors();
        app.MapMcp(pattern).WithMetadata(new McpParseErrorProtectionMetadata());
    }

    private static IReadOnlyList<McpServerTool> Create(IWorkbenchAutomationV2 service, Func<RequestContext<CallToolRequestParams>, AutomationCaller> authenticatedCaller)
    {
        McpServerTool Bind<T, R>(string name, bool read, bool idempotent, Func<AutomationCaller, T, CancellationToken, Task<R>> call)
            where T : class, IRequestV2 where R : class, IContractV2 =>
            new Binding<T, R>(name, read, idempotent, authenticatedCaller, call);
        return [
            Bind<StatusV2, ResultV2<ControlStatus>>("workbench_get_status", true, true, service.GetStatusV2Async),
            Bind<VariablesV2, ResultV2<VariableSnapshot>>("workbench_get_variables", true, true, service.GetVariablesV2Async),
            Bind<ReadEditorV2, ResultV2<EditorSnapshot>>("workbench_get_editor", true, true, service.GetEditorV2Async),
            Bind<AcquireEditorV2, LeaseReplyV2<Acquired>>("workbench_acquire_editor", false, true, service.AcquireEditorAsync),
            Bind<RenewEditorV2, LeaseReplyV2<Renewed>>("workbench_renew_editor", false, true, service.RenewEditorAsync),
            Bind<ReleaseEditorV2, LeaseReplyV2<Released>>("workbench_release_editor", false, true, service.ReleaseEditorAsync),
            Bind<EditorEditV2, EditorMutationReceiptV2>("workbench_update_editor", false, false, service.UpdateEditorV2Async),
            Bind<EditorExecutionV2, ResultV2<ExecutionResult>>("workbench_execute_editor", false, false, service.ExecuteEditorV2Async),
            Bind<JobRequestV2, ResultV2<ExecutionResult>>("workbench_get_execution", true, true, service.GetExecutionV2Async),
            Bind<JobRequestV2, ResultV2<DiagnosticSnapshot>>("workbench_get_diagnostics", true, true, service.GetDiagnosticsV2Async),
            Bind<JobRequestV2, ResultV2<CancelReceipt>>("workbench_cancel_execution", false, true, service.CancelExecutionV2Async)
        ];
    }

    private sealed class Binding<T, R>(string name, bool read, bool idempotent, Func<RequestContext<CallToolRequestParams>, AutomationCaller> authenticate,
        Func<AutomationCaller, T, CancellationToken, Task<R>> call) : McpServerTool where T : class, IRequestV2 where R : class, IContractV2
    {
        public override IReadOnlyList<object> Metadata { get; } = [];
        public override Tool ProtocolTool { get; } = new()
        {
            Name = name, InputSchema = AutomationSchemas.For<T>(), OutputSchema = AutomationSchemas.For<R>(),
            Annotations = new() { ReadOnlyHint = read, DestructiveHint = !read, IdempotentHint = idempotent, OpenWorldHint = typeof(T) == typeof(EditorExecutionV2) }
        };

        public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            if (request.Params?.Name != name || !SafeMcpParseErrors.ConsumeValidatedPost(request, this, name))
                throw new McpProtocolException("Unvalidated MCP request.", McpErrorCode.InvalidRequest);
            T arguments;
            try
            {
                // The SDK's typed Arguments dictionary has already collapsed duplicate keys.
                // JsonRpcRequest still carries the original parameter node.
                using var raw = JsonDocument.Parse(request.JsonRpcRequest.Params?.ToJsonString() ?? "{}",
                    new JsonDocumentOptions { AllowDuplicateProperties = false });
                if (raw.RootElement.ValueKind != JsonValueKind.Object ||
                    !raw.RootElement.TryGetProperty("arguments", out var input) || input.ValueKind != JsonValueKind.Object)
                    throw new JsonException();
                if ((typeof(T) == typeof(EditorEditV2) || typeof(T) == typeof(EditorExecutionV2)) && !input.TryGetProperty("leaseId", out _))
                {
                    // Distinguish a recognizable legacy payload from malformed arguments without inventing a lease.
                    var legacy = JsonNode.Parse(input.GetRawText())!.AsObject();
                    string? version = null;
                    if (legacy.Remove("contractVersion", out var supplied))
                    {
                        if (supplied is not JsonValue value || !value.TryGetValue<string>(out version)) throw new JsonException();
                    }
                    var payload = legacy.ToJsonString();
                    if (typeof(T) == typeof(EditorEditV2))
                    {
                        var edit = AutomationJson.Deserialize<EditorEdit>(payload);
                        if (version == "2") AutomationJson.ValidateUnleasedV2(edit);
                    }
                    else
                    {
                        var execution = AutomationJson.Deserialize<EditorExecution>(payload);
                        if (version == "2") AutomationJson.ValidateUnleasedV2(execution);
                    }
                    throw new McpProtocolException(version == "2" ? nameof(AutomationErrorCode.LeaseRequired) :
                        nameof(AutomationErrorCode.UnsupportedContractVersion), McpErrorCode.InvalidParams);
                }
                arguments = AutomationJson.Deserialize<T>(input.GetRawText());
            }
            catch (JsonException) { throw new McpProtocolException("Invalid v2 arguments.", McpErrorCode.InvalidParams); }
            try
            {
                var caller = authenticate(request);
                if (caller.Source != CallerSource.Mcp) throw new InvalidOperationException();
                var reply = await call(caller, arguments, cancellationToken);
                string json = AutomationJson.Serialize(reply);
                var element = JsonSerializer.Deserialize<JsonElement>(json);
                return new CallToolResult
                {
                    IsError = element.GetProperty("error").ValueKind != JsonValueKind.Null ||
                        typeof(T) == typeof(EditorExecutionV2) && reply is ResultV2<ExecutionResult> { Result.State: ExecutionState.Rejected or ExecutionState.SyntaxError or
                            ExecutionState.Cancelled or ExecutionState.TimedOut or ExecutionState.Failed or ExecutionState.Aborted },
                    StructuredContent = element, Content = [new TextContentBlock { Text = reply is EditorMutationReceiptV2
                        { TextEffect: TextEffect.Applied, Error.Code: AutomationErrorCode.CallbackFailed }
                        ? "Editor operation failed after an applied change; refresh state." : json }]
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { throw new McpProtocolException("Workbench request failed.", McpErrorCode.InternalError); }
        }
    }
}
