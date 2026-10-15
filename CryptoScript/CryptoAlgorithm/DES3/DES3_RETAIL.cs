using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.DES3
{
    public class DES3_RETAIL : EncryptionMode
    {
        private const int BlockSize = 8;

        public override StringVariableDeclaration ModeMac(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            ValidateParameters(parameter);

            byte[]? keyBytes = null;
            byte[]? input = null;
            byte[]? result = null;
            try
            {
                keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
                DES3.ValidateKeyLength(keyBytes);
                DES3.ValidateUsableKey(keyBytes);
                input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
                result = ComputeMac(keyBytes, input, parameter.GetParameter("PAD"),
                    int.Parse(parameter.GetParameter("MACLEN")));
                string value = FormatConversions.ByteArrayToHexString(result);
                return new StringVariableDeclaration
                {
                    Value = value,
                    ValueFormat = FormatConversions.ParseString(value),
                    Type = new CryptoTypeVar()
                };
            }
            finally
            {
                if (keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
                if (input is not null) CryptographicOperations.ZeroMemory(input);
                if (result is not null) CryptographicOperations.ZeroMemory(result);
            }
        }

        // Extracted Retail MAC; generic 16/24-byte keys and M1/M2 stay supported.
        // Bouncy Castle owns additional key-schedule copies, not cleared by this adapter.
        internal static byte[] ComputeMac(byte[] key, byte[] data, string padding, int macLength)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(padding);
            ValidatePadding(padding);
            if (macLength < 4 || macLength > 8)
                throw new ArgumentException("DES3-RETAIL MAC length must be between 4 and 8 bytes.");

            byte[]? keySnapshot = null;
            byte[]? dataSnapshot = null;
            byte[]? padded = null;
            byte[]? chainingValue = null;
            byte[]? result = null;
            bool returned = false;
            try
            {
                keySnapshot = (byte[])key.Clone();
                dataSnapshot = (byte[])data.Clone();
                DES3.ValidateKeyLength(keySnapshot);
                DES3.ValidateUsableKey(keySnapshot);
                padded = Pad(dataSnapshot, padding);
                chainingValue = new byte[BlockSize];
                var des = new DesEngine();
                des.Init(true, new KeyParameter(keySnapshot, 0, BlockSize));
                for (int offset = 0; offset < padded.Length; offset += BlockSize)
                {
                    for (int i = 0; i < BlockSize; i++)
                        chainingValue[i] ^= padded[offset + i];
                    des.ProcessBlock(chainingValue, 0, chainingValue, 0);
                }

                des.Init(false, new KeyParameter(keySnapshot, BlockSize, BlockSize));
                des.ProcessBlock(chainingValue, 0, chainingValue, 0);
                int finalKeyOffset = keySnapshot.Length == 16 ? 0 : 2 * BlockSize;
                des.Init(true, new KeyParameter(keySnapshot, finalKeyOffset, BlockSize));
                des.ProcessBlock(chainingValue, 0, chainingValue, 0);
                result = chainingValue[..macLength];
                returned = true;
                return result;
            }
            finally
            {
                if (keySnapshot is not null) CryptographicOperations.ZeroMemory(keySnapshot);
                if (dataSnapshot is not null) CryptographicOperations.ZeroMemory(dataSnapshot);
                if (padded is not null) CryptographicOperations.ZeroMemory(padded);
                if (chainingValue is not null) CryptographicOperations.ZeroMemory(chainingValue);
                if (!returned && result is not null) CryptographicOperations.ZeroMemory(result);
            }
        }

        private static void ValidateParameters(ParameterVariableDeclaration parameter)
        {
            if (parameter.GetParameter("IV") != string.Empty)
                throw new ArgumentException("DES3-RETAIL does not use an IV.");

            ValidatePadding(parameter.GetParameter("PAD"));

            string macLength = parameter.GetParameter("MACLEN");
            if (!int.TryParse(macLength.Trim('"'), out int length) || length < 4 || length > 8)
                throw new ArgumentException("DES3-RETAIL MAC length must be between 4 and 8 bytes.");
        }

        private static void ValidatePadding(string padding)
        {
            if (!padding.Equals("ISO-9797-M1", StringComparison.OrdinalIgnoreCase) &&
                !padding.Equals("ISO-9797-M2", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("DES3-RETAIL supports only ISO-9797-M1 or ISO-9797-M2 padding.");
        }

        private static byte[] Pad(byte[] input, string padding)
        {
            if (padding.Equals("ISO-9797-M1", StringComparison.OrdinalIgnoreCase))
            {
                return new Iso9797M1Padding(BlockSize).Pad(input);
            }

            return new Iso7816Padding(BlockSize).Pad(input);
        }
    }
}
