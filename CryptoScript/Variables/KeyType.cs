using Newtonsoft.Json;

namespace CryptoScript.Variables;

public enum KeyAlgorithm
{
    // Persisted numeric values must remain stable. Values 4 and 5 are unsupported.
    Unknown = 0,
    Aes = 1,
    Tdea = 2,
    Hmac = 3
}

public enum KeyMaterialKind
{
    // Retained in the serialized contract for existing symmetric keys.
    Secret = 0
}

public sealed record KeyType
{
    [JsonConstructor]
    public KeyType(KeyAlgorithm algorithm, KeyMaterialKind materialKind)
    {
        if (!Enum.IsDefined(algorithm))
            throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported key algorithm in CryptoScript v1.");
        if (!Enum.IsDefined(materialKind))
            throw new ArgumentOutOfRangeException(nameof(materialKind), materialKind, "Unsupported key material kind in CryptoScript v1.");

        Algorithm = algorithm;
        MaterialKind = materialKind;
    }

    public KeyAlgorithm Algorithm { get; }
    public KeyMaterialKind MaterialKind { get; }

    public static KeyType Secret(KeyAlgorithm algorithm) => new(algorithm, KeyMaterialKind.Secret);
}

public readonly record struct KeySize
{
    public KeySize(int bits)
        : this(bits >= 0
            ? (ulong)bits
            : throw new ArgumentOutOfRangeException(nameof(bits), "Key size cannot be negative."))
    {
    }

    [JsonConstructor]
    public KeySize(ulong bits)
    {
        Bits = bits;
    }

    public ulong Bits { get; }

    [JsonIgnore]
    public bool IsKnown => Bits > 0;

    public static KeySize Unknown => new(0);
}
