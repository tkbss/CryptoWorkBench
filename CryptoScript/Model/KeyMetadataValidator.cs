using CryptoScript.ErrorListner;
using CryptoScript.Variables;

namespace CryptoScript.Model;

/// <summary>
/// Evaluates declared metadata only. Does not validate technical executability, mutate
/// keys, select an algorithm, or assess EP2/PCI conformity. Not wired into algorithms.
/// </summary>
public static class KeyMetadataValidator
{
    /// <summary>
    /// Returns warnings in type-then-usage order and optionally reports the same objects.
    /// Unknown algorithms and unspecified usage impose no semantic restriction.
    /// A supplied sink owns reporting behavior; its exceptions are not suppressed.
    /// </summary>
    public static IReadOnlyList<SemanticError> Validate(KeyVariableDeclaration key,
        KeyAlgorithm expectedAlgorithm, KeyUsage requiredUsage, string functionName,
        string mechanism, ISemanticDiagnosticSink? diagnosticSink = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(functionName);
        ArgumentNullException.ThrowIfNull(mechanism);
        if (!Enum.IsDefined(expectedAlgorithm))
            throw new ArgumentOutOfRangeException(nameof(expectedAlgorithm));
        KeyUsage defined = Enum.GetValues<KeyUsage>().Aggregate(KeyUsage.None, (all, value) => all | value);
        if ((requiredUsage & ~defined) != KeyUsage.None)
            throw new ArgumentOutOfRangeException(nameof(requiredUsage));

        var diagnostics = new List<SemanticError>();
        if (expectedAlgorithm != KeyAlgorithm.Unknown && key.KeyType.Algorithm != KeyAlgorithm.Unknown &&
            key.KeyType.Algorithm != expectedAlgorithm)
            Add(SemanticDiagnosticCode.KeyTypeConflict,
                $"Declared key algorithm {key.KeyType.Algorithm} conflicts with expected algorithm {expectedAlgorithm} for {functionName} ({mechanism}).");
        if (!key.Usage.Allows(requiredUsage))
            Add(SemanticDiagnosticCode.KeyUsageConflict,
                $"Declared key usage {key.Usage.AllowedUsages} does not allow {requiredUsage} for {functionName} ({mechanism}).");
        return diagnostics.AsReadOnly();

        void Add(SemanticDiagnosticCode code, string message)
        {
            var diagnostic = new SemanticError
            {
                Severity = SemanticDiagnosticSeverity.Warning,
                DiagnosticCode = code,
                Type = "KeyMetadata",
                FunctionName = functionName,
                Identifier = key.Id ?? string.Empty,
                Location = new DiagnosticLocation(FunctionName: functionName, Mechanism: mechanism,
                    Identifier: string.IsNullOrEmpty(key.Id) ? null : key.Id),
                Message = message
            };
            diagnostics.Add(diagnostic);
            diagnosticSink?.Report(diagnostic);
        }
    }
}
