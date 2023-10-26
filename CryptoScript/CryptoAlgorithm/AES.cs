using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm
{
    public class AES
    {
        public KeyVariableDeclaration GenerateKey(string mechanism, int keySize) 
        {
            if (keySize != 128 || keySize != 192 || keySize != 256)
                throw new ArgumentException("worong key size");
            var key= new KeyVariableDeclaration();
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.KeySize = keySize; // Setting the KeySize to 256 bits
                aesAlg.GenerateKey(); // Generating the AES key

                byte[] aesKey = aesAlg.Key; // Getting the generated AES key
                string keyValue="0x("+FormatConversions.ByteArrayToHexString(aesKey)+")";
                key.Mechanism = mechanism;
                key.KeySize = keySize;
                key.ValueFormat = "HEX_STRING";
                            }
            return key;
        }
    }
}
