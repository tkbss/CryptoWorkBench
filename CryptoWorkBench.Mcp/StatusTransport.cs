using CryptoWorkBench.Automation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace CryptoWorkBench.Mcp;

/// <summary>Registers only the MCP-01.1 status probe. Does not start a listener or integrate the desktop.
/// The host supplies trusted caller resolution; production authentication is a later step.</summary>
public static class StatusTransport
{
    public static IMcpServerBuilder AddWorkbenchStatusProbe(this IServiceCollection services,
        IWorkbenchAutomationService service, Func<AutomationCaller> trustedCaller)
    {
        return services.AddMcpServer().WithHttpTransport(options =>
        {
            options.SessionMode = HttpServerSessionMode.Stateless;
#pragma warning disable MCP9004 // Explicitly disable obsolete transport, including external feature switches.
            options.EnableLegacySse = false;
#pragma warning restore MCP9004
        }).WithTools([new StatusTool(service, trustedCaller)]);
    }

    private sealed class StatusTool(IWorkbenchAutomationService service, Func<AutomationCaller> caller) : McpServerTool
    {
        public override IReadOnlyList<object> Metadata { get; } = Array.Empty<object>();
        public override Tool ProtocolTool { get; } = new()
        {
            Name = "workbench_get_status",
            Description = "Read the published workbench status.",
            InputSchema = JsonSerializer.Deserialize<JsonElement>("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{},"required":[],"additionalProperties":false}"""),
            OutputSchema = AutomationSchemas.For<Reply<WorkbenchStatus>>(),
            Annotations = new() { ReadOnlyHint = true, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false }
        };
        public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            if (request.Params?.Arguments is { Count: > 0 })
                throw new McpProtocolException("Invalid tool arguments.", McpErrorCode.InvalidParams);
            return AutomationToolResults.Create(await service.GetStatusAsync(caller(), cancellationToken));
        }
    }
}
