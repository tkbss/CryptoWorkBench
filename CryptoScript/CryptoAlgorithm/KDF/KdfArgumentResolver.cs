using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.KDF;

internal static class KdfArgumentResolver
{
    internal static KeyVariableDeclaration ResolveKey(
        AlgorithmCallArguments arguments,
        int index)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        AlgorithmCallArgument argument = arguments.Arguments[index];

        if (argument.SourceVariable is KeyVariableDeclaration sourceKey)
            return sourceKey;

        if (arguments.IsLegacyDeriveCall && argument.SourceVariable is null)
            return LegacyDeriveArgumentAdapter.ResolveKey(argument);

        throw new ArgumentException("wrong key argument");
    }
}
