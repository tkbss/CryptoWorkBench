using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.DES3
{
    public class DES3_CBC : EncryptionMode
    {
        private const int BlockSizeBytes = 8;

        public override StringVariableDeclaration ModeEncryption(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            byte[] keyBytes = GetValidatedKey(key);
            byte[] ivBytes = GetValidatedIv(parameter);
            byte[] dataBytes = FormatConversions.ToByteArray(data.Value, data.ValueFormat);

            byte[] input = Pad(parameter, out PaddingMode padding, dataBytes, "Encrypt", BlockSizeBytes);
            byte[] encrypted = Transform(keyBytes, ivBytes, input, padding, encrypt: true);
            return CreateResult(encrypted);
        }

        public override StringVariableDeclaration ModeDecryption(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            byte[] keyBytes = GetValidatedKey(key);
            byte[] ivBytes = GetValidatedIv(parameter);
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            if (input.Length == 0 || input.Length % BlockSizeBytes != 0)
                throw new ArgumentException("DES3-CBC ciphertext length must be a non-zero multiple of 8 bytes.");

            SetPadding(parameter, out PaddingMode padding, input, "Decrypt", BlockSizeBytes);
            byte[] decrypted = Transform(keyBytes, ivBytes, input, padding, encrypt: false);
            decrypted = Unpad(parameter, decrypted, "Decrypt", BlockSizeBytes);
            return CreateResult(decrypted);
        }

        public override StringVariableDeclaration ModeMac(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            byte[] keyBytes = GetValidatedKey(key);
            byte[] dataBytes = FormatConversions.ToByteArray(data.Value, data.ValueFormat);

            byte[] input = Pad(parameter, out PaddingMode padding, dataBytes, "Mac", BlockSizeBytes);
            byte[] encrypted = Transform(keyBytes, new byte[BlockSizeBytes], input, padding, encrypt: true);
            if (encrypted.Length == 0)
                throw new ArgumentException("DES3-CBC-MAC requires padding that produces at least one 8-byte block.");

            int macLength = GetMacLength(parameter);
            byte[] mac = encrypted[(encrypted.Length - BlockSizeBytes)..][..macLength];
            return CreateResult(mac);
        }

        private static byte[] Transform(
            byte[] key,
            byte[] iv,
            byte[] input,
            PaddingMode padding,
            bool encrypt)
        {
            using TripleDES des3 = TripleDES.Create();
            des3.Mode = CipherMode.CBC;
            des3.Padding = padding;
            des3.Key = key;
            des3.IV = iv;
            using ICryptoTransform transform = encrypt
                ? des3.CreateEncryptor()
                : des3.CreateDecryptor();
            return transform.TransformFinalBlock(input, 0, input.Length);
        }

        private static byte[] GetValidatedKey(KeyVariableDeclaration key)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            DES3.ValidateKeyLength(keyBytes);
            DES3.ValidateUsableKey(keyBytes);
            return keyBytes;
        }

        private static byte[] GetValidatedIv(ParameterVariableDeclaration parameter)
        {
            string iv = parameter.GetParameter("IV");
            byte[] ivBytes = FormatConversions.ToByteArray(iv, FormatConversions.ParseString(iv));
            if (ivBytes.Length != BlockSizeBytes)
                throw new ArgumentException("DES3-CBC IV must contain exactly 8 bytes.");
            return ivBytes;
        }

        private static int GetMacLength(ParameterVariableDeclaration parameter)
        {
            string value = parameter.GetParameter("MACLEN");
            if (value == string.Empty)
                return BlockSizeBytes;
            if (!int.TryParse(value.Trim('"'), out int length) || length < 4 || length > BlockSizeBytes)
                throw new ArgumentException("DES3-CBC MAC length must be between 4 and 8 bytes.");
            return length;
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
