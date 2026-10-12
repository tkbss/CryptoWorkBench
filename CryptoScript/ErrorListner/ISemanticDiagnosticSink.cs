namespace CryptoScript.ErrorListner;

public interface ISemanticDiagnosticSink
{
    /// <summary>
    /// Collects a diagnostic without changing execution control flow or throwing
    /// SemanticErrorException. Callers are responsible for safe diagnostic content.
    /// </summary>
    void Report(SemanticError diagnostic);
}
