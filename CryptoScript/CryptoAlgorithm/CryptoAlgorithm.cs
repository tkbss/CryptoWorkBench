using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;

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
    }
}
