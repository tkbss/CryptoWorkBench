using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScript.Operations;

internal sealed class SnapshotArgumentException(string message,
    SemanticDiagnosticCode code = SemanticDiagnosticCode.FunctionContractViolation) : Exception(message)
{
    internal SemanticDiagnosticCode Code { get; } = code;
}

public static class SnapshotOperations
{
    internal static bool IsSave(string name) => name.Equals("Save", StringComparison.OrdinalIgnoreCase);
    internal static bool IsLoad(string name) => name.Equals("Load", StringComparison.OrdinalIgnoreCase);
    internal static bool IsSnapshot(string name) => IsSave(name) || IsLoad(name);
    internal static string CanonicalName(string name) => IsLoad(name) ? "Load" : "Save";

    internal static IEnumerable<Argument> ResolveLoadArguments(FunctionCallExpressionNode call)
    {
        if (call.Arguments.Count == 0 || call.Arguments[0] is not VariableArgumentNode source)
            throw new SnapshotArgumentException("Load requires a PATH variable as its first argument.");
        // Validate the entire shape before evaluating anything. In particular no nested
        // call, expression or generic parameter evaluator runs during import resolution.
        if (call.Arguments.Skip(1).Any(argument => argument is not VariableArgumentNode and not ParameterArgumentNode))
            throw new SnapshotArgumentException("Load accepts only snapshot identifiers and a final #ONCONFLICT parameter.");
        var dictionary = VariableDictionary.Instance();
        if (!dictionary.Contains(source.Identifier))
            throw new SnapshotArgumentException("Load references an undeclared PATH variable.", SemanticDiagnosticCode.UnknownVariable);
        if (dictionary.Get(source.Identifier) is not PathVariableDeclaration { Type: CryptoTypePath } path)
            throw new SnapshotArgumentException("Load requires a PATH variable as its first argument.");
        var arguments = new List<Argument> { new ArgumentVariable { Id = path } };
        foreach (var argument in call.Arguments.Skip(1))
            arguments.Add(argument switch
            {
                VariableArgumentNode selector => new ArgumentSnapshotSelector { Identifier = selector.Identifier },
                ParameterArgumentNode parameter => new ArgumentParameter { Type = parameter.TypeName, Value = parameter.RawValue },
                _ => throw new InvalidOperationException()
            });
        return arguments;
    }

    public static VariableDeclaration? Load(OperationInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var arguments = invocation.Arguments;
        var dictionary = VariableDictionary.Instance();
        if (arguments.Count == 0 || arguments[0].Kind != ResolvedCallArgumentKind.Variable ||
            arguments[0].SourceVariable is not PathVariableDeclaration { Type: CryptoTypePath } path ||
            !ReferenceEquals(dictionary.Get(path.Id), path))
            throw new SnapshotArgumentException("Load requires a currently declared PATH variable as its first argument.");

        // S02 validates the entire file and S01 reconstructs the entire detached graph.
        // Selection never bypasses validation of unselected roots or dependencies.
        var roots = new VariableSnapshotFileStore().Read(path);
        bool replace = false;
        var names = new List<string>();
        for (int i = 1; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (argument.Kind == ResolvedCallArgumentKind.SnapshotSelector)
                names.Add(argument.Value!);
            else if (argument.Kind == ResolvedCallArgumentKind.Parameter && i == arguments.Count - 1 &&
                argument.Value is "#ONCONFLICT:REJECT" or "#ONCONFLICT:REPLACE")
                replace = argument.Value == "#ONCONFLICT:REPLACE";
            else
                throw new SnapshotArgumentException("Load accepts #ONCONFLICT:REJECT or #ONCONFLICT:REPLACE once, as the last argument.");
        }
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count)
            throw new SnapshotArgumentException("Load does not accept duplicate selectors.");
        bool all = names.Contains("ALL", StringComparer.Ordinal);
        if (all && names.Count != 1)
            throw new SnapshotArgumentException("Load ALL cannot be combined with other selections.");
        var byName = roots.ToDictionary(variable => variable.Id, StringComparer.Ordinal);
        if (names.Count != 0 && !all && names.Any(name => !byName.ContainsKey(name)))
            throw new SnapshotArgumentException("Load selection references an unknown snapshot variable.", SemanticDiagnosticCode.UnknownVariable);
        var selected = names.Count == 0 || all ? roots.ToArray() : names.Select(name => byName[name]).ToArray();
        var existing = dictionary.GetVariables().ToArray();
        var selectedNames = selected.Select(variable => variable.Id).ToHashSet(StringComparer.Ordinal);
        var replaced = existing.Where(variable => selectedNames.Contains(variable.Id))
            .ToHashSet(ReferenceEqualityComparer.Instance);
        if (!replace && replaced.Count != 0)
            throw new SnapshotArgumentException("Load rejected a variable name conflict.");
        // PARAM.HeaderReference is the only direct variable-to-variable reference in
        // the supported model. Use object identity, never an identifier-based relink.
        if (replace && existing.OfType<ParameterVariableDeclaration>().Any(parameter =>
            !replaced.Contains(parameter) && parameter.HeaderReference is { } header && replaced.Contains(header)))
            throw new SnapshotArgumentException("Load replacement conflicts with a reference held by an unchanged variable.");

        // Preserve imported objects, metadata and shared/embedded references unchanged.
        // Unselected dependencies stay reachable internally, never become implicit roots.
        var prepared = existing.ToDictionary(variable => variable.Id, StringComparer.Ordinal);
        foreach (var variable in selected) prepared[variable.Id] = variable;
        dictionary.PublishBatch(prepared.Values);
        return null;
    }

    // Contextual ALL selection: no lexer keyword and no value-based variable lookup.
    // Validate syntax before evaluating any argument, so nested calls cannot have side effects.
    internal static IEnumerable<Argument> ResolveSaveArguments(FunctionCallExpressionNode call)
    {
        if (call.Arguments.Count < 2)
            throw new SnapshotArgumentException("Save requires a PATH reference and at least one variable or ALL.");
        if (call.Arguments.Any(a => a is not VariableArgumentNode))
            throw new SnapshotArgumentException("Save accepts only variable references or the ALL selector.");
        var names = call.Arguments.Cast<VariableArgumentNode>().Select(a => a.Identifier).ToArray();
        bool all = names.Skip(1).Contains("ALL", StringComparer.Ordinal);
        if (all && names.Length != 2)
            throw new SnapshotArgumentException("Save ALL cannot be combined with other selections.");
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
                throw new SnapshotArgumentException($"Save argument {i + 1} references an undeclared variable.",
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
            throw new SnapshotArgumentException("Save requires a PATH variable as its first argument.");
        var dictionary = VariableDictionary.Instance();
        foreach (var argument in arguments)
            if (argument.Kind != ResolvedCallArgumentKind.Variable ||
                argument.SourceVariable is not { } variable ||
                !ReferenceEquals(dictionary.Get(variable.Id), variable))
                throw new SnapshotArgumentException("Save requires references to currently declared variables.");
        var selected = arguments.Skip(1).Select(a => a.SourceVariable!).ToArray();
        if (selected.Distinct(ReferenceEqualityComparer.Instance).Count() != selected.Length ||
            selected.Select(v => v.Id).Distinct(StringComparer.Ordinal).Count() != selected.Length)
            throw new SnapshotArgumentException("Save does not accept duplicate selected variables.");
        new VariableSnapshotFileStore().Write(path, selected, VariableSnapshotWriteMode.CreateNew);
        return null;
    }
}
