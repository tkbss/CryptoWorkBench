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

        ValidateEmvCryptogramParameterSupport(mechanism.CanonicalName, parameters, function);

        if (mechanism.CanonicalName is "MAC-EMV-AC" or "MAC-EMV-AC-VERIFY")
        {
            if (parameters.ExplicitParameterNames.GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
                throw new ArgumentException("EMV AC does not allow duplicate parameters.");
            if (parameters.GetParameters().ContainsKey("#EXPECTED"))
            {
                string expected = parameters.GetParameter("#EXPECTED");
                if (expected.Length != 20 || !expected.StartsWith("0x(", StringComparison.Ordinal) ||
                    expected[^1] != ')' || expected.AsSpan(3, 16).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
                    throw new ArgumentException("Expected cryptogram must be a HEX value containing exactly eight bytes.");
            }
        }

        if (IsEmvMasterMechanism(mechanism.CanonicalName))
        {
            if (parameters.ExplicitParameterNames.GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
                throw new ArgumentException($"{mechanism.CanonicalName} does not allow duplicate parameters.");
            if (parameters.GetParameters().ContainsKey("#PSN"))
            {
                string psn = parameters.GetParameter("#PSN");
                if (psn.Length != 4 || psn[0] != '"' || psn[3] != '"' ||
                    psn[1] is < '0' or > '9' || psn[2] is < '0' or > '9')
                    throw new ArgumentException("PSN must be a normal string literal containing exactly two ASCII decimal digits.");
            }
        }

        if (mechanism.CanonicalName is "MAC-EMV-ARPC-METHOD-1" or "MAC-EMV-ARPC-METHOD-2")
        {
            if (parameters.ExplicitParameterNames.GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
                throw new ArgumentException("EMV ARPC does not allow duplicate parameters.");
            bool method2 = mechanism.CanonicalName == "MAC-EMV-ARPC-METHOD-2";
            ValidateArpcBinaryParameter(parameters, method2 ? "#CSU" : "#ARC", method2 ? 4 : 2);
            if (method2 && parameters.GetParameters().ContainsKey("#ADATA"))
                ValidateArpcBinaryParameter(parameters, "#ADATA", 8, maximumLength: true);
            if (method2 && parameters.GetParameters().ContainsKey("#VARIANT") &&
                parameters.GetParameter("#VARIANT") != "\"IAD\"")
                throw new ArgumentException("EMV ARPC Method 2 supports only the explicit variant string IAD.");
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

    private static void ValidateArpcBinaryParameter(ParameterVariableDeclaration parameters, string name,
        int bytes, bool maximumLength = false)
    {
        // Missing mandatory parameters are reported by the existing metadata contract below.
        if (!parameters.GetParameters().ContainsKey(name))
            return;
        string value = parameters.GetParameter(name);
        int digits = value.Length - 4;
        if (digits <= 0 || digits % 2 != 0 || (maximumLength ? digits > bytes * 2 : digits != bytes * 2) ||
            !value.StartsWith("0x(", StringComparison.Ordinal) || value[^1] != ')' ||
            value.AsSpan(3, digits).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
            throw new ArgumentException(maximumLength
                ? "EMV ARPC proprietary data must be a HEX value of at most eight bytes; omit it for empty data."
                : $"EMV ARPC {name} must be a HEX value containing exactly {bytes} bytes.");
    }

    // Run before legacy SetInstance can discard malformed segments. Other mechanisms
    // retain the existing parser behavior; this is not a second general PARAM parser.
    internal static void ValidateSerializedEmvMasterParameters(string serialized, string? selectedMechanism = null)
    {
        string[] segments = serialized.Split('#');
        // Outside an explicitly selected EMV master call, preserve other mechanisms'
        // legacy last-write-wins selection, even for conflicting MECH declarations.
        string? storedMechanism = segments.Select(segment => segment.Split(':', 2))
            .Where(pair => pair.Length == 2 && pair[0] == "MECH")
            .Select(pair => pair[1]).LastOrDefault();
        string? masterMechanism = selectedMechanism ?? storedMechanism;
        if (masterMechanism is null || !IsEmvMasterMechanism(masterMechanism.ToUpperInvariant()))
            return;
        masterMechanism = masterMechanism.ToUpperInvariant();

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string segment in segments.Skip(serialized.StartsWith('#') ? 1 : 0))
        {
            string[] pair = segment.Split(':', 2);
            if (pair.Length != 2 || pair[1].Length == 0)
                throw new ArgumentException($"{masterMechanism} contains a malformed serialized parameter.");
            if (!NamesEqual(pair[0], "#MECH") && !NamesEqual(pair[0], "#PSN"))
                throw new ArgumentException($"{masterMechanism} allows only MECH and PSN parameters.");
            if (!names.Add(Normalize(pair[0])))
                throw new ArgumentException($"{masterMechanism} does not allow duplicate parameters.");
            if (NamesEqual(pair[0], "#MECH") &&
                !pair[1].Equals(masterMechanism, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"{masterMechanism} requires a consistent MECH parameter.");
        }
    }

    private static bool IsEmvMasterMechanism(string mechanism) =>
        mechanism is "KDF-EMV-MASTER-A" or "KDF-EMV-MASTER-B" or "KDF-EMV-MASTER-C";

    // Only AP6's three new names are restricted here. Historical globally known
    // parameters and their mechanism-specific/default/legacy behavior stay untouched.
    // Inspect the same '#' / first ':' boundaries as legacy SetInstance before it
    // discards valueless segments. Legacy generators use individual argument names
    // instead: a '#' inside one of their values is not a new argument boundary.
    internal static void ValidateSerializedEmvCryptogramParameters(string serialized)
    {
        string[][] segments = serialized.Split('#', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Split(':', 2)).ToArray();
        // Preserve legacy MECH selection (exact spelling, last write wins).
        string? storedMechanism = segments.Where(pair => pair.Length == 2 && pair[0] == "MECH")
            .Select(pair => pair[1]).LastOrDefault();
        ValidateEmvCryptogramParameterNames(storedMechanism ?? string.Empty, segments.Select(pair => pair[0]));
    }

    internal static void ValidateEmvCryptogramParameterNames(string mechanism, IEnumerable<string> names)
    {
        string[] restrictedNames = names.Select(name => Normalize(name).ToUpperInvariant())
            .Where(name => EmvCryptogramParameterOwner(name) is not null).ToArray();
        if (restrictedNames.Length == 0)
            return;
        var supplied = new ParameterVariableDeclaration { Mechanism = mechanism };
        foreach (string name in restrictedNames)
            supplied.SetParameter(name, string.Empty);
        ValidateEmvCryptogramParameterSupport(supplied.Mechanism, supplied);
    }

    private static string? EmvCryptogramParameterOwner(string normalizedName) => normalizedName switch
    {
        "#EXPECTED" => "MAC-EMV-AC-VERIFY",
        "#ARC" => "MAC-EMV-ARPC-METHOD-1",
        "#CSU" => "MAC-EMV-ARPC-METHOD-2",
        _ => null
    };

    internal static void ValidateEmvCryptogramParameterSupport(string mechanism,
        ParameterVariableDeclaration parameters,
        CryptoScriptFunction function = CryptoScriptFunction.Parameters)
    {
        string canonical = NormalizeMechanismValue(mechanism);
        foreach (string name in parameters.GetParameters().Keys)
        {
            string normalized = Normalize(name).ToUpperInvariant();
            string? owner = EmvCryptogramParameterOwner(normalized);
            if (owner is not null &&
                (!canonical.Equals(owner, StringComparison.OrdinalIgnoreCase) ||
                 !NormalizeMechanismValue(parameters.Mechanism).Equals(owner, StringComparison.OrdinalIgnoreCase) ||
                 !NormalizeMechanismValue(parameters.GetParameter("#MECH"))
                     .Equals(owner, StringComparison.OrdinalIgnoreCase)))
                throw new FunctionContractException(FunctionContractError.ForbiddenAdditionalParameter,
                    MechanismRegistry.TryGet(canonical, out MechanismRegistryEntry? entry)
                        ? entry!.CanonicalName : "<unknown mechanism>", function, normalized);
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
