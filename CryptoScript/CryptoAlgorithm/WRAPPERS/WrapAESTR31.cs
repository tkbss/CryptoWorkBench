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

namespace CryptoScript.CryptoAlgorithm.WRAPPERS
{
    public class WrapAESTR31 : CryptoAlgorithm
    {
        public override StringVariableDeclaration Wrap(string[] parameters)
        {
            ParameterVariableDeclaration p = new ParameterVariableDeclaration();
            p.SetInstance(parameters[0]);
            string rnd = p.GetParameter("#RND");
            var variables=VariableDictionary.Instance().GetVariables();
            KeyVariableDeclaration keyProtectionKey = null;
            KeyVariableDeclaration keyToWrap = null;
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
            byte[] binaryKeyData,rndArray;
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
            byte[] result = null;
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
