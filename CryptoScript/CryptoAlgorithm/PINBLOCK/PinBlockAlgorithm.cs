using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.PINBLOCK;

public enum PinBlockCipherFamily
{
    Des3,
    Aes
}

public sealed class PinBlockAlgorithm : CryptoAlgorithm
{
    public PinBlockAlgorithm(string mechanismName, PinBlockCipherFamily cipherFamily)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mechanismName);
        MechanismName = mechanismName;
        CipherFamily = cipherFamily;
    }

    public string MechanismName { get; }
    public PinBlockCipherFamily CipherFamily { get; }

    public override StringVariableDeclaration Wrap(string[] parameters) =>
        Wrap(AlgorithmCallArguments.FromValues(parameters));

    public override StringVariableDeclaration Wrap(AlgorithmCallArguments parameters) =>
        throw NotImplemented(CryptoScript.Model.CryptoScriptFunction.Wrap);

    public override VariableDeclaration Unwrap(string[] parameters) =>
        Unwrap(AlgorithmCallArguments.FromValues(parameters));

    public override VariableDeclaration Unwrap(AlgorithmCallArguments parameters) =>
        throw NotImplemented(CryptoScript.Model.CryptoScriptFunction.Unwrap);

    private NotImplementedException NotImplemented(CryptoScript.Model.CryptoScriptFunction function) =>
        new($"{MechanismName} {function} is not implemented.");
}
