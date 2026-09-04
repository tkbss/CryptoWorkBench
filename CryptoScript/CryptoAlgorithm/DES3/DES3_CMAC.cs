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
        public override StringVariableDeclaration ModeMac(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            DES3.ValidateKeyLength(keyBytes);
            DES3.ValidateUsableKey(keyBytes);

            IMac mac = new CMac(new DesEdeEngine());
            mac.Init(new KeyParameter(keyBytes));
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            mac.BlockUpdate(input, 0, input.Length);
            byte[] result = new byte[mac.GetMacSize()];
            mac.DoFinal(result, 0);

            string value = FormatConversions.ByteArrayToHexString(result);
            return new StringVariableDeclaration
            {
                Value = value,
                ValueFormat = FormatConversions.ParseString(value),
                Type = new CryptoTypeVar()
            };
        }
    }
}
