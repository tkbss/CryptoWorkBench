namespace CryptoScript.ErrorListner;

/// <summary>
/// Optional execution context. Statement indices are one-based when available.
/// Identifiers and names must not contain key material or other sensitive argument values.
/// </summary>
public sealed record DiagnosticLocation(
    Guid? ExecutionId = null,
    int? StatementIndex = null,
    int? OperationId = null,
    int? ParentOperationId = null,
    string? FunctionName = null,
    string? Mechanism = null,
    string? Identifier = null);
