using CryptoScript.Model;
using CryptoScript.Variables;

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

            byte[] result = HmacPrimitive.Compute(parameter.Mechanism, keyBytes, input);

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
