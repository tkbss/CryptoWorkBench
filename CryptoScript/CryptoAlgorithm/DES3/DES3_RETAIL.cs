using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

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

            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            DES3.ValidateKeyLength(keyBytes);
            DES3.ValidateUsableKey(keyBytes);

            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            byte[] padded = Pad(input, parameter.GetParameter("PAD"));
            byte[] chainingValue = new byte[BlockSize];
            var des = new DesEngine();
            des.Init(true, new KeyParameter(keyBytes, 0, BlockSize));

            for (int offset = 0; offset < padded.Length; offset += BlockSize)
            {
                for (int i = 0; i < BlockSize; i++)
                    chainingValue[i] ^= padded[offset + i];
                des.ProcessBlock(chainingValue, 0, chainingValue, 0);
            }

            des.Init(false, new KeyParameter(keyBytes, BlockSize, BlockSize));
            des.ProcessBlock(chainingValue, 0, chainingValue, 0);
            int finalKeyOffset = keyBytes.Length == 16 ? 0 : 2 * BlockSize;
            des.Init(true, new KeyParameter(keyBytes, finalKeyOffset, BlockSize));
            des.ProcessBlock(chainingValue, 0, chainingValue, 0);

            int macLength = int.Parse(parameter.GetParameter("MACLEN"));
            byte[] result = chainingValue[..macLength];
            string value = FormatConversions.ByteArrayToHexString(result);
            return new StringVariableDeclaration
            {
                Value = value,
                ValueFormat = FormatConversions.ParseString(value),
                Type = new CryptoTypeVar()
            };
        }

        private static void ValidateParameters(ParameterVariableDeclaration parameter)
        {
            if (parameter.GetParameter("IV") != string.Empty)
                throw new ArgumentException("DES3-RETAIL does not use an IV.");

            string padding = parameter.GetParameter("PAD");
            if (!padding.Equals("ISO-9797-M1", StringComparison.OrdinalIgnoreCase) &&
                !padding.Equals("ISO-9797-M2", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("DES3-RETAIL supports only ISO-9797-M1 or ISO-9797-M2 padding.");

            string macLength = parameter.GetParameter("MACLEN");
            if (!int.TryParse(macLength.Trim('"'), out int length) || length < 4 || length > 8)
                throw new ArgumentException("DES3-RETAIL MAC length must be between 4 and 8 bytes.");
        }

        private static byte[] Pad(byte[] input, string padding)
        {
            if (padding.Equals("ISO-9797-M1", StringComparison.OrdinalIgnoreCase))
            {
                if (input.Length == 0)
                    throw new ArgumentException("DES3-RETAIL with ISO-9797-M1 requires a non-empty message.");
                int paddedLength = ((input.Length + BlockSize - 1) / BlockSize) * BlockSize;
                byte[] output = new byte[paddedLength];
                Buffer.BlockCopy(input, 0, output, 0, input.Length);
                return output;
            }

            int m2Length = ((input.Length + 1 + BlockSize - 1) / BlockSize) * BlockSize;
            byte[] m2Output = new byte[m2Length];
            Buffer.BlockCopy(input, 0, m2Output, 0, input.Length);
            m2Output[input.Length] = 0x80;
            return m2Output;
        }
    }
}
