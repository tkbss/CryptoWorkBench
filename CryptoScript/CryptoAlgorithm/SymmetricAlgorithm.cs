using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.CryptoAlgorithm
{
    public class SymmetricAlgorithm
    {
        public virtual KeyVariableDeclaration GenerateKey(string mechanism,string keySize) 
        {
            return new KeyVariableDeclaration();
        }
    }
}
