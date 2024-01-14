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
    public class AES_GCM : EncryptionMode
    {
        public override StringVariableDeclaration ModeDecryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            return ModeEncryption(parameter, key, data);
        }

        public override StringVariableDeclaration ModeEncryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            // Create a GCM block cipher using AES
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            byte[] nonce = FormatConversions.ToByteArray(parameter.Nonce, FormatConversions.ParseString(parameter.Nonce));
            byte[] associatedData = null;
            if(string.IsNullOrEmpty(parameter.AData)==false)
                associatedData=FormatConversions.ToByteArray(parameter.AData, FormatConversions.ParseString(parameter.AData));
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            GcmBlockCipher gcm = new GcmBlockCipher(new AesEngine());
            AeadParameters parameters = new AeadParameters(new KeyParameter(keyBytes), 128, nonce);

            // Initialize for encryption (true)
            gcm.Init(true, parameters);

            // Process the associated data - it will be authenticated but not encrypted
            if (associatedData != null)
            {
                gcm.ProcessAadBytes(associatedData, 0, associatedData.Length);
            }

            // Create a buffer to hold the encryption output, which includes ciphertext
            byte[] encOutput = new byte[gcm.GetOutputSize(input.Length)];

            // Process the plaintext
            int len = gcm.ProcessBytes(input, 0, input.Length, encOutput, 0);

            // Finalize the encryption operation
            gcm.DoFinal(encOutput, len);

            // The GMAC tag is the last 16 bytes of the encOutput
            byte[] gmacTag = new byte[16];
            Array.Copy(encOutput, encOutput.Length - 16, gmacTag, 0, 16);
            StringVariableDeclaration cyphertext = new StringVariableDeclaration();
            cyphertext.Value = FormatConversions.ByteArrayToHexString(encOutput);
            cyphertext.GMAC = FormatConversions.ByteArrayToHexString(gmacTag);
            cyphertext.ValueFormat = FormatConversions.ParseString(cyphertext.Value);
            cyphertext.Type = new CryptoTypeVar();
            return cyphertext;
        }
    }
}
