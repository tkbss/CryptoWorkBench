using CryptoScript.ErrorListner;

namespace CryptoScript.Model;

/// <summary>Explicit, execution-owned context; operation IDs are execution-wide and one-based.</summary>
internal sealed class SemanticDiagnosticExecution
{
    private readonly SemanticDiagnosticSink sink = new();
    private readonly Dictionary<SemanticError, DiagnosticLocation> locations = new(ReferenceEqualityComparer.Instance);
    private readonly Action<FunctionCall, ISemanticDiagnosticSink>? beforeOperation;
    private readonly Func<string, CryptoAlgorithm.CryptoAlgorithm>? algorithmFactory;
    private int operationId;

    internal SemanticDiagnosticExecution(Action<FunctionCall, ISemanticDiagnosticSink>? beforeOperation = null,
        Func<string, CryptoAlgorithm.CryptoAlgorithm>? algorithmFactory = null)
    {
        this.beforeOperation = beforeOperation;
        this.algorithmFactory = algorithmFactory;
    }

    internal Guid ExecutionId { get; } = Guid.NewGuid();
    internal IReadOnlyList<SemanticError> GetSnapshot() => sink.GetSnapshot();

    internal DiagnosticLocation BeginOperation(DiagnosticLocation parent, string functionName) =>
        new(ExecutionId, parent.StatementIndex, ++operationId, parent.OperationId, functionName);

    internal void BeforeOperation(FunctionCall call) =>
        beforeOperation?.Invoke(call, call.Invocation!.DiagnosticSink!);

    internal ISemanticDiagnosticSink CreateSink(DiagnosticLocation location) =>
        new LocatedSink(this, location);

    internal Func<OperationInvocation, Variables.VariableDeclaration?> CreateOperation(string functionName) =>
        OperationFactory.CreateInvocationOperation(functionName, algorithmFactory);

    internal void Capture(List<SemanticError> errors, int start, DiagnosticLocation location)
    {
        for (int i = start; i < errors.Count; i++)
            CaptureError(errors[i], location);
    }

    // Legacy-list and exception transport must never relabel another execution's diagnostic.
    internal void CaptureError(SemanticError diagnostic, DiagnosticLocation location)
    {
        if (diagnostic.Location?.ExecutionId is Guid origin && origin != ExecutionId)
            return;
        Report(diagnostic, location);
    }

    /// <summary>
    /// Explicit reporting assigns this execution's first location for the object.
    /// Reuse in a later execution updates the same mutable object, including older snapshots.
    /// </summary>
    internal void Report(SemanticError diagnostic, DiagnosticLocation location,
        SemanticDiagnosticCode? code = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (!locations.TryGetValue(diagnostic, out var firstLocation))
        {
            // A previous execution's assigned Location is not fresh producer metadata.
            // Reference identity also distinguishes an explicitly replaced, equal-valued record.
            var supplied = ReferenceEquals(diagnostic.Location, diagnostic.ExecutionAssignedLocation)
                ? null : diagnostic.Location;
            // The current operation owns context; fresh metadata only fills missing fields.
            firstLocation = location with
            {
                Mechanism = location.Mechanism ?? supplied?.Mechanism,
                Identifier = location.Identifier ?? supplied?.Identifier
            };
            locations.Add(diagnostic, firstLocation);
        }
        diagnostic.Location = firstLocation;
        diagnostic.ExecutionAssignedLocation = firstLocation;
        if (diagnostic.DiagnosticCode == SemanticDiagnosticCode.Unclassified)
            diagnostic.DiagnosticCode = code ?? diagnostic.Type switch
            {
                "Variable" => SemanticDiagnosticCode.UnknownVariable,
                "Argument:Mechanism" => SemanticDiagnosticCode.InvalidMechanism,
                "Declaration" => SemanticDiagnosticCode.InvalidDeclaration,
                "FunctionContract" => SemanticDiagnosticCode.FunctionContractViolation,
                "FunctionCall" => SemanticDiagnosticCode.FunctionExecutionFailed,
                _ => SemanticDiagnosticCode.Unclassified
            };
        sink.Report(diagnostic);
    }

    private sealed class LocatedSink(SemanticDiagnosticExecution execution, DiagnosticLocation location)
        : ISemanticDiagnosticSink
    {
        public void Report(SemanticError diagnostic)
        {
            ArgumentNullException.ThrowIfNull(diagnostic);
            execution.Report(diagnostic, location);
        }
    }
}
