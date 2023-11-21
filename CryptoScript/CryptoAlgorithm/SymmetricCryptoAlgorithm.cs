using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.CryptoAlgorithm
{
    
    public class SymmetricCryptoAlgorithm : CryptoAlgorithm
    {
        public override KeyVariableDeclaration GenerateKey(string mechanism,string keySize) 
        {
            return new KeyVariableDeclaration();
        }
        public override ParameterVariableDeclaration GenerateParameters(string mechanism)
        {
            return new ParameterVariableDeclaration();
        }
        public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[]parameters)
        {
            return new ParameterVariableDeclaration();
        }
        public virtual SymmetricCryptoAlgorithm CreateMode(string mechanism) { return new SymmetricCryptoAlgorithm();}

        public override StringVariableDeclaration Encrypt(string[] parameters)
        {
            return base.Encrypt(parameters);
        }
    }
    
}
