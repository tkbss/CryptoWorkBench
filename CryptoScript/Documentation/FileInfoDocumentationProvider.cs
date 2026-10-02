using CryptoScript.Model;
using System.Collections.Frozen;

namespace CryptoScript.Documentation;

public sealed class FileInfoDocumentationProvider : IInfoDocumentationProvider
{
    private static readonly FrozenDictionary<string, string> LegacyDocumentNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["types"] = "Info.Types.md",
            ["keymap"] = "Info.Keymap.md"
        }.ToFrozenDictionary(StringComparer.Ordinal);

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

        if (string.IsNullOrEmpty(name))
            return false;

        if (LegacyDocumentNames.TryGetValue(name, out string? legacyFileName))
            return TryReadDocumentation(legacyFileName, out documentation);

        return TryResolveCatalogDocument(name, out InfoDocumentId? documentId) &&
            TryGetDocument(documentId, out documentation);
    }

    public bool HasDocumentation(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return LegacyDocumentNames.ContainsKey(name) ||
            TryResolveCatalogDocument(name, out InfoDocumentId? documentId) &&
            HasDocument(documentId);
    }

    public bool TryGetDocument(InfoDocumentId id, out string documentation)
    {
        documentation = string.Empty;
        if (!InfoDocumentCatalog.TryGet(id, out InfoDocumentCatalogEntry? entry))
            return false;

        return TryReadDocumentation(entry.MarkdownFileName, out documentation);
    }

    public bool HasDocument(InfoDocumentId id) =>
        InfoDocumentCatalog.TryGet(id, out _);

    private bool TryReadDocumentation(string fileName, out string documentation)
    {
        documentation = File.ReadAllText(Path.Combine(_infoDocsDirectory, fileName));
        return true;
    }

    private static bool TryResolveCatalogDocument(
        string name,
        out InfoDocumentId? documentId)
    {
        documentId = name switch
        {
            "functions" => InfoDocumentId.CreateFunctionsOverview(),
            "mechanisms" => InfoDocumentId.CreateMechanismsOverview(),
            "parameters" => InfoDocumentId.CreateParametersOverview(),
            "paddings" => InfoDocumentId.CreatePaddingsOverview(),
            _ => null
        };

        if (documentId is not null)
            return true;

        if (!MechanismRegistry.TryGet(name, out MechanismRegistryEntry? mechanism))
            return false;

        documentId = InfoDocumentId.CreateMechanism(mechanism.CanonicalName);
        return true;
    }
}
