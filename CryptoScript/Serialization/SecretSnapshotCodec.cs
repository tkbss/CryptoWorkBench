using System.Security.Cryptography;
using CryptoScript.SecretSharing;
using CryptoScript.Variables;
using static CryptoScript.Serialization.SnapshotJson;

namespace CryptoScript.Serialization;

internal static class SecretSnapshotCodec
{
    internal static SecretPayloadDto Encode(SecretVariableDeclaration variable)
    {
        var set = variable.SecretSet;
        var d = set.Descriptor;
        var m = set.OriginalKeyMetadata;
        return new(new(d.Mechanism, d.Parts, d.MinParts, d.KeySizeBits, d.Profile), set.PreferredShareFormat,
            set.Shares.Select(s => new ShareDto(s.X, Convert.ToBase64String(s.Bytes))).ToList(),
            m is null ? null : new(KeySnapshotCodec.WriteType(m.KeyType), KeySnapshotCodec.WriteUsage(m.Usage),
                m.DerivationMechanism, m.Attributes.Select(a => new AttributeDto(a.Id, a.Length, a.Data)).ToList()),
            SourceDto.Capture(variable));
    }

    internal static SecretVariableDeclaration Decode(SecretPayloadDto payload)
    {
        var d = payload.Descriptor;
        var descriptor = new SecretDescriptor(d.Mechanism, d.Parts, d.MinParts, d.KeySizeBits, d.Profile);
        Require(descriptor.Profile == d.Profile, "Missing Shamir profile.");
        var m = payload.OriginalKeyMetadata;
        var metadata = m is null ? null : new SecretKeyMetadata(KeySnapshotCodec.ReadType(m.KeyType),
            KeySnapshotCodec.ReadUsage(m.Usage), m.DerivationMechanism,
            m.Attributes.Select(a => new SecretKeyAttribute(a.Id, a.Length, a.Data)));
        var shares = new List<SecretShare>();
        foreach (var share in payload.Shares)
        {
            byte[] bytes = Convert.FromBase64String(share.Bytes);
            try { shares.Add(new SecretShare(share.X, bytes)); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        return new(new SecretSet(descriptor, shares, payload.PreferredShareFormat, metadata));
    }
}
