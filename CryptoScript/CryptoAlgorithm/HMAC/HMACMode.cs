using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.CryptoAlgorithm.HMAC
{
    public class HMACMode : EncryptionMode
    {
        public override StringVariableDeclaration ModeMac(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);

            IMac mac = new HMac(DigestFactory.Create(parameter.Mechanism));
            mac.Init(new KeyParameter(keyBytes));
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
