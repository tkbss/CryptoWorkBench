using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.AES
{
    public class AES_ECB : EncryptionMode
    {
        private const int BlockSizeBytes = 16;

        public override StringVariableDeclaration ModeDecryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            return ECBKernel(parameter, key, data, false);
        }

        public override StringVariableDeclaration ModeEncryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            return ECBKernel(parameter, key, data, true);
        }
        private StringVariableDeclaration ECBKernel(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data, bool encryption)
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

        internal static byte[] EncryptNoPadding(byte[] key, byte[] plaintext) =>
            TransformSingleBlock(key, plaintext, encrypt: true);

        internal static byte[] DecryptNoPadding(byte[] key, byte[] ciphertext) =>
            TransformSingleBlock(key, ciphertext, encrypt: false);

        internal static void ValidateKeyLength(byte[] key)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.Length is not (16 or 24 or 32))
                throw new ArgumentException("AES key must contain 16, 24 or 32 bytes.", nameof(key));
        }

        private static byte[] TransformSingleBlock(byte[] key, byte[] input, bool encrypt)
        {
            ArgumentNullException.ThrowIfNull(input);
            ValidateKeyLength(key);
            if (input.Length != BlockSizeBytes)
                throw new ArgumentException("AES-ECB input must contain exactly 16 bytes.", nameof(input));

            using Aes aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = key;
            using ICryptoTransform transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
            return transform.TransformFinalBlock(input, 0, input.Length);
        }
    }
}
