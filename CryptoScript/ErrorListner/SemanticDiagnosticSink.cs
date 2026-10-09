namespace CryptoScript.ErrorListner;

/// <summary>
/// An ordered collection owned by one execution. Reporting the same object more than
/// once is ignored; distinct objects with equal content are retained.
/// This collector neither generates messages nor sanitizes supplied diagnostic content.
/// </summary>
public sealed class SemanticDiagnosticSink : ISemanticDiagnosticSink
{
    private readonly List<SemanticError> diagnostics = new();
    private readonly HashSet<SemanticError> reported = new(ReferenceEqualityComparer.Instance);

    public void Report(SemanticError diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (reported.Add(diagnostic))
            diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Returns an immutable snapshot of collection membership and order.
    /// Diagnostic objects retain their reference identity and remain mutable for compatibility.
    /// Later reports do not change this snapshot.
    /// </summary>
    public IReadOnlyList<SemanticError> GetSnapshot() => Array.AsReadOnly(diagnostics.ToArray());
}
