using System.Collections.ObjectModel;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm;

public sealed record AlgorithmCallArgument
{
    public AlgorithmCallArgument(string? value, VariableDeclaration? sourceVariable = null)
    {
        Value = value;
        SourceVariable = sourceVariable;
    }

    public string? Value { get; }
    public VariableDeclaration? SourceVariable { get; }
}

public sealed class AlgorithmCallArguments
{
    private readonly ReadOnlyCollection<AlgorithmCallArgument> arguments;

    public AlgorithmCallArguments(IEnumerable<AlgorithmCallArgument> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        AlgorithmCallArgument[] copiedArguments = arguments.ToArray();
        if (copiedArguments.Any(argument => argument is null))
            throw new ArgumentException("Algorithm arguments must not contain null entries.", nameof(arguments));

        this.arguments = Array.AsReadOnly(copiedArguments);
    }

    public IReadOnlyList<AlgorithmCallArgument> Arguments => arguments;

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
        updated[index] = new AlgorithmCallArgument(updated[index].Value, sourceVariable);
        return new AlgorithmCallArguments(updated);
    }
}
