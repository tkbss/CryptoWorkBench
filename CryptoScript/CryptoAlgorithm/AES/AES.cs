using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.AES
{
    public class AES : SymmetricCryptoAlgorithm
    {

        public override KeyVariableDeclaration GenerateKey(string mechanism, string Size)
        {
            if(FormatConversions.ParseString(Size)== FormatConversions.HEX)
            {
                return CreateExistingKey(Size,mechanism);
            }
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
                key.KeyType = KeyType.Secret(KeyAlgorithm.Aes);
                key.Type = new CryptoTypeKey();
            }
            return key;
        }
        private KeyVariableDeclaration CreateExistingKey(string key, string mechanism)
        {
            var k = new KeyVariableDeclaration();
            k.Value = key;
            k.ValueFormat = FormatConversions.ParseString(key);
            var keySize = FormatConversions.ToByteArray(key,FormatConversions.HEX).Length*8;
            if (keySize != 128 && keySize != 192 && keySize != 256)
                throw new ArgumentException("wrong key size");
            k.KeySize = keySize.ToString();
            k.KeyType = KeyType.Secret(KeyAlgorithm.Aes);
            k.KeyValue = key;
            k.Type = new CryptoTypeKey();
            return k;
        }
        public override ParameterVariableDeclaration GenerateParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            mechanism = ExtractMechanismen(mechanism);
            switch (mechanism.ToLower())
            {
                case "aes-ctr":
                    param = AESDefaultParameters.GenerateDefaultCTRParameters(mechanism);
                    break;
                case "aes-cbc":
                    param = AESDefaultParameters.GenerateDefaultCBCParameters(mechanism);
                    break;
                case "aes-cbc-mac":
                    param = AESDefaultParameters.GenerateDefaultCBCMACParameters(mechanism);
                    break;
                case "aes-ecb":
                    param = AESDefaultParameters.GenerateDefaultECBParameters(mechanism);
                    break;
                case "aes-cmac":
                    param = AESDefaultParameters.GenerateDefaultCMACParameters(mechanism);
                    break;
                case "aes-gmac":
                    param = AESDefaultParameters.GenerateDefaultGMACParameters(mechanism);
                    break;
                case "aes-gcm":
                    param = AESDefaultParameters.GenerateDefaultGCMParameters(mechanism);
                    break;
                case "aes-ccm":
                    param = AESDefaultParameters.GenerateDefaultCCMParameters(mechanism);
                    break;
                default:
                    throw new ArgumentException("wrong mechanism");
            }            
            param.ValueFormat = FormatConversions.ParseString(param.Value);
            return param;
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
        {
            var param = new ParameterVariableDeclaration();
            mechanism = ExtractMechanismen(mechanism);
            param.Mechanism = mechanism;
            foreach (var p in parameters)
            {
                param.SetParameter(p);
                
            }
            SetDefaultParameterValues(param);
            if (param.Mechanism.Equals("AES-CBC", StringComparison.OrdinalIgnoreCase))
                AES_CBC.ValidateAndDecodeIv(param.GetParameter("IV"));
            if (param.Mechanism.Equals("AES-CBC-MAC", StringComparison.OrdinalIgnoreCase))
                AES_CBC_MAC.ValidateParameters(param);
            param.ValueFormat = FormatConversions.ParseString(param.Value);
            return param;
        }

        private void SetDefaultParameterValues(ParameterVariableDeclaration param)
        {
            param.Mechanism = ExtractMechanismen(param.Mechanism);
            if (param.Mechanism.Equals("AES-CBC-MAC", StringComparison.OrdinalIgnoreCase))
            {
                if (param.GetParameter("PAD") == string.Empty)
                    param.SetParameter("PAD", "PKCS-7");
                if (param.GetParameter("MACLEN") == string.Empty)
                    param.SetParameter("MACLEN", "16");
                return;
            }
            if (param.Mechanism.ToLower().Contains("cbc"))
            {
                if (param.GetParameter("IV") == string.Empty)
                {
                    byte[] IV = RandomNumberGenerator.GetBytes(16);                    
                    param.SetParameter("IV", FormatConversions.ByteArrayToHexString(IV));
                }
                if (param.GetParameter("PAD") == string.Empty)                    
                    param.SetParameter("PAD", "PKCS-7");
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
        private void ParseArguments(AlgorithmCallArguments arguments)
        {
            parameter = AlgorithmArgumentResolver.ResolveParameter(arguments.Arguments[0]);
            key = AlgorithmArgumentResolver.ResolveKey(arguments.Arguments[1]);
            string p = arguments.Arguments[2].Value!;
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
            => Mac(AlgorithmCallArguments.FromValues(parameters));

        public override StringVariableDeclaration Mac(AlgorithmCallArguments parameters)
        {

            ParseArguments(parameters);
            key!.Usage.EnsureAllows(KeyUsage.MacGenerate);
            var mode = CreateMode(parameter.Mechanism) as EncryptionMode;
            if (mode.IsMACAlgorithm(parameter.Mechanism))
            {
                var res = mode.ModeMac(parameter, key, data);
                return res;
            }
            else
            {
                var se = new SemanticError() { Type = "Mechanism" };
                se.Message = "Mechanism: "+parameter.Mechanism+" cannot be used in MAC calculation";
                se.FunctionName = "Mac";                
                throw new SemanticErrorException() { SemanticError = se };                
            }
        }
        public override StringVariableDeclaration Encrypt(string[] parameters)
            => Encrypt(AlgorithmCallArguments.FromValues(parameters));

        public override StringVariableDeclaration Encrypt(AlgorithmCallArguments parameters)
        {

            ParseArguments(parameters);
            key!.Usage.EnsureAllows(KeyUsage.Encrypt);
            var mode = CreateMode(parameter.Mechanism) as EncryptionMode;
            if(mode.IsMACAlgorithm(parameter.Mechanism))
            {
                var se = new SemanticError() { Type = "Mechanism" };
                se.Message = "Mechanism: " + parameter.Mechanism + " cannot be used in Encryption";
                se.FunctionName = "Encrypt";
                throw new SemanticErrorException() { SemanticError = se };
            }
            else
            {
                var res = mode.ModeEncryption(parameter, key, data);
                return res;
            }
            
        }

        public override EncryptionMode CreateMode(string mechanism)
        {
            switch (mechanism.ToLower())
            {
                case "aes-ctr":
                    return new AES_CTR();
                case "aes-cbc":
                    return new AES_CBC();
                case "aes-cbc-mac":
                    return new AES_CBC_MAC();
                case "aes-ecb":
                    return new AES_ECB();
                case "aes-cmac":
                    return new AES_CMAC();
                case "aes-gcm":
                    return new AES_GCM();
                case "aes-ccm":
                    return new AES_CCM();
                case "aes-gmac":
                    return new AES_GMAC();
                default:
                    throw new ArgumentException("wrong mechanism");
            }
        }

        public override StringVariableDeclaration Decrypt(string[] parameters)
            => Decrypt(AlgorithmCallArguments.FromValues(parameters));

        public override StringVariableDeclaration Decrypt(AlgorithmCallArguments parameters)
        {
            ParseArguments(parameters);
            key!.Usage.EnsureAllows(KeyUsage.Decrypt);
            var mode = CreateMode(parameter.Mechanism) as EncryptionMode;
            if(mode.IsMACAlgorithm(parameter.Mechanism))
            {
                var se = new SemanticError() { Type = "Mechanism" };
                se.Message = "Mechanism: " + parameter.Mechanism + " cannot be used in Decryption";
                se.FunctionName = "Decrypt";
                throw new SemanticErrorException() { SemanticError = se };
            }
            else
            {
                var res = mode.ModeDecryption(parameter, key, data);
                return res;
            }            
        }
    }
}
