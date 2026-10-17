using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm;

internal static class AlgorithmArgumentResolver
{
    internal static ParameterVariableDeclaration ResolveParameter(AlgorithmCallArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);

        if (argument.SourceVariable is ParameterVariableDeclaration sourceParameter)
            return ValidateParameterSupport(sourceParameter);

        string value = argument.Value ?? throw new ArgumentException("wrong parameter argument");
        if (IsLegacyVariableIdentifier(value) &&
            VariableDictionary.Instance().Get(value) is ParameterVariableDeclaration declared)
            return ValidateParameterSupport(declared);
        if (FormatConversions.ParseString(value) == FormatConversions.PAR)
        {
            var temporary = new ParameterVariableDeclaration();
            temporary.SetInstance(value);
            return temporary;
        }

        throw new ArgumentException("wrong parameter argument");
    }

    private static ParameterVariableDeclaration ValidateParameterSupport(ParameterVariableDeclaration parameters)
    {
        MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(parameters.Mechanism, parameters);
        return parameters;
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

    // TR-31 historically accepts projected values. Preserve only unambiguous legacy
    // lookups; structured invocations must use the original object, even for equal bytes.
    internal static KeyVariableDeclaration ResolveTr31Key(AlgorithmCallArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.SourceVariable is KeyVariableDeclaration key)
            return key;
        if (argument.SourceVariable is not null)
            throw new ArgumentException("wrong key argument");
        string value = argument.Value ?? throw new ArgumentException("wrong key argument");
        if (VariableDictionary.Instance().Get(value) is KeyVariableDeclaration declared)
            return declared;
        var matches = VariableDictionary.Instance().GetVariables().OfType<KeyVariableDeclaration>()
            .Where(candidate => candidate.Value == value).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            > 1 => throw new ArgumentException("Ambiguous TR-31 key value; use an original KEY reference or identifier."),
            _ => throw new ArgumentException("Key variable not found.")
        };
    }

    private static bool IsLegacyVariableIdentifier(string value) =>
        value.Length > 0 &&
        char.IsLetter(value[0]) &&
        value.Skip(1).All(char.IsLetterOrDigit);
}
