using Newtonsoft.Json;

namespace CryptoScript.Variables;

[Flags]
public enum KeyUsage
{
    None = 0,
    Encrypt = 1 << 0,
    Decrypt = 1 << 1,
    MacGenerate = 1 << 2,
    MacVerify = 1 << 3,
    DeriveKey = 1 << 4,
    WrapKey = 1 << 5,
    UnwrapKey = 1 << 6,
    PinEncrypt = 1 << 7
}

public enum KeyUsageMode
{
    Unspecified,
    Restricted
}

public sealed record KeyUsagePolicy
{
    private const KeyUsage AllDefinedUsages =
        KeyUsage.Encrypt |
        KeyUsage.Decrypt |
        KeyUsage.MacGenerate |
        KeyUsage.MacVerify |
        KeyUsage.DeriveKey |
        KeyUsage.WrapKey |
        KeyUsage.UnwrapKey |
        KeyUsage.PinEncrypt;

    [JsonConstructor]
    public KeyUsagePolicy(KeyUsageMode mode, KeyUsage allowedUsages)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown key usage mode value.");
        if ((allowedUsages & ~AllDefinedUsages) != KeyUsage.None)
            throw new ArgumentOutOfRangeException(nameof(allowedUsages), allowedUsages, "Unknown key usage value.");
        if (mode == KeyUsageMode.Unspecified && allowedUsages != KeyUsage.None)
            throw new ArgumentException("Unspecified key usage cannot contain allowed usages.", nameof(allowedUsages));
        if (mode == KeyUsageMode.Restricted && allowedUsages == KeyUsage.None)
            throw new ArgumentException("Restricted key usage must contain at least one allowed usage.", nameof(allowedUsages));

        Mode = mode;
        AllowedUsages = allowedUsages;
    }

    public KeyUsageMode Mode { get; }
    public KeyUsage AllowedUsages { get; }

    public static KeyUsagePolicy Unspecified { get; } =
        new(KeyUsageMode.Unspecified, KeyUsage.None);

    public static KeyUsagePolicy Restricted(KeyUsage allowedUsages) =>
        new(KeyUsageMode.Restricted, allowedUsages);
}
