using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
namespace CryptoScript.CryptoAlgorithm
{
    public class AES_CMAC : EncryptionMode
    {
        public override StringVariableDeclaration ModeMac(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            IMac mac = new CMac(new AesEngine());
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            mac.Init(new KeyParameter(keyBytes));
            byte[] input=FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            mac.BlockUpdate(input, 0, input.Length);
            byte[] result = new byte[mac.GetMacSize()];
            mac.DoFinal(result, 0);
            StringVariableDeclaration MacValue = new StringVariableDeclaration();
            MacValue.Value = FormatConversions.ByteArrayToHexString(result);
            MacValue.ValueFormat = FormatConversions.ParseString(MacValue.Value);
            MacValue.Type = new CryptoTypeVar();
            return MacValue;            
        }
    }
}
