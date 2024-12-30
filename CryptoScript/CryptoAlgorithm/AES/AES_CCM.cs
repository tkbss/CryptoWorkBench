using CryptoScript.ErrorListner;
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
    public class AES_CCM : EncryptionMode
    {
        public override StringVariableDeclaration ModeDecryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            string n = parameter.GetParameter("NONCE");
            if (string.IsNullOrEmpty(n))
            {
                SemanticError se = new SemanticError();
                se.Type = parameter.Type.Name;
                se.Value = parameter.Value;
                se.FunctionName = "Decryption";
                se.Message = "Nonce is required for GCM decryption";
                throw new SemanticErrorException() { SemanticError = se };
            }
            byte[] nonce = FormatConversions.ToByteArray(n, FormatConversions.ParseString(n));
            byte[]? associatedData = null;
            if (string.IsNullOrEmpty(parameter.GetParameter("ADATA")) == false)
                associatedData = FormatConversions.ToByteArray(parameter.GetParameter("ADATA"), FormatConversions.ParseString(parameter.GetParameter("ADATA")));

            // Create a CCM cipher in 'decrypt' mode
            CcmBlockCipher ccm = new CcmBlockCipher(new AesEngine());
            // We used a 128-bit tag size in encryption (16 bytes),
            // so specify 'macSize' as 128 bits here as well.
            AeadParameters parameters = new AeadParameters(new KeyParameter(keyBytes), 128, nonce);
            try
            {
                // Initialize for decryption (false)
                ccm.Init(false, parameters);
            }
            catch
            {
                SemanticError se = new SemanticError();
                se.Type = parameter.Type.Name;
                se.Value = parameter.Value;
                se.FunctionName = "Decryption";
                se.Message = "Error in initializing decryption";
                throw new SemanticErrorException() { SemanticError = se };
            }
            // If there is associated data (AAD), process it now
            if (associatedData != null && associatedData.Length > 0)
            {
                try
                {
                    ccm.ProcessAadBytes(associatedData, 0, associatedData.Length);
                }
                catch
                {
                    SemanticError se = new SemanticError();
                    se.Type = parameter.Type.Name;
                    se.Value = parameter.Value;
                    se.FunctionName = "Decryption";
                    se.Message = "Invalid ADATA (authentication data)";
                    throw new SemanticErrorException() { SemanticError = se };
                }
            }
            // Get the ciphertext and tag
            byte[] cipherTextAndTag = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            // Prepare buffer for the plaintext output
            // The GetOutputSize includes the space for the tag, but since we
            // are decrypting, the final plaintext will be smaller.
            byte[] plainOutput = new byte[ccm.GetOutputSize(cipherTextAndTag.Length)];

            // Decrypt
            int len = 0;
            try
            {
                len = ccm.ProcessBytes(cipherTextAndTag, 0, cipherTextAndTag.Length, plainOutput, 0);
            }
            catch
            {
                SemanticError se = new SemanticError();
                se.Type = data.Type.Name;
                se.Value = data.Value;
                se.FunctionName = "Decryption";
                se.Message = "Error in decrypting data";
                throw new SemanticErrorException() { SemanticError = se };
            }
            try
            {
                // Finalize the decryption operation.
                // If the tag is invalid, this line will throw an InvalidCipherTextException.
                ccm.DoFinal(plainOutput, len);
            }
            catch
            {
                SemanticError se = new SemanticError();
                se.Type = data.Type.Name;
                se.Value = data.Value;
                se.FunctionName = "Decryption";
                se.Message = "Error in authenticate data during decryption";
                throw new SemanticErrorException() { SemanticError = se };
            }

            // Bouncy Castle’s CCM cipher typically returns the entire plaintext
            // in plainOutput up to (len + any bytes from DoFinal).
            // If you want to be strict, you could slice it accordingly,
            // but in GCM usually plainOutput is exactly the plaintext size after DoFinal.
            StringVariableDeclaration plaintext = new StringVariableDeclaration();
            plaintext.Value = FormatConversions.ByteArrayToHexString(plainOutput);
            plaintext.ValueFormat = FormatConversions.ParseString(plaintext.Value);
            plaintext.Type = new CryptoTypeVar();
            return plaintext;

        }

        public override StringVariableDeclaration ModeEncryption(ParameterVariableDeclaration parameter, KeyVariableDeclaration key, StringVariableDeclaration data)
        {
           
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            string n = parameter.GetParameter("NONCE");
            if (string.IsNullOrEmpty(n))
            {
                SemanticError se = new SemanticError();
                se.Type = parameter.Type.Name;
                se.Value = parameter.Value;
                se.FunctionName = "Encryption";
                se.Message = "Nonce is required for CCM encryption";
                throw new SemanticErrorException() { SemanticError = se };
            }
            byte[] nonce = FormatConversions.ToByteArray(n, FormatConversions.ParseString(n));
            byte[]? associatedData = null;
            if (string.IsNullOrEmpty(parameter.GetParameter("ADATA")) == false)
                associatedData = FormatConversions.ToByteArray(parameter.GetParameter("ADATA"), FormatConversions.ParseString(parameter.GetParameter("ADATA")));
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
            // Create a CCM block cipher using AES
            CcmBlockCipher ccm = new CcmBlockCipher(new AesEngine());
            AeadParameters parameters = new AeadParameters(new KeyParameter(keyBytes), 128, nonce);
            try
            {
                // Initialize for encryption (true)
                ccm.Init(true, parameters);
            }
            catch
            {
                SemanticError se = new SemanticError();
                se.Type = parameter.Type.Name;
                se.Value = parameter.Value;
                se.FunctionName = "Encryption";
                se.Message = "Error in initializing encryption";
                throw new SemanticErrorException() { SemanticError = se };
            }
            // Process the associated data - it will be authenticated but not encrypted
            if (associatedData != null)
            {
                try
                {
                    ccm.ProcessAadBytes(associatedData, 0, associatedData.Length);
                }
                catch
                {
                    SemanticError se = new SemanticError();
                    se.Type = parameter.Type.Name;
                    se.Value = parameter.Value;
                    se.FunctionName = "Encryption";
                    se.Message = "Invalid ADATA (authentication data)";
                    throw new SemanticErrorException() { SemanticError = se };
                }

            }

            // Create a buffer to hold the encryption output, which includes ciphertext
            byte[] encOutput = new byte[ccm.GetOutputSize(input.Length)];
            int len = 0;
            try
            {
                // Process the plaintext
                len = ccm.ProcessBytes(input, 0, input.Length, encOutput, 0);
            }
            catch
            {
                SemanticError se = new SemanticError();
                se.Type = parameter.Type.Name;
                se.Value = parameter.Value;
                se.FunctionName = "Encrypt";
                se.Message = "Error in encrypting data";
                throw new SemanticErrorException() { SemanticError = se };
            }
            try
            {
                // Finalize the encryption operation
                ccm.DoFinal(encOutput, len);
            }
            catch
            {
                SemanticError se = new SemanticError();
                se.Type = parameter.Type.Name;
                se.Value = parameter.Value;
                se.FunctionName = "Encrypt";
                se.Message = "Error in finalize encryption and computing CMAC";
                throw new SemanticErrorException() { SemanticError = se };
            }


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
