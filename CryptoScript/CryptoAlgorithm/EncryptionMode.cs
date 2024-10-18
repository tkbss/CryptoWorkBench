using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm
{
    public class EncryptionMode
    {
        public virtual StringVariableDeclaration ModeMac(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            return new StringVariableDeclaration();
        }
        public virtual StringVariableDeclaration ModeEncryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            return new StringVariableDeclaration();
        }
        public virtual StringVariableDeclaration ModeDecryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            return new StringVariableDeclaration();
        }
        //iso7816 padding   
        public byte[] ISO7816(byte[] input)
        {
            int length = input.Length;
            int padding = 16 - length % 16;
            byte[] output = new byte[length + padding];
            Array.Copy(input, output, length);
            output[length] = 0x80;
            return output;
        }
        public byte[] SetPadding(ParameterVariableDeclaration parameter, out PaddingMode padding, byte[] input)
        {
            byte[] output = input;
            switch (parameter.GetParameter("PAD").ToLower())
            {
                case "pkcs-7":
                    padding = PaddingMode.PKCS7;
                    break;
                case "iso-10126":
                    padding = PaddingMode.ISO10126;
                    break;
                case "ansi-x923":
                    padding = PaddingMode.ANSIX923;
                    break;
                case "iso-7816":
                    padding = PaddingMode.None;
                    output = ISO7816(input);
                    break;
                case "iso-9797":
                    padding = PaddingMode.Zeros;
                    break;
                case "none":
                    padding = PaddingMode.None;
                    break;
                default:
                    throw new ArgumentException("wrong padding");
            }
            return output;
        }
    }
}
