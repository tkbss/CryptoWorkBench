using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.CryptoAlgorithm.DES3
{
    public class DES3_CMAC : EncryptionMode
    {
        private readonly bool analysisKeys;
        public DES3_CMAC() { }
        internal DES3_CMAC(bool analysisKeys) => this.analysisKeys = analysisKeys;
        public override StringVariableDeclaration ModeMac(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            DES3.ValidateKeyLength(keyBytes);
            if (!analysisKeys) DES3.ValidateUsableKey(keyBytes);

            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            byte[] result = Compute(keyBytes, input);

            string value = FormatConversions.ByteArrayToHexString(result);
            return new StringVariableDeclaration
            {
                Value = value,
                ValueFormat = FormatConversions.ParseString(value),
                Type = new CryptoTypeVar()
            };
        }

        // Explicit analysis boundary; no quality gate on the effective key bits.
        internal static byte[] ComputeForAnalysis(byte[] keyBytes, byte[] input)
        {
            ArgumentNullException.ThrowIfNull(keyBytes);
            ArgumentNullException.ThrowIfNull(input);
            DES3.ValidateKeyLength(keyBytes);
            return Compute(keyBytes, input);
        }

        // Existing shared primitive: callers validate the key before invoking it.
        internal static byte[] Compute(byte[] keyBytes, byte[] input)
        {
            IMac mac = new CMac(new DesEdeEngine());
            mac.Init(new KeyParameter(keyBytes));
            mac.BlockUpdate(input, 0, input.Length);
            byte[] result = new byte[mac.GetMacSize()];
            mac.DoFinal(result, 0);
            return result;
        }
    }
}
