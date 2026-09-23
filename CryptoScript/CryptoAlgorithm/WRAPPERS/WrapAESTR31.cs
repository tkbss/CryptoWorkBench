using CryptoScript.Variables;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Modes;
using CryptoScript.Model;
using Microsoft.VisualBasic;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS
{
    public class WrapAESTR31 : CryptoAlgorithm
    {
        /// <summary>
        /// Unwraps a TR-31 block, returning the unwrapped key as a KeyVariableDeclaration.
        /// </summary>
        /// <param name="parameters">
        /// parameters[0]: name/id for a ParameterVariableDeclaration
        /// parameters[1]: the name of the "protection key" variable
        /// parameters[2]: the name of the string variable containing the TR-31 block
        /// </param>
        /// <returns>The unwrapped key in a KeyVariableDeclaration</returns>
        public override KeyVariableDeclaration Unwrap(string[] parameters)
        {
            // 1) Set up ParameterVariableDeclaration from parameters[0], as in Wrap
            ParameterVariableDeclaration p = new ParameterVariableDeclaration();
            p.SetInstance(parameters[0]);

            // 2) Retrieve the keyProtectionKey and the TR-31 block from the variable dictionary
            var variables = VariableDictionary.Instance().GetVariables();

            KeyVariableDeclaration? keyProtectionKey = null;
            StringVariableDeclaration? wrappedBlockVar = null;

            foreach (var variable in variables)
            {
                // The name is in variable.Value
                if (variable.Value == parameters[1])
                {
                    // Found the key-protection key used for wrapping
                    keyProtectionKey = (KeyVariableDeclaration)variable;
                }
                else if (variable.Value == parameters[2])
                {
                    // Found the string that contains the TR-31 block
                    wrappedBlockVar = (StringVariableDeclaration)variable;
                }
            }

            if (keyProtectionKey == null)
                throw new Exception("Protection key variable not found: " + parameters[1]);
            if (wrappedBlockVar == null)
                throw new Exception("TR-31 block variable not found: " + parameters[2]);
            
            byte[]? blockHeaderBytes =null;
            byte[]? encryptedKeyData = null;
            byte[]? blockMac = null;
            TR31Block? block =null;
            // 3) Parse the TR-31 block into (header, wrappedKey, mac).
            //    This depends on how your TR31String is implemented.
            //    Example assumes a constructor that can parse a string:
            if (FormatConversions.ParseString(wrappedBlockVar.Value) == FormatConversions.TR31)
            {
                TR31String tr31 = TR31String.FromString(wrappedBlockVar.Value);
                RequireVersionD(tr31.Block);
                block = TR31Block.FromString(tr31.Block);
                blockHeaderBytes = FormatConversions.StringToByteArray(tr31.Block);
                block.Cryptogram=encryptedKeyData = tr31.Cryptogram;
                block.Mac=blockMac = tr31.Mac;
            }
            if(FormatConversions.ParseString(wrappedBlockVar.Value) == FormatConversions.STR) 
            {
                RequireVersionD(FormatConversions.ToString(wrappedBlockVar.Value));
                wrappedBlockVar.Value = FormatConversions.ToString(wrappedBlockVar.Value);
                block =TR31Block.FromString(wrappedBlockVar.Value);
                blockHeaderBytes = block.HeaderDataToMac;
                encryptedKeyData = block.Cryptogram;
                blockMac = block.Mac;
            }
            // 4) Derive the encryption and MAC keys from the protection key
            byte[] masterKeyBytes = FormatConversions.HexStringToByteArray(keyProtectionKey.KeyValue);
            int protKeySizeBits = Convert.ToInt32(keyProtectionKey.KeySize);

            byte[] macKey = DeriveKey(masterKeyBytes, MACDerivationData(protKeySizeBits), protKeySizeBits);
            byte[] encKey = DeriveKey(masterKeyBytes, EncryptionDerivationData(protKeySizeBits), protKeySizeBits);

            // 5) Decrypt using AES/CBC/no-padding with the derived encKey
            //    and the TR-31 MAC as IV. That recovers the raw key data block.
            byte[] recoveredKeyData = DecryptNoPadding(encKey, blockMac, encryptedKeyData);

            // 6) Verify the MAC: reconstruct dataToMAC = blockHeader + recoveredKeyData,
            //    compute MAC with macKey, and compare to blockMac.
            byte[] dataToMAC = new byte[blockHeaderBytes.Length + recoveredKeyData.Length];
            Array.Copy(blockHeaderBytes, 0, dataToMAC, 0, blockHeaderBytes.Length);
            Array.Copy(recoveredKeyData, 0, dataToMAC, blockHeaderBytes.Length, recoveredKeyData.Length);

            byte[] recomputedMac = ComputeMAC(dataToMAC, macKey);

            // Compare
            if (!AreEqual(recomputedMac, blockMac))
            {
                throw new Exception("TR-31 block MAC verification failed.");
            }

            // 7) Parse the recoveredKeyData to figure out the actual key bits.
            //    This is the reverse of ConstructBinaryKeyData. You can examine
            //    length/marker bytes for 128,192,256 etc. Below is a simple example:
            (string actualKeyHex, int actualBitSize) = ParseBinaryKeyData(recoveredKeyData);

            // 8) Build a KeyVariableDeclaration to hold the result
            KeyVariableDeclaration unwrappedKey = new KeyVariableDeclaration()
            {
                KeyValue = actualKeyHex,
                KeySize = actualBitSize.ToString(),  // or store the bits as needed
                KeyType = KeyTypeFromHeader(block.Header),
                Value = actualKeyHex, // pick some name or pass in an extra param
                KeyAttributes=block.HeaderOptionalBlocks(),
                Type =new CryptoTypeKey()
            };

            // Optionally store it in the dictionary if you want it globally accessible:
            // VariableDictionary.Instance().AddOrReplace(unwrappedKey);

            // 9) Return the KeyVariableDeclaration
            return unwrappedKey;
        }

        /// <summary>
        /// Decrypt with AES/CBC/NoPadding. Mirrors EncryptNoPadding but for decryption.
        /// </summary>
        private byte[] DecryptNoPadding(byte[] key, byte[] iv, byte[] ciphertext)
        {
            var cipher = new BufferedBlockCipher(new CbcBlockCipher(new AesEngine()));
            cipher.Init(false, new ParametersWithIV(new KeyParameter(key), iv));
            byte[] outputBuffer = new byte[cipher.GetOutputSize(ciphertext.Length)];
            int outputLength = cipher.ProcessBytes(ciphertext, 0, ciphertext.Length, outputBuffer, 0);
            cipher.DoFinal(outputBuffer, outputLength);
            return outputBuffer;
        }

        /// <summary>
        /// Compare two byte arrays in constant time. 
        /// </summary>
        private bool AreEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= (a[i] ^ b[i]);
            }
            return diff == 0;
        }

        /// <summary>
        /// Reverse of ConstructBinaryKeyData. Inspects recoveredKeyData’s length 
        /// and marker bytes to figure out whether it’s a 128/192/256-bit key. 
        /// Then extracts the actual key bytes (and ignores random).
        /// </summary>
        private (string keyHex, int keyBits) ParseBinaryKeyData(byte[] recovered)
        {
            // Example logic matching your ConstructBinaryKeyData:
            //  - For 128 bits: total length 32 bytes, recovered[1] = 0x80
            //  - For 192 bits: total length 32 bytes, recovered[1] = 0xC0
            //  - For 256 bits: total length 48 bytes, recovered[0] = 0x01
            // (You may adjust if your data has other markers)

            if (recovered.Length == 32 && recovered[1] == 0x80)
            {
                // 128-bit key
                byte[] keyBytes = new byte[16];
                Array.Copy(recovered, 2, keyBytes, 0, 16);
                return (FormatConversions.ByteArrayToHexString(keyBytes), 128);
            }
            else if (recovered.Length == 32 && recovered[1] == 0xC0)
            {
                // 192-bit key
                byte[] keyBytes = new byte[24];
                Array.Copy(recovered, 2, keyBytes, 0, 24);
                return (FormatConversions.ByteArrayToHexString(keyBytes), 192);
            }
            else if (recovered.Length == 48 && recovered[0] == 0x01)
            {
                // 256-bit key
                byte[] keyBytes = new byte[32];
                Array.Copy(recovered, 2, keyBytes, 0, 32);
                return (FormatConversions.ByteArrayToHexString(keyBytes), 256);
            }
            else 
            {
                //arbitary length in bits defined in the first two bytes
                int l = (recovered[0] << 8) | recovered[1];
                l = l / 8;
                byte[] keyBytes = new byte[l];
                Array.Copy(recovered, 2, keyBytes, 0, l);
                return (FormatConversions.ByteArrayToHexString(keyBytes),l*8);
            }                
        }
        public override StringVariableDeclaration Wrap(string[] parameters)
        {
            ParameterVariableDeclaration p = new ParameterVariableDeclaration();
            p.SetInstance(parameters[0]);
            RequireVersionD(FormatConversions.ToString(p.GetParameter("#BLKH")));
            string rnd = p.GetParameter("#RND");
            var variables=VariableDictionary.Instance().GetVariables();
            KeyVariableDeclaration? keyProtectionKey = null;
            KeyVariableDeclaration? keyToWrap = null;
            foreach (var variable in variables)
            {
                if (variable.Value == parameters[1])
                {
                    keyProtectionKey = (KeyVariableDeclaration)variable;
                }
                if (variable.Value == parameters[2])
                {
                    keyToWrap = (KeyVariableDeclaration)variable;
                }
            }
            byte[] macKey=DeriveKey(FormatConversions.HexStringToByteArray(keyProtectionKey.KeyValue), 
                MACDerivationData(Convert.ToInt32(keyProtectionKey.KeySize)),
                Convert.ToInt32(keyProtectionKey.KeySize));
            byte[] encryptionKey = DeriveKey(FormatConversions.HexStringToByteArray(keyProtectionKey.KeyValue),
                EncryptionDerivationData(Convert.ToInt32(keyProtectionKey.KeySize)),
                Convert.ToInt32(keyProtectionKey.KeySize));
            byte[] keyData = ConstructBinaryKeyData(keyToWrap, rnd);
            byte[] dataToMAC = ConstructDataToMAC(p, keyData);
            byte[] macResult = ComputeMAC(dataToMAC, macKey);
            byte[] wrappedKey = EncryptNoPadding(encryptionKey, macResult, keyData);
            string header = FormatConversions.ToString(p.GetParameter("#BLKH"));
            ValidateDeclaredBlockLength(header, wrappedKey.Length, macResult.Length);
            TR31String tr31 = new TR31String(header, wrappedKey, macResult);            
            StringVariableDeclaration block = new StringVariableDeclaration() { Value = tr31.ToString(), ValueFormat = FormatConversions.TR31 };
            return block;
        }        
        private static void RequireVersionD(string header)
        {
            if (string.IsNullOrEmpty(header) || header[0] != 'D')
                throw new NotSupportedException("WRAP-AES-TR31 supports only version D.");
        }

        byte[] ComputeMAC(byte[] dataToMAC, byte[] key)
        {            
            IMac mac = new CMac(new AesEngine());
            mac.Init(new KeyParameter(key));
            byte[] result = new byte[mac.GetMacSize()];            
            mac.BlockUpdate(dataToMAC, 0, dataToMAC.Length);
            mac.DoFinal(result, 0);
            return result;
        }
        private  byte[] EncryptNoPadding(byte[] key, byte[] iv, byte[] plaintext)
        {           

            // Create a BufferedBlockCipher with AES/CBC **without** a padding layer
            var cipher = new BufferedBlockCipher(new CbcBlockCipher(new AesEngine()));
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
            // Process encryption
            byte[] outputBuffer = new byte[cipher.GetOutputSize(plaintext.Length)];
            int outputLength = cipher.ProcessBytes(plaintext, 0, plaintext.Length, outputBuffer, 0);
            // Finalize (should not add padding, so typically returns 0 additional bytes)
            cipher.DoFinal(outputBuffer, outputLength);            
            return outputBuffer;
        }
        private byte[] ConstructDataToMAC(ParameterVariableDeclaration p, byte[]keyData) 
        { 
            string blk=p.GetParameter("#BLKH");
            byte[] byteArray=FormatConversions.StringToByteArray(blk);
            byte[] dataToMAC = new byte[byteArray.Length + keyData.Length];
            Array.Copy(byteArray, 0, dataToMAC, 0, byteArray.Length);
            Array.Copy(keyData, 0, dataToMAC, byteArray.Length, keyData.Length);
            return dataToMAC;
        }
        private byte[] ConstructBinaryKeyData(KeyVariableDeclaration keyToWrap, string rd)
        {
            byte[]? random = string.IsNullOrEmpty(rd) ? null : FormatConversions.HexStringToByteArray(rd);
            int keySize = Convert.ToInt32(keyToWrap.KeySize);
            if (keySize == 128 || keySize == 192 || keySize == 256)
            {
                byte[] keyBytes = new byte[keySize / 8];
                Array.Copy(FormatConversions.HexStringToByteArray(keyToWrap.KeyValue), 0, keyBytes, 0, keyBytes.Length);
                return Tr31ConfidentialData.Create(keyBytes, blockSize: 16,
                    obfuscationPaddingLength: GetObfuscationPaddingLength(keyToWrap, keyBytes.Length),
                    random: random).ToArray();
            }

            // Keep the unsupported-size legacy path unchanged in this extraction.
            byte[] binaryKeyData = new byte[16];
            Array.Copy(FormatConversions.HexStringToByteArray(keyToWrap.KeyValue), 0, binaryKeyData, 2, 16);
            return binaryKeyData;
        }
        internal static int GetObfuscationPaddingLength(KeyVariableDeclaration key, int keyLength)
        {
            char? algorithm = key.KeyType.Algorithm switch
            {
                KeyAlgorithm.Aes => 'A',
                KeyAlgorithm.Tdea => 'T',
                _ => null
            };
            if (algorithm == null)
            {
                string? header = key.KeyAttributes.FirstOrDefault(block => block.ID == "HDR")?.Data;
                if (!string.IsNullOrEmpty(header) && header.Length > 7)
                    algorithm = char.ToUpperInvariant(header[7]);
            }
            return algorithm switch
            {
                'A' => Math.Max(0, 32 - keyLength),
                'T' => Math.Max(0, 24 - keyLength),
                _ => 0
            };
        }
        private static KeyType KeyTypeFromHeader(string? header)
        {
            if (string.IsNullOrEmpty(header) || header.Length <= 7)
                return KeyType.Secret(KeyAlgorithm.Unknown);

            return char.ToUpperInvariant(header[7]) switch
            {
                'A' => KeyType.Secret(KeyAlgorithm.Aes),
                'T' => KeyType.Secret(KeyAlgorithm.Tdea),
                'H' => KeyType.Secret(KeyAlgorithm.Hmac),
                _ => KeyType.Secret(KeyAlgorithm.Unknown)
            };
        }
        private static void ValidateDeclaredBlockLength(string header, int ciphertextLength, int authenticationLength)
        {
            if (header.Length < 5 || !int.TryParse(header.AsSpan(1, 4), out int declaredLength))
                throw new ArgumentException("TR-31 header must contain a four-digit total length.");
            int actualLength = checked(header.Length + 2 * (ciphertextLength + authenticationLength));
            if (declaredLength != actualLength)
                throw new ArgumentException(
                    $"TR-31 header declares total length {declaredLength}, but wrap produces {actualLength} characters.");
        }
        private byte[] DeriveKey(byte[] key, byte[] data, int keySize)
        {
            IMac mac = new CMac(new AesEngine());
            byte[] derivedKey = new byte[32];
            byte[]? result = null;
            switch (keySize)
            {
                case 128:
                    result = new byte[16];
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data, 0, data.Length);                    
                    mac.DoFinal(result, 0);
                    break;
                case 192:
                    byte[] data_1 = new byte[data.Length / 2];
                    byte[] data_2 = new byte[data.Length / 2];
                    Array.Copy(data, 0, data_1, 0, data.Length / 2);
                    Array.Copy(data, data.Length / 2, data_2, 0, data.Length / 2);
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data_1, 0, data_1.Length);
                    mac.DoFinal(derivedKey, 0);
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data_2, 0, data_2.Length);
                    mac.DoFinal(derivedKey, 16);
                    result=new byte[192/8];
                    Array.Copy(derivedKey, 0, result, 0, 192 / 8);
                    break;
                case 256:
                    byte[] data_3 = new byte[data.Length / 2];
                    byte[] data_4 = new byte[data.Length / 2];
                    Array.Copy(data, 0, data_3, 0, data.Length / 2);
                    Array.Copy(data, data.Length / 2, data_4, 0, data.Length / 2);
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data_3, 0, data_3.Length);
                    mac.DoFinal(derivedKey, 0);
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data_4, 0, data_4.Length);
                    mac.DoFinal(derivedKey, 16);
                    result=derivedKey;
                    break;
                default:
                    result = new byte[16];
                    mac.Init(new KeyParameter(key));
                    mac.BlockUpdate(data, 0, data.Length);
                    mac.DoFinal(result, 0);
                    break;
            }  
            return result;
        }
        private byte[] MACDerivationData(int BPkeySize)
        {
            switch(BPkeySize)
            {
                case 128:
                    return new byte[] { 0x01,0x00,0x01,0x00,0x00,0x02,0x00,0x80 };
                case 192:
                    return new byte[] { 0x01, 0x00, 0x01, 0x00, 0x00, 0x03, 0x00, 0xc0, 0x02, 0x00, 0x01, 0x00, 0x00, 0x03, 0x00, 0xc0 };
                case 256:
                    return new byte[] { 0x01, 0x00, 0x01, 0x00, 0x00, 0x04, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x04, 0x01, 0x00 };
                default:
                    return new byte[] { 0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x00, 0x80 };
            }
            
        }
        private byte[] EncryptionDerivationData(int BPkeySize)
        {
            switch (BPkeySize)
            {
                case 128:
                    return new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x80 };
                case 192:
                    return new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0xC0, 0x02, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0xC0 };
                case 256:
                    return new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x04, 0x01, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x04, 0x01, 0x00 };
                default:
                    return new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x80 };
            }
        }   
    }
}
