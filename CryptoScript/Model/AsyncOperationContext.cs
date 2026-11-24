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
    private readonly InterpreterExecution.Frame? frame = InterpreterExecution.CaptureFrame();
    internal void Verify()
    {
        using var access = InterpreterExecution.EnterState(runner: true);
        InterpreterExecution.VerifyFrame(frame);
    }

    internal void Print(string text)
    {
        using var access = InterpreterExecution.EnterState(runner: true);
        InterpreterExecution.VerifyFrame(frame);
        new OutputOperations().Print([text]);
    }

    internal async Task<PasswordResponse> RequestPasswordAsync(PasswordPurpose purpose)
    {
        Verify();
        var response = await request(purpose, CancellationToken).ConfigureAwait(false);
        try
        {
            Verify();
            return response;
        }
        catch { response.Dispose(); throw; }
    }
}
