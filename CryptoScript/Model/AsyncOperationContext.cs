using CryptoScript.ErrorListner;

namespace CryptoScript.Model;

// Explicit async dispatch contract. No script name is registered in B1.
internal delegate Task AsyncStatementOperation(AsyncOperationContext context);

// Friend-test-only seam. The parser/factories cannot construct or invoke it.
internal sealed record AsyncInfrastructureStatement(AsyncStatementOperation Operation) : Ast.StatementNode;

internal sealed class AsyncOperationContext(
    Func<PasswordPurpose, CancellationToken, Task<PasswordResponse>> request,
    CancellationToken cancellationToken, ISemanticDiagnosticSink diagnostics)
{
    internal CancellationToken CancellationToken { get; } = cancellationToken;
    internal ISemanticDiagnosticSink Diagnostics { get; } = diagnostics;
    internal Task<PasswordResponse> RequestPasswordAsync(PasswordPurpose purpose) => request(purpose, CancellationToken);
}
