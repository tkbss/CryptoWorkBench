using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.DES3
{
    public class DES3 : SymmetricCryptoAlgorithm
    {
        private ParameterVariableDeclaration? parameter;
        private KeyVariableDeclaration? key;
        private StringVariableDeclaration? data;

        public override KeyVariableDeclaration GenerateKey(string mechanism, string size)
        {
            if (FormatConversions.ParseString(size) == FormatConversions.HEX)
                return CreateExistingKey(size, mechanism);

            if (!int.TryParse(size, out int keySize) || (keySize != 128 && keySize != 192))
                throw new ArgumentException("DES3 key size must be 128 or 192 bits.");

            using TripleDES des3 = TripleDES.Create();
            des3.KeySize = keySize;
            des3.GenerateKey();
            return CreateKeyVariable(
                FormatConversions.ByteArrayToHexString(des3.Key),
                mechanism,
                keySize);
        }

        private static KeyVariableDeclaration CreateExistingKey(string value, string mechanism)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(value, FormatConversions.HEX);
            ValidateKeyLength(keyBytes);
            ValidateUsableKey(keyBytes);
            return CreateKeyVariable(value, mechanism, keyBytes.Length * 8);
        }

        private static KeyVariableDeclaration CreateKeyVariable(string value, string mechanism, int keySize)
        {
            return new KeyVariableDeclaration
            {
                Value = value,
                KeyValue = value,
                ValueFormat = FormatConversions.ParseString(value),
                KeySize = keySize.ToString(),
                Mechanism = mechanism,
                Type = new CryptoTypeKey()
            };
        }

        internal static void ValidateKeyLength(byte[] keyBytes)
        {
            if (keyBytes.Length != 16 && keyBytes.Length != 24)
                throw new ArgumentException("DES3 key must contain exactly 16 or 24 bytes.");
        }

        internal static void ValidateUsableKey(byte[] keyBytes)
        {
            if (TripleDES.IsWeakKey(keyBytes))
                throw new ArgumentException("DES3 key is weak or degenerates to single DES.");
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism)
        {
            mechanism = ExtractMechanismen(mechanism);
            var result = mechanism.ToUpperInvariant() switch
            {
                "DES3-CBC" => DES3DefaultParameters.GenerateDefaultCBCParameters(mechanism),
                "DES3-ECB" => DES3DefaultParameters.GenerateDefaultECBParameters(mechanism),
                "DES3-RETAIL" => DES3DefaultParameters.GenerateDefaultRetailParameters(mechanism),
                "DES3-CMAC" => DES3DefaultParameters.GenerateDefaultCMACParameters(mechanism),
                _ => throw new ArgumentException($"Unsupported DES3 mechanism: {mechanism}.")
            };
            result.ValueFormat = FormatConversions.ParseString(result.Value);
            return result;
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
        {
            mechanism = ExtractMechanismen(mechanism);
            if (!mechanism.Equals("DES3-CBC", StringComparison.OrdinalIgnoreCase) &&
                !mechanism.Equals("DES3-ECB", StringComparison.OrdinalIgnoreCase) &&
                !mechanism.Equals("DES3-RETAIL", StringComparison.OrdinalIgnoreCase) &&
                !mechanism.Equals("DES3-CMAC", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Unsupported DES3 mechanism: {mechanism}.");

            var result = new ParameterVariableDeclaration { Mechanism = mechanism };
            foreach (string item in parameters)
                result.SetParameter(item);

            if (mechanism.Equals("DES3-CBC", StringComparison.OrdinalIgnoreCase))
            {
                if (result.GetParameter("IV") == string.Empty)
                    result.SetParameter("IV", FormatConversions.ByteArrayToHexString(RandomNumberGenerator.GetBytes(8)));
            }
            else if (result.GetParameter("IV") != string.Empty)
            {
                throw new ArgumentException($"{mechanism} does not use an IV.");
            }
            if (mechanism.Equals("DES3-RETAIL", StringComparison.OrdinalIgnoreCase))
            {
                string padding = result.GetParameter("PAD");
                if (padding == string.Empty)
                    result.SetParameter("PAD", "ISO-9797-M2");
                else if (!padding.Equals("ISO-9797-M1", StringComparison.OrdinalIgnoreCase) &&
                         !padding.Equals("ISO-9797-M2", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("DES3-RETAIL supports only ISO-9797-M1 or ISO-9797-M2 padding.");

                string macLength = result.GetParameter("MACLEN");
                if (macLength == string.Empty)
                    result.SetParameter("MACLEN", "8");
                else if (!int.TryParse(macLength.Trim('"'), out int length) || length < 4 || length > 8)
                    throw new ArgumentException("DES3-RETAIL MAC length must be between 4 and 8 bytes.");
                else
                    result.SetParameter("MACLEN", length.ToString());
            }
            else if (mechanism.Equals("DES3-CMAC", StringComparison.OrdinalIgnoreCase))
                result.SetParameter("PAD", "NONE");
            else if (result.GetParameter("PAD") == string.Empty)
                result.SetParameter("PAD", "PKCS-7");

            result.ValueFormat = FormatConversions.ParseString(result.Value);
            return result;
        }

        public override EncryptionMode CreateMode(string mechanism)
        {
            return mechanism.ToUpperInvariant() switch
            {
                "DES3-CBC" => new DES3_CBC(),
                "DES3-ECB" => new DES3_ECB(),
                "DES3-RETAIL" => new DES3_RETAIL(),
                "DES3-CMAC" => new DES3_CMAC(),
                _ => throw new ArgumentException($"Unsupported DES3 mechanism: {mechanism}.")
            };
        }

        public override StringVariableDeclaration Encrypt(string[] parameters)
        {
            ParseArguments(parameters);
            RejectRetailEncryption("Encrypt");
            return CreateMode(parameter!.Mechanism).ModeEncryption(parameter, key!, data!);
        }

        public override StringVariableDeclaration Decrypt(string[] parameters)
        {
            ParseArguments(parameters);
            RejectRetailEncryption("Decrypt");
            return CreateMode(parameter!.Mechanism).ModeDecryption(parameter, key!, data!);
        }

        private void RejectRetailEncryption(string functionName)
        {
            if (!parameter!.Mechanism.Equals("DES3-RETAIL", StringComparison.OrdinalIgnoreCase))
                return;
            throw new ArgumentException("Mechanism: DES3-RETAIL can only be used in MAC calculation");
        }

        public override StringVariableDeclaration Mac(string[] parameters)
        {
            ParseArguments(parameters);
            if (!parameter!.Mechanism.Equals("DES3-CMAC", StringComparison.OrdinalIgnoreCase) &&
                !parameter.Mechanism.Equals("DES3-RETAIL", StringComparison.OrdinalIgnoreCase))
            {
                var error = new SemanticError { Type = "Mechanism" };
                error.Message = $"Mechanism: {parameter.Mechanism} cannot be used in MAC calculation";
                error.FunctionName = "Mac";
                throw new SemanticErrorException { SemanticError = error };
            }
            return CreateMode(parameter.Mechanism).ModeMac(parameter, key!, data!);
        }

        private void ParseArguments(string[] arguments)
        {
            parameter = ResolveParameter(arguments[0]);
            key = ResolveKey(arguments[1]);
            data = ResolveData(arguments[2]);

            if (!parameter.Mechanism.Equals("DES3-CBC", StringComparison.OrdinalIgnoreCase) &&
                !parameter.Mechanism.Equals("DES3-ECB", StringComparison.OrdinalIgnoreCase) &&
                !parameter.Mechanism.Equals("DES3-RETAIL", StringComparison.OrdinalIgnoreCase) &&
                !parameter.Mechanism.Equals("DES3-CMAC", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("DES3 requires parameters with mechanism DES3-CBC, DES3-ECB, DES3-RETAIL or DES3-CMAC.");
            if (!string.IsNullOrEmpty(key.Mechanism) &&
                !key.Mechanism.StartsWith("DES3-", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"{parameter.Mechanism} requires a DES3 key.");
        }

        private static ParameterVariableDeclaration ResolveParameter(string value)
        {
            if (VariableDictionary.Instance().Get(value) is ParameterVariableDeclaration declared)
                return declared;
            if (FormatConversions.ParseString(value) == FormatConversions.PAR)
            {
                var inline = new ParameterVariableDeclaration();
                inline.SetInstance(value);
                return inline;
            }
            throw new ArgumentException("wrong parameter argument");
        }

        private static KeyVariableDeclaration ResolveKey(string value)
        {
            if (VariableDictionary.Instance().Get(value) is KeyVariableDeclaration declared)
                return declared;
            if (FormatConversions.ParseString(value) == FormatConversions.HEX)
                return new KeyVariableDeclaration { Value = value, ValueFormat = FormatConversions.HEX };
            if (FormatConversions.ParseString(value) == FormatConversions.JSO)
                return KeyVariableDeclaration.Deserialize(value);
            throw new ArgumentException("wrong key argument");
        }

        private static StringVariableDeclaration ResolveData(string value)
        {
            if (VariableDictionary.Instance().Get(value) is StringVariableDeclaration declared)
                return declared;
            if (FormatConversions.ParseString(value) != string.Empty)
                return new StringVariableDeclaration { Value = value, ValueFormat = FormatConversions.ParseString(value) };
            throw new ArgumentException("wrong data argument");
        }
    }
}
