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
            param.SetParameter("MECH", mechanism);
            //generate random nonce
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            param.SetParameter("NONCE", FormatConversions.ByteArrayToHexString(nonce));
            //set counter (4 bytes) to 0
            param.SetParameter("COUNTER","0x(00000000)");
            return param;
        }
        //generate default parameters for CBC mode
        public static ParameterVariableDeclaration GenerateDefaultCBCParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            param.SetParameter("MECH", mechanism);
            //generate random IV
            byte[] IV = RandomNumberGenerator.GetBytes(16);
            param.SetParameter("IV", FormatConversions.ByteArrayToHexString(IV));
            param.SetParameter("PAD", "PKCS-7"); 
            return param;
        }
        public static ParameterVariableDeclaration GenerateDefaultECBParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            param.SetParameter("MECH", mechanism);
            //generate random IV            
            param.SetParameter("IV", string.Empty);
            param.SetParameter("PAD", "NONE");
            return param;
        }
        public static ParameterVariableDeclaration GenerateDefaultCMACParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            param.SetParameter("MECH", mechanism);
            //generate random IV            
            param.SetParameter("IV", string.Empty);
            param.SetParameter("PAD", "NONE");
            return param;
        }
        public static ParameterVariableDeclaration GenerateDefaultCCMParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            param.SetParameter("MECH", mechanism);
            //generate random nonce
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            param.SetParameter("NONCE", FormatConversions.ByteArrayToHexString(nonce));
            param.SetParameter("ADATA", "\"DEFAULT_CCM_AUTHENTICATION_DATA\"");
            
            return param;
        }
        public static ParameterVariableDeclaration GenerateDefaultGCMParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            param.SetParameter("MECH", mechanism);
            //generate random nonce
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            param.SetParameter("NONCE", FormatConversions.ByteArrayToHexString(nonce));
            param.SetParameter("ADATA", "\"DEFAULT_GCM_AUTHENTICATION_DATA\"");
            
            return param;
        }
        public static ParameterVariableDeclaration GenerateDefaultGMACParameters(string mechanism)
        {
            var param = new ParameterVariableDeclaration();
            param.Mechanism = mechanism;
            param.SetParameter("MECH", mechanism);
            //generate random nonce
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            param.SetParameter("NONCE", FormatConversions.ByteArrayToHexString(nonce));
            //generate random IV            
            param.SetParameter("IV", string.Empty);
            param.SetParameter("PAD", "NONE");
            return param;
        }
    }
}
