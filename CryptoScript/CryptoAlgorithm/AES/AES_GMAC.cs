using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
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
            string n = parameter.GetParameter("NONCE");
            byte[] nonce = FormatConversions.ToByteArray(n, FormatConversions.ParseString(n));
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            var gMac = new GMac(new GcmBlockCipher(new AesEngine()));
            var parameters = new ParametersWithIV(new KeyParameter(keyBytes), nonce);
            gMac.Init(parameters);
            // Update with data
            gMac.BlockUpdate(input, 0, input.Length);
            // Output the MAC
            byte[] gmacTag = new byte[16]; // 128 bits
            gMac.DoFinal(gmacTag, 0);
            //Covert to script variable
            StringVariableDeclaration MacValue = new StringVariableDeclaration();
            MacValue.Value = FormatConversions.ByteArrayToHexString(gmacTag);
            MacValue.ValueFormat = FormatConversions.ParseString(MacValue.Value);
            MacValue.Type = new CryptoTypeVar();
            return MacValue;
        }
    }
}
