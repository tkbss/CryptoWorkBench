using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace CryptoWorkBench.Automation.Abstractions;

public enum Lifecycle { Starting, Ready, Stopping, Stopped, Faulted }
public enum WorkbenchExecutionState { Idle, AwaitingConfirmation, Running, WaitingForUser, CancellationRequested }
public enum DirtyState { Clean, Dirty, Unknown }
public enum EditorEditMode { Insert, Append, Replace }
public enum OutputPolicy { MetadataOnly, IncludeText }
public enum TextExecutionMode { Submission, ScriptBook }
public enum ExecutionState { AwaitingConfirmation, Running, WaitingForUser, CancellationRequested, Completed, SyntaxError, Aborted, Cancelled, TimedOut, Failed, Rejected }
public enum CancellationReason { Explicit, Timeout, Shutdown, McpDisabled, User }
public enum ExecutionSource { Editor, Text, GuiLine, ScriptBookFile }
public enum Effects { None, Possible }
public enum DiagnosticKind { Syntax, Semantic, Host }
public enum DiagnosticSeverity { Warning, Error }
public enum OutputKind { Print, Info }
public enum CallerSource { Gui, Mcp }

/// <summary>Trusted host context. Never bind this type to client tool arguments.</summary>
public sealed record AutomationCaller(string PrincipalId, CallerSource Source,
    ImmutableHashSet<string> Permissions, Guid ActivationEpoch);

public static class AutomationPermissions
{
    public const string StatusRead = "status.read";
    public const string EditorRead = "editor.read";
    public const string EditorWrite = "editor.write";
    public const string VariablesMetadataRead = "variables.metadata.read";
    public const string ExecutionRead = "execution.read";
    public const string ExecutionRun = "execution.run";
    public const string OutputRead = "output.read";
}

public sealed record Reply<T>(string SchemaVersion, Guid InstanceId, bool Ok, T? Data, AutomationError? Error) where T : class
{
    public static Reply<T> Success(Guid instanceId, T data) => new("1", instanceId, true, data, null);
    public static Reply<T> Failure(Guid instanceId, AutomationError error) => new("1", instanceId, false, null, error);
}

public sealed record WorkbenchStatus(Lifecycle Lifecycle, bool McpEnabled, bool EditorAvailable,
    WorkbenchExecutionState ExecutionState, Guid? ActiveExecutionId, bool MutationBusy,
    string StateVersion, Guid? LastExecutionId, ImmutableArray<string> Capabilities);
public sealed record EditorSnapshot(Guid EditorId, string Revision, string Text, int Length, bool IsModified, DirtyState DirtyState);
public sealed record EditorEdit(Guid InstanceId, Guid EditorId, string ExpectedRevision, EditorEditMode Mode,
    string Text, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Offset = null);
public sealed record EditorMutation(Guid EditorId, string PreviousRevision, string Revision, int Length, DirtyState DirtyState);
public sealed record EditorExecution(Guid InstanceId, Guid ExecutionId, Guid EditorId, string ExpectedRevision,
    string ExpectedStateVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TimeoutMs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OutputPolicy? OutputPolicy = null);
public sealed record TextExecution(Guid InstanceId, Guid ExecutionId, string ExpectedStateVersion, string Text,
    TextExecutionMode Mode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TimeoutMs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OutputPolicy? OutputPolicy = null);
public sealed record ExecutionResult(Guid ExecutionId, ExecutionState State, DateTimeOffset AcceptedAt,
    DateTimeOffset DeadlineAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    CancellationReason? CancellationReason, ExecutionSource Source, Guid? EditorId, string? SourceRevision,
    string StateVersion, bool HasErrors, Effects Effects, ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<Output> Outputs, VariableSnapshot Variables, bool Truncated, AutomationError? Error);
public sealed record DiagnosticSnapshot(Guid ExecutionId, bool IsFinal, ImmutableArray<Diagnostic> Items, bool Truncated);
public sealed record Diagnostic(DiagnosticKind Kind, DiagnosticSeverity Severity, string Code, string Message,
    int? Line, int? Column, int? StatementIndex, Guid? RunnerExecutionId, int? OperationId,
    int? ParentOperationId, string? FunctionName, string? Mechanism);
public sealed record Output(long Sequence, OutputKind Kind, string? Text, bool Redacted, bool Truncated);
public sealed record VariableSnapshot(string StateVersion, DateTimeOffset CapturedAt, bool Stale,
    ImmutableArray<VariableInfo> Items, bool Truncated);
/// <summary>Metadata only. There is deliberately no value, byte buffer or parameter dictionary.</summary>
public sealed record VariableInfo(string Identifier, string Type, bool IsKey, string? Algorithm,
    string? MaterialKind, int? KeySizeBits, string? Mechanism, bool ValueIncluded);
public sealed record CancelReceipt(Guid ExecutionId, bool Accepted, ExecutionState State);

public static class AutomationContracts
{
    public const int MaxTextLength = 256 * 1024;
    public const int MaxHttpBodyBytes = 1024 * 1024;
    public const int MaxOutputTextBytes = 64 * 1024;
    public const int MaxCollectionEntries = 1000;
    public const int DefaultTimeoutMs = 30000;
    public const int MinTimeoutMs = 1000;
    public const int MaxTimeoutMs = 120000;
    public const int MaxRetainedResults = 64;
    public const int MaxResultBytes = 8 * 1024 * 1024;
    public const int MaxExecutionIds = 10000;
    public static readonly TimeSpan ResultRetention = TimeSpan.FromMinutes(15);

    public static bool IsTerminal(ExecutionState state) => state is ExecutionState.Completed or ExecutionState.SyntaxError
        or ExecutionState.Aborted or ExecutionState.Cancelled or ExecutionState.TimedOut or ExecutionState.Failed or ExecutionState.Rejected;

    /// <summary>UTF-16 offset validation needs the host's current, revision-checked document.</summary>
    public static bool IsValidInsertionOffset(string text, int offset) => offset >= 0 && offset <= text.Length &&
        !(offset > 0 && offset < text.Length &&
          ((char.IsHighSurrogate(text[offset - 1]) && char.IsLowSurrogate(text[offset])) ||
           (text[offset - 1] == '\r' && text[offset] == '\n')));
}
