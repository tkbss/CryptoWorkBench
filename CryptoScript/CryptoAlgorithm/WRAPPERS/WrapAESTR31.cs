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

namespace CryptoScript.CryptoAlgorithm.WRAPPERS
{
    public class WrapAESTR31 : CryptoAlgorithm
    {
        public override StringVariableDeclaration Wrap(string[] parameters)
        {
            ParameterVariableDeclaration p = new ParameterVariableDeclaration();
            p.SetInstance(parameters[0]);
            var keyEncryption = (AES_CBC)new AES.AES().CreateMode("AES-CBC");
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
            return base.Wrap(parameters);
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
