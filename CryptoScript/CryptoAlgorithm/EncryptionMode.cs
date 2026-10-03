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
        public byte[] Unpad(ParameterVariableDeclaration parameter, byte[] input, string fn, int blocksize = 16) 
        {
            byte[] output = input;
            string mechanism = parameter.GetParameter("MECH").ToUpper();
            switch (parameter.GetParameter("PAD").ToLower())
            {
                case "iso-7816":                    
                    var iso7816 = new Iso7816Padding(blocksize);
                    output = iso7816.Unpad(input);
                    break;
                case "iso-9797-m2":
                    var iso9797m2 = new Iso7816Padding(blocksize);
                    output = iso9797m2.Unpad(input);                    
                    break;
                case "iso-9797-m3":                    
                    var iso9797m3 = new Iso9797M3Padding(blocksize);
                    output = iso9797m3.Unpad(input);
                    break;
                case "tls-cbc":                    
                    var tlscbc = new TlsCbcPadding(blocksize);
                    output = tlscbc.Unpad(input);
                    break;
            }
            return output;  
        }

        protected static void ValidateNoPaddingBlockInput(
            ParameterVariableDeclaration parameter,
            byte[] input,
            int blockSize,
            string inputName)
        {
            if (parameter.GetParameter("PAD").Equals("NONE", StringComparison.OrdinalIgnoreCase) &&
                (input.Length == 0 || input.Length % blockSize != 0))
            {
                string mechanism = parameter.GetParameter("MECH").ToUpperInvariant();
                throw new ArgumentException(
                    $"{mechanism} with PAD=NONE requires {inputName} length to be a non-zero multiple of {blockSize} bytes.");
            }
        }

        public byte[] Pad(ParameterVariableDeclaration parameter, out PaddingMode padding, byte[] input, string fn, int blocksize = 16)
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
                    var iso7816 = new Iso7816Padding(blocksize);
                    output = iso7816.Pad(input);
                    break;
                case "iso-9797-m1":
                    padding = PaddingMode.None;
                    var iso9797m1 = new Iso9797M1Padding(blocksize);
                    output = iso9797m1.Pad(input);
                    break;
                case "iso-9797-m2":
                    padding = PaddingMode.None;
                    var iso9797m2 = new Iso7816Padding(blocksize);
                    output = iso9797m2.Pad(input);
                    break;
                case "iso-9797-m3":
                    padding = PaddingMode.None;
                    var iso9797m3 = new Iso9797M3Padding(blocksize);
                    output = iso9797m3.Pad(input);
                    break;
                case "tls-cbc":
                    padding = PaddingMode.None;
                    var tlscbc = new TlsCbcPadding(blocksize);
                    output = tlscbc.Pad(input);
                    break;
                case "none":
                    if (input.Length % blocksize != 0)
                        throw new ArgumentException(
                            mechanism + $" with PAD=NONE requires input length to be a multiple of {blocksize} bytes.");
                    padding = PaddingMode.None;
                    break;
                default:
                    throw new ArgumentException("wrong padding");
            }
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
                    break;
                case "iso-9797-m1":
                    // Method 1 is not self-describing, so decryption retains all
                    // zero bytes and does not attempt to remove padding.
                    padding = PaddingMode.None;
                    break;
                case "iso-9797-m2":
                    padding = PaddingMode.None;                    
                    break;
                case "iso-9797-m3":
                    padding = PaddingMode.None;                    
                    break;
                case "tls-cbc":
                    padding = PaddingMode.None;
                    break;
                case "none":                    
                    padding = PaddingMode.None;
                    break;
                default:
                    throw new ArgumentException("wrong padding");
            }
            return output;
        }
        public byte[] Iso9797M3(byte[] input, int blockSizeBytes)
        {
            return new Iso9797M3Padding(blockSizeBytes).Pad(input);
        }

    }
}
