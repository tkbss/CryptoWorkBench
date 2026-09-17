using CryptoScript.Model;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.CryptoAlgorithm.HMAC
{
    public class HMACMode : EncryptionMode
    {
        public override StringVariableDeclaration ModeMac(
            ParameterVariableDeclaration parameter,
            KeyVariableDeclaration key,
            StringVariableDeclaration data)
        {
            byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
            byte[] input = FormatConversions.ToByteArray(data.Value, data.ValueFormat);

            IMac mac = new HMac(CreateDigest(parameter.Mechanism));
            mac.Init(new KeyParameter(keyBytes));
            mac.BlockUpdate(input, 0, input.Length);
            byte[] result = new byte[mac.GetMacSize()];
            mac.DoFinal(result, 0);

            string value = FormatConversions.ByteArrayToHexString(result);
            return new StringVariableDeclaration
            {
                Value = value,
                ValueFormat = FormatConversions.ParseString(value),
                Type = new CryptoTypeVar()
            };
        }

        private static IDigest CreateDigest(string mechanism)
        {
            return mechanism.ToUpperInvariant() switch
            {
                "HMAC-SHA1" => new Sha1Digest(),
                "HMAC-SHA224" => new Sha224Digest(),
                "HMAC-SHA256" => new Sha256Digest(),
                "HMAC-SHA384" => new Sha384Digest(),
                "HMAC-SHA512" => new Sha512Digest(),
                "HMAC-SHA512-224" => new Sha512tDigest(224),
                "HMAC-SHA512-256" => new Sha512tDigest(256),
                "HMAC-SHA3-224" => new Sha3Digest(224),
                "HMAC-SHA3-256" => new Sha3Digest(256),
                "HMAC-SHA3-384" => new Sha3Digest(384),
                "HMAC-SHA3-512" => new Sha3Digest(512),
                _ => throw new ArgumentException($"Unsupported HMAC mechanism: {mechanism}.")
            };
        }
    }
}
