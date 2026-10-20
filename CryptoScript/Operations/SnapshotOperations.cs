using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScript.Operations;

internal sealed class SaveArgumentException(string message,
    SemanticDiagnosticCode code = SemanticDiagnosticCode.FunctionContractViolation) : Exception(message)
{
    internal SemanticDiagnosticCode Code { get; } = code;
}

public static class SnapshotOperations
{
    internal static bool IsSave(string name) => name.Equals("Save", StringComparison.OrdinalIgnoreCase);

    // Contextual ALL selection: no lexer keyword and no value-based variable lookup.
    // Validate syntax before evaluating any argument, so nested calls cannot have side effects.
    internal static IEnumerable<Argument> ResolveSaveArguments(FunctionCallExpressionNode call)
    {
        if (call.Arguments.Count < 2)
            throw new SaveArgumentException("Save requires a PATH reference and at least one variable or ALL.");
        if (call.Arguments.Any(a => a is not VariableArgumentNode))
            throw new SaveArgumentException("Save accepts only variable references or the ALL selector.");
        var names = call.Arguments.Cast<VariableArgumentNode>().Select(a => a.Identifier).ToArray();
        bool all = names.Skip(1).Contains("ALL", StringComparer.Ordinal);
        if (all && names.Length != 2)
            throw new SaveArgumentException("Save ALL cannot be combined with other selections.");
        var dictionary = VariableDictionary.Instance();
        var arguments = new List<Argument>();
        for (int i = 0; i < names.Length; i++)
        {
            if (i == 1 && all)
            {
                arguments.AddRange(dictionary.GetVariables().ToArray()
                    .Select(v => new ArgumentVariable { Id = v }));
                break;
            }
            if (!dictionary.Contains(names[i]))
                throw new SaveArgumentException($"Save argument {i + 1} references an undeclared variable.",
                    SemanticDiagnosticCode.UnknownVariable);
            arguments.Add(new ArgumentVariable { Id = dictionary.Get(names[i]) });
        }
        return arguments;
    }

    public static VariableDeclaration? Save(OperationInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var arguments = invocation.Arguments;
        if (arguments.Count < 2 ||
            arguments[0].Kind != ResolvedCallArgumentKind.Variable ||
            arguments[0].SourceVariable is not PathVariableDeclaration { Type: CryptoTypePath } path)
            throw new SaveArgumentException("Save requires a PATH variable as its first argument.");
        var dictionary = VariableDictionary.Instance();
        foreach (var argument in arguments)
            if (argument.Kind != ResolvedCallArgumentKind.Variable ||
                argument.SourceVariable is not { } variable ||
                !ReferenceEquals(dictionary.Get(variable.Id), variable))
                throw new SaveArgumentException("Save requires references to currently declared variables.");
        var selected = arguments.Skip(1).Select(a => a.SourceVariable!).ToArray();
        if (selected.Distinct(ReferenceEqualityComparer.Instance).Count() != selected.Length ||
            selected.Select(v => v.Id).Distinct(StringComparer.Ordinal).Count() != selected.Length)
            throw new SaveArgumentException("Save does not accept duplicate selected variables.");
        new VariableSnapshotFileStore().Write(path, selected, VariableSnapshotWriteMode.CreateNew);
        return null;
    }
}
