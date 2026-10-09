using CryptoScript.Model;
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

    internal static KeyVariableDeclaration ResolveKey(AlgorithmCallArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);

        if (argument.SourceVariable is KeyVariableDeclaration sourceKey)
            return sourceKey;
        if (argument.SourceVariable is not null)
            throw new ArgumentException("wrong key argument");

        string value = argument.Value ?? throw new ArgumentException("wrong key argument");
        if (VariableDictionary.Instance().Get(value) is KeyVariableDeclaration declared)
            return declared;
        if (FormatConversions.ParseString(value) == FormatConversions.HEX)
        {
            return new KeyVariableDeclaration
            {
                Value = value,
                ValueFormat = FormatConversions.HEX,
                Type = new CryptoTypeKey()
            };
        }
        if (FormatConversions.ParseString(value) == FormatConversions.JSO)
            return KeyVariableDeclaration.Deserialize(value);

        throw new ArgumentException("wrong key argument");
    }

    private static bool IsLegacyVariableIdentifier(string value) =>
        value.Length > 0 &&
        char.IsLetter(value[0]) &&
        value.Skip(1).All(char.IsLetterOrDigit);
}
