using CryptoScript.Documentation;
using CryptoScript.Model;
using FluentAssertions;
using System.Reflection;

namespace CryptoScriptUnitTest;

public class InfoDocumentUriTests
{
    public static IEnumerable<TestCaseData> ValidDocuments()
    {
        yield return DocumentCase(
            InfoDocumentId.CreateMechanismsOverview(),
            "cryptoscript-info://overview/mechanisms");
        yield return DocumentCase(
            InfoDocumentId.CreateFunctionsOverview(),
            "cryptoscript-info://overview/functions");
        yield return DocumentCase(
            InfoDocumentId.CreateParametersOverview(),
            "cryptoscript-info://overview/parameters");
        yield return DocumentCase(
            InfoDocumentId.CreatePaddingsOverview(),
            "cryptoscript-info://overview/paddings");
        yield return DocumentCase(
            InfoDocumentId.CreateMechanism("AES-CBC"),
            "cryptoscript-info://mechanism/AES-CBC");
        yield return DocumentCase(
            InfoDocumentId.CreateMechanism("AES-CBC-MAC"),
            "cryptoscript-info://mechanism/AES-CBC-MAC");
        foreach (PaddingDefinition padding in PaddingRegistry.Entries)
        {
            yield return DocumentCase(
                InfoDocumentId.CreatePadding(padding.CanonicalName),
                $"cryptoscript-info://padding/{padding.CanonicalName}");
        }
        yield return DocumentCase(
            InfoDocumentId.CreateFunction("Encrypt"),
            "cryptoscript-info://function/Encrypt");
        yield return DocumentCase(
            InfoDocumentId.CreateMechanismFunction("Encrypt", "AES-CBC"),
            "cryptoscript-info://function/Encrypt/AES-CBC");
        yield return DocumentCase(
            InfoDocumentId.CreateMechanismParameter("AES-CBC", "IV"),
            "cryptoscript-info://parameter/AES-CBC/IV");
    }

    [Test]
    public void CreatesOnlyTheDataRequiredByEachDocumentKind()
    {
        var expected = new (
            InfoDocumentId Document,
            InfoDocumentKind Kind,
            string? Mechanism,
            string? Padding,
            string? Function,
            string? Parameter)[]
        {
            (InfoDocumentId.CreateMechanismsOverview(), InfoDocumentKind.MechanismsOverview, null, null, null, null),
            (InfoDocumentId.CreateFunctionsOverview(), InfoDocumentKind.FunctionsOverview, null, null, null, null),
            (InfoDocumentId.CreateParametersOverview(), InfoDocumentKind.ParametersOverview, null, null, null, null),
            (InfoDocumentId.CreatePaddingsOverview(), InfoDocumentKind.PaddingsOverview, null, null, null, null),
            (InfoDocumentId.CreateMechanism("AES-CBC"), InfoDocumentKind.Mechanism, "AES-CBC", null, null, null),
            (InfoDocumentId.CreatePadding("PKCS-7"), InfoDocumentKind.Padding, null, "PKCS-7", null, null),
            (InfoDocumentId.CreateFunction("Encrypt"), InfoDocumentKind.Function, null, null, "Encrypt", null),
            (InfoDocumentId.CreateMechanismFunction("Encrypt", "AES-CBC"), InfoDocumentKind.MechanismFunction, "AES-CBC", null, "Encrypt", null),
            (InfoDocumentId.CreateMechanismParameter("AES-CBC", "IV"), InfoDocumentKind.MechanismParameter, "AES-CBC", null, null, "IV")
        };

        foreach (var item in expected)
        {
            Assert.Multiple(() =>
            {
                item.Document.Kind.Should().Be(item.Kind);
                item.Document.Mechanism.Should().Be(item.Mechanism);
                item.Document.Padding.Should().Be(item.Padding);
                item.Document.Function.Should().Be(item.Function);
                item.Document.Parameter.Should().Be(item.Parameter);
            });
        }
    }

    [Test]
    public void CreatesCanonicalPaddingIdentityForEveryRegisteredPadding()
    {
        foreach (PaddingDefinition padding in PaddingRegistry.Entries)
        {
            InfoDocumentId document = InfoDocumentId.CreatePadding(padding.CanonicalName);

            Assert.Multiple(() =>
            {
                document.Kind.Should().Be(InfoDocumentKind.Padding);
                document.Padding.Should().Be(padding.CanonicalName);
            });
        }
    }

    [Test]
    public void IdentityHasNoPublicConstructorOrMutableProperties()
    {
        typeof(InfoDocumentId).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Should().BeEmpty();
        typeof(InfoDocumentId).GetProperties()
            .Select(property => property.SetMethod)
            .Should().OnlyContain(setter => setter == null);
    }

    [Test]
    public void PreservesExistingDocumentKindNumericValues()
    {
        Assert.Multiple(() =>
        {
            ((int)InfoDocumentKind.MechanismsOverview).Should().Be(0);
            ((int)InfoDocumentKind.FunctionsOverview).Should().Be(1);
            ((int)InfoDocumentKind.ParametersOverview).Should().Be(2);
            ((int)InfoDocumentKind.Mechanism).Should().Be(3);
            ((int)InfoDocumentKind.Function).Should().Be(4);
            ((int)InfoDocumentKind.MechanismFunction).Should().Be(5);
            ((int)InfoDocumentKind.MechanismParameter).Should().Be(6);
        });
    }

    [Test]
    public void SeparatelyCreatedDocumentIdentitiesUseValueEquality()
    {
        InfoDocumentId first = InfoDocumentId.CreateMechanismFunction("Encrypt", "AES-CBC");
        InfoDocumentId equivalent = InfoDocumentId.CreateMechanismFunction("Encrypt", "AES-CBC");
        InfoDocumentId differentFunction = InfoDocumentId.CreateMechanismFunction("Decrypt", "AES-CBC");
        InfoDocumentId differentKind = InfoDocumentId.CreateFunction("Encrypt");

        Assert.Multiple(() =>
        {
            first.Should().Be(equivalent);
            first.GetHashCode().Should().Be(equivalent.GetHashCode());
            first.Should().NotBe(differentFunction);
            first.Should().NotBe(differentKind);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("AES-cbc")]
    [TestCase("UNKNOWN")]
    public void RejectsMissingUnknownOrNonCanonicalMechanisms(string? mechanism)
    {
        Assert.Catch<ArgumentException>(() => InfoDocumentId.CreateMechanism(mechanism!));
        Assert.Catch<ArgumentException>(() =>
            InfoDocumentId.CreateMechanismFunction("Encrypt", mechanism!));
        Assert.Catch<ArgumentException>(() =>
            InfoDocumentId.CreateMechanismParameter(mechanism!, "IV"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("pkcs-7")]
    [TestCase("UNKNOWN")]
    public void RejectsMissingUnknownOrNonCanonicalPaddings(string? padding)
    {
        Assert.Catch<ArgumentException>(() => InfoDocumentId.CreatePadding(padding!));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("#IV")]
    [TestCase("iv")]
    [TestCase("UNKNOWN")]
    public void RejectsMissingUnknownOrNonCanonicalParameters(string? parameter)
    {
        Assert.Catch<ArgumentException>(() =>
            InfoDocumentId.CreateMechanismParameter("AES-CBC", parameter!));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("encrypt")]
    [TestCase("E")]
    [TestCase("Encrypt1")]
    [TestCase("Encrypt-Data")]
    public void RejectsFunctionNamesOutsideTheLexerSyntax(string? function)
    {
        Assert.Catch<ArgumentException>(() => InfoDocumentId.CreateFunction(function!));
        Assert.Catch<ArgumentException>(() =>
            InfoDocumentId.CreateMechanismFunction(function!, "AES-CBC"));
    }

    [Test]
    public void AcceptsSyntacticallyCanonicalFunctionWithoutAssumingAFunctionRegistry()
    {
        InfoDocumentId document = InfoDocumentId.CreateFunction("FutureFunction");

        document.Function.Should().Be("FutureFunction");
    }

    [TestCaseSource(nameof(ValidDocuments))]
    public void GeneratesCanonicalUriForEveryDocumentKind(
        InfoDocumentId documentId,
        string expectedUri)
    {
        InfoDocumentUri.ToCanonicalString(documentId).Should().Be(expectedUri);
    }

    [TestCaseSource(nameof(ValidDocuments))]
    public void ParsesEveryCanonicalUri(
        InfoDocumentId expectedDocumentId,
        string uri)
    {
        bool parsed = InfoDocumentUri.TryParse(uri, out InfoDocumentId? actualDocumentId);

        Assert.Multiple(() =>
        {
            parsed.Should().BeTrue();
            actualDocumentId.Should().Be(expectedDocumentId);
        });
    }

    [TestCaseSource(nameof(ValidDocuments))]
    public void RoundTripsEveryDocumentKind(
        InfoDocumentId expectedDocumentId,
        string expectedUri)
    {
        InfoDocumentUri.TryParse(
            InfoDocumentUri.ToCanonicalString(expectedDocumentId),
            out InfoDocumentId? parsedDocumentId).Should().BeTrue();

        Assert.Multiple(() =>
        {
            parsedDocumentId.Should().Be(expectedDocumentId);
            InfoDocumentUri.ToCanonicalString(parsedDocumentId!).Should().Be(expectedUri);
        });
    }

    [Test]
    public void RoundTripsLongValidFunctionName()
    {
        string function = "Aa" + new string('a', 254);
        InfoDocumentId expectedDocumentId = InfoDocumentId.CreateFunction(function);
        string uri = InfoDocumentUri.ToCanonicalString(expectedDocumentId);

        bool parsed = InfoDocumentUri.TryParse(uri, out InfoDocumentId? actualDocumentId);

        Assert.Multiple(() =>
        {
            parsed.Should().BeTrue();
            actualDocumentId.Should().Be(expectedDocumentId);
            InfoDocumentUri.ToCanonicalString(actualDocumentId!).Should().Be(uri);
        });
    }

    [Test]
    public void PreservesTheExistingAesCbcMechanismLink()
    {
        const string existingLink = "cryptoscript-info://mechanism/AES-CBC";

        InfoDocumentUri.TryParse(existingLink, out InfoDocumentId? documentId)
            .Should().BeTrue();
        documentId.Should().Be(InfoDocumentId.CreateMechanism("AES-CBC"));
        InfoDocumentUri.ToCanonicalString(documentId!).Should().Be(existingLink);
    }

    [TestCase("cryptoscript-info://mechanism/UNKNOWN")]
    [TestCase("cryptoscript-info://padding/UNKNOWN")]
    [TestCase("cryptoscript-info://parameter/AES-CBC/UNKNOWN")]
    [TestCase("cryptoscript-info://parameter/UNKNOWN/IV")]
    public void RejectsUrisWithUnknownRegistryNames(string uri)
    {
        AssertRejected(uri);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-uri")]
    [TestCase(" cryptoscript-info://mechanism/AES-CBC")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC ")]
    [TestCase("CRYPTOSCRIPT-INFO://mechanism/AES-CBC")]
    [TestCase("cryptoscript-info://MECHANISM/AES-CBC")]
    [TestCase("cryptoscript-info://mechanism/aes-cbc")]
    [TestCase("cryptoscript-info://padding/pkcs-7")]
    [TestCase("cryptoscript-info://padding/PKCS-7/extra")]
    [TestCase("cryptoscript-info://function/encrypt")]
    [TestCase("cryptoscript-info://parameter/AES-CBC/iv")]
    [TestCase("cryptoscript-info://overview/Mechanisms")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC/")]
    [TestCase("cryptoscript-info://mechanism//AES-CBC")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC/extra")]
    [TestCase("cryptoscript-info://function/Encrypt/AES-CBC/extra")]
    [TestCase("cryptoscript-info://parameter/AES-CBC")]
    [TestCase("cryptoscript-info://parameter/AES-CBC/IV/extra")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC?query=true")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC?")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC#fragment")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC#")]
    [TestCase("cryptoscript-info://user@mechanism/AES-CBC")]
    [TestCase("cryptoscript-info://mechanism:123/AES-CBC")]
    [TestCase("cryptoscript-info://mechanism/%2E%2E")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC%2Fextra")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC%5Cextra")]
    [TestCase("cryptoscript-info://mechanism/%41ES-CBC")]
    [TestCase("cryptoscript-info://mechanism/%")]
    [TestCase("cryptoscript-info://function/Éncrypt")]
    [TestCase("cryptoscript-info://function/%C3%89ncrypt")]
    [TestCase("cryptoscript-info://mechanism/%252E%252E")]
    public void RejectsMalformedManipulatedOrNonCanonicalUris(string? uri)
    {
        AssertRejected(uri);
    }

    private static TestCaseData DocumentCase(InfoDocumentId documentId, string uri) =>
        new(documentId, uri) { TestName = $"{documentId.Kind}: {uri}" };

    private static void AssertRejected(string? uri)
    {
        bool parsed = InfoDocumentUri.TryParse(uri, out InfoDocumentId? documentId);

        Assert.Multiple(() =>
        {
            parsed.Should().BeFalse();
            documentId.Should().BeNull();
        });
    }
}
