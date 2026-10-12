using CryptoScript.ErrorListner;

namespace CryptoWorkBenchAvalonia.Models;

/// <summary>Immutable presentation of a diagnostic at execution completion.</summary>
public sealed class DiagnosticDisplayItem
{
    public SemanticDiagnosticSeverity Severity { get; }
    public string Text { get; }
    public int? ScriptBookLine { get; }
    public bool IsWarning => Severity == SemanticDiagnosticSeverity.Warning;
    public bool IsError => Severity != SemanticDiagnosticSeverity.Warning;
    public bool HasScriptBookLine => ScriptBookLine.HasValue;

    public DiagnosticDisplayItem(SemanticError diagnostic, int? scriptBookLine = null)
    {
        Text = SemanticDiagnosticFormatter.Format(diagnostic);
        Severity = diagnostic.Severity;
        ScriptBookLine = scriptBookLine;
    }
}
