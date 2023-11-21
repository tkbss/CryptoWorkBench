using CryptoScript.Model;
using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.CryptoAlgorithm
{
    public class AES_CBC : AES
    {
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
                byte[] iv= FormatConversions.ToByteArray(parameter.IV,FormatConversions.ParseString(parameter.IV));
                //set iv
                aesAlg.IV = iv;
                byte[] input= SetPadding(parameter,aesAlg,FormatConversions.ToByteArray(data.Value,data.ValueFormat));
                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                // Create the streams used for encryption.                
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(input);
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
        private byte[] SetPadding(ParameterVariableDeclaration parameter, Aes aesAlg, byte[] input)
        {
            byte[] output=input;
            switch(parameter.Padding.ToLower())
            {
                case "pkcs-7":
                    aesAlg.Padding=PaddingMode.PKCS7;
                    break;
                case "iso-10126":
                    aesAlg.Padding=PaddingMode.ISO10126;
                    break;
                case "ansi-x923":
                    aesAlg.Padding=PaddingMode.ANSIX923;
                    break;
                case "iso-7816":
                    aesAlg.Padding=PaddingMode.None;
                    output=ISO7816(input);
                    break;
                case "iso-9797":
                    aesAlg.Padding=PaddingMode.Zeros;
                    break;
                case "none":
                    aesAlg.Padding=PaddingMode.None;
                    break;
                default:
                    throw new ArgumentException("wrong padding");
            }
            return output;
        }
        //iso7816 padding   
        private byte[] ISO7816(byte[] input)
        {
            int length = input.Length;
            int padding = 16 - length % 16;
            byte[] output = new byte[length + padding];
            Array.Copy(input, output, length);
            output[length] = 0x80;
            return output;
        }
    }
}
