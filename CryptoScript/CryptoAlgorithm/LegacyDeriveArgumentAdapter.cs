using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm;

internal static class LegacyDeriveArgumentAdapter
{
    internal static AlgorithmCallArguments Create(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new AlgorithmCallArguments(
            values.Select(value => new AlgorithmCallArgument(value)),
            isLegacyDeriveCall: true);
    }

    internal static KeyVariableDeclaration ResolveKey(AlgorithmCallArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);

        string value = argument.Value ?? throw new ArgumentException("wrong key argument");
        KeyVariableDeclaration[] matches = VariableDictionary.Instance().GetVariables()
            .OfType<KeyVariableDeclaration>()
            .Where(key => key.Value == value)
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new ArgumentException("wrong key argument"),
            _ => throw new ArgumentException(
                "Ambiguous KEY argument: multiple KEY variables have the same value.")
        };
    }
}
