using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services.Automation;

/// <summary>The shared host coordinator. No editor/viewmodel or transport ownership.</summary>
public sealed class WorkbenchAutomationService
{
    public static WorkbenchAutomationService Shared { get; } = new();
    private readonly WorkbenchExecutionPipeline pipeline = new();
    public bool IsBusy => InterpreterHostQueue.IsBusy;

    internal Task<T> RunAsync<T>(Func<WorkbenchExecutionPipeline, Task<T>> operation, CancellationToken token) =>
        InterpreterHostQueue.RunAsync(() => operation(pipeline), token);

    internal Task<ExecutionPresentation> ExecuteSubmissionAsync(string text, PasswordRequestHandler password,
        CancellationToken token = default, CryptoScriptRunner? testRunner = null,
        IEnumerable<StatementNode?>? testStatements = null) =>
        RunAsync(p => p.ExecuteAsync(text, password, token, testRunner, testStatements), token);

    internal async Task<bool> ExecuteScriptBookAsync(WorkbenchExecutionPipeline admittedPipeline,
        IEnumerable<string> lines, PasswordRequestHandler password,
        Func<string, int, Task> beforeLine, Func<string, int, ExecutionPresentation?, Task<bool>> afterLine,
        CancellationToken token, Func<CryptoScriptRunner>? testRunnerFactory = null,
        IEnumerable<StatementNode?>? testStatements = null)
    {
        int number = 0;
        foreach (string line in lines)
        {
            if (token.IsCancellationRequested) return false;
            ++number;
            ExecutionPresentation? result = null;
            if (!line.StartsWith("out:", StringComparison.Ordinal))
            {
                await beforeLine(line, number).ConfigureAwait(false);
                result = await admittedPipeline.ExecuteAsync(line, password, token,
                    testRunnerFactory?.Invoke(), testStatements).ConfigureAwait(false);
            }
            bool presented = await afterLine(line, number, result).ConfigureAwait(false);
            if (!presented || result is { HostSucceeded: false }) return false;
        }
        return true;
    }

    internal IReadOnlyList<VariableModel> Mutate(Action mutation,
        Action<IReadOnlyList<VariableModel>>? present = null) => InterpreterHostQueue.RunState(() =>
    {
        mutation();
        return VariablePresentationCapture.Capture();
    }, present);

    internal static IReadOnlyList<VariableModel> ClearAdmitted() => InterpreterExecution.AccessState(() =>
    {
        VariableDictionary.Instance().Clear();
        return VariablePresentationCapture.Capture();
    });
}
