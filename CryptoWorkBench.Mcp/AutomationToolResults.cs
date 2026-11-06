using CryptoWorkBench.Automation.Abstractions;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CryptoWorkBench.Mcp;

public static class AutomationToolResults
{
    public static CallToolResult Create<T>(Reply<T> reply, bool executionCall = false) where T : class
    {
        var json = AutomationJson.Serialize(reply);
        var failedExecution = executionCall && reply.Data is ExecutionResult execution &&
            execution.State is ExecutionState.SyntaxError or ExecutionState.Aborted or ExecutionState.Cancelled
                or ExecutionState.TimedOut or ExecutionState.Failed or ExecutionState.Rejected;
        return new CallToolResult
        {
            IsError = !reply.Ok || failedExecution,
            StructuredContent = JsonSerializer.Deserialize<JsonElement>(json),
            Content = [new TextContentBlock { Text = json }]
        };
    }
}
