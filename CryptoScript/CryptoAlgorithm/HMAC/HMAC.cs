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
            mechanism = GenerateKeyContract.RequireMechanism(mechanism, KeyAlgorithm.Hmac).CanonicalName;
            if (FormatConversions.ParseString(keySize) == FormatConversions.HEX)
            {
                byte[] existingKey = GenerateKeyContract.DecodeImport(keySize);
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
            => Mac(AlgorithmCallArguments.FromValues(parameters));

        public override StringVariableDeclaration Mac(AlgorithmCallArguments parameters)
        {
            ParameterVariableDeclaration parameter =
                AlgorithmArgumentResolver.ResolveParameter(parameters.Arguments[0]);
            NormalizeAndValidateMechanism(parameter.Mechanism);
            KeyVariableDeclaration key = AlgorithmArgumentResolver.ResolveKey(parameters.Arguments[1]);
            KeyMetadataValidator.Validate(key, KeyAlgorithm.Hmac, KeyUsage.MacGenerate,
                "Mac", parameter.Mechanism, parameters.DiagnosticSink);
            StringVariableDeclaration data = ResolveData(parameters.Arguments[2].Value!);

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
                ValueFormat = FormatConversions.ParseString(value),
                KeySize = keySizeBits.ToString(),
                KeyType = KeyType.Secret(KeyAlgorithm.Hmac),
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
