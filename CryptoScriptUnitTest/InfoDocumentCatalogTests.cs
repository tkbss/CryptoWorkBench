using CryptoScript.Documentation;
using CryptoScript.Model;
using FluentAssertions;
using System.Reflection;

namespace CryptoScriptUnitTest;

public class InfoDocumentCatalogTests
{
    [Test]
    public void ContainsTheFourOverviewDocumentsExactlyOnce()
    {
        var expected = new[]
        {
            (InfoDocumentId.CreateMechanismsOverview(), "Info.Mechanisms.md", "Mechanisms"),
            (InfoDocumentId.CreateFunctionsOverview(), "Info.Functions.md", "Functions"),
            (InfoDocumentId.CreateParametersOverview(), "Info.Parameters.md", "Parameters"),
            (InfoDocumentId.CreatePaddingsOverview(), "Info.Paddings.md", "Paddings")
        };

        foreach (var (documentId, fileName, displayTitle) in expected)
        {
            bool found = InfoDocumentCatalog.TryGet(documentId, out InfoDocumentCatalogEntry? entry);

            Assert.Multiple(() =>
            {
                found.Should().BeTrue();
                entry.Should().NotBeNull();
                entry!.DocumentId.Should().Be(documentId);
                entry.MarkdownFileName.Should().Be(fileName);
                entry.DisplayTitle.Should().Be(displayTitle);
                InfoDocumentCatalog.Entries.Count(candidate => candidate.DocumentId == documentId)
                    .Should().Be(1);
            });
        }
    }

    [Test]
    public void ContainsExactlyTheRegistryPaddingDocuments()
    {
        InfoDocumentCatalogEntry[] paddingDocuments = InfoDocumentCatalog.Entries
            .Where(entry => entry.DocumentId.Kind == InfoDocumentKind.Padding)
            .ToArray();

        Assert.Multiple(() =>
        {
            paddingDocuments.Should().HaveCount(PaddingRegistry.Entries.Count);
            paddingDocuments.Select(entry => entry.DocumentId.Padding)
                .Should().Equal(PaddingRegistry.Entries.Select(entry => entry.CanonicalName));
        });

        foreach (PaddingDefinition padding in PaddingRegistry.Entries)
        {
            InfoDocumentId documentId = InfoDocumentId.CreatePadding(padding.CanonicalName);
            bool found = InfoDocumentCatalog.TryGet(documentId, out InfoDocumentCatalogEntry? entry);

            Assert.Multiple(() =>
            {
                found.Should().BeTrue(padding.CanonicalName);
                entry.Should().NotBeNull();
                entry!.DocumentId.Should().Be(documentId);
                entry.DocumentId.Padding.Should().Be(padding.CanonicalName);
                entry.MarkdownFileName.Should().Be($"Info.Padding.{padding.CanonicalName}.md");
                entry.DisplayTitle.Should().Be(padding.CanonicalName);
            });
        }
    }

    [Test]
    public void ContainsExactlyTheRegistryMechanismDocuments()
    {
        InfoDocumentCatalogEntry[] mechanismDocuments = InfoDocumentCatalog.Entries
            .Where(entry => entry.DocumentId.Kind == InfoDocumentKind.Mechanism)
            .ToArray();

        Assert.Multiple(() =>
        {
            mechanismDocuments.Should().HaveCount(55);
            mechanismDocuments.Select(entry => entry.DocumentId.Mechanism)
                .Should().Equal(MechanismRegistry.Entries.Select(entry => entry.CanonicalName));
            InfoDocumentCatalog.Entries.Should().HaveCount(
                MechanismRegistry.Entries.Count + PaddingRegistry.Entries.Count + 4);
        });

        foreach (MechanismRegistryEntry mechanism in MechanismRegistry.Entries)
        {
            InfoDocumentId documentId = InfoDocumentId.CreateMechanism(mechanism.CanonicalName);
            bool found = InfoDocumentCatalog.TryGet(documentId, out InfoDocumentCatalogEntry? entry);

            Assert.Multiple(() =>
            {
                found.Should().BeTrue(mechanism.CanonicalName);
                entry.Should().NotBeNull();
                entry!.DocumentId.Should().Be(documentId);
                entry.MarkdownFileName.Should().Be(mechanism.DocumentationFileName);
                entry.MarkdownFileName.Should().Be($"Info.Mech.{mechanism.CanonicalName}.md");
                entry.DisplayTitle.Should().Be(mechanism.CanonicalName);
            });
        }
    }

    [Test]
    public void DocumentIdentitiesAndFileNamesAreUnique()
    {
        InfoDocumentCatalog.Entries.Select(entry => entry.DocumentId)
            .Should().OnlyHaveUniqueItems();
        InfoDocumentCatalog.Entries.Select(entry => entry.MarkdownFileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Should().HaveCount(InfoDocumentCatalog.Entries.Count);
    }

    [Test]
    public void RejectsFileNamesThatDifferOnlyByCase()
    {
        InfoDocumentCatalogEntry[] entries =
        {
            new(
                InfoDocumentId.CreateFunction("Encrypt"),
                "Info.Func.Encrypt.md",
                "Encrypt"),
            new(
                InfoDocumentId.CreateFunction("EnCrypt"),
                "Info.Func.EnCrypt.md",
                "EnCrypt")
        };

        Action validate = () => InfoDocumentCatalog.ValidateEntries(entries);

        validate.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate info document file name*");
    }

    [TestCase("info.Mechanisms.md")]
    [TestCase("subdirectory/Info.Mechanisms.md")]
    [TestCase("subdirectory\\Info.Mechanisms.md")]
    [TestCase("/absolute/Info.Mechanisms.md")]
    [TestCase("C:\\absolute\\Info.Mechanisms.md")]
    [TestCase("../Info.Mechanisms.md")]
    [TestCase("..\\Info.Mechanisms.md")]
    public void RejectsNonCanonicalPathLikeOrTraversalFileNames(string fileName)
    {
        var entry = new InfoDocumentCatalogEntry(
            InfoDocumentId.CreateMechanismsOverview(),
            fileName,
            "Mechanisms");

        Action validate = () => InfoDocumentCatalog.ValidateEntries(new[] { entry });

        validate.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void RejectsUnknownAndNotYetDocumentedIdentities()
    {
        InfoDocumentId[] undocumented =
        {
            InfoDocumentId.CreateFunction("Encrypt"),
            InfoDocumentId.CreateMechanismFunction("Encrypt", "AES-CBC"),
            InfoDocumentId.CreateMechanismParameter("AES-CBC", "IV"),
            InfoDocumentId.CreateFunction("FutureFunction")
        };

        InfoDocumentCatalog.TryGet(null, out InfoDocumentCatalogEntry? nullEntry)
            .Should().BeFalse();
        nullEntry.Should().BeNull();

        foreach (InfoDocumentId documentId in undocumented)
        {
            bool found = InfoDocumentCatalog.TryGet(documentId, out InfoDocumentCatalogEntry? entry);

            Assert.Multiple(() =>
            {
                found.Should().BeFalse(documentId.ToString());
                entry.Should().BeNull();
            });
        }
    }

    [Test]
    public void EntriesAndCatalogCollectionAreReadOnly()
    {
        typeof(InfoDocumentCatalogEntry).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Should().BeEmpty();
        typeof(InfoDocumentCatalogEntry).GetProperties()
            .Select(property => property.SetMethod)
            .Should().OnlyContain(setter => setter == null);

        InfoDocumentCatalog.Entries.Should().BeAssignableTo<IList<InfoDocumentCatalogEntry>>();
        var entries = (IList<InfoDocumentCatalogEntry>)InfoDocumentCatalog.Entries;
        entries.IsReadOnly.Should().BeTrue();
        Action add = () => entries.Add(entries[0]);
        add.Should().Throw<NotSupportedException>();
    }

    [Test]
    public void EveryCatalogEntryProducesAResolvableCanonicalUri()
    {
        foreach (InfoDocumentCatalogEntry entry in InfoDocumentCatalog.Entries)
        {
            string uri = InfoDocumentUri.ToCanonicalString(entry.DocumentId);

            InfoDocumentUri.TryParse(uri, out InfoDocumentId? parsedDocumentId)
                .Should().BeTrue(uri);
            parsedDocumentId.Should().Be(entry.DocumentId);
            InfoDocumentCatalog.TryGet(parsedDocumentId, out InfoDocumentCatalogEntry? parsedEntry)
                .Should().BeTrue(uri);
            parsedEntry.Should().Be(entry);
        }
    }

    [Test]
    public void EveryDeliveredCatalogFileExistsInTheSourceDirectoryWithExactCasing()
    {
        string infoDocsDirectory = FindSourceInfoDocsDirectory();
        HashSet<string> sourceFileNames = Directory
            .EnumerateFiles(infoDocsDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        InfoDocumentCatalogEntry[] paddingDocuments = PaddingRegistry.Entries
            .Select(padding =>
            {
                InfoDocumentId documentId = InfoDocumentId.CreatePadding(padding.CanonicalName);
                InfoDocumentCatalog.TryGet(documentId, out InfoDocumentCatalogEntry? entry)
                    .Should().BeTrue(padding.CanonicalName);
                entry!.DocumentId.Should().Be(documentId);
                entry.MarkdownFileName.Should().Be($"Info.Padding.{padding.CanonicalName}.md");
                return entry;
            })
            .ToArray();

        Assert.Multiple(() =>
        {
            PaddingRegistry.Entries.Should().HaveCount(9);
            paddingDocuments.Should().HaveSameCount(PaddingRegistry.Entries);
            paddingDocuments.Select(entry => entry.DocumentId.Padding)
                .Should().Equal(PaddingRegistry.Entries.Select(padding => padding.CanonicalName));
            InfoDocumentCatalog.Entries
                .Where(entry => entry.DocumentId.Kind == InfoDocumentKind.Padding)
                .Should().HaveCount(9);
        });

        foreach (InfoDocumentCatalogEntry entry in InfoDocumentCatalog.Entries)
        {
            sourceFileNames.Should().Contain(
                entry.MarkdownFileName,
                $"{entry.MarkdownFileName} must exist with exact casing");
        }
    }

    private static string FindSourceInfoDocsDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "CryptoScript", "InfoDocs");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the CryptoScript/InfoDocs source directory.");
    }
}
