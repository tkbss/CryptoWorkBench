using System.Globalization;
using System.Text;

namespace CryptoScript.ErrorListner;

/// <summary>Formats diagnostics as plain text without mutation, redaction or external output.</summary>
public static class SemanticDiagnosticFormatter
{
    /// <summary>
    /// Formats the current diagnostic contents. Structural separators use LF and no trailing
    /// separator is added. Message contents, including their own line endings, are preserved.
    /// Location names take precedence over legacy names; null or empty names use the fallback.
    /// </summary>
    /// <exception cref="ArgumentNullException">The diagnostic is null.</exception>
    public static string Format(SemanticError diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        var text = new StringBuilder();
        text.Append(EnumText(diagnostic.Severity)).Append(" [")
            .Append(EnumText(diagnostic.DiagnosticCode)).Append(']');
        var location = diagnostic.Location;
        AppendContext(text, "ExecutionId", location?.ExecutionId?.ToString("D"));
        AppendContext(text, "Statement", location?.StatementIndex?.ToString(CultureInfo.InvariantCulture));
        AppendContext(text, "Operation", location?.OperationId?.ToString(CultureInfo.InvariantCulture));
        AppendContext(text, "ParentOperation", location?.ParentOperationId?.ToString(CultureInfo.InvariantCulture));
        AppendContext(text, "Function", Fallback(location?.FunctionName, diagnostic.FunctionName));
        AppendContext(text, "Mechanism", location?.Mechanism);
        AppendContext(text, "Identifier", Fallback(location?.Identifier, diagnostic.Identifier));
        text.Append('\n').Append(string.IsNullOrEmpty(diagnostic.Message)
            ? "No diagnostic message." : diagnostic.Message);
        if (diagnostic.ErrorCode is { } errorCode)
            text.Append("\nContract: ").Append(EnumText(errorCode));
        if (!string.IsNullOrEmpty(diagnostic.FunctionCall))
            text.Append("\nCall: ").Append(diagnostic.FunctionCall);
        return text.ToString();
    }

    /// <summary>
    /// Formats a source once in input order, preserving repeated entries. Blocks are joined
    /// with two LF characters; existing content is not trimmed. An empty source yields empty text.
    /// </summary>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentException">The source contains a null diagnostic.</exception>
    public static string FormatMany(IEnumerable<SemanticError> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var text = new StringBuilder();
        var first = true;
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic is null)
                throw new ArgumentException("The source contains a null diagnostic.", nameof(diagnostics));
            if (!first)
                text.Append("\n\n");
            text.Append(Format(diagnostic));
            first = false;
        }
        return text.ToString();
    }

    private static string EnumText<T>(T value) where T : struct, Enum =>
        Enum.GetName(value) ?? Convert.ToInt64(value, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture);

    private static string? Fallback(string? preferred, string? legacy) =>
        string.IsNullOrEmpty(preferred) ? legacy : preferred;

    private static void AppendContext(StringBuilder text, string label, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            text.Append(" | ").Append(label).Append('=').Append(value);
    }
}
