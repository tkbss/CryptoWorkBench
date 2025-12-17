using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto.Macs;
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
        public bool IsMACAlgorithm(string mechanism)
        {
            List<string> macModes = new List<string>() {"AES-GMAC","AES-CMAC" };
            return macModes.Contains(mechanism);
            
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
        public byte[] SetPadding(ParameterVariableDeclaration parameter, out PaddingMode padding, byte[] input,string fn,int blocksize=16)
        {
            byte[] output = input;
            string mechanism = parameter.GetParameter("MECH").ToUpper();
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
                case "iso-9797-m1":
                    padding = PaddingMode.Zeros;
                    break;
                case "iso-9797-m2":
                    padding = PaddingMode.None;
                    output = ISO7816(input);
                    break;
                case "iso-9797-m3":
                    padding = PaddingMode.None;
                    output = Iso9797M3(input,blocksize);
                    break;
                case "none":
                    if (input.Length % blocksize != 0)
                    {                    
                        var se = new SemanticError() { Type = "Parameters" };
                        se.Message = mechanism+" with PAD=NONE requires plaintext length multiple of 16 bytes.";
                        se.FunctionName = fn;
                        throw new SemanticErrorException() { SemanticError = se };
                    }
                    padding = PaddingMode.None;
                    break;
                default:
                    throw new ArgumentException("wrong padding");
            }
            return output;
        }
        public byte[] Iso9797M3(byte[] input, int blockSizeBytes)
        {
            const int lengthFieldBytes = 8; // 64 Bit

            ulong bitLength = (ulong)input.Length * 8;

            int totalLen = input.Length + lengthFieldBytes;
            int paddedLen = ((totalLen + blockSizeBytes - 1) / blockSizeBytes) * blockSizeBytes;

            byte[] output = new byte[paddedLen];
            Buffer.BlockCopy(input, 0, output, 0, input.Length);

            // length field: big endian, unsigned
            for (int i = 0; i < lengthFieldBytes; i++)
            {
                output[paddedLen - 1 - i] = (byte)(bitLength >> (8 * i));
            }

            return output;
        }

    }
}
