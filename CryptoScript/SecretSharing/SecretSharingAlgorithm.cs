using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.SecretSharing;

// Parameter creation follows the existing AlgorithmFactory convention. The object-valued
// Share/Secret/Restore operations use OperationInvocation directly, never serialized variables.
public sealed class SecretSharingAlgorithm : CryptoAlgorithm.CryptoAlgorithm
{
    public override ParameterVariableDeclaration GenerateParameters(string mechanism) => GenerateParameters(mechanism, []);
    public override ParameterVariableDeclaration GenerateParameters(string mechanism, params string[] arguments)
    {
        if (mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)) mechanism = mechanism[6..];
        var parameters = new ParameterVariableDeclaration { Mechanism = mechanism.ToUpperInvariant(), Type = new CryptoTypeParameters() };
        parameters.RecordExplicitParameter("#MECH");
        foreach (string argument in arguments)
        {
            parameters.SetParameter(argument);
            parameters.RecordExplicitParameter(argument.Split(':', 2)[0]);
        }
        _ = SecretParameters.Read(parameters);
        return parameters;
    }
}
