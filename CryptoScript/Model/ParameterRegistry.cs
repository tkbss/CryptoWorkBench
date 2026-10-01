using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace CryptoScript.Model;

public sealed record ParameterDefinition
{
    public ParameterDefinition(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Name = name;
        Description = description;
    }

    public string Name { get; }
    public string Description { get; }
}

public static class ParameterRegistry
{
    private static readonly ReadOnlyCollection<ParameterDefinition> RegistryEntries =
        Array.AsReadOnly(new ParameterDefinition[]
        {
            new("#ADATA",
                "Additional data authenticated by mechanisms that support associated data without encrypting it."),
            new("#BLKH",
                "Key block header associated with a key wrapping operation."),
            new("#COUNTER",
                "Counter value or counter configuration used by mechanisms whose operation depends on a counter."),
            new("#HASH",
                "Hash function selection used by mechanisms that require a configurable hash algorithm."),
            new("#IV",
                "Initialization vector used by mechanisms that require an initial cryptographic state."),
            new("#KEYTYPE",
                "Key type selection used by mechanisms that derive or produce a selectable kind of key."),
            new("#LABEL",
                "Label or domain-separation input used by mechanisms that support labelled derivation."),
            new("#MACLEN",
                "Requested or selected length of a message authentication code."),
            new("#MECH",
                "Cryptographic mechanism selected for a parameter set."),
            new("#NONCE",
                "Nonce used by mechanisms that require a unique or per-operation value."),
            new("#OUTLEN",
                "Requested output length for mechanisms that produce configurable-length output."),
            new("#PAD",
                "Padding scheme selected for mechanisms that process padded data."),
            new("#PRF",
                "Pseudorandom function selection used by mechanisms that support a configurable PRF."),
            new("#RND",
                "Random or caller-supplied filler material used by mechanisms that require such material."),
            new("#SALT",
                "Salt input used by mechanisms that support salted derivation or extraction."),
            new("#USAGE",
                "Intended usage or purpose selected for a derived or produced key."),
            new("#VARIANT",
                "Variant selection used by mechanisms that expose multiple defined processing variants.")
        });

    private static readonly FrozenDictionary<string, ParameterDefinition> EntriesByName =
        RegistryEntries.ToFrozenDictionary(entry => entry.Name, StringComparer.Ordinal);

    public static IReadOnlyList<ParameterDefinition> Entries => RegistryEntries;

    public static bool TryGet(string? canonicalName, out ParameterDefinition? definition)
    {
        if (canonicalName is null)
        {
            definition = null;
            return false;
        }

        return EntriesByName.TryGetValue(canonicalName, out definition);
    }
}
