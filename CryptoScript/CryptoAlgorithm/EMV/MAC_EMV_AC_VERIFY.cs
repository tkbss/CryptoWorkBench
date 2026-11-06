using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.EMV;

public sealed class MAC_EMV_AC_VERIFY : CryptoAlgorithm
{
    internal const string MechanismName = "MAC-EMV-AC-VERIFY";

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters) =>
        EmvAcArgumentContract.GenerateParameters(MechanismName, mechanism, parameters);

    public override StringVariableDeclaration Mac(AlgorithmCallArguments parameters) =>
        EmvAcArgumentContract.Execute(MechanismName, parameters, verify: true);

    public override StringVariableDeclaration Mac(string[] parameters) =>
        throw new ArgumentException("MAC-EMV-AC-VERIFY requires structured arguments with genuine PARAM and KEY references.");
}
