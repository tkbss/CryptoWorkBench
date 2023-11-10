using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.CryptoAlgorithm
{
    public class SymmetricAlgorithmFactory
    {
        public SymmetricAlgorithmFactory() { }
        public static SymmetricAlgorithm Create(string mechanism) 
        {
            if (mechanism.StartsWith("AES"))
                return new AES();
            return new SymmetricAlgorithm();
        }
    }
}
