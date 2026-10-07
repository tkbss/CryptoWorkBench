using CryptoScript.Variables;

namespace CryptoScript.Model;

public static class MechanismParameterContractValidator
{
    public static void Validate(
        string mechanism,
        CryptoScriptFunction function,
        ParameterVariableDeclaration parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mechanism);
        if (!MechanismRegistry.TryGet(mechanism, out MechanismRegistryEntry? entry))
            throw new ArgumentException($"Unknown mechanism: {mechanism}.", nameof(mechanism));

        Validate(entry!, function, parameters);
    }

    internal static void Validate(
        MechanismRegistryEntry mechanism,
        CryptoScriptFunction function,
        ParameterVariableDeclaration parameters)
    {
        ArgumentNullException.ThrowIfNull(mechanism);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!mechanism.Supports(function))
        {
            throw new FunctionContractException(
                FunctionContractError.UnsupportedFunction,
                mechanism.CanonicalName,
                function);
        }

        if (!mechanism.FunctionMetadata.TryGetValue(
                function, out MechanismFunctionMetadata? metadata))
        {
            return;
        }

        MechanismParameterMetadata[] namedParameters = metadata.Parameters
            .Where(parameter => parameter.Kind == MechanismParameterKind.NamedParameter)
            .ToArray();
        bool positionalMechanismIsStoredParameter =
            function == CryptoScriptFunction.Parameters &&
            metadata.Parameters.Any(parameter =>
                parameter.Kind == MechanismParameterKind.PositionalArgument &&
                parameter.ResultingDataTypes.Contains(MechanismParameterDataType.Mechanism));
        string[] actualNames = parameters.GetParameters().Keys.ToArray();

        foreach (string actualName in actualNames)
        {
            string normalizedName = Normalize(actualName);
            if (!IsGloballyKnown(normalizedName))
            {
                throw new FunctionContractException(
                    FunctionContractError.UnknownParameter,
                    mechanism.CanonicalName,
                    function,
                    normalizedName);
            }
        }

        foreach (MechanismParameterMetadata required in namedParameters.Where(parameter =>
                     parameter.IsRequired &&
                     parameter.Direction is MechanismParameterDirection.Input or
                         MechanismParameterDirection.InOut))
        {
            if (!Contains(actualNames, required.Name))
            {
                throw new FunctionContractException(
                    FunctionContractError.MissingRequiredParameter,
                    mechanism.CanonicalName,
                    function,
                    Normalize(required.Name));
            }
        }

        if (metadata.AdditionalNamedParameterHandling != AdditionalNamedParameterHandling.None)
            return;

        foreach (string actualName in actualNames)
        {
            string normalizedName = Normalize(actualName);
            if (positionalMechanismIsStoredParameter &&
                normalizedName.Equals("#MECH", StringComparison.OrdinalIgnoreCase))
                continue;
            if (namedParameters.Any(parameter => NamesEqual(parameter.Name, normalizedName)))
                continue;

            throw new FunctionContractException(
                FunctionContractError.ForbiddenAdditionalParameter,
                mechanism.CanonicalName,
                function,
                normalizedName);
        }
    }

    private static bool Contains(IEnumerable<string> names, string expected) =>
        names.Any(name => NamesEqual(name, expected));

    private static bool IsGloballyKnown(string name) =>
        ParameterRegistry.Entries.Any(parameter => NamesEqual(parameter.Name, name));

    private static bool NamesEqual(string first, string second) =>
        Normalize(first).Equals(Normalize(second), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string name) =>
        name.StartsWith('#') ? name : "#" + name;
}
