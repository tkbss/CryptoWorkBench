using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace CryptoScript.Model;

public sealed record MechanismRegistryEntry(
    string CanonicalName,
    string Description,
    string DocumentationFileName);

public static class MechanismRegistry
{
    private static readonly ReadOnlyCollection<MechanismRegistryEntry> RegistryEntries =
        Array.AsReadOnly(new MechanismRegistryEntry[]
        {
            new("AES-CBC", "Symmetric Advanced Encryption Standard in Cipher Block Chaining mode.", "Info.Mech.AES-CBC.md"),
            new("AES-CCM", "Symmetric Advanced Encryption Standard in Counter with CBC-MAC mode.", "Info.Mech.AES-CCM.md"),
            new("AES-CMAC", "Symmetric Advanced Encryption Standard in Cipher-based Message Authentication Code mode.", "Info.Mech.AES-CMAC.md"),
            new("AES-CTR", "Symmetric Advanced Encryption Standard in Counter mode.", "Info.Mech.AES-CTR.md"),
            new("AES-ECB", "Symmetric Advanced Encryption Standard in Electronic Codebook mode.", "Info.Mech.AES-ECB.md"),
            new("AES-GCM", "Symmetric Advanced Encryption Standard in Galois/Counter mode.", "Info.Mech.AES-GCM.md"),
            new("AES-GMAC", "Symmetric Advanced Encryption Standard in Galois/Counter mode.", "Info.Mech.AES-GMAC.md"),
            new("DES3-CBC", "Symmetric Triple Data Encryption Standard in Cipher Block Chaining mode.", "Info.Mech.DES3-CBC.md"),
            new("DES3-CMAC", "Symmetric Triple Data Encryption Standard in Cipher-based Message Authentication Code mode.", "Info.Mech.DES3-CMAC.md"),
            new("DES3-ECB", "Symmetric Triple Data Encryption Standard in Electronic Codebook mode.", "Info.Mech.DES3-ECB.md"),
            new("DES3-RETAIL", "Symmetric Triple Data Encryption Standard in Retail mode.", "Info.Mech.DES3-RETAIL.md"),
            new("DUKPT-AES-INITIAL-KEY", "ANSI X9.24-3-2017 derivation of an AES DUKPT Initial Key from an AES BDK and 64-bit IKID.", "Info.Mech.DUKPT-AES-INITIAL-KEY.md"),
            new("DUKPT-AES-WORKING-KEY", "ANSI X9.24-3-2017 stateless host derivation of a directly usable Working Key from an AES DUKPT Initial Key and complete 96-bit KSN.", "Info.Mech.DUKPT-AES-WORKING-KEY.md"),
            new("DUKPT-TDEA-INITIAL-KEY", "Legacy ANSI X9.24 double-length TDEA DUKPT Initial Key derivation from a 16-byte BDK and complete 80-bit KSN.", "Info.Mech.DUKPT-TDEA-INITIAL-KEY.md"),
            new("DUKPT-TDEA-WORKING-KEY", "ANSI X9.24-3 Annex C stateless host derivation of a double-length TDEA Working Key from a TDEA Initial Key and complete 80-bit KSN.", "Info.Mech.DUKPT-TDEA-WORKING-KEY.md"),
            new("HASH-SHA1", "Unkeyed message digest using SHA-1.", "Info.Mech.HASH-SHA1.md"),
            new("HASH-SHA224", "Unkeyed message digest using SHA-224.", "Info.Mech.HASH-SHA224.md"),
            new("HASH-SHA256", "Unkeyed message digest using SHA-256.", "Info.Mech.HASH-SHA256.md"),
            new("HASH-SHA3-224", "Unkeyed message digest using SHA3-224.", "Info.Mech.HASH-SHA3-224.md"),
            new("HASH-SHA3-256", "Unkeyed message digest using SHA3-256.", "Info.Mech.HASH-SHA3-256.md"),
            new("HASH-SHA3-384", "Unkeyed message digest using SHA3-384.", "Info.Mech.HASH-SHA3-384.md"),
            new("HASH-SHA3-512", "Unkeyed message digest using SHA3-512.", "Info.Mech.HASH-SHA3-512.md"),
            new("HASH-SHA384", "Unkeyed message digest using SHA-384.", "Info.Mech.HASH-SHA384.md"),
            new("HASH-SHA512", "Unkeyed message digest using SHA-512.", "Info.Mech.HASH-SHA512.md"),
            new("HASH-SHA512-224", "Unkeyed message digest using SHA-512/224.", "Info.Mech.HASH-SHA512-224.md"),
            new("HASH-SHA512-256", "Unkeyed message digest using SHA-512/256.", "Info.Mech.HASH-SHA512-256.md"),
            new("HKDF-EXPAND", "RFC 5869 HKDF Expand operation only; derives output keying material from an existing PRK.", "Info.Mech.HKDF-EXPAND.md"),
            new("HKDF-EXTRACT", "RFC 5869 HKDF Extract operation only; returns the pseudorandom key (PRK).", "Info.Mech.HKDF-EXTRACT.md"),
            new("HMAC-SHA1", "Keyed-Hash Message Authentication Code using SHA-1.", "Info.Mech.HMAC-SHA1.md"),
            new("HMAC-SHA224", "Keyed-Hash Message Authentication Code using SHA-224.", "Info.Mech.HMAC-SHA224.md"),
            new("HMAC-SHA256", "Keyed-Hash Message Authentication Code using SHA-256.", "Info.Mech.HMAC-SHA256.md"),
            new("HMAC-SHA3-224", "Keyed-Hash Message Authentication Code using SHA3-224.", "Info.Mech.HMAC-SHA3-224.md"),
            new("HMAC-SHA3-256", "Keyed-Hash Message Authentication Code using SHA3-256.", "Info.Mech.HMAC-SHA3-256.md"),
            new("HMAC-SHA3-384", "Keyed-Hash Message Authentication Code using SHA3-384.", "Info.Mech.HMAC-SHA3-384.md"),
            new("HMAC-SHA3-512", "Keyed-Hash Message Authentication Code using SHA3-512.", "Info.Mech.HMAC-SHA3-512.md"),
            new("HMAC-SHA384", "Keyed-Hash Message Authentication Code using SHA-384.", "Info.Mech.HMAC-SHA384.md"),
            new("HMAC-SHA512", "Keyed-Hash Message Authentication Code using SHA-512.", "Info.Mech.HMAC-SHA512.md"),
            new("HMAC-SHA512-224", "Keyed-Hash Message Authentication Code using SHA-512/224.", "Info.Mech.HMAC-SHA512-224.md"),
            new("HMAC-SHA512-256", "Keyed-Hash Message Authentication Code using SHA-512/256.", "Info.Mech.HMAC-SHA512-256.md"),
            new("KDF-EP2-PAN-RECEIPT-TRM", "ep2 8.13 Extract-and-Expand using SHA-256(Terminal Properties) as info and returning the leftmost 16 of 32 bytes.", "Info.Mech.KDF-EP2-PAN-RECEIPT-TRM.md"),
            new("KDF-EP2-PAN-RECEIPT-TRX", "ep2 8.12 direct Expand using SHA-256(DOL) as info and returning the leftmost 16 of 32 bytes.", "Info.Mech.KDF-EP2-PAN-RECEIPT-TRX.md"),
            new("KDF-EP2-PAN-SURROGATE-TRX", "ep2 8.14 direct Expand using raw DOL as info and returning all 32 bytes.", "Info.Mech.KDF-EP2-PAN-SURROGATE-TRX.md"),
            new("KDF-EP2-SESSION", "ep2 8.11 Extract-and-Expand derivation of a selected Session Key Variant.", "Info.Mech.KDF-EP2-SESSION.md"),
            new("KDF-HKDF", "HMAC-based Extract-and-Expand Key Derivation Function specified in RFC 5869.", "Info.Mech.KDF-HKDF.md"),
            new("KDF-SP800-108-COUNTER", "NIST SP 800-108 Rev. 1 Update 1 Counter Mode KDF using a supported HMAC PRF or AES-CMAC.", "Info.Mech.KDF-SP800-108-COUNTER.md"),
            new("WRAP-AES-TR31", "TR-31 Version D key wrapping with AES Key Derivation Binding.", "Info.Mech.WRAP-AES-TR31.md"),
            new("WRAP-DES3-TR31", "TR-31 Version A/B/C key wrapping with TDEA Variant or Derivation Binding.", "Info.Mech.WRAP-DES3-TR31.md")
        });

    private static readonly FrozenDictionary<string, MechanismRegistryEntry> EntriesByName =
        RegistryEntries.ToFrozenDictionary(entry => entry.CanonicalName, StringComparer.Ordinal);

    public static IReadOnlyList<MechanismRegistryEntry> Entries => RegistryEntries;

    public static bool TryGet(string? canonicalName, out MechanismRegistryEntry? entry)
    {
        if (canonicalName is null)
        {
            entry = null;
            return false;
        }

        return EntriesByName.TryGetValue(canonicalName, out entry);
    }
}
