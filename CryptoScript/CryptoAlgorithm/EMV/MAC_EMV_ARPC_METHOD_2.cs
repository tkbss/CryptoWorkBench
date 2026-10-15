using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.EMV;

public sealed class MAC_EMV_ARPC_METHOD_2 : CryptoAlgorithm
{
    internal const string MechanismName = "MAC-EMV-ARPC-METHOD-2";

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters) =>
        EmvAcArgumentContract.GenerateParameters(MechanismName, mechanism, parameters);

    public override StringVariableDeclaration Mac(AlgorithmCallArguments parameters) =>
        EmvArpcArgumentContract.Execute(MechanismName, parameters, method2: true);

    public override StringVariableDeclaration Mac(string[] parameters) =>
        throw new ArgumentException("EMV ARPC requires structured arguments with genuine PARAM and KEY references.");
}
