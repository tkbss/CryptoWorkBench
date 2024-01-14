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
        public virtual StringVariableDeclaration Encrypt(string[] parameters) 
        { 
            return new StringVariableDeclaration(); 
        }
        public virtual StringVariableDeclaration Decrypt(string[] parameters)
        {
            return new StringVariableDeclaration();
        }
        public virtual StringVariableDeclaration Mac(string[] parameters)
        {
            return new StringVariableDeclaration();
        }
    }
}
