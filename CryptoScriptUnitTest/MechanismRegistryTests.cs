using CryptoScript.Documentation;
using CryptoScript.Model;
using System.Text.RegularExpressions;

namespace CryptoScriptUnitTest;

public class MechanismRegistryTests
{
    // Formatting tolerance is limited to Markdown list indentation, whitespace around
    // the list marker and colon, and trailing line whitespace.
    private static readonly Regex MechanismDescriptionLine = new(
        @"^\s*-\s+(?<name>[A-Z0-9-]+)\s+:\s+(?<description>\S(?:.*\S)?)\s*$",
        RegexOptions.CultureInvariant);

    private static readonly string[] RoadmapMechanisms =
    {
        "WRAP-AES", "WRAP-DES3", "RSA-PSS", "RSA-OAEP", "ECDSA"
    };

    [Test]
    public void ContainsExactlyFortySevenProductiveMechanisms()
    {
        Assert.That(MechanismRegistry.Entries, Has.Count.EqualTo(47));
        Assert.That(
            MechanismRegistry.Entries.Select(entry => entry.CanonicalName),
            Is.EquivalentTo(MechanismList.Instance.Mechanisms));
    }

    [Test]
    public void EntriesHaveUniqueNamesAndDocumentationFiles()
    {
        string[] names = MechanismRegistry.Entries
            .Select(entry => entry.CanonicalName)
            .ToArray();
        string[] documentationFiles = MechanismRegistry.Entries
            .Select(entry => entry.DocumentationFileName)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                names.Distinct(StringComparer.Ordinal).ToArray(),
                Has.Length.EqualTo(names.Length));
            Assert.That(
                documentationFiles.Distinct(StringComparer.Ordinal).ToArray(),
                Has.Length.EqualTo(documentationFiles.Length));
        });
    }

    [Test]
    public void EntriesAreSortedByCanonicalNameAndReadOnly()
    {
        string[] names = MechanismRegistry.Entries
            .Select(entry => entry.CanonicalName)
            .ToArray();

        Assert.That(names, Is.EqualTo(names.OrderBy(name => name, StringComparer.Ordinal)));
        Assert.That(MechanismRegistry.Entries, Is.InstanceOf<IList<MechanismRegistryEntry>>());

        var list = (IList<MechanismRegistryEntry>)MechanismRegistry.Entries;
        Assert.Multiple(() =>
        {
            Assert.That(list.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => list.Add(
                new MechanismRegistryEntry("TEST", "Test.", "Info.Mech.TEST.md")));
        });
    }

    [Test]
    public void ExactSearchReturnsEveryRegisteredEntry()
    {
        foreach (MechanismRegistryEntry expected in MechanismRegistry.Entries)
        {
            bool found = MechanismRegistry.TryGet(
                expected.CanonicalName,
                out MechanismRegistryEntry? actual);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True, expected.CanonicalName);
                Assert.That(actual, Is.SameAs(expected), expected.CanonicalName);
            });
        }
    }

    [TestCase("aes-cbc")]
    [TestCase("AES-CBC ")]
    [TestCase("UNKNOWN")]
    [TestCase("")]
    [TestCase(null)]
    public void ExactSearchRejectsNonCanonicalOrUnknownNames(string? name)
    {
        Assert.That(MechanismRegistry.TryGet(name, out MechanismRegistryEntry? entry), Is.False);
        Assert.That(entry, Is.Null);
    }

    [Test]
    public void RoadmapMechanismsAreExcluded()
    {
        foreach (string mechanism in RoadmapMechanisms)
        {
            Assert.That(
                MechanismRegistry.Entries.Select(entry => entry.CanonicalName),
                Does.Not.Contain(mechanism));
            Assert.That(MechanismRegistry.TryGet(mechanism, out _), Is.False);
        }
    }

    [Test]
    public void DescriptionsMatchTheMechanismDocumentationIndex()
    {
        IReadOnlyDictionary<string, string> documentedDescriptions =
            ReadDocumentedMechanismDescriptions();

        Assert.That(documentedDescriptions, Has.Count.EqualTo(47));

        foreach (MechanismRegistryEntry entry in MechanismRegistry.Entries)
        {
            Assert.That(
                documentedDescriptions.TryGetValue(
                    entry.CanonicalName,
                    out string? documentedDescription),
                Is.True,
                $"Info.Mechanisms.md contains {entry.CanonicalName}");
            Assert.That(
                entry.Description,
                Is.EqualTo(documentedDescription),
                entry.CanonicalName);
        }
    }

    [Test]
    public void DocumentationFilesMatchTheDocumentationProviderMapping()
    {
        var documentationProvider = new FileInfoDocumentationProvider();
        string infoDocsDirectory = Path.Combine(AppContext.BaseDirectory, "InfoDocs");

        foreach (MechanismRegistryEntry entry in MechanismRegistry.Entries)
        {
            string canonicalFileName = $"Info.Mech.{entry.CanonicalName}.md";
            string registryDocumentPath = Path.Combine(
                infoDocsDirectory,
                entry.DocumentationFileName);

            bool providerFound = documentationProvider.TryGetDocumentation(
                entry.CanonicalName,
                out string providerDocumentation);

            Assert.Multiple(() =>
            {
                Assert.That(
                    entry.DocumentationFileName,
                    Is.EqualTo(canonicalFileName),
                    entry.CanonicalName);
                Assert.That(
                    File.Exists(registryDocumentPath),
                    Is.True,
                    $"{entry.CanonicalName} references {entry.DocumentationFileName}");
                Assert.That(
                    providerFound,
                    Is.True,
                    $"documentation provider maps {entry.CanonicalName}");
            });

            if (!File.Exists(registryDocumentPath) || !providerFound)
                continue;

            Assert.That(
                File.ReadAllText(registryDocumentPath),
                Is.EqualTo(providerDocumentation),
                $"{entry.DocumentationFileName} matches the provider mapping for {entry.CanonicalName}");
        }
    }

    private static IReadOnlyDictionary<string, string> ReadDocumentedMechanismDescriptions()
    {
        string mechanismsDocumentPath = Path.Combine(
            AppContext.BaseDirectory,
            "InfoDocs",
            "Info.Mechanisms.md");
        var descriptions = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string line in File.ReadLines(mechanismsDocumentPath))
        {
            Match match = MechanismDescriptionLine.Match(line);
            if (!match.Success)
                continue;

            string name = match.Groups["name"].Value;
            string description = match.Groups["description"].Value;
            if (!descriptions.TryAdd(name, description))
                throw new InvalidOperationException(
                    $"Info.Mechanisms.md contains duplicate mechanism {name}.");
        }

        return descriptions;
    }
}
