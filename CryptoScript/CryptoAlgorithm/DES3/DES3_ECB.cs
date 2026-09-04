using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.DES3
{
    public class DES3_ECB : EncryptionMode
    {
        private const int BlockSizeBytes = 8;

        public override StringVariableDeclaration ModeEncryption(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            ValidateNoIv(parameter);
            byte[] keyBytes = GetValidatedKey(key);
            byte[] dataBytes = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            ValidateNoPaddingAlignment(parameter, dataBytes, "plaintext");

            byte[] input = Pad(parameter, out PaddingMode padding, dataBytes, "Encrypt", BlockSizeBytes);
            return CreateResult(Transform(keyBytes, input, padding, encrypt: true));
        }

        public override StringVariableDeclaration ModeDecryption(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            ValidateNoIv(parameter);
            byte[] keyBytes = GetValidatedKey(key);
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            if (input.Length == 0 || input.Length % BlockSizeBytes != 0)
                throw new ArgumentException("DES3-ECB ciphertext length must be a non-zero multiple of 8 bytes.");

            SetPadding(parameter, out PaddingMode padding, input, "Decrypt", BlockSizeBytes);
            byte[] decrypted = Transform(keyBytes, input, padding, encrypt: false);
            return CreateResult(Unpad(parameter, decrypted, "Decrypt", BlockSizeBytes));
        }

        private static byte[] Transform(byte[] key, byte[] input, PaddingMode padding, bool encrypt)
        {
            using TripleDES des3 = TripleDES.Create();
            des3.Mode = CipherMode.ECB;
            des3.Padding = padding;
            des3.Key = key;
            using ICryptoTransform transform = encrypt ? des3.CreateEncryptor() : des3.CreateDecryptor();
            return transform.TransformFinalBlock(input, 0, input.Length);
        }

        private static byte[] GetValidatedKey(KeyVariableDeclaration key)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            DES3.ValidateKeyLength(keyBytes);
            DES3.ValidateUsableKey(keyBytes);
            return keyBytes;
        }

        private static void ValidateNoIv(ParameterVariableDeclaration parameter)
        {
            if (parameter.GetParameter("IV") != string.Empty)
                throw new ArgumentException("DES3-ECB does not use an IV.");
        }

        private static void ValidateNoPaddingAlignment(
            ParameterVariableDeclaration parameter,
            byte[] input,
            string inputName)
        {
            if (parameter.GetParameter("PAD").Equals("NONE", StringComparison.OrdinalIgnoreCase) &&
                input.Length % BlockSizeBytes != 0)
                throw new ArgumentException($"DES3-ECB with PAD=NONE requires {inputName} length to be a multiple of 8 bytes.");
        }

        private static StringVariableDeclaration CreateResult(byte[] value)
        {
            string encoded = FormatConversions.ByteArrayToHexString(value);
            return new StringVariableDeclaration
            {
                Value = encoded,
                ValueFormat = FormatConversions.ParseString(encoded),
                Type = new CryptoTypeVar()
            };
        }
    }
}
