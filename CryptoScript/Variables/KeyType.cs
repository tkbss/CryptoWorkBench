using Newtonsoft.Json;

namespace CryptoScript.Variables;

public enum KeyAlgorithm
{
    Unknown,
    Aes,
    Tdea,
    Hmac,
    Rsa,
    Ec
}

public enum KeyMaterialKind
{
    Secret,
    Public,
    Private
}

public sealed record KeyType
{
    [JsonConstructor]
    public KeyType(KeyAlgorithm algorithm, KeyMaterialKind materialKind)
    {
        if (!Enum.IsDefined(algorithm))
            throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unknown key algorithm value.");
        if (!Enum.IsDefined(materialKind))
            throw new ArgumentOutOfRangeException(nameof(materialKind), materialKind, "Unknown key material kind value.");

        bool valid = algorithm switch
        {
            KeyAlgorithm.Unknown or KeyAlgorithm.Aes or KeyAlgorithm.Tdea or KeyAlgorithm.Hmac =>
                materialKind == KeyMaterialKind.Secret,
            KeyAlgorithm.Rsa or KeyAlgorithm.Ec =>
                materialKind is KeyMaterialKind.Public or KeyMaterialKind.Private,
            _ => false
        };
        if (!valid)
            throw new ArgumentException($"{algorithm} key material cannot be {materialKind}.", nameof(materialKind));

        Algorithm = algorithm;
        MaterialKind = materialKind;
    }

    public KeyAlgorithm Algorithm { get; }
    public KeyMaterialKind MaterialKind { get; }

    public static KeyType Secret(KeyAlgorithm algorithm) => new(algorithm, KeyMaterialKind.Secret);
    public static KeyType Public(KeyAlgorithm algorithm) => new(algorithm, KeyMaterialKind.Public);
    public static KeyType Private(KeyAlgorithm algorithm) => new(algorithm, KeyMaterialKind.Private);
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
