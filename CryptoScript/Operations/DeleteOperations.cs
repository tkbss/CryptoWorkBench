using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScript.Operations;

public static class DeleteOperations
{
    internal static bool IsDelete(string name) => name.Equals("Delete", StringComparison.OrdinalIgnoreCase);

    internal static IEnumerable<Argument> ResolveArguments(FunctionCallExpressionNode call)
    {
        if (call.Arguments.Count == 0)
            throw new SnapshotArgumentException("Delete requires at least one variable or ALL.");
        if (call.Arguments.Any(a => a is not VariableArgumentNode))
            throw new SnapshotArgumentException("Delete accepts only variable references or ALL.");
        var names = call.Arguments.Cast<VariableArgumentNode>().Select(a => a.Identifier).ToArray();
        if (names.Contains("ALL", StringComparer.Ordinal))
        {
            if (names.Length != 1) throw new SnapshotArgumentException("Delete ALL cannot be combined with other selectors.");
            return new Argument[] { new ArgumentSnapshotSelector { Identifier = "ALL" } };
        }
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new SnapshotArgumentException("Delete does not accept duplicate selectors.");
        var dictionary = VariableDictionary.Instance();
        var arguments = new List<Argument>();
        foreach (string name in names)
        {
            if (!dictionary.Contains(name))
                throw new SnapshotArgumentException("Delete references an unknown variable.", SemanticDiagnosticCode.UnknownVariable);
            arguments.Add(new ArgumentVariable { Id = dictionary.Get(name) });
        }
        return arguments;
    }

    public static VariableDeclaration? Delete(OperationInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var dictionary = VariableDictionary.Instance();
        var existing = dictionary.GetVariables().ToArray();
        bool all = invocation.Arguments.Count == 1 && invocation.Arguments[0] is
            { Kind: ResolvedCallArgumentKind.SnapshotSelector, Value: "ALL" };
        var selected = new HashSet<VariableDeclaration>(ReferenceEqualityComparer.Instance);
        if (all) selected.UnionWith(existing);
        else
        {
            if (invocation.Arguments.Count == 0)
                throw new SnapshotArgumentException("Delete requires at least one variable or ALL.");
            foreach (var argument in invocation.Arguments)
            {
                if (argument.Kind != ResolvedCallArgumentKind.Variable || argument.SourceVariable is not { } variable ||
                    !ReferenceEquals(dictionary.Get(variable.Id), variable))
                    throw new SnapshotArgumentException("Delete requires references to currently declared variables.");
                if (!selected.Add(variable)) throw new SnapshotArgumentException("Delete does not accept duplicate selectors.");
            }
        }
        if (existing.OfType<ParameterVariableDeclaration>().Any(p => !selected.Contains(p) &&
            p.HeaderReference is { } header && selected.Contains(header)))
            throw new SnapshotArgumentException("Delete reference conflict: a remaining PARAM references a selected TR31H object.");
        var remaining = existing.Where(v => !selected.Contains(v)).ToArray();
        dictionary.PublishBatch(remaining);
        return null;
    }
}
