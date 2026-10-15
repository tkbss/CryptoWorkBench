using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace CryptoScript.CryptoAlgorithm.DES3
{
    public class DES3_CBC : EncryptionMode
    {
        private const int BlockSizeBytes = 8;
        private readonly bool analysisKeys;
        public DES3_CBC() { }
        internal DES3_CBC(bool analysisKeys) => this.analysisKeys = analysisKeys;

        public override StringVariableDeclaration ModeEncryption(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            byte[] keyBytes = GetValidatedKey(key);
            byte[] ivBytes = GetValidatedIv(parameter);
            byte[] dataBytes = FormatConversions.ToByteArray(data.Value, data.ValueFormat);

            ValidateNoPaddingBlockInput(parameter, dataBytes, BlockSizeBytes, "plaintext");
            byte[] input = Pad(parameter, out PaddingMode padding, dataBytes, "Encrypt", BlockSizeBytes);
            byte[] encrypted = Transform(keyBytes, ivBytes, input, padding, encrypt: true, analysisKeys);
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
            ValidateNoPaddingBlockInput(parameter, input, BlockSizeBytes, "ciphertext");
            if (input.Length == 0 || input.Length % BlockSizeBytes != 0)
                throw new ArgumentException("DES3-CBC ciphertext length must be a non-zero multiple of 8 bytes.");

            SetPadding(parameter, out PaddingMode padding, input, "Decrypt", BlockSizeBytes);
            byte[] decrypted = Transform(keyBytes, ivBytes, input, padding, encrypt: false, analysisKeys);
            decrypted = Unpad(parameter, decrypted, "Decrypt", BlockSizeBytes);
            return CreateResult(decrypted);
        }

        public override StringVariableDeclaration ModeMac(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            if (parameter.GetParameter("PAD").Equals("ISO-10126", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("DES3-CBC-MAC does not support ISO-10126 padding.");

            byte[] keyBytes = GetValidatedKey(key);
            byte[] dataBytes = FormatConversions.ToByteArray(data.Value, data.ValueFormat);

            byte[] input = PadToCompleteBlocks(parameter, dataBytes, "Mac", BlockSizeBytes);
            if (input.Length == 0)
                throw new ArgumentException("DES3-CBC-MAC requires padding that produces at least one 8-byte block.");

            int macLength = GetMacLength(parameter);
            if (analysisKeys && TripleDES.IsWeakKey(keyBytes))
            {
                byte[] encrypted = Transform(keyBytes, new byte[8], input, PaddingMode.None, true, true);
                try { return CreateResult(encrypted.AsSpan(encrypted.Length - 8, macLength).ToArray()); }
                finally { CryptographicOperations.ZeroMemory(encrypted); }
            }
            byte[] mac = CbcMacPrimitive.Compute(
                TripleDES.Create, keyBytes, input, BlockSizeBytes, macLength);
            return CreateResult(mac);
        }

        internal static byte[] EncryptNoPadding(byte[] key, byte[] iv, byte[] plaintext) =>
            Transform(key, iv, plaintext, PaddingMode.None, encrypt: true);

        internal static byte[] DecryptNoPadding(byte[] key, byte[] iv, byte[] ciphertext) =>
            Transform(key, iv, ciphertext, PaddingMode.None, encrypt: false);

        internal static byte[] ComputeMacNoPadding(byte[] key, byte[] data, int macLength)
        {
            if (data.Length == 0 || data.Length % BlockSizeBytes != 0)
                throw new ArgumentException("DES3-CBC-MAC input must be a non-zero multiple of 8 bytes.", nameof(data));
            if (macLength < 1 || macLength > BlockSizeBytes)
                throw new ArgumentOutOfRangeException(nameof(macLength));

            return CbcMacPrimitive.Compute(
                TripleDES.Create, key, data, BlockSizeBytes, macLength);
        }

        private static byte[] Transform(
            byte[] key,
            byte[] iv,
            byte[] input,
            PaddingMode padding,
            bool encrypt, bool analysisKeys = false)
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
                    var cipher = CipherUtilities.GetCipher("DESEDE/CBC/" + bcPadding);
                    cipher.Init(encrypt, new ParametersWithIV(new KeyParameter(key), iv));
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
            des3.Mode = CipherMode.CBC;
            des3.Padding = padding;
            des3.Key = key;
            des3.IV = iv;
            using ICryptoTransform transform = encrypt
                ? des3.CreateEncryptor()
                : des3.CreateDecryptor();
            return transform.TransformFinalBlock(input, 0, input.Length);
        }

        private byte[] GetValidatedKey(KeyVariableDeclaration key)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            DES3.ValidateKeyLength(keyBytes);
            if (!analysisKeys) DES3.ValidateUsableKey(keyBytes);
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
