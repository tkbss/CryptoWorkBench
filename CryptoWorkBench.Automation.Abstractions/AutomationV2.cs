using System.Text.Json.Serialization;

namespace CryptoWorkBench.Automation.Abstractions;

public interface IContractV2 { string ContractVersion { get; } }
public interface IRequestV2 : IContractV2 { }
public sealed record StatusV2(string ContractVersion) : IRequestV2;
public sealed record VariablesV2(string ContractVersion, Guid InstanceId) : IRequestV2;
public sealed record ReadEditorV2(string ContractVersion, Guid InstanceId, Guid EditorId) : IRequestV2;
public sealed record AcquireEditorV2(string ContractVersion, Guid InstanceId, Guid EditorId, Guid AcquisitionId, string ExpectedRevision) : IRequestV2;
public sealed record RenewEditorV2(string ContractVersion, Guid InstanceId, Guid EditorId, Guid LeaseId, string ExpectedLeaseVersion) : IRequestV2;
public sealed record ReleaseEditorV2(string ContractVersion, Guid InstanceId, Guid EditorId, Guid LeaseId) : IRequestV2;
public sealed record EditorEditV2(string ContractVersion, Guid InstanceId, Guid EditorId, Guid LeaseId, string ExpectedRevision,
    EditorEditMode Mode, string Text, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Offset = null) : IRequestV2;
public sealed record EditorExecutionV2(string ContractVersion, Guid InstanceId, Guid EditorId, Guid LeaseId, Guid ExecutionId,
    string ExpectedRevision, string ExpectedStateVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TimeoutMs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OutputPolicy? OutputPolicy = null) : IRequestV2;
public sealed record JobRequestV2(string ContractVersion, Guid InstanceId, Guid ExecutionId) : IRequestV2;

public sealed record SafeError(AutomationErrorCode Code, string MessageKey)
{
    public static SafeError From(AutomationErrorCode code) => new(code, "mcp." + code);
}
public sealed record ResultV2<T>(string ContractVersion, Guid InstanceId, T? Result, SafeError? Error) : IContractV2 where T : class;
public enum LeaseState { Active, Releasing, Released, Expired, Revoked }
public enum ReleasePhase { Releasing, Released }
public enum ReleaseReason { Released, Expired, Revoked, Reclaimed, Detached, Shutdown }
public enum RetryPolicy { SameRequestOnly, NoAutomaticRetry }
public enum ControlPhase { Gui, Acquiring, Mcp, Releasing, Unavailable, Faulted }
public sealed record OwnLease(Guid EditorId, Guid LeaseId, string LeaseVersion, DateTimeOffset ExpiresAt);
public sealed record ControlStatus(Lifecycle Lifecycle, bool McpEnabled, bool EditorAvailable, WorkbenchExecutionState ExecutionState,
    Guid? ActiveExecutionId, bool MutationBusy, string StateVersion, Guid? LastExecutionId,
    System.Collections.Immutable.ImmutableArray<string> Capabilities, Guid? EditorId, ControlPhase Phase, bool EditorWriteBlocked, OwnLease? OwnLease);
public enum AcquiredKind { Acquired }
public enum RenewedKind { Renewed }
public enum ReleasedKind { Released }
public sealed record Acquired(AcquiredKind Kind, Guid AcquisitionId, Guid EditorId, Guid LeaseId, string Revision, string LeaseVersion,
    DateTimeOffset AcceptedAt, DateTimeOffset ExpiresAt, LeaseState LeaseState);
public sealed record Renewed(RenewedKind Kind, Guid EditorId, Guid LeaseId, string InputLeaseVersion, string LeaseVersion, DateTimeOffset AcceptedAt, DateTimeOffset ExpiresAt);
public sealed record Released(ReleasedKind Kind, Guid EditorId, Guid LeaseId, ReleasePhase Phase, ReleaseReason Reason);
public sealed record LeaseReplyV2<T>(string ContractVersion, Guid InstanceId, T? Result, SafeError? Error,
    bool Replayed, RetryPolicy RetryPolicy, bool RequiresStateRefresh) : IContractV2 where T : class;

public enum TextEffect { None, Applied, Partial, Indeterminate }
public enum HistoryEffect { None, Applied, Indeterminate }
public enum Finalization { Complete, Faulted }
public sealed record EditorMutationReceiptV2(string ContractVersion, Guid InstanceId, Guid EditorId, string ExpectedRevision,
    string? ObservedRevision, TextEffect TextEffect, HistoryEffect HistoryEffect, Finalization Finalization,
    SafeError? Error, RetryPolicy RetryPolicy, bool RequiresStateRefresh) : IContractV2;

public interface IWorkbenchAutomationV2
{
    Task<ResultV2<ControlStatus>> GetStatusV2Async(AutomationCaller caller, StatusV2 request, CancellationToken ct);
    Task<ResultV2<VariableSnapshot>> GetVariablesV2Async(AutomationCaller caller, VariablesV2 request, CancellationToken ct);
    Task<ResultV2<EditorSnapshot>> GetEditorV2Async(AutomationCaller caller, ReadEditorV2 request, CancellationToken ct);
    Task<LeaseReplyV2<Acquired>> AcquireEditorAsync(AutomationCaller caller, AcquireEditorV2 request, CancellationToken ct);
    Task<LeaseReplyV2<Renewed>> RenewEditorAsync(AutomationCaller caller, RenewEditorV2 request, CancellationToken ct);
    Task<LeaseReplyV2<Released>> ReleaseEditorAsync(AutomationCaller caller, ReleaseEditorV2 request, CancellationToken ct);
    Task<EditorMutationReceiptV2> UpdateEditorV2Async(AutomationCaller caller, EditorEditV2 request, CancellationToken ct);
    Task<ResultV2<ExecutionResult>> ExecuteEditorV2Async(AutomationCaller caller, EditorExecutionV2 request, CancellationToken ct);
    Task<ResultV2<ExecutionResult>> GetExecutionV2Async(AutomationCaller caller, JobRequestV2 request, CancellationToken ct);
    Task<ResultV2<DiagnosticSnapshot>> GetDiagnosticsV2Async(AutomationCaller caller, JobRequestV2 request, CancellationToken ct);
    Task<ResultV2<CancelReceipt>> CancelExecutionV2Async(AutomationCaller caller, JobRequestV2 request, CancellationToken ct);
}
