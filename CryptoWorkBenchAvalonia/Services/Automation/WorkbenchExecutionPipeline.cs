using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoWorkBenchAvalonia.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoWorkBench.Automation.Abstractions;

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
    internal ExecutionState Outcome { get; init; }
}
internal sealed record ExecutionOutput(string Kind, string Text);
internal sealed record SyntaxDiagnostic(string Line, string Column, string Symbol, string Message);

/// <summary>Runs one parse unit within an already admitted host operation.</summary>
internal sealed class WorkbenchExecutionPipeline
{
    internal CancellationToken ExecutionToken { get; init; }
    internal Action<ExecutionPresentation>? Publish { get; init; }
    internal Action<bool>? WaitingForUser { get; init; }
    internal Action<CancellationReason>? CancellationObserved { get; init; }
    internal Action? BeforeRunnerStart { get; set; }
    internal async Task<ExecutionPresentation> ExecuteAsync(string text, PasswordRequestHandler passwordHost,
        CancellationToken token, CryptoScriptRunner? testRunner = null,
        IEnumerable<StatementNode?>? testStatements = null)
    {
        if (Publish is not null) token = ExecutionToken;
        if (token.IsCancellationRequested)
            return Finish(FinalizePresentation(false, "Script execution cancelled.", null,
                Array.Empty<SemanticError>(), null, Array.Empty<ExecutionOutput>(),
                Array.Empty<SyntaxDiagnostic>(), false) with { Outcome = ExecutionState.Cancelled });
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
            return Finish(FinalizePresentation(false, syntax, null, Array.Empty<SemanticError>(), null,
                Array.Empty<ExecutionOutput>(), syntaxDiagnostics, false) with { Outcome = ExecutionState.SyntaxError });

        var runner = testRunner ?? new CryptoScriptRunner();
        var previousExecution = runner.ExecutionId;
        var pending = new List<Task<PasswordResponse>>();
        Task<PasswordResponse> Request(object sender, PasswordRequestEventArgs request)
        {
            WaitingForUser?.Invoke(true);
            async Task<PasswordResponse> AwaitResponse()
            {
                try { return await passwordHost(sender, request).ConfigureAwait(false); }
                finally { WaitingForUser?.Invoke(false); }
            }
            var task = AwaitResponse();
            pending.Add(task);
            return task;
        }
        var output = new List<ExecutionOutput>();
        void Print(string text) { lock (output) output.Add(new("print", text)); }
        void Info(string text) { lock (output) output.Add(new("info", text)); }
        runner.PasswordRequested += Request;
        OutputOperations.PrintEvent += Print;
        OutputOperations.InfoEvent += Info;
        bool succeeded = false;
        ExecutionState outcome = ExecutionState.Failed;
        string status;
        SemanticError? error = null;
        try
        {
            try
            {
                BeforeRunnerStart?.Invoke();
                if (testStatements is null) await runner.ExecuteAsync(context, token).ConfigureAwait(false);
                else await runner.ExecuteAsync(testStatements, token).ConfigureAwait(false);
                succeeded = true;
                outcome = ExecutionState.Completed;
                status = "Line successfull parsed";
            }
            catch (SecurePasswordCancelledException)
            {
                CancellationObserved?.Invoke(CancellationReason.User);
                outcome = ExecutionState.Cancelled; status = "Script execution cancelled by user.";
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { outcome = ExecutionState.Cancelled; status = "Script execution cancelled."; }
            catch (Exception failure)
            {
                status = failure is SemanticErrorException ? "Script execution aborted (semantic error)." : "Script execution failed.";
                error = (failure as SemanticErrorException)?.SemanticError;
                outcome = failure is SemanticErrorException ? ExecutionState.Aborted : ExecutionState.Failed;
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
            WaitingForUser?.Invoke(false);
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
        return Finish(FinalizePresentation(succeeded, status, runner.RunnerId, Array.AsReadOnly(diagnostics), errorCopy,
            output.AsReadOnly(), syntaxDiagnostics, runner.ExecutionId != previousExecution) with { Outcome = outcome });
    }

    private ExecutionPresentation Finish(ExecutionPresentation result) { Publish?.Invoke(result); return result; }

    private static ExecutionPresentation FinalizePresentation(bool succeeded, string status, Guid? runnerId,
        IReadOnlyList<SemanticError> diagnostics, SemanticError? error,
        IReadOnlyList<ExecutionOutput> output, IReadOnlyList<SyntaxDiagnostic> syntaxDiagnostics, bool executionStarted)
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
            captureFailed, EffectsPossible: executionStarted);
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
