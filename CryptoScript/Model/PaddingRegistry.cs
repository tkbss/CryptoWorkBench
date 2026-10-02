using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace CryptoScript.Model;

public sealed record PaddingDefinition
{
    public PaddingDefinition(string canonicalName, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        CanonicalName = canonicalName;
        Description = description;
    }

    public string CanonicalName { get; }
    public string Description { get; }
}

public static class PaddingRegistry
{
    private static readonly ReadOnlyCollection<PaddingDefinition> RegistryEntries =
        Array.AsReadOnly(new PaddingDefinition[]
        {
            new("ANSI-X923",
                "Padding with zero-valued bytes followed by a final byte that records the padding length."),
            new("ISO-10126",
                "Padding with random filler bytes followed by a final byte that records the padding length."),
            new("ISO-7816",
                "Padding that appends 0x80 followed by zero-valued bytes to the required boundary."),
            new("ISO-9797-M1",
                "ISO/IEC 9797-1 method 1 padding with zero-valued bytes added to the required boundary."),
            new("ISO-9797-M2",
                "ISO/IEC 9797-1 method 2 padding that appends 0x80 followed by zero-valued bytes."),
            new("ISO-9797-M3",
                "ISO/IEC 9797-1 method 3 padding that includes the original message length."),
            new("NONE",
                "Canonical selection indicating that no padding bytes are added or removed."),
            new("PKCS-7",
                "Padding in which every added byte contains the total number of padding bytes."),
            new("TLS-CBC",
                "TLS CBC padding in which each added byte contains a value one less than the total number of padding bytes.")
        });

    private static readonly FrozenDictionary<string, PaddingDefinition> EntriesByName =
        RegistryEntries.ToFrozenDictionary(
            entry => entry.CanonicalName,
            StringComparer.Ordinal);

    public static IReadOnlyList<PaddingDefinition> Entries => RegistryEntries;

    public static bool TryGet(string? canonicalName, out PaddingDefinition? definition)
    {
        if (canonicalName is null)
        {
            definition = null;
            return false;
        }

        return EntriesByName.TryGetValue(canonicalName, out definition);
    }
}
