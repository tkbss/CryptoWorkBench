using CryptoScript.Documentation;
using FluentAssertions;

namespace CryptoScriptUnitTest;

public class InfoDocumentationProviderTests
{
    private static readonly string[] ExpectedDocumentNames =
    {
        "functions", "mechanisms", "types", "parameters", "keymap", "paddings",
        "AES-CBC", "AES-ECB", "AES-CTR", "AES-CMAC", "AES-GMAC", "AES-GCM", "AES-CCM",
        "WRAP-AES-TR31", "WRAP-DES3-TR31",
        "DES3-CBC", "DES3-ECB", "DES3-CMAC", "DES3-RETAIL",
        "HMAC-SHA1", "HMAC-SHA224", "HMAC-SHA256", "HMAC-SHA384", "HMAC-SHA512",
        "HMAC-SHA512-224", "HMAC-SHA512-256", "HMAC-SHA3-224", "HMAC-SHA3-256",
        "HMAC-SHA3-384", "HMAC-SHA3-512",
        "HASH-SHA1", "HASH-SHA224", "HASH-SHA256", "HASH-SHA384", "HASH-SHA512",
        "HASH-SHA512-224", "HASH-SHA512-256", "HASH-SHA3-224", "HASH-SHA3-256",
        "HASH-SHA3-384", "HASH-SHA3-512",
        "KDF-HKDF", "HKDF-EXTRACT", "HKDF-EXPAND", "KDF-SP800-108-COUNTER",
        "DUKPT-AES-INITIAL-KEY", "DUKPT-AES-WORKING-KEY",
        "DUKPT-TDEA-INITIAL-KEY", "DUKPT-TDEA-WORKING-KEY",
        "KDF-EP2-SESSION", "KDF-EP2-PAN-RECEIPT-TRX", "KDF-EP2-PAN-RECEIPT-TRM",
        "KDF-EP2-PAN-SURROGATE-TRX"
    };

    private readonly FileInfoDocumentationProvider _sut = new();

    [TestCase("mechanisms", "Info.Mechanisms.md")]
    [TestCase("functions", "Info.Functions.md")]
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
    public void HasDocumentation_KnownName_DoesNotReadDocument()
    {
        string missingBaseDirectory = Path.Combine(
            Path.GetTempPath(), $"missing-info-docs-{Guid.NewGuid():N}");
        var sut = new FileInfoDocumentationProvider(missingBaseDirectory);

        bool available = sut.HasDocumentation("AES-CBC");

        available.Should().BeTrue();
        Action read = () => sut.TryGetDocumentation("AES-CBC", out _);
        read.Should().Throw<DirectoryNotFoundException>();
    }

    [Test]
    public void DocumentationWhitelist_AllExpectedDocumentsAreAvailableAndReadable()
    {
        foreach (string name in ExpectedDocumentNames)
        {
            _sut.HasDocumentation(name).Should().BeTrue(name);
            _sut.TryGetDocumentation(name, out string documentation).Should().BeTrue(name);
            documentation.Should().NotBeNullOrWhiteSpace(name);
        }
    }

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
}
