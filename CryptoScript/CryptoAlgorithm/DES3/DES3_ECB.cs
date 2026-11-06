using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace CryptoScript.CryptoAlgorithm.DES3
{
    public class DES3_ECB : EncryptionMode
    {
        private const int BlockSizeBytes = 8;
        private readonly bool analysisKeys;
        public DES3_ECB() { }
        internal DES3_ECB(bool analysisKeys) => this.analysisKeys = analysisKeys;

        public override StringVariableDeclaration ModeEncryption(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            ValidateNoIv(parameter);
            byte[] keyBytes = GetValidatedKey(key);
            byte[] dataBytes = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            ValidateNoPaddingBlockInput(parameter, dataBytes, BlockSizeBytes, "plaintext");

            byte[] input = Pad(parameter, out PaddingMode padding, dataBytes, "Encrypt", BlockSizeBytes);
            return CreateResult(Transform(keyBytes, input, padding, encrypt: true, analysisKeys));
        }

        public override StringVariableDeclaration ModeDecryption(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            ValidateNoIv(parameter);
            byte[] keyBytes = GetValidatedKey(key);
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            ValidateNoPaddingBlockInput(parameter, input, BlockSizeBytes, "ciphertext");
            if (input.Length == 0 || input.Length % BlockSizeBytes != 0)
                throw new ArgumentException("DES3-ECB ciphertext length must be a non-zero multiple of 8 bytes.");

            SetPadding(parameter, out PaddingMode padding, input, "Decrypt", BlockSizeBytes);
            byte[] decrypted = Transform(keyBytes, input, padding, encrypt: false, analysisKeys);
            return CreateResult(Unpad(parameter, decrypted, "Decrypt", BlockSizeBytes));
        }

        private static byte[] Transform(byte[] key, byte[] input, PaddingMode padding, bool encrypt,
            bool analysisKeys = false)
        {
            if (analysisKeys && TripleDES.IsWeakKey(key))
            {
                string bcPadding = padding switch
                {
                    PaddingMode.None or PaddingMode.ANSIX923 => "NoPadding",
                    PaddingMode.PKCS7 => "PKCS7Padding",
                    PaddingMode.ISO10126 => "ISO10126Padding",
                    _ => throw new ArgumentException("Unsupported DES3 padding.")
                };
                byte[]? temporary = null;
                try
                {
                    if (encrypt && padding == PaddingMode.ANSIX923)
                    {
                        int count = 8 - input.Length % 8;
                        temporary = new byte[checked(input.Length + count)];
                        input.CopyTo(temporary, 0);
                        temporary[^1] = (byte)count;
                    }
                    var cipher = CipherUtilities.GetCipher("DESEDE/ECB/" + bcPadding);
                    cipher.Init(encrypt, new KeyParameter(key));
                    byte[] output = cipher.DoFinal(temporary ?? input);
                    if (!encrypt && padding == PaddingMode.ANSIX923)
                    {
                        int count = output.Length == 0 ? 0 : output[^1];
                        if (count < 1 || count > 8 || count > output.Length ||
                            output.AsSpan(output.Length - count, count - 1).IndexOfAnyExcept((byte)0) >= 0)
                        {
                            CryptographicOperations.ZeroMemory(output);
                            throw new CryptographicException("Invalid ANSI X9.23 padding.");
                        }
                        byte[] clear = output[..^count];
                        CryptographicOperations.ZeroMemory(output);
                        return clear;
                    }
                    return output;
                }
                finally { if (temporary is not null) CryptographicOperations.ZeroMemory(temporary); }
            }
            using TripleDES des3 = TripleDES.Create();
            des3.Mode = CipherMode.ECB;
            des3.Padding = padding;
            des3.Key = key;
            using ICryptoTransform transform = encrypt ? des3.CreateEncryptor() : des3.CreateDecryptor();
            return transform.TransformFinalBlock(input, 0, input.Length);
        }

        internal static byte[] EncryptNoPadding(byte[] key, byte[] plaintext) =>
            Transform(key, plaintext, PaddingMode.None, encrypt: true);

        internal static byte[] DecryptNoPadding(byte[] key, byte[] ciphertext) =>
            Transform(key, ciphertext, PaddingMode.None, encrypt: false);

        internal static byte[] EncryptNoPaddingForAnalysis(byte[] key, byte[] plaintext)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(plaintext);
            DES3.ValidateKeyLength(key);
            if (plaintext.Length % BlockSizeBytes != 0)
                throw new ArgumentException("DES3-ECB input must be a multiple of 8 bytes.", nameof(plaintext));
            return Transform(key, plaintext, PaddingMode.None, encrypt: true, analysisKeys: true);
        }

        // Explicit PIN-block opt-in only. The shared NoPadding primitives above remain strict.
        internal static byte[] EncryptPinBlockForAnalysis(byte[] key, byte[] plaintext) =>
            TransformPinBlockForAnalysis(key, plaintext, encrypt: true);

        internal static byte[] DecryptPinBlockForAnalysis(byte[] key, byte[] ciphertext) =>
            TransformPinBlockForAnalysis(key, ciphertext, encrypt: false);

        private static byte[] TransformPinBlockForAnalysis(byte[] key, byte[] block, bool encrypt)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(block);
            DES3.ValidateKeyLength(key);
            if (block.Length != BlockSizeBytes)
                throw new ArgumentException("TDEA PIN-block analysis requires exactly one 8-byte block.", nameof(block));
            return Transform(key, block, PaddingMode.None, encrypt, analysisKeys: true);
        }

        private byte[] GetValidatedKey(KeyVariableDeclaration key)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            DES3.ValidateKeyLength(keyBytes);
            if (!analysisKeys) DES3.ValidateUsableKey(keyBytes);
            return keyBytes;
        }

        private static void ValidateNoIv(ParameterVariableDeclaration parameter)
        {
            if (parameter.GetParameter("IV") != string.Empty)
                throw new ArgumentException("DES3-ECB does not use an IV.");
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
