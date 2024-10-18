using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.AES
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
                key.KeyValue = keyValue;
                key.ValueFormat = FormatConversions.ParseString(keyValue);
                key.KeySize = Size;
                key.Mechanism = mechanism;
                key.Type = new CryptoTypeKey();
            }
            return key;
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            switch (mechanism.ToLower())
            {
                case "aes-ctr":
                    param = AESDefaultParameters.GenerateDefaultCTRParameters(mechanism);
                    break;
                case "aes-cbc":
                    param = AESDefaultParameters.GenerateDefaultCBCParameters(mechanism);
                    break;
                case "aes-ecb":
                    param = AESDefaultParameters.GenerateDefaultECBParameters(mechanism);
                    break;
                case "aes-cmac":
                    param = AESDefaultParameters.GenerateDefaultCMACParameters(mechanism);
                    break;
                case "aes-gcm":
                    param = AESDefaultParameters.GenerateDefaultGCMParameters(mechanism);
                    break;
                default:
                    throw new ArgumentException("wrong mechanism");
            }
            //param.Value = param.Serialize();
            param.ValueFormat = FormatConversions.ParseString(param.Value);
            return param;
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            foreach (var p in parameters)
            {
                param.SetParameter(p);
                
            }
            SetDefaultParameterValues(param);
            //param.Value = param.Serialize();
            param.ValueFormat = FormatConversions.ParseString(param.Value);
            return param;
        }

        private void SetDefaultParameterValues(ParameterVariableDeclaration param)
        {
            if (param.Mechanism.ToLower().Contains("cbc"))
            {
                if (param.GetParameter("IV") == string.Empty)
                {
                    byte[] IV = RandomNumberGenerator.GetBytes(16);                    
                    param.SetParameter("IV", FormatConversions.ByteArrayToHexString(IV));
                }
                if (param.GetParameter("PAD") == string.Empty)                    
                    param.SetParameter("PAD", "PKCS7");
                return;
            }
            if (param.Mechanism.ToLower().Contains("ctr"))
            {
                if (param.GetParameter("NONCE") == string.Empty)
                {
                    byte[] nonce = RandomNumberGenerator.GetBytes(12);
                    param.SetParameter("NONCE",FormatConversions.ByteArrayToHexString(nonce));
                }
                if (param.GetParameter("COUNTER") == string.Empty)
                    param.SetParameter("COUNTER","0x(00000000)");
            }
            if (param.Mechanism.ToLower().Contains("ecb"))
            {                
                param.SetParameter("IV", string.Empty);
                param.SetParameter("PAD","NONE");
            }
            if (param.Mechanism.ToLower().Contains("cmac"))
            {
                param.SetParameter("IV", string.Empty);
                param.SetParameter("PAD", "NONE");
            }
            if (param.Mechanism.ToLower().Contains("gcm"))
            {
                if (param.GetParameter("NONCE") == string.Empty)
                {
                    byte[] nonce = RandomNumberGenerator.GetBytes(12);
                    param.SetParameter("NONCE", FormatConversions.ByteArrayToHexString(nonce));
                }
                param.SetParameter("IV", string.Empty);
                param.SetParameter("PAD", "NONE");
            }

        }
        ParameterVariableDeclaration? parameter = null;
        KeyVariableDeclaration? key = null;
        StringVariableDeclaration? data = null;
        private void ParseArguments(string[] arguments)
        {
            var p = arguments[0];
            if (VariableDictionary.Instance().Get(p) as ParameterVariableDeclaration != null)
            {
                parameter = VariableDictionary.Instance().Get(p) as ParameterVariableDeclaration;
            }
            else
            {
                //json deserialization
                if (FormatConversions.ParseString(p) == FormatConversions.PAR)
                {
                    parameter = new ParameterVariableDeclaration();
                    parameter.SetInstance(p);
                }
                else
                {
                    throw new ArgumentException("wrong parameter argument");
                }
            }
            p = arguments[1];
            if (VariableDictionary.Instance().Get(p) as KeyVariableDeclaration != null)
            {
                key = VariableDictionary.Instance().Get(p) as KeyVariableDeclaration;
            }
            else
            {
                if (FormatConversions.ParseString(p) == FormatConversions.HEX)
                {
                    key = new KeyVariableDeclaration();
                    key.Value = p;
                    key.ValueFormat = FormatConversions.ParseString(p);
                }
                else if (FormatConversions.ParseString(p) == FormatConversions.JSO)
                {
                    key = KeyVariableDeclaration.Deserialize(p);
                }
                else
                {
                    throw new ArgumentException("wrong key argument");
                }
            }
            p = arguments[2];
            if (VariableDictionary.Instance().Get(p) as StringVariableDeclaration != null)
            {
                data = VariableDictionary.Instance().Get(p) as StringVariableDeclaration;

            }
            else
            {
                if (FormatConversions.ParseString(p) != string.Empty)
                {
                    data = new StringVariableDeclaration();
                    data.Value = p;
                    data.ValueFormat = FormatConversions.ParseString(p);
                }
                else
                {
                    throw new ArgumentException("wrong data argument");
                }
            }

        }
        public override StringVariableDeclaration Mac(string[] parameters)
        {

            ParseArguments(parameters);
            var mode = CreateMode(parameter.Mechanism) as EncryptionMode;
            var res = mode.ModeEncryption(parameter, key, data);
            return res;
        }
        public override StringVariableDeclaration Encrypt(string[] parameters)
        {

            ParseArguments(parameters);
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
                case "aes-ecb":
                    return new AES_ECB();
                case "aes-cmac":
                    return new AES_CMAC();
                default:
                    throw new ArgumentException("wrong mechanism");
            }
        }

        public override StringVariableDeclaration Decrypt(string[] parameters)
        {
            ParseArguments(parameters);
            var mode = CreateMode(parameter.Mechanism) as EncryptionMode;
            var res = mode.ModeDecryption(parameter, key, data);
            return res;
        }
    }
}
