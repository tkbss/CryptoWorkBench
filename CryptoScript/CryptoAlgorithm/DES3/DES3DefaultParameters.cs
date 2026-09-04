using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.DES3
{
    internal static class DES3DefaultParameters
    {
        public static ParameterVariableDeclaration GenerateDefaultCBCParameters(string mechanism)
        {
            var parameter = new ParameterVariableDeclaration
            {
                Mechanism = mechanism
            };
            parameter.SetParameter("MECH", mechanism);
            parameter.SetParameter("IV", FormatConversions.ByteArrayToHexString(RandomNumberGenerator.GetBytes(8)));
            parameter.SetParameter("PAD", "PKCS-7");
            return parameter;
        }

        public static ParameterVariableDeclaration GenerateDefaultECBParameters(string mechanism)
        {
            var parameter = new ParameterVariableDeclaration
            {
                Mechanism = mechanism
            };
            parameter.SetParameter("MECH", mechanism);
            parameter.SetParameter("PAD", "PKCS-7");
            return parameter;
        }

        public static ParameterVariableDeclaration GenerateDefaultCMACParameters(string mechanism)
        {
            var parameter = new ParameterVariableDeclaration
            {
                Mechanism = mechanism
            };
            parameter.SetParameter("MECH", mechanism);
            parameter.SetParameter("IV", string.Empty);
            parameter.SetParameter("PAD", "NONE");
            return parameter;
        }

        public static ParameterVariableDeclaration GenerateDefaultRetailParameters(string mechanism)
        {
            var parameter = new ParameterVariableDeclaration
            {
                Mechanism = mechanism
            };
            parameter.SetParameter("MECH", mechanism);
            parameter.SetParameter("IV", string.Empty);
            parameter.SetParameter("PAD", "ISO-9797-M2");
            parameter.SetParameter("MACLEN", "8");
            return parameter;
        }
    }
}
