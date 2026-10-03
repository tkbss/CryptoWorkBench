using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm
{
    public class AES_CBC : EncryptionMode
    {
        internal static byte[] ValidateAndDecodeIv(string iv)
        {
            string format = FormatConversions.ParseString(iv);
            if (format != FormatConversions.HEX && format != FormatConversions.B64)
            {
                throw new ArgumentException(
                    "AES-CBC #IV must be a hexadecimal or Base64 value, or a variable resolving to one.",
                    nameof(iv));
            }

            if (!HasValidEncodingShape(iv, format))
            {
                throw new ArgumentException(
                    $"AES-CBC #IV contains invalid {GetEncodingName(format)} encoding.",
                    nameof(iv));
            }

            byte[] ivBytes;
            try
            {
                ivBytes = FormatConversions.ToByteArray(iv, format);
            }
            catch (Exception exception) when (exception is FormatException or
                                              ArgumentOutOfRangeException or
                                              IndexOutOfRangeException or
                                              OverflowException)
            {
                throw new ArgumentException(
                    $"AES-CBC #IV contains invalid {GetEncodingName(format)} encoding.",
                    nameof(iv), exception);
            }

            if (ivBytes.Length != 16)
            {
                throw new ArgumentException(
                    $"AES-CBC #IV must decode to exactly 16 bytes; actual length is {ivBytes.Length} bytes.",
                    nameof(iv));
            }

            return ivBytes;
        }

        private static string GetEncodingName(string format) =>
            format == FormatConversions.HEX ? "hexadecimal" : "Base64";

        private static bool HasValidEncodingShape(string iv, string format)
        {
            if (!iv.EndsWith(')'))
                return false;

            string encodedValue = iv.Substring(format == FormatConversions.HEX ? 3 : 4,
                iv.Length - (format == FormatConversions.HEX ? 4 : 5));
            if (encodedValue.Length == 0)
                return false;

            return format != FormatConversions.HEX ||
                   encodedValue.Length % 2 == 0 && encodedValue.All(Uri.IsHexDigit);
        }

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
                byte[] iv = ValidateAndDecodeIv(parameter.GetParameter("IV"));
                //set iv
                aesAlg.IV = iv;
                PaddingMode padding;
                byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
                ValidateNoPaddingBlockInput(parameter, input, 16, "ciphertext");
                input = SetPadding(parameter,out padding,input,"Decrypt");
                if (input.Length == 0 &&
                    padding is PaddingMode.ISO10126 or PaddingMode.ANSIX923 or PaddingMode.PKCS7)
                    throw new ArgumentException(
                        $"AES-CBC with PAD={parameter.GetParameter("PAD")} requires ciphertext length to be a non-zero multiple of 16 bytes.");
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
            decrypted = Unpad(parameter, decrypted,"Decrypt");
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
                byte[] iv = ValidateAndDecodeIv(parameter.GetParameter("IV"));
                //set iv
                aesAlg.IV = iv;
                PaddingMode padding;
                byte[] dataBytes = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
                ValidateNoPaddingBlockInput(parameter, dataBytes, 16, "plaintext");
                byte[] input= Pad(parameter,out padding,dataBytes,"Encrypt");
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
