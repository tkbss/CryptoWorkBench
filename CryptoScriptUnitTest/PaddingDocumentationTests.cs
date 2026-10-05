using CryptoScript.Documentation;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class PaddingDocumentationTests
{
    private const string NoneFileName = "Info.Padding.NONE.md";
    private const string Pkcs7FileName = "Info.Padding.PKCS-7.md";
    private static readonly InfoDocumentId NoneId = InfoDocumentId.CreatePadding("NONE");
    private static readonly InfoDocumentId Pkcs7Id = InfoDocumentId.CreatePadding("PKCS-7");

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void NoneDocument_IsDeliveredAndResolvedByItsCanonicalUri()
    {
        const string uri = "cryptoscript-info://padding/NONE";
        var provider = new FileInfoDocumentationProvider();

        InfoDocumentUri.ToCanonicalString(NoneId).Should().Be(uri);
        InfoDocumentUri.TryParse(uri, out InfoDocumentId? parsedId).Should().BeTrue();
        parsedId.Should().Be(NoneId);
        InfoDocumentCatalog.TryGet(parsedId, out InfoDocumentCatalogEntry? entry)
            .Should().BeTrue();
        entry!.MarkdownFileName.Should().Be(NoneFileName);

        string expected = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", NoneFileName));
        provider.HasDocument(NoneId).Should().BeTrue();
        provider.TryGetDocument(NoneId, out string documentation).Should().BeTrue();
        documentation.Should().Be(expected);

        string overview = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Paddings.md"));
        overview.Should().Contain("[NONE](cryptoscript-info://padding/NONE)");
    }

    [Test]
    public void NoneDocument_StatesThePublicContractAndItsExampleExecutes()
    {
        string document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", NoneFileName));

        document.Should().StartWith("# PADDING NONE");
        document.Should().Contain("#PAD:NONE");
        document.Should().NotContain("#PAD=NONE");
        document.Should().Contain("does not add or remove padding bytes");
        document.Should().Contain("does not remove any bytes as padding");
        document.Should().Contain("A non-zero multiple of 16 bytes");
        document.Should().Contain("A non-zero multiple of 8 bytes");
        document.Should().Contain("DES3-RETAIL");
        document.Should().Contain("does not define a padding scheme named `NONE`");

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);
        examples.Should().ContainSingle();

        CryptoScriptProgram result = Execute(examples.Single());
        var ciphertext = result.Statements[3]
            .Should().BeOfType<StringVariableDeclaration>().Subject;
        var decrypted = result.Statements[4]
            .Should().BeOfType<StringVariableDeclaration>().Subject;

        ciphertext.Value.Should().BeEquivalentTo(
            "0x(7649ABAC8119B246CEE98E9B12E9197D)",
            options => options.IgnoringCase());
        decrypted.Value.Should().BeEquivalentTo(
            "0x(6BC1BEE22E409F96E93D7E117393172A)",
            options => options.IgnoringCase());
    }

    [Test]
    public void Pkcs7Document_IsDeliveredAndResolvedByItsCanonicalUri()
    {
        const string uri = "cryptoscript-info://padding/PKCS-7";
        var provider = new FileInfoDocumentationProvider();

        InfoDocumentUri.ToCanonicalString(Pkcs7Id).Should().Be(uri);
        InfoDocumentUri.TryParse(uri, out InfoDocumentId? parsedId).Should().BeTrue();
        parsedId.Should().Be(Pkcs7Id);
        InfoDocumentCatalog.TryGet(parsedId, out InfoDocumentCatalogEntry? entry)
            .Should().BeTrue();
        entry!.MarkdownFileName.Should().Be(Pkcs7FileName);

        string expected = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Pkcs7FileName));
        provider.HasDocument(Pkcs7Id).Should().BeTrue();
        provider.TryGetDocument(Pkcs7Id, out string documentation).Should().BeTrue();
        documentation.Should().Be(expected);

        string overview = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Paddings.md"));
        overview.Should().Contain("[PKCS-7](cryptoscript-info://padding/PKCS-7)");
    }

    [Test]
    public void Pkcs7Document_StatesThePublicContractAndItsExampleExecutes()
    {
        string document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Pkcs7FileName));

        document.Should().StartWith("# PADDING PKCS-7");
        document.Should().Contain("#PAD:PKCS-7");
        document.Should().Contain("N = B - (L mod B)");
        document.Should().Contain("sixteen `0x10` bytes");
        document.Should().Contain("eight `0x08` bytes");
        document.Should().Contain("empty input becomes exactly one full padding block");

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);
        examples.Should().ContainSingle();

        CryptoScriptProgram result = Execute(examples.Single());
        var ciphertext = result.Statements[3]
            .Should().BeOfType<StringVariableDeclaration>().Subject;
        var decrypted = result.Statements[4]
            .Should().BeOfType<StringVariableDeclaration>().Subject;

        ciphertext.Value.Should().BeEquivalentTo(
            "0x(66B68414C193C1C7A3EA6B5A54A786B1)",
            options => options.IgnoringCase());
        decrypted.Value.Should().BeEquivalentTo(
            "0x(010203)", options => options.IgnoringCase());
    }

    [Test]
    public void ParametersDocument_ListsExactlyTheRegisteredPaddings()
    {
        string document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Parameters.md"));
        string[] lines = document.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        int headingIndex = Array.FindIndex(
            lines, line => line.Trim().Equals("- PAD VALUES:", StringComparison.Ordinal));

        headingIndex.Should().BeGreaterThanOrEqualTo(0);
        string[] documentedPaddings = lines
            .Skip(headingIndex + 1)
            .TakeWhile(line => line.StartsWith("        - ", StringComparison.Ordinal))
            .Select(line => line[10..].Trim())
            .ToArray();

        documentedPaddings.Should().Equal(
            PaddingRegistry.Entries.Select(padding => padding.CanonicalName));
    }

    private static CryptoScriptProgram Execute(string script)
    {
        var context = ParserBuilder.StringBuild(script).program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
