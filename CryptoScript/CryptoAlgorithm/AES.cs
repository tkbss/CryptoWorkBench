using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm
{
    public class AES : SymmetricCryptoAlgorithm
    {
        
        public override KeyVariableDeclaration GenerateKey(string mechanism, string Size)
        {
            int keySize = Convert.ToInt32(Size);
            if (keySize != 128 && keySize != 192 && keySize != 256)
                throw new ArgumentException("wrong key size");
            var key = new KeyVariableDeclaration();
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.KeySize = keySize;
                aesAlg.GenerateKey();

                byte[] aesKey = aesAlg.Key; // Getting the generated AES key
                string keyValue = FormatConversions.ByteArrayToHexString(aesKey);
                key.Value = keyValue;
                key.ValueFormat = FormatConversions.ParseString(keyValue);
                key.KeySize = Size;
                key.Mechanism = mechanism;
                key.Type = new CryptoTypeKey();
            }
            return key;
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism)
        {

            switch (mechanism.ToLower())
            {
                case "aes-ctr":
                    return GenerateDefaultCTRParameters(mechanism);
                case "aes-cbc":
                    return GenerateDefaultCBCParameters(mechanism);
                default:
                    throw new ArgumentException("wrong mechanism");
            }
            return base.GenerateParameters(mechanism);
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            foreach (var p in parameters)
            {                
                if (p.ToLower().Contains("iv"))
                {                     
                    param.IV = p.Split(':')[1];                    
                }
                if (p.ToLower().Contains("pad"))
                {                    
                    param.Padding = p.Split(':')[1];                    
                }
                if (p.ToLower().Contains("nonce"))
                {                    
                    param.Nonce = p.Split(':')[1];                   
                }
                if (p.ToLower().Contains("counter"))
                {              
                    param.Counter = p.Split(':')[1];                    
                }
            }
            SetDefaultParameterValues(param);
            return param;
        }
        //generate default parameters for CTR mode
        private ParameterVariableDeclaration GenerateDefaultCTRParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            //generate random nonce
            byte[] nonce = RandomNumberGenerator.GetBytes(12);            
            param.Nonce = FormatConversions.ByteArrayToHexString(nonce);
            //set counter (4 bytes) to 0
            param.Counter = "0x(00000000)";
            return param;
        }
        //generate default parameters for CBC mode
        private ParameterVariableDeclaration GenerateDefaultCBCParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            //generate random IV
            byte[] IV = RandomNumberGenerator.GetBytes(16);
            param.IV= FormatConversions.ByteArrayToHexString(IV);
            param.Padding = "PKCS-7";
            return param;
        }
        private void SetDefaultParameterValues(ParameterVariableDeclaration param)
        {
            if (param.Mechanism.ToLower().Contains("cbc"))
            {
                if (param.IV == string.Empty)
                {
                    byte[] IV = RandomNumberGenerator.GetBytes(16);
                    param.IV = FormatConversions.ByteArrayToHexString(IV);
                }
                if (param.Padding == string.Empty)
                    param.Padding = "PKCS7";
                return;
            }
            if (param.Mechanism.ToLower().Contains("ctr"))
            {
                if (param.Nonce == string.Empty)
                {
                    byte[] nonce = RandomNumberGenerator.GetBytes(12);
                    param.Nonce = FormatConversions.ByteArrayToHexString(nonce);
                }
                if (param.Counter == string.Empty)
                    param.Counter = "0x(00000000)";
            }
        }
        ParameterVariableDeclaration? parameter = null;
        KeyVariableDeclaration? key = null;
        StringVariableDeclaration? data = null;
        private void ParseParameters(string[] parameters)
        {
            foreach (var p in parameters)
            {
                if (VariableDictionary.Instance().Get(p) as ParameterVariableDeclaration != null)
                {
                    parameter = VariableDictionary.Instance().Get(p) as ParameterVariableDeclaration;
                    continue;
                }
                if (VariableDictionary.Instance().Get(p) as KeyVariableDeclaration != null)
                {
                    key = VariableDictionary.Instance().Get(p) as KeyVariableDeclaration;
                    continue;
                }
                if (VariableDictionary.Instance().Get(p) as StringVariableDeclaration != null)
                {
                    data = VariableDictionary.Instance().Get(p) as StringVariableDeclaration;
                    continue;
                }
                if (FormatConversions.ParseString(p) != string.Empty)
                {
                    data = new StringVariableDeclaration();
                    data.Value = p;
                    data.ValueFormat = FormatConversions.ParseString(p);
                    continue;
                }
            }
        }
        public override StringVariableDeclaration Encrypt(string[] parameters)
        {
            
            ParseParameters(parameters);
            var mode = CreateMode(parameter.Mechanism) as EncryptionMode;
            var res = mode.ModeEncryption(parameter, key, data);
            return res;            
        }

        public override EncryptionMode CreateMode(string mechanism)
        {
            switch (mechanism.ToLower())
            {
                case "aes-ctr":
                    return new AES_CTR();
                case "aes-cbc":
                    return new AES_CBC();
                default:
                    throw new ArgumentException("wrong mechanism");
            }            
        }

        public override StringVariableDeclaration Decrypt(string[] parameters)
        {
            ParseParameters(parameters);
            var mode = CreateMode(parameter.Mechanism) as EncryptionMode;
            var res = mode.ModeDecryption(parameter, key, data);
            return res;
        }
    }
}
