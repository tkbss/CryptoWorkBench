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

        ValidatePsnSupport(mechanism.CanonicalName, parameters, function);

        if (!mechanism.Supports(function))
        {
            throw new FunctionContractException(
                FunctionContractError.UnsupportedFunction,
                mechanism.CanonicalName,
                function);
        }

        if (mechanism.StrictMechanismParameterContract)
            ValidateMechanismParameter(mechanism, parameters);

        if (mechanism.CanonicalName == "KDF-EMV-MASTER-A")
        {
            if (parameters.ExplicitParameterNames.GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
                throw new ArgumentException("KDF-EMV-MASTER-A does not allow duplicate parameters.");
            if (parameters.GetParameters().ContainsKey("#PSN"))
            {
                string psn = parameters.GetParameter("#PSN");
                if (psn.Length != 4 || psn[0] != '"' || psn[3] != '"' ||
                    psn[1] is < '0' or > '9' || psn[2] is < '0' or > '9')
                    throw new ArgumentException("PSN must be a normal string literal containing exactly two ASCII decimal digits.");
            }
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

    // Run before legacy SetInstance can discard malformed segments. Other mechanisms
    // retain the existing parser behavior; this is not a second general PARAM parser.
    internal static void ValidateSerializedOptionAParameters(string serialized, bool selectedOptionA = false)
    {
        string[] segments = serialized.Split('#');
        // Outside an explicitly selected Option A call, preserve other mechanisms'
        // legacy last-write-wins selection, even for conflicting MECH declarations.
        string? storedMechanism = segments.Select(segment => segment.Split(':', 2))
            .Where(pair => pair.Length == 2 && pair[0] == "MECH")
            .Select(pair => pair[1]).LastOrDefault();
        bool optionA = selectedOptionA ||
            string.Equals(storedMechanism, "KDF-EMV-MASTER-A", StringComparison.OrdinalIgnoreCase);
        if (!optionA)
            return;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string segment in segments.Skip(serialized.StartsWith('#') ? 1 : 0))
        {
            string[] pair = segment.Split(':', 2);
            if (pair.Length != 2 || pair[1].Length == 0)
                throw new ArgumentException("KDF-EMV-MASTER-A contains a malformed serialized parameter.");
            if (!NamesEqual(pair[0], "#MECH") && !NamesEqual(pair[0], "#PSN"))
                throw new ArgumentException("KDF-EMV-MASTER-A allows only MECH and PSN parameters.");
            if (!names.Add(Normalize(pair[0])))
                throw new ArgumentException("KDF-EMV-MASTER-A does not allow duplicate parameters.");
            if (NamesEqual(pair[0], "#MECH") &&
                !pair[1].Equals("KDF-EMV-MASTER-A", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("KDF-EMV-MASTER-A requires a consistent MECH parameter.");
        }
    }

    internal static void ValidatePsnSupport(string mechanism, ParameterVariableDeclaration parameters,
        CryptoScriptFunction function = CryptoScriptFunction.Parameters)
    {
        if (!parameters.GetParameters().Keys.Any(name => NamesEqual(name, "#PSN")))
            return;
        string canonical = NormalizeMechanismValue(mechanism);
        if (!MechanismRegistry.TryGet(canonical, out MechanismRegistryEntry? entry) ||
            !entry!.FunctionMetadata.Values.Any(metadata => metadata.Parameters.Any(parameter =>
                parameter.Kind == MechanismParameterKind.NamedParameter && NamesEqual(parameter.Name, "#PSN"))))
            throw new FunctionContractException(FunctionContractError.ForbiddenAdditionalParameter,
                entry?.CanonicalName ?? "selected mechanism", function, "#PSN");
    }

    private static void ValidateMechanismParameter(
        MechanismRegistryEntry mechanism,
        ParameterVariableDeclaration parameters)
    {
        string storedMechanism = parameters.GetParameter("#MECH");
        if (!NormalizeMechanismValue(storedMechanism).Equals(
                mechanism.CanonicalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Mechanism {mechanism.CanonicalName} requires a consistent #MECH:{mechanism.CanonicalName} parameter.");
        }

        int occurrences = parameters.ExplicitParameterNames.Count(name =>
            NamesEqual(name, "#MECH"));
        if (occurrences > 1)
        {
            throw new ArgumentException(
                $"Mechanism {mechanism.CanonicalName} does not allow duplicate #MECH parameters.");
        }
    }

    private static bool IsGloballyKnown(string name) =>
        ParameterRegistry.Entries.Any(parameter => NamesEqual(parameter.Name, name));

    private static bool NamesEqual(string first, string second) =>
        Normalize(first).Equals(Normalize(second), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string name) =>
        name.StartsWith('#') ? name : "#" + name;

    private static string NormalizeMechanismValue(string value) =>
        value.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
            ? value["#MECH:".Length..]
            : value;
}
