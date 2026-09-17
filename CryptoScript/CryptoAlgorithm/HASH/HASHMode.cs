using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;

namespace CryptoScript.CryptoAlgorithm.HASH
{
    public class HASHMode
    {
        public StringVariableDeclaration ModeHash(
            ParameterVariableDeclaration parameter,
            StringVariableDeclaration data)
        {
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);

            IDigest digest = DigestFactory.Create(parameter.Mechanism);
            digest.BlockUpdate(input, 0, input.Length);
            byte[] result = new byte[digest.GetDigestSize()];
            digest.DoFinal(result, 0);

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
