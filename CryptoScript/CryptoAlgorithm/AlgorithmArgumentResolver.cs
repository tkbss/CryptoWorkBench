using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm;

internal static class AlgorithmArgumentResolver
{
    internal static ParameterVariableDeclaration ResolveParameter(AlgorithmCallArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);

        if (argument.SourceVariable is ParameterVariableDeclaration sourceParameter)
            return sourceParameter;

        string value = argument.Value ?? throw new ArgumentException("wrong parameter argument");
        if (IsLegacyVariableIdentifier(value) &&
            VariableDictionary.Instance().Get(value) is ParameterVariableDeclaration declared)
            return declared;
        if (FormatConversions.ParseString(value) == FormatConversions.PAR)
        {
            var temporary = new ParameterVariableDeclaration();
            temporary.SetInstance(value);
            return temporary;
        }

        throw new ArgumentException("wrong parameter argument");
    }

    private static bool IsLegacyVariableIdentifier(string value) =>
        value.Length > 0 &&
        char.IsLetter(value[0]) &&
        value.Skip(1).All(char.IsLetterOrDigit);
}
