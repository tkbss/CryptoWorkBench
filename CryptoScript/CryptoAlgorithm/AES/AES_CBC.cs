using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm
{
    public class AES_CBC : EncryptionMode
    {
        public override StringVariableDeclaration ModeDecryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            byte[] decrypted;
            //using aes in cbc mode with external key and iv
            using (Aes aesAlg = Aes.Create())
            {
                //cbc mode 
                aesAlg.Mode = CipherMode.CBC;
                //set key
                byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
                aesAlg.Key = keyBytes;
                string IV = parameter.GetParameter("IV");
                byte[] iv= FormatConversions.ToByteArray(IV,FormatConversions.ParseString(IV));
                //set iv
                aesAlg.IV = iv;
                PaddingMode padding;
                byte[] input= SetPadding(parameter,out padding,FormatConversions.ToByteArray(data.Value,data.ValueFormat));
                aesAlg.Padding = padding;
                // Create an encryptor to perform the stream transform.
                using (ICryptoTransform encryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV))
                {
                    // Create the streams used for encryption.                
                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {

                            csEncrypt.Write(input, 0, input.Length);
                            csEncrypt.FlushFinalBlock();
                        }
                        decrypted = msEncrypt.ToArray();
                    }
                    
                }
            }
            StringVariableDeclaration cleartext = new StringVariableDeclaration();
            cleartext.Value = FormatConversions.ByteArrayToHexString(decrypted);
            cleartext.ValueFormat = FormatConversions.ParseString(cleartext.Value);
            cleartext.Type = new CryptoTypeVar();
            return cleartext;
        }
        

        public override StringVariableDeclaration ModeEncryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            byte[] encrypted;
            //using aes in cbc mode with external key and iv
            using (Aes aesAlg = Aes.Create())
            {
                //cbc mode 
                aesAlg.Mode = CipherMode.CBC;
                //set key
                byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
                aesAlg.Key = keyBytes;
                string IV = parameter.GetParameter("IV");
                byte[] iv= FormatConversions.ToByteArray(IV,FormatConversions.ParseString(IV));
                //set iv
                aesAlg.IV = iv;
                PaddingMode padding;                
                byte[] input= SetPadding(parameter,out padding,FormatConversions.ToByteArray(data.Value,data.ValueFormat));
                aesAlg.Padding = padding;
                // Create an encryptor to perform the stream transform.
                using (ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV))
                {
                    // Create the streams used for encryption.                
                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(input, 0, input.Length);
                            csEncrypt.FlushFinalBlock();                            
                        }
                        encrypted = msEncrypt.ToArray();

                    }
                }               
            }
            StringVariableDeclaration cyphertext = new StringVariableDeclaration();
            cyphertext.Value = FormatConversions.ByteArrayToHexString(encrypted);
            cyphertext.ValueFormat = FormatConversions.ParseString(cyphertext.Value);
            cyphertext.Type = new CryptoTypeVar();
            return cyphertext;
        }      
        
        
    }
}
