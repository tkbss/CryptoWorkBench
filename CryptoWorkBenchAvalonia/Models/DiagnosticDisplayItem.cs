using CryptoScript.ErrorListner;
using System;
using AvaloniaEdit.Document;

namespace CryptoWorkBenchAvalonia.Models;

public enum DiagnosticCategory { SyntaxError, SemanticError, SemanticWarning, ExecutionError }
public enum DiagnosticFilter { All, Errors, Warnings }

/// <summary>Immutable presentation of a diagnostic at execution completion.</summary>
public sealed class DiagnosticDisplayItem
{
    public SemanticDiagnosticSeverity Severity { get; }
    public string Text { get; }
    public int? ScriptBookLine { get; }
    public DiagnosticCategory Category { get; }
    public Guid? DocumentId { get; }
    public Guid? ExecutionId { get; }
    public int? SourceLine { get; }
    public int? SourceColumn { get; }
    public ITextSourceVersion? SourceRevision { get; }
    public int Sequence { get; }
    public string CategoryText => Category switch
    {
        DiagnosticCategory.SyntaxError => "Syntax Error",
        DiagnosticCategory.SemanticWarning => "Semantic Warning",
        DiagnosticCategory.ExecutionError => "Execution Error",
        _ => "Semantic Error"
    };
    public bool HasSource => DocumentId.HasValue && SourceRevision != null && SourceLine is > 0;
    public string SourceLabel => HasSource
        ? $"Go to line {SourceLine}" + (SourceColumn.HasValue ? $", column {SourceColumn}" : " (column unknown)")
        : "Source unknown";
    public bool IsWarning => Severity == SemanticDiagnosticSeverity.Warning;
    public bool IsError => Severity != SemanticDiagnosticSeverity.Warning;
    public bool HasScriptBookLine => ScriptBookLine.HasValue;

    public DiagnosticDisplayItem(SemanticError diagnostic, int? scriptBookLine = null)
    {
        Text = SemanticDiagnosticFormatter.Format(diagnostic);
        Severity = diagnostic.Severity;
        Category = IsWarning ? DiagnosticCategory.SemanticWarning : DiagnosticCategory.SemanticError;
        ScriptBookLine = scriptBookLine;
    }

    internal DiagnosticDisplayItem(DiagnosticCategory category, SemanticDiagnosticSeverity severity, string text,
        Guid? documentId, Guid? executionId, ITextSourceVersion? revision, int? line, int? column,
        int sequence, int? scriptBookLine = null)
    {
        Category = category; Severity = severity; Text = text;
        DocumentId = documentId; ExecutionId = executionId; SourceRevision = revision;
        SourceLine = line; SourceColumn = column; Sequence = sequence; ScriptBookLine = scriptBookLine;
    }
}
