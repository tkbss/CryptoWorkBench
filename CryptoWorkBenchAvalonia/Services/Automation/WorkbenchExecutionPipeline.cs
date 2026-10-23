using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoWorkBenchAvalonia.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services.Automation;

// Host-local copies, deliberately not the external execution/retention contract.
internal sealed record ExecutionPresentation(bool Succeeded, string Status,
    Guid? RunnerId, IReadOnlyList<SemanticError> Diagnostics, SemanticError? Error,
    IReadOnlyList<VariableModel> Variables, IReadOnlyList<ExecutionOutput> Output,
    IReadOnlyList<SyntaxDiagnostic> SyntaxDiagnostics, bool CaptureFailed, bool EffectsPossible)
{
    // Succeeded/Status describe the script; capture is an independent host step.
    internal bool HostSucceeded => Succeeded && !CaptureFailed;
    internal string PresentationStatus => CaptureFailed
        ? Status + " Variable state capture failed; displayed variables are stale."
        : Status;
}
internal sealed record ExecutionOutput(string Kind, string Text);
internal sealed record SyntaxDiagnostic(string Line, string Column, string Symbol, string Message);

/// <summary>Runs one parse unit within an already admitted host operation.</summary>
internal sealed class WorkbenchExecutionPipeline
{
    internal async Task<ExecutionPresentation> ExecuteAsync(string text, PasswordRequestHandler passwordHost,
        CancellationToken token, CryptoScriptRunner? testRunner = null,
        IEnumerable<StatementNode?>? testStatements = null)
    {
        string? syntax = null;
        IReadOnlyList<SyntaxDiagnostic> syntaxDiagnostics = Array.Empty<SyntaxDiagnostic>();
        var context = InterpreterExecution.AccessState(() =>
        {
            SyntaxErrorList.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
            SyntaxErrorListner.ErrorMessage = new();
            var parsed = ParserBuilder.StringBuild(text).program();
            syntaxDiagnostics = Array.AsReadOnly(SyntaxErrorList.Instance().Select(e =>
                new SyntaxDiagnostic(e.Line, e.Column, e.Symbol, e.Message)).ToArray());
            if (SyntaxErrorListner.SyntaxErrorOccured || LexerErrorListener.LexerErrorOccured)
            {
                syntax = SyntaxErrorListner.ErrorMessage.ToString();
                if (syntax.Length == 0)
                    syntax = string.Join(Environment.NewLine, syntaxDiagnostics.Select(e =>
                        $"SYNTAX ERROR: Token {e.Symbol} (line {e.Line}, column {e.Column}) : {e.Message}"));
            }
            SyntaxErrorListner.SyntaxErrorOccured = false;
            return parsed;
        });
        if (syntax is not null)
            return FinalizePresentation(false, syntax, null, Array.Empty<SemanticError>(), null,
                Array.Empty<ExecutionOutput>(), syntaxDiagnostics);

        var runner = testRunner ?? new CryptoScriptRunner();
        var pending = new List<Task<PasswordResponse>>();
        Task<PasswordResponse> Request(object sender, PasswordRequestEventArgs request)
        {
            var task = passwordHost(sender, request);
            if (task is not null) pending.Add(task);
            return task!;
        }
        var output = new List<ExecutionOutput>();
        void Print(string text) { lock (output) output.Add(new("print", text)); }
        void Info(string text) { lock (output) output.Add(new("info", text)); }
        runner.PasswordRequested += Request;
        OutputOperations.PrintEvent += Print;
        OutputOperations.InfoEvent += Info;
        bool succeeded = false;
        string status;
        SemanticError? error = null;
        try
        {
            try
            {
                if (testStatements is null) await runner.ExecuteAsync(context, token).ConfigureAwait(false);
                else await runner.ExecuteAsync(testStatements, token).ConfigureAwait(false);
                succeeded = true;
                status = "Line successfull parsed";
            }
            catch (SecurePasswordCancelledException) { status = "Script execution cancelled by user."; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { status = "Script execution cancelled."; }
            catch (Exception failure)
            {
                status = failure is SemanticErrorException ? "Script execution aborted (semantic error)." : "Script execution failed.";
                error = (failure as SemanticErrorException)?.SemanticError;
            }
        }
        finally
        {
            // Cancellation may finish the runner before a host callback returns.
            // Keep both admission and the core host gate until all callbacks unwind.
            // Dispose is idempotent, including races with the runner's late observer.
            foreach (var response in pending)
            {
                try { (await response.ConfigureAwait(false))?.Dispose(); }
                catch { /* Observe faults without retaining host exception text. */ }
            }
            runner.PasswordRequested -= Request;
            OutputOperations.PrintEvent -= Print;
            OutputOperations.InfoEvent -= Info;
        }
        var diagnostics = runner.Diagnostics.Select(Copy).ToArray();
        var errorCopy = error is null ? null : Copy(error);
        if (error is not null)
        {
            int index = runner.Diagnostics.ToList().FindIndex(item => ReferenceEquals(item, error));
            if (index >= 0) errorCopy = diagnostics[index];
        }
        return FinalizePresentation(succeeded, status, runner.RunnerId, Array.AsReadOnly(diagnostics), errorCopy,
            output.AsReadOnly(), syntaxDiagnostics);
    }

    private static ExecutionPresentation FinalizePresentation(bool succeeded, string status, Guid? runnerId,
        IReadOnlyList<SemanticError> diagnostics, SemanticError? error,
        IReadOnlyList<ExecutionOutput> output, IReadOnlyList<SyntaxDiagnostic> syntaxDiagnostics)
    {
        IReadOnlyList<VariableModel> variables = Array.Empty<VariableModel>();
        bool captureFailed = false;
        try { variables = InterpreterExecution.AccessState(VariablePresentationCapture.Capture); }
        catch (Exception)
        {
            // Capture is attempted once. Preserve the already completed script
            // outcome and diagnostics without exposing a raw capture exception.
            captureFailed = true;
        }
        return new(succeeded, status, runnerId, diagnostics, error, variables, output, syntaxDiagnostics,
            captureFailed, EffectsPossible: runnerId.HasValue);
    }

    private static SemanticError Copy(SemanticError source) => new()
    {
        Severity = source.Severity,
        DiagnosticCode = source.DiagnosticCode,
        Location = source.Location is null ? null : source.Location with { },
        Message = source.Message,
        Type = source.Type,
        Identifier = source.Identifier,
        FunctionName = source.FunctionName,
        FunctionCall = source.FunctionCall,
        Value = source.Value,
        ErrorCode = source.ErrorCode
    };
}
