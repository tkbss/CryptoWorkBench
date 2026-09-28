namespace CryptoScript.Documentation;

public sealed class FileInfoDocumentationProvider : IInfoDocumentationProvider
{
    private static readonly IReadOnlyDictionary<string, string> DocumentNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["functions"] = "Info.Functions.md",
            ["mechanisms"] = "Info.Mechanisms.md",
            ["types"] = "Info.Types.md",
            ["parameters"] = "Info.Parameters.md",
            ["keymap"] = "Info.Keymap.md",
            ["paddings"] = "Info.Paddings.md",
            ["AES-CBC"] = "Info.Mech.AES-CBC.md",
            ["AES-ECB"] = "Info.Mech.AES-ECB.md",
            ["AES-CTR"] = "Info.Mech.AES-CTR.md",
            ["AES-CMAC"] = "Info.Mech.AES-CMAC.md",
            ["AES-GMAC"] = "Info.Mech.AES-GMAC.md",
            ["AES-GCM"] = "Info.Mech.AES-GCM.md",
            ["AES-CCM"] = "Info.Mech.AES-CCM.md",
            ["WRAP-AES-TR31"] = "Info.Mech.WRAP-AES-TR31.md",
            ["WRAP-DES3-TR31"] = "Info.Mech.WRAP-DES3-TR31.md",
            ["DES3-CBC"] = "Info.Mech.DES3-CBC.md",
            ["DES3-ECB"] = "Info.Mech.DES3-ECB.md",
            ["DES3-CMAC"] = "Info.Mech.DES3-CMAC.md",
            ["DES3-RETAIL"] = "Info.Mech.DES3-RETAIL.md",
            ["HMAC-SHA1"] = "Info.Mech.HMAC-SHA1.md",
            ["HMAC-SHA224"] = "Info.Mech.HMAC-SHA224.md",
            ["HMAC-SHA256"] = "Info.Mech.HMAC-SHA256.md",
            ["HMAC-SHA384"] = "Info.Mech.HMAC-SHA384.md",
            ["HMAC-SHA512"] = "Info.Mech.HMAC-SHA512.md",
            ["HMAC-SHA512-224"] = "Info.Mech.HMAC-SHA512-224.md",
            ["HMAC-SHA512-256"] = "Info.Mech.HMAC-SHA512-256.md",
            ["HMAC-SHA3-224"] = "Info.Mech.HMAC-SHA3-224.md",
            ["HMAC-SHA3-256"] = "Info.Mech.HMAC-SHA3-256.md",
            ["HMAC-SHA3-384"] = "Info.Mech.HMAC-SHA3-384.md",
            ["HMAC-SHA3-512"] = "Info.Mech.HMAC-SHA3-512.md",
            ["HASH-SHA1"] = "Info.Mech.HASH-SHA1.md",
            ["HASH-SHA224"] = "Info.Mech.HASH-SHA224.md",
            ["HASH-SHA256"] = "Info.Mech.HASH-SHA256.md",
            ["HASH-SHA384"] = "Info.Mech.HASH-SHA384.md",
            ["HASH-SHA512"] = "Info.Mech.HASH-SHA512.md",
            ["HASH-SHA512-224"] = "Info.Mech.HASH-SHA512-224.md",
            ["HASH-SHA512-256"] = "Info.Mech.HASH-SHA512-256.md",
            ["HASH-SHA3-224"] = "Info.Mech.HASH-SHA3-224.md",
            ["HASH-SHA3-256"] = "Info.Mech.HASH-SHA3-256.md",
            ["HASH-SHA3-384"] = "Info.Mech.HASH-SHA3-384.md",
            ["HASH-SHA3-512"] = "Info.Mech.HASH-SHA3-512.md",
            ["KDF-HKDF"] = "Info.Mech.KDF-HKDF.md",
            ["HKDF-EXTRACT"] = "Info.Mech.HKDF-EXTRACT.md",
            ["HKDF-EXPAND"] = "Info.Mech.HKDF-EXPAND.md",
            ["KDF-SP800-108-COUNTER"] = "Info.Mech.KDF-SP800-108-COUNTER.md",
            ["DUKPT-AES-INITIAL-KEY"] = "Info.Mech.DUKPT-AES-INITIAL-KEY.md",
            ["DUKPT-AES-WORKING-KEY"] = "Info.Mech.DUKPT-AES-WORKING-KEY.md",
            ["DUKPT-TDEA-INITIAL-KEY"] = "Info.Mech.DUKPT-TDEA-INITIAL-KEY.md",
            ["DUKPT-TDEA-WORKING-KEY"] = "Info.Mech.DUKPT-TDEA-WORKING-KEY.md",
            ["KDF-EP2-SESSION"] = "Info.Mech.KDF-EP2-SESSION.md",
            ["KDF-EP2-PAN-RECEIPT-TRX"] = "Info.Mech.KDF-EP2-PAN-RECEIPT-TRX.md",
            ["KDF-EP2-PAN-RECEIPT-TRM"] = "Info.Mech.KDF-EP2-PAN-RECEIPT-TRM.md",
            ["KDF-EP2-PAN-SURROGATE-TRX"] = "Info.Mech.KDF-EP2-PAN-SURROGATE-TRX.md"
        };

    private readonly string _infoDocsDirectory;

    public FileInfoDocumentationProvider()
        : this(AppContext.BaseDirectory)
    {
    }

    public FileInfoDocumentationProvider(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _infoDocsDirectory = Path.Combine(baseDirectory, "InfoDocs");
    }

    public bool TryGetDocumentation(string name, out string documentation)
    {
        documentation = string.Empty;

        if (string.IsNullOrEmpty(name) || !DocumentNames.TryGetValue(name, out string? documentName))
            return false;

        documentation = File.ReadAllText(Path.Combine(_infoDocsDirectory, documentName));
        return true;
    }

    public bool HasDocumentation(string name) =>
        !string.IsNullOrEmpty(name) && DocumentNames.ContainsKey(name);
}
