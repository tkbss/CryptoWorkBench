using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm
{
    public class CryptoAlgorithm
    {
        public virtual KeyVariableDeclaration GenerateKey(string mechanism, string keySize)
        {
            return new KeyVariableDeclaration();
        }
        public virtual ParameterVariableDeclaration GenerateParameters(string mechanism)
        {
            return new ParameterVariableDeclaration();
        }
        public virtual ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
        {
            return new ParameterVariableDeclaration();
        }
        public virtual BlockHeaderVariableDeclaration GenerateBlockHeader(string[] parameters)
        {
            return new BlockHeaderVariableDeclaration();
        }
        public virtual BlockHeaderVariableDeclaration GenerateBlockHeader(string mechanism)
        {
            return new BlockHeaderVariableDeclaration();
        }
        public virtual StringVariableDeclaration Encrypt(string[] parameters) 
        { 
            return new StringVariableDeclaration(); 
        }
        public virtual StringVariableDeclaration Encrypt(AlgorithmCallArguments parameters) =>
            Encrypt(parameters.Values);
        public virtual StringVariableDeclaration Decrypt(string[] parameters)
        {
            return new StringVariableDeclaration();
        }
        public virtual StringVariableDeclaration Decrypt(AlgorithmCallArguments parameters) =>
            Decrypt(parameters.Values);
        public virtual StringVariableDeclaration Mac(string[] parameters)
        {
            return new StringVariableDeclaration();
        }
        public virtual StringVariableDeclaration Mac(AlgorithmCallArguments parameters) =>
            Mac(parameters.Values);
        public virtual StringVariableDeclaration Hash(string[] parameters)
        {
            return new StringVariableDeclaration();
        }
        public virtual KeyVariableDeclaration Derive(string[] parameters) =>
            Derive(LegacyDeriveArgumentAdapter.Create(parameters));
        public virtual KeyVariableDeclaration Derive(AlgorithmCallArguments parameters)
        {
            throw new NotSupportedException("The selected mechanism does not support key derivation.");
        }
        public virtual StringVariableDeclaration Wrap(string[] parameters)
        {
            return new StringVariableDeclaration();
        }
        public virtual StringVariableDeclaration Wrap(AlgorithmCallArguments parameters) =>
            Wrap(parameters.Values);
        public virtual VariableDeclaration Unwrap(string[] parameters)
        {
            return new KeyVariableDeclaration();
        }
        public virtual VariableDeclaration Unwrap(AlgorithmCallArguments parameters) =>
            Unwrap(parameters.Values);
    }
}
