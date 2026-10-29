namespace CryptoWorkBench.Automation.Abstractions;

// Error codes preserve their specified PascalCase spelling, unlike state enums.
public enum AutomationErrorCode
{
    InvalidArgument, LimitExceeded, NotReady, ShuttingDown, Forbidden, InternalError, InstanceMismatch,
    EditorUnavailable, RevisionConflict, Busy, InvalidRange, ConfirmationDenied, ConfirmationUnavailable,
    StateConflict, NotFound, ResultExpired
}
public sealed record AutomationError(AutomationErrorCode Code, string Message, bool Retryable, Guid? ExecutionId);

public static class AutomationErrors
{
    // Catalog text is deliberately independent of exception, script and input text.
    public static AutomationError Create(AutomationErrorCode code, Guid? executionId = null) => new(code, code switch
    {
        AutomationErrorCode.InvalidArgument => "The request is invalid.",
        AutomationErrorCode.LimitExceeded => "A contract limit was exceeded.",
        AutomationErrorCode.NotReady => "The workbench is not ready.",
        AutomationErrorCode.ShuttingDown => "The workbench is shutting down.",
        AutomationErrorCode.Forbidden => "The operation is not permitted.",
        AutomationErrorCode.InternalError => "The operation could not be completed.",
        AutomationErrorCode.InstanceMismatch => "The workbench instance has changed.",
        AutomationErrorCode.EditorUnavailable => "The editor is unavailable.",
        AutomationErrorCode.RevisionConflict => "The document revision has changed.",
        AutomationErrorCode.Busy => "The workbench is busy.",
        AutomationErrorCode.InvalidRange => "The editor range is invalid.",
        AutomationErrorCode.ConfirmationDenied => "Confirmation was denied.",
        AutomationErrorCode.ConfirmationUnavailable => "Confirmation is unavailable.",
        AutomationErrorCode.StateConflict => "The workbench state has changed.",
        AutomationErrorCode.NotFound => "The execution was not found.",
        AutomationErrorCode.ResultExpired => "The execution result has expired.",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    }, code is AutomationErrorCode.Busy or AutomationErrorCode.NotReady or AutomationErrorCode.EditorUnavailable
        or AutomationErrorCode.ConfirmationUnavailable, executionId);
}
