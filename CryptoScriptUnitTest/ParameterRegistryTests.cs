using CryptoScript.Model;
using FluentAssertions;

namespace CryptoScriptUnitTest;

public class ParameterRegistryTests
{
    [Test]
    public void ContainsExactlyTheParametersDefinedByTheLanguage()
    {
        string[] expectedNames = AntlrLanguageMetadata.GetParameters().ToArray();
        string[] actualNames = ParameterRegistry.Entries
            .Select(entry => entry.Name)
            .ToArray();

        actualNames.Should().BeEquivalentTo(expectedNames);
        actualNames.Should().HaveCount(expectedNames.Length);
    }

    [Test]
    public void EntriesHaveUniqueCanonicalNamesAndDescriptions()
    {
        string[] names = ParameterRegistry.Entries
            .Select(entry => entry.Name)
            .ToArray();

        Assert.Multiple(() =>
        {
            names.Distinct(StringComparer.Ordinal)
                .Should().HaveCount(names.Length);
            names.Should().OnlyContain(name =>
                name.StartsWith('#') &&
                name == name.ToUpperInvariant());
            ParameterRegistry.Entries.Should().OnlyContain(entry =>
                !string.IsNullOrWhiteSpace(entry.Description));
        });
    }

    [Test]
    public void EntriesAreSortedByCanonicalNameAndReadOnly()
    {
        string[] names = ParameterRegistry.Entries
            .Select(entry => entry.Name)
            .ToArray();

        names.Should().Equal(names.OrderBy(name => name, StringComparer.Ordinal));
        ParameterRegistry.Entries.Should().BeAssignableTo<IList<ParameterDefinition>>();

        var list = (IList<ParameterDefinition>)ParameterRegistry.Entries;
        Assert.Multiple(() =>
        {
            list.IsReadOnly.Should().BeTrue();
            Assert.Throws<NotSupportedException>(() => list.Add(
                new ParameterDefinition("#TEST", "Test parameter.")));
            typeof(ParameterDefinition).GetProperties()
                .Select(property => property.SetMethod)
                .Should().OnlyContain(setter => setter == null);
        });
    }

    [Test]
    public void ExactSearchReturnsEveryRegisteredDefinition()
    {
        foreach (ParameterDefinition expected in ParameterRegistry.Entries)
        {
            bool found = ParameterRegistry.TryGet(expected.Name, out ParameterDefinition? actual);

            Assert.Multiple(() =>
            {
                found.Should().BeTrue(expected.Name);
                actual.Should().BeSameAs(expected);
            });
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("#UNKNOWN")]
    [TestCase("#iv")]
    [TestCase("IV")]
    public void ExactSearchRejectsUnknownOrNonCanonicalNames(string? name)
    {
        bool found = ParameterRegistry.TryGet(name, out ParameterDefinition? definition);

        Assert.Multiple(() =>
        {
            found.Should().BeFalse();
            definition.Should().BeNull();
        });
    }

    [TestCase(null, "Description")]
    [TestCase("", "Description")]
    [TestCase(" ", "Description")]
    [TestCase("#TEST", null)]
    [TestCase("#TEST", "")]
    [TestCase("#TEST", " ")]
    public void DefinitionRejectsMissingNamesOrDescriptions(string? name, string? description)
    {
        Assert.Catch<ArgumentException>(() => new ParameterDefinition(name!, description!));
    }
}
