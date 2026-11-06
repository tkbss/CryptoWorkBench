using System.Collections.Immutable;
using CryptoWorkBench.Automation.Abstractions;

namespace CryptoWorkBench.Mcp.Tests;

/// <summary>Only a test double: no parsing, editor or interpreter. Tasks simulate admission and waiting.</summary>
internal sealed class FakeAutomationService : IWorkbenchAutomationService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public static AutomationCaller Caller { get; } = new("test", CallerSource.Mcp,
        ImmutableHashSet.Create(AutomationPermissions.StatusRead, AutomationPermissions.ExecutionRun), Guid.NewGuid());
    public TaskCompletionSource StatusEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource StatusRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool DelayStatus { get; set; }
    public bool AdmitJobDuringStatus { get; set; }
    public TaskCompletionSource JobFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int StatusCalls { get; private set; }
    public AutomationCaller? LastCaller { get; private set; }
    public TaskCompletionSource Admitted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource FinishExecution { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object executionGate = new();
    private readonly CancellationTokenSource executionCancellation = new();
    private Task<Reply<ExecutionResult>>? job;
    public ExecutionResult? Result { get; private set; }
    public static VariableSnapshot Variables => new("0", DateTimeOffset.UtcNow, false, [], false);
    public static ExecutionResult Completed(Guid id) => new(id, ExecutionState.Completed, DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow.AddSeconds(30), null, DateTimeOffset.UtcNow, null, ExecutionSource.Text,
        null, null, "1", false, Effects.None, [], [], Variables with { StateVersion = "1" }, false, null);
    public async Task<Reply<WorkbenchStatus>> GetStatusAsync(AutomationCaller caller, CancellationToken ct)
    {
        LastCaller = caller;
        StatusCalls++;
        StatusEntered.TrySetResult();
        if (AdmitJobDuringStatus) await ExecuteTextAsync(caller, new(InstanceId, Guid.NewGuid(), "0", "", TextExecutionMode.Submission), ct);
        if (DelayStatus) await StatusRelease.Task.WaitAsync(ct);
        return Reply<WorkbenchStatus>.Success(InstanceId, new(Lifecycle.Ready, false, false,
            WorkbenchExecutionState.Idle, null, false, "9007199254740993", null, ["status.read"]));
    }
    public Task<Reply<ExecutionResult>> ExecuteTextAsync(AutomationCaller caller, TextExecution request, CancellationToken requestCancellation)
    {
        requestCancellation.ThrowIfCancellationRequested();
        AutomationJson.Validate(request);
        lock (executionGate)
        {
            requestCancellation.ThrowIfCancellationRequested();
            // One accepted job per fake instance; never overwrite its identity or cancellation source.
            if (job is not null)
                return Task.FromResult(Reply<ExecutionResult>.Failure(InstanceId, AutomationErrors.Create(AutomationErrorCode.Busy)));
            Result = Completed(request.ExecutionId) with { State = ExecutionState.Running, FinishedAt = null };
            job = RunAsync();
            Admitted.TrySetResult();
            return job.WaitAsync(requestCancellation);
        }
    }
    private async Task<Reply<ExecutionResult>> RunAsync()
    {
        try { await FinishExecution.Task.WaitAsync(executionCancellation.Token); }
        catch (OperationCanceledException) { }
        lock (executionGate)
        {
            Result = Result! with
            {
                State = executionCancellation.IsCancellationRequested ? ExecutionState.Cancelled : ExecutionState.Completed,
                FinishedAt = DateTimeOffset.UtcNow
            };
            JobFinished.TrySetResult();
            return Reply<ExecutionResult>.Success(InstanceId, Result);
        }
    }
    public Task<Reply<CancelReceipt>> CancelExecutionAsync(AutomationCaller caller, Guid id, CancellationToken ct)
    {
        lock (executionGate)
        {
            ct.ThrowIfCancellationRequested();
            if (Result is null || Result.ExecutionId != id)
                return Task.FromResult(Reply<CancelReceipt>.Failure(InstanceId, AutomationErrors.Create(AutomationErrorCode.NotFound, id)));
            if (AutomationContracts.IsTerminal(Result.State))
                return Task.FromResult(Reply<CancelReceipt>.Success(InstanceId, new(id, false, Result.State)));
            Result = Result with { State = ExecutionState.CancellationRequested, CancellationReason = CancellationReason.Explicit };
            executionCancellation.Cancel();
            return Task.FromResult(Reply<CancelReceipt>.Success(InstanceId, new(id, true, ExecutionState.CancellationRequested)));
        }
    }
    public Task<Reply<ExecutionResult>> GetExecutionAsync(AutomationCaller caller, Guid id, CancellationToken ct)
    {
        lock (executionGate)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Result is null || Result.ExecutionId != id
                ? Reply<ExecutionResult>.Failure(InstanceId, AutomationErrors.Create(AutomationErrorCode.NotFound, id))
                : Reply<ExecutionResult>.Success(InstanceId, Result));
        }
    }
    public Task<Reply<EditorSnapshot>> GetEditorAsync(AutomationCaller caller, CancellationToken ct) => throw new NotSupportedException();
    public Task<Reply<EditorMutation>> UpdateEditorAsync(AutomationCaller caller, EditorEdit request, CancellationToken ct) => throw new NotSupportedException();
    public Task<Reply<ExecutionResult>> ExecuteEditorAsync(AutomationCaller caller, EditorExecution request, CancellationToken requestCancellation) => throw new NotSupportedException();
    public Task<Reply<DiagnosticSnapshot>> GetDiagnosticsAsync(AutomationCaller caller, Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Reply<VariableSnapshot>> GetVariablesAsync(AutomationCaller caller, CancellationToken ct) => Task.FromResult(Reply<VariableSnapshot>.Success(InstanceId, Variables));
}
