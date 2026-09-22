using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.HMAC
{
    public class HMAC : SymmetricCryptoAlgorithm
    {
        private static readonly HashSet<string> SupportedMechanisms = new(StringComparer.OrdinalIgnoreCase)
        {
            "HMAC-SHA1",
            "HMAC-SHA224",
            "HMAC-SHA256",
            "HMAC-SHA384",
            "HMAC-SHA512",
            "HMAC-SHA512-224",
            "HMAC-SHA512-256",
            "HMAC-SHA3-224",
            "HMAC-SHA3-256",
            "HMAC-SHA3-384",
            "HMAC-SHA3-512"
        };

        public override ParameterVariableDeclaration GenerateParameters(string mechanism)
        {
            mechanism = NormalizeAndValidateMechanism(mechanism);
            var parameter = new ParameterVariableDeclaration
            {
                Mechanism = mechanism
            };
            parameter.ValueFormat = FormatConversions.ParseString(parameter.Value);
            return parameter;
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
        {
            if (parameters.Length != 0)
                throw new ArgumentException("HMAC does not support additional parameters.");

            return GenerateParameters(mechanism);
        }

        public override KeyVariableDeclaration GenerateKey(string mechanism, string keySize)
        {
            mechanism = NormalizeAndValidateMechanism(mechanism);
            if (FormatConversions.ParseString(keySize) == FormatConversions.HEX)
            {
                byte[] existingKey = FormatConversions.ToByteArray(keySize, FormatConversions.HEX);
                if (existingKey.Length == 0)
                    throw new ArgumentException("HMAC key must not be empty.");

                return CreateKeyVariable(keySize, mechanism, existingKey.Length * 8);
            }

            if (!int.TryParse(keySize, out int keySizeBits) || keySizeBits <= 0 || keySizeBits % 8 != 0)
                throw new ArgumentException("HMAC key size must be a positive multiple of 8 bits.");

            byte[] keyBytes = RandomNumberGenerator.GetBytes(keySizeBits / 8);
            return CreateKeyVariable(
                FormatConversions.ByteArrayToHexString(keyBytes),
                mechanism,
                keySizeBits);
        }

        public override StringVariableDeclaration Mac(string[] parameters)
        {
            ParameterVariableDeclaration parameter = ResolveParameter(parameters[0]);
            NormalizeAndValidateMechanism(parameter.Mechanism);
            KeyVariableDeclaration key = ResolveKey(parameters[1]);
            StringVariableDeclaration data = ResolveData(parameters[2]);

            return CreateMode(parameter.Mechanism).ModeMac(parameter, key, data);
        }

        public override EncryptionMode CreateMode(string mechanism)
        {
            NormalizeAndValidateMechanism(mechanism);
            return new HMACMode();
        }

        private static KeyVariableDeclaration CreateKeyVariable(string value, string mechanism, int keySizeBits)
        {
            return new KeyVariableDeclaration
            {
                Value = value,
                KeyValue = value,
                ValueFormat = FormatConversions.ParseString(value),
                KeySize = keySizeBits.ToString(),
                KeyType = KeyType.Secret(KeyAlgorithm.Hmac),
                Mechanism = mechanism,
                Type = new CryptoTypeKey()
            };
        }

        private string NormalizeAndValidateMechanism(string mechanism)
        {
            mechanism = ExtractMechanismen(mechanism);
            if (!SupportedMechanisms.Contains(mechanism))
                throw new ArgumentException($"Unsupported HMAC mechanism: {mechanism}.");

            return mechanism;
        }

        private static ParameterVariableDeclaration ResolveParameter(string value)
        {
            if (VariableDictionary.Instance().Get(value) is ParameterVariableDeclaration declared)
                return declared;
            if (FormatConversions.ParseString(value) == FormatConversions.PAR)
            {
                var parameter = new ParameterVariableDeclaration();
                parameter.SetInstance(value);
                return parameter;
            }

            throw new ArgumentException("wrong parameter argument");
        }

        private static KeyVariableDeclaration ResolveKey(string value)
        {
            if (VariableDictionary.Instance().Get(value) is KeyVariableDeclaration declared)
                return declared;
            if (FormatConversions.ParseString(value) == FormatConversions.HEX)
            {
                return new KeyVariableDeclaration
                {
                    Value = value,
                    KeyValue = value,
                    ValueFormat = FormatConversions.HEX,
                    Type = new CryptoTypeKey()
                };
            }
            if (FormatConversions.ParseString(value) == FormatConversions.JSO)
                return KeyVariableDeclaration.Deserialize(value);

            throw new ArgumentException("wrong key argument");
        }

        private static StringVariableDeclaration ResolveData(string value)
        {
            if (VariableDictionary.Instance().Get(value) is StringVariableDeclaration declared)
                return declared;

            string format = FormatConversions.ParseString(value);
            if (format == FormatConversions.HEX || format == FormatConversions.B64 || format == FormatConversions.STR)
                return new StringVariableDeclaration { Value = value, ValueFormat = format };

            throw new ArgumentException("wrong data argument");
        }
    }
}
