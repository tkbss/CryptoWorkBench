using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm
{
    public class AES_ECB : EncryptionMode
    {
        public override StringVariableDeclaration ModeDecryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            return ECBKernel(parameter, key, data, false);
        }

        public override StringVariableDeclaration ModeEncryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            return ECBKernel(parameter, key, data, true);
        }
        private StringVariableDeclaration ECBKernel(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data,bool encryption)
        {
            byte[] encrypted;
            using (Aes aesAlg = Aes.Create())
            {
                // ECB mode
                aesAlg.Mode = CipherMode.ECB;
                // No padding
                aesAlg.Padding = PaddingMode.None;                
                aesAlg.Key = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
                byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
                if (input.Length % 16 != 0)
                    throw new ArgumentException("wrong input length for ecb mode");
                if (encryption)
                    encrypted = aesAlg.CreateEncryptor().TransformFinalBlock(input, 0, input.Length);
                else
                    encrypted = aesAlg.CreateDecryptor().TransformFinalBlock(input, 0, input.Length);

            }
            StringVariableDeclaration cyphertext = new StringVariableDeclaration();
            cyphertext.Value = FormatConversions.ByteArrayToHexString(encrypted);
            cyphertext.ValueFormat = FormatConversions.ParseString(cyphertext.Value);
            cyphertext.Type = new CryptoTypeVar();
            return cyphertext;
        }
    }
}
