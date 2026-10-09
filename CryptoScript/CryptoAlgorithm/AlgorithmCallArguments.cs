using System.Collections.ObjectModel;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm;

public sealed record AlgorithmCallArgument
{
    public AlgorithmCallArgument(
        string? value,
        VariableDeclaration? sourceVariable = null,
        ResolvedCallArgumentKind kind = ResolvedCallArgumentKind.Expression)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The argument kind is not defined.");

        Value = value;
        SourceVariable = sourceVariable;
        Kind = kind;
    }

    public string? Value { get; }
    public VariableDeclaration? SourceVariable { get; }
    public ResolvedCallArgumentKind Kind { get; }
}

public sealed class AlgorithmCallArguments
{
    private readonly ReadOnlyCollection<AlgorithmCallArgument> arguments;

    public AlgorithmCallArguments(IEnumerable<AlgorithmCallArgument> arguments)
        : this(arguments, isLegacyDeriveCall: false)
    {
    }

    internal AlgorithmCallArguments(
        IEnumerable<AlgorithmCallArgument> arguments,
        bool isLegacyDeriveCall)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        AlgorithmCallArgument[] copiedArguments = arguments.ToArray();
        if (copiedArguments.Any(argument => argument is null))
            throw new ArgumentException("Algorithm arguments must not contain null entries.", nameof(arguments));

        this.arguments = Array.AsReadOnly(copiedArguments);
        IsLegacyDeriveCall = isLegacyDeriveCall;
    }

    public IReadOnlyList<AlgorithmCallArgument> Arguments => arguments;

    internal bool IsLegacyDeriveCall { get; }

    public string[] Values => arguments.Select(argument => argument.Value!).ToArray();

    public static AlgorithmCallArguments FromValues(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new AlgorithmCallArguments(values.Select(value => new AlgorithmCallArgument(value)));
    }

    internal AlgorithmCallArguments WithSourceVariable(int index, VariableDeclaration sourceVariable)
    {
        ArgumentNullException.ThrowIfNull(sourceVariable);
        if (index < 0 || index >= arguments.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        AlgorithmCallArgument[] updated = arguments.ToArray();
        updated[index] = new AlgorithmCallArgument(
            updated[index].Value, sourceVariable, updated[index].Kind);
        return new AlgorithmCallArguments(updated, IsLegacyDeriveCall);
    }
}
