namespace CryptoScript.Model;

public enum FunctionContractError
{
    UnsupportedFunction,
    MissingRequiredParameter,
    ForbiddenAdditionalParameter,
    UnknownParameter
}

public sealed class FunctionContractException : Exception
{
    public FunctionContractException(
        FunctionContractError error,
        string mechanism,
        CryptoScriptFunction function,
        string? parameterName = null)
        : base(CreateMessage(error, mechanism, function, parameterName))
    {
        Error = error;
        Mechanism = mechanism;
        Function = function;
        ParameterName = parameterName;
    }

    public FunctionContractError Error { get; }
    public string Mechanism { get; }
    public CryptoScriptFunction Function { get; }
    public string? ParameterName { get; }

    private static string CreateMessage(
        FunctionContractError error,
        string mechanism,
        CryptoScriptFunction function,
        string? parameterName) => error switch
        {
            FunctionContractError.UnsupportedFunction =>
                $"Mechanism {mechanism} does not support function {function}.",
            FunctionContractError.MissingRequiredParameter =>
                $"Mechanism {mechanism} function {function} requires parameter {parameterName}.",
            FunctionContractError.ForbiddenAdditionalParameter =>
                $"Mechanism {mechanism} function {function} does not allow parameter {parameterName}.",
            FunctionContractError.UnknownParameter =>
                $"Mechanism {mechanism} function {function} contains unknown parameter {parameterName}.",
            _ => throw new ArgumentOutOfRangeException(nameof(error), error,
                "The function contract error is not defined.")
        };
}
