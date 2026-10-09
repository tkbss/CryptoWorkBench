using System.Collections.ObjectModel;
using CryptoScript.Variables;

namespace CryptoScript.Model;

public enum ResolvedCallArgumentKind
{
    Mechanism,
    Expression,
    HexLiteral,
    OtherLiteral,
    NestedFunctionCall,
    Variable,
    Parameter,
    Info,
    Empty
}

public sealed record ResolvedCallArgument
{
    public ResolvedCallArgument(
        string? value,
        ResolvedCallArgumentKind kind,
        VariableDeclaration? sourceVariable = null)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The argument kind is not defined.");
        if (value is null && kind != ResolvedCallArgumentKind.Empty)
            throw new ArgumentNullException(nameof(value));
        if (value is not null && kind == ResolvedCallArgumentKind.Empty)
            throw new ArgumentException("An empty argument must not have a value.", nameof(value));
        if (kind != ResolvedCallArgumentKind.Variable && sourceVariable is not null)
            throw new ArgumentException("Only a variable argument can have a source variable.", nameof(sourceVariable));

        Value = value;
        Kind = kind;
        SourceVariable = sourceVariable;
    }

    public string? Value { get; }
    public ResolvedCallArgumentKind Kind { get; }
    public VariableDeclaration? SourceVariable { get; }
}

public sealed class OperationInvocation
{
    private readonly ReadOnlyCollection<ResolvedCallArgument> arguments;

    public OperationInvocation(IEnumerable<ResolvedCallArgument> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ResolvedCallArgument[] copiedArguments = arguments.ToArray();
        if (copiedArguments.Any(argument => argument is null))
            throw new ArgumentException("Resolved arguments must not contain null entries.", nameof(arguments));

        this.arguments = Array.AsReadOnly(copiedArguments);
    }

    public IReadOnlyList<ResolvedCallArgument> Arguments => arguments;

    // Compatibility projection for operations and algorithms that still use the legacy string[] contract.
    public string[] Values => arguments.Select(argument => argument.Value!).ToArray();
}
