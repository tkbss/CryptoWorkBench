namespace CryptoWorkBench.Automation.Abstractions;

/// <summary>All reads return detached snapshots. Implementations own admitted jobs independently of callers.</summary>
public interface IWorkbenchAutomationService
{
    Task<Reply<WorkbenchStatus>> GetStatusAsync(AutomationCaller caller, CancellationToken ct);
    Task<Reply<EditorSnapshot>> GetEditorAsync(AutomationCaller caller, CancellationToken ct);
    Task<Reply<EditorMutation>> UpdateEditorAsync(AutomationCaller caller, EditorEdit request, CancellationToken ct);
    /// <summary>Before admission cancellation prevents effects; afterwards it cancels only the response wait.
    /// The service must own and observe the execution task and its separate cancellation source.</summary>
    Task<Reply<ExecutionResult>> ExecuteEditorAsync(AutomationCaller caller, EditorExecution request, CancellationToken requestCancellation);
    /// <summary>Internal GUI entry point; never exposed as an MCP tool. Same admission/cancellation contract as ExecuteEditorAsync.</summary>
    Task<Reply<ExecutionResult>> ExecuteTextAsync(AutomationCaller caller, TextExecution request, CancellationToken requestCancellation);
    Task<Reply<ExecutionResult>> GetExecutionAsync(AutomationCaller caller, Guid executionId, CancellationToken ct);
    Task<Reply<DiagnosticSnapshot>> GetDiagnosticsAsync(AutomationCaller caller, Guid executionId, CancellationToken ct);
    Task<Reply<VariableSnapshot>> GetVariablesAsync(AutomationCaller caller, CancellationToken ct);
    /// <summary>Checks ct before atomically requesting execution cancellation. Losing the reply never retracts that request.</summary>
    Task<Reply<CancelReceipt>> CancelExecutionAsync(AutomationCaller caller, Guid executionId, CancellationToken ct);
}
