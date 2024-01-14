using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.CryptoAlgorithm
{
    internal class AESDefaultParameters
    {
        public static ParameterVariableDeclaration GenerateDefaultCTRParameters(string mechanism)
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
        public static ParameterVariableDeclaration GenerateDefaultCBCParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            //generate random IV
            byte[] IV = RandomNumberGenerator.GetBytes(16);
            param.IV = FormatConversions.ByteArrayToHexString(IV);
            param.Padding = "PKCS-7";
            return param;
        }
        public static ParameterVariableDeclaration GenerateDefaultECBParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            //generate random IV            
            param.IV = string.Empty;
            param.Padding = "NONE";
            return param;
        }
        public static ParameterVariableDeclaration GenerateDefaultCMACParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            //generate random IV            
            param.IV = string.Empty;
            param.Padding = "NONE";
            return param;
        }
        public static ParameterVariableDeclaration GenerateDefaultGCMParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            //generate random nonce
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            param.Nonce = FormatConversions.ByteArrayToHexString(nonce);
            //generate random IV            
            param.IV = string.Empty;
            param.Padding = "NONE";
            return param;
        }
        public static ParameterVariableDeclaration GenerateDefaultGMACParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            //generate random nonce
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            param.Nonce = FormatConversions.ByteArrayToHexString(nonce);
            //generate random IV            
            param.IV = string.Empty;
            param.Padding = "NONE";
            return param;
        }
    }
}
