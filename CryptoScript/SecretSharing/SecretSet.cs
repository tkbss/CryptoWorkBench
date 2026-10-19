using System.Collections.ObjectModel;
using CryptoScript.Variables;

namespace CryptoScript.SecretSharing;

public sealed class SecretShare
{
    private readonly byte[] value;
    public SecretShare(int x, ReadOnlySpan<byte> value)
    {
        if (x is < 1 or > 255) throw new ArgumentOutOfRangeException(nameof(x), "Share index must be 1..255.");
        if (value.IsEmpty) throw new ArgumentException("Share payload must not be empty.");
        X = x;
        this.value = value.ToArray();
    }
    public int X { get; }
    public byte[] Value => (byte[])value.Clone();
    internal ReadOnlySpan<byte> Bytes => value;
}

public sealed class SecretDescriptor
{
    public const string Split = "SECRET_SPLIT", Shamir = "SECRET_SHAMIR";
    public const string Profile11B = "SHAMIR-GF256-11B", Profile11D = "SHAMIR-GF256-11D";
    public SecretDescriptor(string mechanism, int parts, int minParts, int keySizeBits, string? profile = null)
    {
        if (mechanism is not (Split or Shamir)) throw new ArgumentException("Unsupported secret mechanism.");
        if (parts is < 2 or > 255 || minParts < 2 || minParts > parts)
            throw new ArgumentException("Require 2 <= MinParts <= Parts <= 255.");
        if (keySizeBits <= 0 || keySizeBits % 8 != 0) throw new ArgumentException("Secret length must be positive whole bytes.");
        if (mechanism == Split && (minParts != parts || profile is not null))
            throw new ArgumentException("XOR requires all parts and has no field profile.");
        if (mechanism == Shamir)
        {
            profile ??= Profile11B;
            if (profile is not (Profile11B or Profile11D)) throw new ArgumentException("Unsupported Shamir profile.");
        }
        Mechanism = mechanism; Parts = parts; MinParts = minParts; KeySizeBits = keySizeBits; Profile = profile;
    }
    public string Mechanism { get; }
    public int Parts { get; }
    public int MinParts { get; }
    public int KeySizeBits { get; }
    public string? Profile { get; }
    public string? Field => Profile is null ? null : "GF(256)";
    public int? ReductionPolynomial => Profile is null ? null : Profile == Profile11B ? 0x11B : 0x11D;
    public int? SecretPosition => Profile is null ? null : 0;
}

public sealed record SecretKeyAttribute(string? Id, int Length, string? Data);

// The core accepts immutable metadata values, not script variables or mutable OptionalBlocks.
public sealed class SecretKeyMetadata
{
    public SecretKeyMetadata(KeyType keyType, KeyUsagePolicy usage, string derivationMechanism,
        IEnumerable<SecretKeyAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(keyType);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(attributes);
        KeyType = new KeyType(keyType.Algorithm, keyType.MaterialKind);
        if (KeyType.MaterialKind != KeyMaterialKind.Secret) throw new ArgumentException("Share requires symmetric key material.");
        Usage = new KeyUsagePolicy(usage.Mode, usage.AllowedUsages);
        DerivationMechanism = derivationMechanism;
        var copy = attributes.ToArray();
        if (copy.Any(a => a is null)) throw new ArgumentException("Null key attribute.");
        Attributes = Array.AsReadOnly(copy);
    }
    public KeyType KeyType { get; }
    public KeyUsagePolicy Usage { get; }
    public string DerivationMechanism { get; }
    public ReadOnlyCollection<SecretKeyAttribute> Attributes { get; }
}

public sealed class SecretSet
{
    public SecretSet(SecretDescriptor descriptor, IEnumerable<SecretShare> shares,
        string preferredShareFormat = "X-Y", SecretKeyMetadata? originalKeyMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(shares);
        ValidateFormat(preferredShareFormat);
        var copy = shares.ToArray();
        if (copy.Length > descriptor.Parts || copy.Any(s => s is null))
            throw new ArgumentException("Invalid number of shares or null share.");
        if (copy.Select(s => s.X).Distinct().Count() != copy.Length)
            throw new ArgumentException("Duplicate share indices are not allowed.");
        if (copy.Any(s => s.Bytes.Length != descriptor.KeySizeBits / 8))
            throw new ArgumentException("Share lengths must match the descriptor.");
        Descriptor = descriptor;
        Shares = Array.AsReadOnly(copy);
        PreferredShareFormat = preferredShareFormat;
        OriginalKeyMetadata = originalKeyMetadata;
    }
    public SecretDescriptor Descriptor { get; }
    public ReadOnlyCollection<SecretShare> Shares { get; }
    public string PreferredShareFormat { get; }
    public SecretKeyMetadata? OriginalKeyMetadata { get; }
    public static void ValidateFormat(string format)
    {
        if (format is not ("X-Y" or "Y-X" or "SEPARATE-X")) throw new ArgumentException("Unsupported share format.");
    }
}
