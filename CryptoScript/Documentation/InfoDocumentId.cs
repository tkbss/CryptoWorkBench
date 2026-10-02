using CryptoScript.Model;

namespace CryptoScript.Documentation;

public enum InfoDocumentKind
{
    MechanismsOverview,
    FunctionsOverview,
    ParametersOverview,
    Mechanism,
    Function,
    MechanismFunction,
    MechanismParameter,
    PaddingsOverview,
    Padding
}

public sealed record InfoDocumentId
{
    private InfoDocumentId(
        InfoDocumentKind kind,
        string? mechanism = null,
        string? padding = null,
        string? function = null,
        string? parameter = null)
    {
        Kind = kind;
        Mechanism = mechanism;
        Padding = padding;
        Function = function;
        Parameter = parameter;
    }

    public InfoDocumentKind Kind { get; }
    public string? Mechanism { get; }
    public string? Padding { get; }
    public string? Function { get; }
    public string? Parameter { get; }

    public static InfoDocumentId CreateMechanismsOverview() =>
        new(InfoDocumentKind.MechanismsOverview);

    public static InfoDocumentId CreateFunctionsOverview() =>
        new(InfoDocumentKind.FunctionsOverview);

    public static InfoDocumentId CreateParametersOverview() =>
        new(InfoDocumentKind.ParametersOverview);

    public static InfoDocumentId CreatePaddingsOverview() =>
        new(InfoDocumentKind.PaddingsOverview);

    public static InfoDocumentId CreateMechanism(string mechanism) =>
        new(InfoDocumentKind.Mechanism, mechanism: RequireMechanism(mechanism));

    public static InfoDocumentId CreatePadding(string padding) =>
        new(InfoDocumentKind.Padding, padding: RequirePadding(padding));

    public static InfoDocumentId CreateFunction(string function) =>
        new(InfoDocumentKind.Function, function: RequireFunction(function));

    public static InfoDocumentId CreateMechanismFunction(string function, string mechanism) =>
        new(
            InfoDocumentKind.MechanismFunction,
            mechanism: RequireMechanism(mechanism),
            function: RequireFunction(function));

    public static InfoDocumentId CreateMechanismParameter(string mechanism, string parameter) =>
        new(
            InfoDocumentKind.MechanismParameter,
            mechanism: RequireMechanism(mechanism),
            parameter: RequireParameter(parameter));

    private static string RequireMechanism(string mechanism)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mechanism);
        if (!MechanismRegistry.TryGet(mechanism, out _))
        {
            throw new ArgumentException(
                $"Unknown or non-canonical mechanism '{mechanism}'.",
                nameof(mechanism));
        }

        return mechanism;
    }

    private static string RequirePadding(string padding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(padding);
        if (!PaddingRegistry.TryGet(padding, out _))
        {
            throw new ArgumentException(
                $"Unknown or non-canonical padding '{padding}'.",
                nameof(padding));
        }

        return padding;
    }

    private static string RequireFunction(string function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(function);
        if (!IsCanonicalFunctionName(function))
        {
            throw new ArgumentException(
                $"Invalid or non-canonical function name '{function}'.",
                nameof(function));
        }

        return function;
    }

    private static string RequireParameter(string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        if (parameter.StartsWith('#') ||
            !ParameterRegistry.TryGet($"#{parameter}", out _))
        {
            throw new ArgumentException(
                $"Unknown or non-canonical parameter '{parameter}'.",
                nameof(parameter));
        }

        return parameter;
    }

    // CryptoLexer.FN is the only complete source currently available for function-name syntax.
    // No complete canonical registry exists yet, so semantic function validation is deferred.
    private static bool IsCanonicalFunctionName(string function)
    {
        if (function.Length < 2 ||
            !char.IsAsciiLetterUpper(function[0]) ||
            !char.IsAsciiLetterLower(function[1]))
        {
            return false;
        }

        for (int index = 2; index < function.Length; index++)
        {
            if (!char.IsAsciiLetter(function[index]))
                return false;
        }

        return true;
    }
}
