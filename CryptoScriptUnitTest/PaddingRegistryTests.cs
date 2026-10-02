using CryptoScript.Model;
using FluentAssertions;

namespace CryptoScriptUnitTest;

public class PaddingRegistryTests
{
    private static readonly string[] CanonicalNames =
    {
        "ANSI-X923",
        "ISO-10126",
        "ISO-7816",
        "ISO-9797-M1",
        "ISO-9797-M2",
        "ISO-9797-M3",
        "NONE",
        "PKCS-7",
        "TLS-CBC"
    };

    [Test]
    public void ContainsExactlyThePaddingsDefinedByTheLanguage()
    {
        string[] languageNames = AntlrLanguageMetadata.GetPaddings().ToArray();
        string[] registryNames = PaddingRegistry.Entries
            .Select(entry => entry.CanonicalName)
            .ToArray();

        Assert.Multiple(() =>
        {
            registryNames.Should().BeEquivalentTo(languageNames);
            registryNames.Should().HaveCount(9);
        });
    }

    [Test]
    public void EntriesHaveTheStableCanonicalOrder()
    {
        PaddingRegistry.Entries
            .Select(entry => entry.CanonicalName)
            .Should().Equal(CanonicalNames);
    }

    [Test]
    public void EntriesHaveUniqueNamesAndNonemptyDescriptions()
    {
        string[] names = PaddingRegistry.Entries
            .Select(entry => entry.CanonicalName)
            .ToArray();

        Assert.Multiple(() =>
        {
            names.Distinct(StringComparer.Ordinal)
                .Should().HaveCount(names.Length);
            PaddingRegistry.Entries.Should().OnlyContain(entry =>
                !string.IsNullOrWhiteSpace(entry.Description));
        });
    }

    [Test]
    public void ExactSearchReturnsEveryRegisteredDefinition()
    {
        foreach (PaddingDefinition expected in PaddingRegistry.Entries)
        {
            bool found = PaddingRegistry.TryGet(
                expected.CanonicalName,
                out PaddingDefinition? actual);

            Assert.Multiple(() =>
            {
                found.Should().BeTrue(expected.CanonicalName);
                actual.Should().BeSameAs(expected);
            });
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("UNKNOWN")]
    [TestCase("pkcs-7")]
    [TestCase("PKCS-7 ")]
    public void ExactSearchRejectsUnknownOrNonCanonicalNames(string? name)
    {
        bool found = PaddingRegistry.TryGet(name, out PaddingDefinition? definition);

        Assert.Multiple(() =>
        {
            found.Should().BeFalse();
            definition.Should().BeNull();
        });
    }

    [Test]
    public void EntriesAndDefinitionsAreReadOnly()
    {
        PaddingRegistry.Entries.Should().BeAssignableTo<IList<PaddingDefinition>>();
        var entries = (IList<PaddingDefinition>)PaddingRegistry.Entries;

        Assert.Multiple(() =>
        {
            entries.IsReadOnly.Should().BeTrue();
            Assert.Throws<NotSupportedException>(() => entries.Add(
                new PaddingDefinition("TEST", "Test padding.")));
            typeof(PaddingDefinition).GetProperties()
                .Select(property => property.SetMethod)
                .Should().OnlyContain(setter => setter == null);
        });
    }

    [TestCase(null, "Description")]
    [TestCase("", "Description")]
    [TestCase(" ", "Description")]
    [TestCase("TEST", null)]
    [TestCase("TEST", "")]
    [TestCase("TEST", " ")]
    public void DefinitionRejectsMissingNamesOrDescriptions(
        string? canonicalName,
        string? description)
    {
        Assert.Catch<ArgumentException>(() =>
            new PaddingDefinition(canonicalName!, description!));
    }
}
