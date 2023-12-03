using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm
{
    public class Rsa : AsymmtricCryptoAlgorithm
    {

        
        public override KeyVariableDeclaration GenerateKey(string mechanism, string keySize)
        {
            return base.GenerateKey(mechanism, keySize);
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism)
        {
            return base.GenerateParameters(mechanism);
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
        {
            return base.GenerateParameters(mechanism, parameters);
        }
    }
}
