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
public sealed partial class WorkbenchAutomationService
{
    public static WorkbenchAutomationService Shared { get; } = new();
    public bool IsBusy => InterpreterHostQueue.IsBusy;

    internal Task<T> RunAsync<T>(Func<WorkbenchExecutionPipeline, Task<T>> operation, CancellationToken token,
        string requestIdentity = "local-host-operation", Func<bool>? validateIntent = null) => RunGuiAsync(operation, token, requestIdentity, validateIntent);

    internal Task<ExecutionPresentation> ExecuteSubmissionAsync(string text, PasswordRequestHandler password,
        CancellationToken token = default, CryptoScriptRunner? testRunner = null,
        IEnumerable<StatementNode?>? testStatements = null) =>
        RunAsync(p => p.ExecuteAsync(text, password, token, testRunner, testStatements), token, "submission\0" + text);

    internal async Task<bool> ExecuteScriptBookAsync(WorkbenchExecutionPipeline admittedPipeline,
        IEnumerable<string> lines, PasswordRequestHandler password,
        Func<string, int, Task> beforeLine, Func<string, int, ExecutionPresentation?, Task<bool>> afterLine,
        CancellationToken token, Func<CryptoScriptRunner>? testRunnerFactory = null,
        IEnumerable<StatementNode?>? testStatements = null)
    {
        lock (sync) if (current.Value is { } job && job.Result.Source == CryptoWorkBench.Automation.Abstractions.ExecutionSource.GuiLine)
            job.Result = job.Result with { Source = CryptoWorkBench.Automation.Abstractions.ExecutionSource.ScriptBookFile };
        int number = 0;
        foreach (string line in lines)
        {
            if (admittedPipeline.ExecutionToken.IsCancellationRequested || token.IsCancellationRequested)
            {
                lock (sync) if (current.Value is { } cancelled) cancelled.Outcome = CryptoWorkBench.Automation.Abstractions.ExecutionState.Cancelled;
                return false;
            }
            ++number;
            ExecutionPresentation? result = null;
            if (!line.StartsWith("out:", StringComparison.Ordinal))
            {
                lock (sync) if (current.Value is { } unit) unit.SourceLineOffset = number - 1;
                await beforeLine(line, number).ConfigureAwait(false);
                // A UI/lifecycle callback can invalidate an accepted editor
                // before its parse unit starts. Observe the service-owned token
                // here, not only at the top of the next physical-line loop.
                if (admittedPipeline.ExecutionToken.IsCancellationRequested || token.IsCancellationRequested)
                {
                    lock (sync) if (current.Value is { } cancelled) cancelled.Outcome = CryptoWorkBench.Automation.Abstractions.ExecutionState.Cancelled;
                    return false;
                }
                result = await admittedPipeline.ExecuteAsync(line, password, token,
                    testRunnerFactory?.Invoke(), testStatements).ConfigureAwait(false);
            }
            bool presented = await afterLine(line, number, result).ConfigureAwait(false);
            if (!presented || result is { HostSucceeded: false }) return false;
        }
        return true;
    }

    internal bool InPresentationObserver => InterpreterHostQueue.InObservation;

    internal void ObservePresentation(Action action)
    {
        // Detached capture models have no subscribers. They are populated under
        // the interpreter lease before first publication on the dispatcher.
        if (editorBridge.Coordinator is { } owner && Avalonia.Threading.Dispatcher.UIThread.CheckAccess() &&
            (editorBridge.Registered || owner.Pending != 0 || owner.Active))
            owner.Observe(action, propagate: true);
        else action();
    }

    internal long PresentationVersion
    {
        get { lock (sync) return active is { Effects: true } ? checked(version + 1) : version; }
    }

    internal long DraftRevision
    {
        get { lock (sync) return checked(InterpreterHostQueue.StateRevision + (active is { Effects: true } ? 1 : 0)); }
    }

    private void ValidateStateAccess(long? expectedVersion, Func<bool>? validate)
    {
        if (InPresentationObserver) throw new ExecutionBusyException();
        lock (sync) if (stopping || (expectedVersion.HasValue && expectedVersion != version)) throw new ExecutionBusyException();
        EnsureRecovered();
        if (validate != null && !validate()) throw new ExecutionBusyException();
    }

    internal void ReadVariableValue(VariableDeclaration declaration, Func<bool> validate, Action<string> present) =>
        InterpreterHostQueue.RunState(() =>
        {
            ValidateStateAccess(null, validate);
            return declaration.Value;
        }, present);

    internal IReadOnlyList<VariableModel> Mutate(Action mutation,
        Action<IReadOnlyList<VariableModel>>? present = null, long? expectedVersion = null,
        Func<bool>? validate = null) => InterpreterHostQueue.RunState(() =>
    {
        ValidateStateAccess(expectedVersion, validate);
        IReadOnlyList<VariableModel> captured;
        try { mutation(); }
        finally { AdvanceVersion(); captured = CaptureMutation(); }
        return captured;
    }, present);

    internal static IReadOnlyList<VariableModel> ClearAdmitted() => Shared.ClearAdmittedState();

    internal IReadOnlyList<VariableModel> ClearAdmittedState() => InterpreterExecution.AccessState(() =>
    {
        VariableDictionary.Instance().Clear();
        AdvanceVersion();
        return CaptureMutation();
    });
}
