using CryptoScript.Model;
using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace CryptoScript.Documentation;

public sealed record InfoDocumentCatalogEntry
{
    internal InfoDocumentCatalogEntry(
        InfoDocumentId documentId,
        string markdownFileName,
        string displayTitle)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(markdownFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);

        DocumentId = documentId;
        MarkdownFileName = markdownFileName;
        DisplayTitle = displayTitle;
    }

    public InfoDocumentId DocumentId { get; }
    public string MarkdownFileName { get; }
    public string DisplayTitle { get; }
}

public static class InfoDocumentCatalog
{
    private static readonly ReadOnlyCollection<InfoDocumentCatalogEntry> CatalogEntries =
        CreateEntries();

    private static readonly FrozenDictionary<InfoDocumentId, InfoDocumentCatalogEntry> EntriesById =
        CatalogEntries.ToFrozenDictionary(entry => entry.DocumentId);

    public static IReadOnlyList<InfoDocumentCatalogEntry> Entries => CatalogEntries;

    public static bool TryGet(
        InfoDocumentId? documentId,
        out InfoDocumentCatalogEntry? entry)
    {
        if (documentId is null)
        {
            entry = null;
            return false;
        }

        return EntriesById.TryGetValue(documentId, out entry);
    }

    private static ReadOnlyCollection<InfoDocumentCatalogEntry> CreateEntries()
    {
        var entries = new List<InfoDocumentCatalogEntry>
        {
            new(InfoDocumentId.CreateTr31Overview(), "Info.TR31.md", "TR-31"),
            new(InfoDocumentId.CreateTr31Attributes(), "Info.TR31Attributes.md", "TR-31 attributes"),
            new(
                InfoDocumentId.CreateMechanismsOverview(),
                "Info.Mechanisms.md",
                "Mechanisms"),
            new(
                InfoDocumentId.CreateFunctionsOverview(),
                "Info.Functions.md",
                "Functions"),
            new(
                InfoDocumentId.CreateParametersOverview(),
                "Info.Parameters.md",
                "Parameters"),
            new(
                InfoDocumentId.CreatePaddingsOverview(),
                "Info.Paddings.md",
                "Paddings")
        };

        foreach (PaddingDefinition padding in PaddingRegistry.Entries)
        {
            entries.Add(new InfoDocumentCatalogEntry(
                InfoDocumentId.CreatePadding(padding.CanonicalName),
                $"Info.Padding.{padding.CanonicalName}.md",
                padding.CanonicalName));
        }

        foreach (MechanismRegistryEntry mechanism in MechanismRegistry.Entries)
        {
            InfoDocumentId documentId =
                InfoDocumentId.CreateMechanism(mechanism.CanonicalName);
            entries.Add(new InfoDocumentCatalogEntry(
                documentId,
                mechanism.DocumentationFileName,
                mechanism.CanonicalName));
        }

        entries.Add(new(InfoDocumentId.CreatePinTranslationsOverview(), "Info.PinTranslations.md", "PIN translations"));
        foreach (var rule in CryptoAlgorithm.PINBLOCK.PinBlockTranslationRules.Entries)
            entries.Add(new(InfoDocumentId.CreatePinTranslation(rule.SourceFormat, rule.TargetFormat),
                $"Info.PinTranslation.{rule.SourceFormat}-{rule.TargetFormat}.md",
                $"PIN translation {rule.SourceFormat} to {rule.TargetFormat}"));
        foreach (string function in new[] { "Share", "Secret", "Restore", "CreateHeader" })
            entries.Add(new(InfoDocumentId.CreateFunction(function), $"Info.Func.{function}.md", function));
        entries.Add(new(InfoDocumentId.CreateType("SECRET"), "Info.Type.SECRET.md", "SECRET"));
        foreach (string parameter in new[] { "PARTS", "MINPARTS", "POLYNOM", "SHAREFORMAT" })
            entries.Add(new(InfoDocumentId.CreateMechanismParameter("SECRET_SHAMIR", parameter),
                $"Info.Param.SECRET_SHAMIR.{parameter}.md", "#" + parameter));
        foreach (string parameter in new[] { "PARTS", "SHAREFORMAT" })
            entries.Add(new(InfoDocumentId.CreateMechanismParameter("SECRET_SPLIT", parameter),
                $"Info.Param.SECRET_SPLIT.{parameter}.md", "#" + parameter));
        ValidateEntries(entries);
        return Array.AsReadOnly(entries.ToArray());
    }

    internal static void ValidateEntries(IEnumerable<InfoDocumentCatalogEntry> entries)
    {
        var documentIds = new HashSet<InfoDocumentId>();
        var markdownFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (InfoDocumentCatalogEntry entry in entries)
        {
            if (!documentIds.Add(entry.DocumentId))
            {
                throw new InvalidOperationException(
                    $"Duplicate info document identity '{entry.DocumentId}'.");
            }

            if (!markdownFileNames.Add(entry.MarkdownFileName))
            {
                throw new InvalidOperationException(
                    $"Duplicate info document file name '{entry.MarkdownFileName}'.");
            }

            ValidateFileName(entry);
        }
    }

    private static void ValidateFileName(InfoDocumentCatalogEntry entry)
    {
        string fileName = entry.MarkdownFileName;
        if (Path.IsPathRooted(fileName) ||
            fileName.IndexOfAny(['/', '\\']) >= 0 ||
            !fileName.Equals(Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Info document file name '{fileName}' must not contain a path.");
        }

        string expectedFileName = GetExpectedFileName(entry.DocumentId);
        if (!fileName.Equals(expectedFileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Info document '{entry.DocumentId}' must use file name '{expectedFileName}'.");
        }
    }

    private static string GetExpectedFileName(InfoDocumentId documentId) =>
        documentId.Kind switch
        {
            InfoDocumentKind.Tr31Overview => "Info.TR31.md",
            InfoDocumentKind.Tr31Attributes => "Info.TR31Attributes.md",
            InfoDocumentKind.Type => $"Info.Type.{documentId.TypeName}.md",
            InfoDocumentKind.PinTranslationsOverview => "Info.PinTranslations.md",
            InfoDocumentKind.PinTranslation => $"Info.PinTranslation.{documentId.SourceFormat}-{documentId.TargetFormat}.md",
            InfoDocumentKind.MechanismsOverview => "Info.Mechanisms.md",
            InfoDocumentKind.FunctionsOverview => "Info.Functions.md",
            InfoDocumentKind.ParametersOverview => "Info.Parameters.md",
            InfoDocumentKind.PaddingsOverview => "Info.Paddings.md",
            InfoDocumentKind.Mechanism =>
                $"Info.Mech.{documentId.Mechanism}.md",
            InfoDocumentKind.Padding =>
                $"Info.Padding.{documentId.Padding}.md",
            InfoDocumentKind.Function =>
                $"Info.Func.{documentId.Function}.md",
            InfoDocumentKind.MechanismFunction =>
                $"Info.Func.{documentId.Function}.{documentId.Mechanism}.md",
            InfoDocumentKind.MechanismParameter =>
                $"Info.Param.{documentId.Mechanism}.{documentId.Parameter}.md",
            _ => throw new ArgumentOutOfRangeException(
                nameof(documentId), documentId.Kind, "Unknown info document kind.")
        };
}
