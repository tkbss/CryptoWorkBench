using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CryptoScript.ErrorListner;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Models;

namespace CryptoWorkBenchAvalonia.Services.Automation;

// No raw message, exception, expression, value, parameter or GUI model crosses this boundary.
internal static class AutomationProjection
{
    internal static VariableSnapshot Variables(IReadOnlyList<VariableModel> variables, string version, DateTimeOffset now)
    {
        bool truncated = variables.Count > AutomationContracts.MaxCollectionEntries;
        var items = variables.Take(AutomationContracts.MaxCollectionEntries).Select(v =>
        {
            var id = v.Identifier;
            if (id.Length > 256) { truncated = true; id = id[..256]; }
            return new VariableInfo(id, v.Type is "VAR" or "KEY" or "PATH" or "PARAM" or "SECRET" or "TR31H" ? v.Type : "Unknown",
                v.IsKey, v.Algorithm is "AES" or "TDEA" or "HMAC" or "RSA" or "EC" ? v.Algorithm : null,
                v.Material is "Raw" or "Symmetric" or "Public" or "Private" or "Secret" ? v.Material : null,
                v.HasKeySize && int.TryParse(v.KeySize, out int bits) && bits >= 0 ? bits : null, null, false);
        }).ToImmutableArray();
        return new(version, now, false, items, truncated);
    }

    internal static IEnumerable<Diagnostic> Diagnostics(ExecutionPresentation result)
    {
        foreach (var syntax in result.SyntaxDiagnostics)
            yield return new(DiagnosticKind.Syntax, DiagnosticSeverity.Error, "SyntaxError", "The script contains a syntax error.",
                int.TryParse(syntax.Line, out int line) && line > 0 ? line : null,
                int.TryParse(syntax.Column, out int column) && column >= 0 ? column : null,
                null, null, null, null, null, null);
        foreach (var error in result.Diagnostics)
        {
            var location = error.Location;
            yield return new(DiagnosticKind.Semantic,
                error.Severity == SemanticDiagnosticSeverity.Warning ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error,
                Enum.IsDefined(error.DiagnosticCode) ? error.DiagnosticCode.ToString() : "Unclassified",
                "A script diagnostic was reported.", null, null, location?.StatementIndex,
                location?.ExecutionId, location?.OperationId, location?.ParentOperationId, null, null);
        }
        if (result.CaptureFailed)
            yield return new(DiagnosticKind.Host, DiagnosticSeverity.Error, "CaptureFailed",
                "Variable state capture failed. The previous snapshot is stale.", null, null, null, null, null, null, null, null);
    }
}
