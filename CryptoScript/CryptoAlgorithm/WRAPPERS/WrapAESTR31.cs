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
            TR31String? tr31 =null;
            // 3) Parse the TR-31 block into (header, wrappedKey, mac).
            //    This depends on how your TR31String is implemented.
            //    Example assumes a constructor that can parse a string:
            if (FormatConversions.ParseString(wrappedBlockVar.Value) == FormatConversions.TR31)
            {
                tr31 = TR31String.FromString(wrappedBlockVar.Value);
                blockHeaderBytes = FormatConversions.StringToByteArray(tr31.Block);
                encryptedKeyData = tr31.Cryptogram;
                blockMac = tr31.Mac;
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
                Value = "\""+tr31.Block+ "\"" + actualKeyHex, // pick some name or pass in an extra param
                Type=new CryptoTypeKey(),
                Mechanism= "WRAP-AES-TR31"
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

            // Fallback or throw if the format is unexpected
            throw new Exception("Unknown key block format in recovered data.");
        }
        public override StringVariableDeclaration Wrap(string[] parameters)
        {
            ParameterVariableDeclaration p = new ParameterVariableDeclaration();
            p.SetInstance(parameters[0]);
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
            byte[] keyData = ConstructBinaryKeyData(keyToWrap.KeyValue, Convert.ToInt32(keyToWrap.KeySize),rnd);
            byte[] dataToMAC = ConstructDataToMAC(p, keyData);
            byte[] macResult = ComputeMAC(dataToMAC, macKey);
            byte[] wrappedKey = EncryptNoPadding(encryptionKey, macResult, keyData);
            string header=p.GetParameter("#BLKH");
            TR31String tr31 = new TR31String(header, wrappedKey, macResult);            
            StringVariableDeclaration block = new StringVariableDeclaration() { Value = tr31.ToString(), ValueFormat = FormatConversions.TR31 };
            return block;
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
        private byte[] ConstructBinaryKeyData(string key,int KeySize,string rd)
        {
            byte[]? binaryKeyData,rndArray;
            Random rnd = new Random();
            if(!string.IsNullOrEmpty(rd))
            {
                rndArray=FormatConversions.HexStringToByteArray(rd);
            }
            else
            {
                rndArray = null;
            }
            switch (KeySize)
            {
                case 128:
                    binaryKeyData = new byte[32];
                    binaryKeyData[1] = 0x80;
                    Array.Copy(FormatConversions.HexStringToByteArray(key), 0, binaryKeyData, 2, 16);
                    if (rndArray == null || rndArray.Length!=14)
                    {
                        
                        rndArray = new byte[14];
                        rnd.NextBytes(rndArray);
                    }                    
                    Array.Copy(rndArray, 0, binaryKeyData, 18, 14);
                    break;
                case 192:
                    binaryKeyData = new byte[32];
                    binaryKeyData[1] = 0xC0;
                    Array.Copy(FormatConversions.HexStringToByteArray(key), 0, binaryKeyData, 2, 24);
                    if (rndArray == null || rndArray.Length != 6)
                    {

                        rndArray = new byte[6];
                        rnd.NextBytes(rndArray);
                    }
                    Array.Copy(rndArray, 0, binaryKeyData, 26, 6);
                    break;
                case 256:
                    binaryKeyData = new byte[48];
                    binaryKeyData[0] = 0x01;
                    Array.Copy(FormatConversions.HexStringToByteArray(key), 0, binaryKeyData, 2, 32);
                    if (rndArray == null || rndArray.Length != 14)
                    {

                        rndArray = new byte[14];
                        rnd.NextBytes(rndArray);
                    }
                    Array.Copy(rndArray, 0, binaryKeyData, 34, 14);
                    break;
                default:
                    binaryKeyData = new byte[16];
                    Array.Copy(FormatConversions.HexStringToByteArray(key), 0, binaryKeyData, 2, 16);
                    break;
            }

            return binaryKeyData;
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
