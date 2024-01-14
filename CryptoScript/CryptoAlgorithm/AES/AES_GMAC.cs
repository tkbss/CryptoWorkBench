using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.CryptoAlgorithm.AES
{
    public class AES_GMAC : EncryptionMode
    {
        public override StringVariableDeclaration ModeMac(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            byte[] nonce = FormatConversions.ToByteArray(parameter.Nonce, FormatConversions.ParseString(parameter.Nonce));
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            GcmBlockCipher gcm = new GcmBlockCipher(new AesEngine());
            AeadParameters parameters = new AeadParameters(new KeyParameter(keyBytes), 128, nonce);

            gcm.Init(true, parameters);
            byte[] output = new byte[gcm.GetOutputSize(input.Length)];
            int len = gcm.ProcessBytes(input, 0, input.Length, output, 0);
            gcm.DoFinal(output, len);
            // The GMAC tag is the last 16 bytes of the Output
            byte[] gmacTag = new byte[16];
            Array.Copy(output, output.Length - 16, gmacTag, 0, 16);
            StringVariableDeclaration MacValue = new StringVariableDeclaration();
            MacValue.Value = FormatConversions.ByteArrayToHexString(gmacTag);
            MacValue.ValueFormat = FormatConversions.ParseString(MacValue.Value);
            MacValue.Type = new CryptoTypeVar();
            return MacValue;
        }
    }
}
