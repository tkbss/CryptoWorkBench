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
            new("#ARC",
                "Two-byte HEX Authorisation Response Code required by MAC-EMV-ARPC-METHOD-1."),
            new("#BLKH",
                "Key block header associated with a key wrapping operation."),
            new("#COUNTER",
                "Counter value or counter configuration used by mechanisms whose operation depends on a counter."),
            new("#CSU",
                "Four-byte HEX Card Status Update required by MAC-EMV-ARPC-METHOD-2."),
            new("#DIRECTION",
                "Normal string literal SEND or RECEIVE relative to the master for EP2-DATA-ENCRYPTION."),
            new("#EXPECTED",
                "Expected eight-byte HEX Application Cryptogram required by MAC-EMV-AC-VERIFY."),
            new("#FILL",
                "Fill nibbles used by PIN-block formats that include a variable fill field."),
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
            new("#MINPARTS", "Shamir threshold, from 2 to PARTS."),
            new("#NONCE",
                "Nonce used by mechanisms that require a unique or per-operation value."),
            new("#OFFSET",
                "One-byte HEX field offset from EP2 Table 10 for EP2-DATA-ENCRYPTION."),
            new("#OUTLEN",
                "Requested output length for mechanisms that produce configurable-length output."),
            new("#PAD",
                "Padding scheme selected for mechanisms that process padded data."),
            new("#PAN",
                "Complete decimal primary account number used by PIN-block formats."),
            new("#PARTS", "Total number of secret shares, from 2 to 255."),
            new("#POLYNOM", "Shamir GF(256) profile: SHAMIR-GF256-11B (default) or SHAMIR-GF256-11D."),
            new("#PRF",
                "Pseudorandom function selection used by mechanisms that support a configurable PRF."),
            new("#PSN",
                "Two ASCII decimal digits supplied as a normal string literal for EMV ICC Master Key Derivation Options A, B and C PAN Sequence Number."),
            new("#RANDOM",
                "Random field used by PIN-block formats that require random hexadecimal nibbles."),
            new("#RND",
                "Random or caller-supplied filler material used by mechanisms that require such material."),
            new("#SALT",
                "Salt input used by mechanisms that support salted derivation or extraction."),
            new("#SHAREFORMAT", "External share format: X-Y (default), Y-X or SEPARATE-X."),
            new("#TRANSACTION",
                "Transaction field used by PIN-block formats that bind a PIN to transaction data."),
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
