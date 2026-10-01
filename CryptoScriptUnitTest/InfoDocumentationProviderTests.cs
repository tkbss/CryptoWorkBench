using CryptoScript.Documentation;
using CryptoScript.Model;
using FluentAssertions;
using System.Text.RegularExpressions;

namespace CryptoScriptUnitTest;

public class InfoDocumentationProviderTests
{
    private static readonly string[] RoadmapMechanisms =
    {
        "WRAP-AES", "WRAP-DES3", "RSA-PSS", "RSA-OAEP", "ECDSA"
    };

    private static readonly (string Key, InfoDocumentId Id, string FileName)[] CatalogOverviews =
    {
        ("functions", InfoDocumentId.CreateFunctionsOverview(), "Info.Functions.md"),
        ("mechanisms", InfoDocumentId.CreateMechanismsOverview(), "Info.Mechanisms.md"),
        ("parameters", InfoDocumentId.CreateParametersOverview(), "Info.Parameters.md")
    };

    private static readonly (string Key, string FileName)[] LegacyOverviews =
    {
        ("types", "Info.Types.md"),
        ("keymap", "Info.Keymap.md"),
        ("paddings", "Info.Paddings.md")
    };

    private readonly IInfoDocumentationProvider _sut = new FileInfoDocumentationProvider();

    [TestCase("mechanisms", "Info.Mechanisms.md")]
    [TestCase("functions", "Info.Functions.md")]
    [TestCase("parameters", "Info.Parameters.md")]
    [TestCase("types", "Info.Types.md")]
    [TestCase("keymap", "Info.Keymap.md")]
    [TestCase("paddings", "Info.Paddings.md")]
    [TestCase("AES-CBC", "Info.Mech.AES-CBC.md")]
    [TestCase("AES-GCM", "Info.Mech.AES-GCM.md")]
    [TestCase("DES3-ECB", "Info.Mech.DES3-ECB.md")]
    public void TryGetDocumentation_KnownName_ReturnsExistingDocument(
        string name, string expectedFileName)
    {
        string expected = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", expectedFileName));

        bool found = _sut.TryGetDocumentation(name, out string documentation);

        found.Should().BeTrue();
        documentation.Should().Be(expected);
    }

    [TestCase("unknown")]
    [TestCase("Info.Mechanisms.md")]
    [TestCase("../Info.Mechanisms.md")]
    [TestCase("..\\Info.Mechanisms.md")]
    [TestCase("AES-CBC/../../Info.Mechanisms.md")]
    [TestCase("")]
    public void TryGetDocumentation_UnknownOrPathLikeName_IsRejected(string name)
    {
        bool found = _sut.TryGetDocumentation(name, out string documentation);

        found.Should().BeFalse();
        documentation.Should().BeEmpty();
    }

    [Test]
    public void StringApiAcceptsDirectNullLiterals()
    {
#pragma warning disable CS8625
        bool available = _sut.HasDocumentation(null);
        bool found = _sut.TryGetDocumentation(null, out string documentation);
#pragma warning restore CS8625

        Assert.Multiple(() =>
        {
            available.Should().BeFalse();
            found.Should().BeFalse();
            documentation.Should().BeEmpty();
        });
    }

    [Test]
    public void TypedApiRejectsNullIdentities()
    {
        bool available = _sut.HasDocument(null!);
        bool found = _sut.TryGetDocument(null!, out string documentation);

        Assert.Multiple(() =>
        {
            available.Should().BeFalse();
            found.Should().BeFalse();
            documentation.Should().BeEmpty();
        });
    }

    [Test]
    public void TypedDefaultsRejectDocumentsForStringOnlyProviders()
    {
        IInfoDocumentationProvider provider = new StringOnlyDocumentationProvider();
        InfoDocumentId documentId = InfoDocumentId.CreateMechanismsOverview();

        bool available = provider.HasDocument(documentId);
        bool found = provider.TryGetDocument(documentId, out string documentation);

        Assert.Multiple(() =>
        {
            available.Should().BeFalse();
            found.Should().BeFalse();
            documentation.Should().BeEmpty();
        });
    }

    [Test]
    public void HasDocumentation_KnownName_DoesNotReadDocument()
    {
        string missingBaseDirectory = Path.Combine(
            Path.GetTempPath(), $"missing-info-docs-{Guid.NewGuid():N}");
        var sut = new FileInfoDocumentationProvider(missingBaseDirectory);

        InfoDocumentId documentId = InfoDocumentId.CreateMechanism("AES-CBC");

        Assert.Multiple(() =>
        {
            sut.HasDocumentation("AES-CBC").Should().BeTrue();
            sut.HasDocument(documentId).Should().BeTrue();
            sut.HasDocumentation("types").Should().BeTrue();
        });

        Action readByName = () => sut.TryGetDocumentation("AES-CBC", out _);
        Action readById = () => sut.TryGetDocument(documentId, out _);
        Action readLegacy = () => sut.TryGetDocumentation("types", out _);
        readByName.Should().Throw<DirectoryNotFoundException>();
        readById.Should().Throw<DirectoryNotFoundException>();
        readLegacy.Should().Throw<DirectoryNotFoundException>();
    }

    [Test]
    public void TryGetDocument_RegisteredFileMissing_ThrowsFileNotFoundException()
    {
        string baseDirectory = Path.Combine(
            Path.GetTempPath(), $"info-provider-test-{Guid.NewGuid():N}");
        string infoDocsDirectory = Path.Combine(baseDirectory, "InfoDocs");
        Directory.CreateDirectory(infoDocsDirectory);

        try
        {
            var sut = new FileInfoDocumentationProvider(baseDirectory);
            InfoDocumentId documentId = InfoDocumentId.CreateMechanism("AES-CBC");

            sut.HasDocument(documentId).Should().BeTrue();
            Action read = () => sut.TryGetDocument(documentId, out _);
            read.Should().Throw<FileNotFoundException>();
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Test]
    public void AllOverviewKeysAreAvailableAndReadable()
    {
        foreach ((string key, InfoDocumentId _, string _) in CatalogOverviews)
        {
            _sut.HasDocumentation(key).Should().BeTrue(key);
            _sut.TryGetDocumentation(key, out string documentation).Should().BeTrue(key);
            documentation.Should().NotBeNullOrWhiteSpace(key);
        }

        foreach ((string key, string _) in LegacyOverviews)
        {
            _sut.HasDocumentation(key).Should().BeTrue(key);
            _sut.TryGetDocumentation(key, out string documentation).Should().BeTrue(key);
            documentation.Should().NotBeNullOrWhiteSpace(key);
        }
    }

    [Test]
    public void CatalogOverviewsReturnIdenticalContentThroughBothApis()
    {
        foreach ((string key, InfoDocumentId documentId, string fileName) in CatalogOverviews)
        {
            string expected = File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "InfoDocs", fileName));

            Assert.Multiple(() =>
            {
                _sut.HasDocumentation(key).Should().BeTrue(key);
                _sut.HasDocument(documentId).Should().BeTrue(key);
                _sut.TryGetDocumentation(key, out string byName).Should().BeTrue(key);
                _sut.TryGetDocument(documentId, out string byId).Should().BeTrue(key);
                byName.Should().Be(expected);
                byId.Should().Be(byName);
            });
        }
    }

    [Test]
    public void EveryRegistryMechanismReturnsIdenticalContentThroughBothApis()
    {
        foreach (MechanismRegistryEntry mechanism in MechanismRegistry.Entries)
        {
            InfoDocumentId documentId = InfoDocumentId.CreateMechanism(mechanism.CanonicalName);

            Assert.Multiple(() =>
            {
                _sut.HasDocumentation(mechanism.CanonicalName)
                    .Should().BeTrue(mechanism.CanonicalName);
                _sut.HasDocument(documentId).Should().BeTrue(mechanism.CanonicalName);
                _sut.TryGetDocumentation(mechanism.CanonicalName, out string byName)
                    .Should().BeTrue(mechanism.CanonicalName);
                _sut.TryGetDocument(documentId, out string byId)
                    .Should().BeTrue(mechanism.CanonicalName);
                byName.Should().Be(byId, mechanism.CanonicalName);
            });
        }
    }

    [Test]
    public void ValidButUncataloguedDocumentIdentitiesAreRejected()
    {
        InfoDocumentId[] documentIds =
        {
            InfoDocumentId.CreateFunction("Encrypt"),
            InfoDocumentId.CreateMechanismFunction("Encrypt", "AES-CBC"),
            InfoDocumentId.CreateMechanismParameter("AES-CBC", "IV")
        };

        foreach (InfoDocumentId documentId in documentIds)
        {
            _sut.HasDocument(documentId).Should().BeFalse(documentId.ToString());
            _sut.TryGetDocument(documentId, out string documentation)
                .Should().BeFalse(documentId.ToString());
            documentation.Should().BeEmpty();
        }
    }

    [TestCase("Functions")]
    [TestCase("MECHANISMS")]
    [TestCase("Parameters")]
    [TestCase("Types")]
    [TestCase("KEYMAP")]
    [TestCase("Paddings")]
    [TestCase("aes-cbc")]
    [TestCase("AES-cbc")]
    public void StringKeysRequireExactCanonicalCasing(string name)
    {
        _sut.HasDocumentation(name).Should().BeFalse();
        _sut.TryGetDocumentation(name, out string documentation).Should().BeFalse();
        documentation.Should().BeEmpty();
    }

    [Test]
    public void RoadmapMechanismsAreSeparatedFromProductiveDocumentation()
    {
        string mechanisms = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Mechanisms.md"));
        string parameters = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Parameters.md"));
        string roadmap = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "MechanismRoadmap.md"));

        foreach (string mechanism in RoadmapMechanisms)
        {
            mechanisms.Should().NotContain($"- {mechanism} ");
            parameters.Split(Environment.NewLine)
                .Select(line => line.Trim())
                .Should().NotContain($"- {mechanism}");
            roadmap.Should().Contain($"## {mechanism}");
        }

        roadmap.Should().Contain("not implemented");
    }

    [Test]
    public void ProductiveMechanismDocumentsDoNotPresentRoadmapMechanismsAsSupported()
    {
        string infoDocsDirectory = Path.Combine(AppContext.BaseDirectory, "InfoDocs");

        foreach (string productiveMechanism in CryptoScript.Model.MechanismList.Instance.Mechanisms)
        {
            string fileName = $"Info.Mech.{productiveMechanism}.md";
            string[] lines = File.ReadAllLines(Path.Combine(infoDocsDirectory, fileName));

            foreach (string roadmapMechanism in RoadmapMechanisms)
            {
                string[] unsupportedReferences = lines
                    .Where(line => ReferencesExactMechanism(line, roadmapMechanism))
                    .Where(line => !line.Contains("not implemented", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                unsupportedReferences.Should().BeEmpty(
                    $"{fileName} must not present roadmap mechanism {roadmapMechanism} as supported");
            }
        }
    }

    private static bool ReferencesExactMechanism(string line, string mechanism) =>
        Regex.IsMatch(
            line,
            $@"(?<![A-Z0-9-]){Regex.Escape(mechanism)}(?![A-Z0-9-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [TestCase("RSA-PSS")]
    [TestCase("RSA-OAEP")]
    [TestCase("ECDSA")]
    [TestCase("WRAP-AES")]
    [TestCase("WRAP-DES3")]
    [TestCase("iso-9797-m1")]
    public void HasDocumentation_UndocumentedName_ReturnsFalse(string name)
    {
        _sut.HasDocumentation(name).Should().BeFalse();
    }

    private sealed class StringOnlyDocumentationProvider : IInfoDocumentationProvider
    {
        public bool HasDocumentation(string name) => name == "mechanisms";

        public bool TryGetDocumentation(string name, out string documentation)
        {
            documentation = name == "mechanisms" ? "# Mechanisms" : string.Empty;
            return documentation.Length > 0;
        }
    }
}
