using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm
{
    public class AES : SymmetricAlgorithm
    {
        public override KeyVariableDeclaration GenerateKey(string mechanism, string Size) 
        {
            int keySize=Convert.ToInt32(Size);
            if (keySize != 128 && keySize != 192 && keySize != 256)
                throw new ArgumentException("wrong key size");
            var key = new KeyVariableDeclaration();
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.KeySize = keySize; 
                aesAlg.GenerateKey(); 

                byte[] aesKey = aesAlg.Key; // Getting the generated AES key
                string keyValue="0x("+FormatConversions.ByteArrayToHexString(aesKey)+")";
                key.Value = keyValue;
                key.ValueFormat = FormatConversions.ParseString(keyValue);
                key.KeySize = Size;
                key.Mechanism = mechanism;
                key.Type = new CryptoTypeKey();
            }
            return key;
        }
    }
}
