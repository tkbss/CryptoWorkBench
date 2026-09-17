using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;

namespace CryptoScript.CryptoAlgorithm
{
    internal static class DigestFactory
    {
        internal static IDigest Create(string mechanism)
        {
            string digest = NormalizeMechanism(mechanism);
            return digest switch
            {
                "SHA1" => new Sha1Digest(),
                "SHA224" => new Sha224Digest(),
                "SHA256" => new Sha256Digest(),
                "SHA384" => new Sha384Digest(),
                "SHA512" => new Sha512Digest(),
                "SHA512-224" => new Sha512tDigest(224),
                "SHA512-256" => new Sha512tDigest(256),
                "SHA3-224" => new Sha3Digest(224),
                "SHA3-256" => new Sha3Digest(256),
                "SHA3-384" => new Sha3Digest(384),
                "SHA3-512" => new Sha3Digest(512),
                _ => throw new ArgumentException($"Unsupported digest mechanism: {mechanism}.")
            };
        }

        private static string NormalizeMechanism(string mechanism)
        {
            string normalized = mechanism.ToUpperInvariant();
            if (normalized.StartsWith("HASH-", StringComparison.Ordinal))
                return normalized["HASH-".Length..];
            if (normalized.StartsWith("HMAC-", StringComparison.Ordinal))
                return normalized["HMAC-".Length..];

            throw new ArgumentException($"Unsupported digest mechanism: {mechanism}.");
        }
    }
}
