using CryptoScript.Documentation;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class PaddingDocumentationTests
{
    private const string AnsiX923FileName = "Info.Padding.ANSI-X923.md";
    private const string Iso10126FileName = "Info.Padding.ISO-10126.md";
    private const string Iso7816FileName = "Info.Padding.ISO-7816.md";
    private const string Iso9797M1FileName = "Info.Padding.ISO-9797-M1.md";
    private const string Iso9797M2FileName = "Info.Padding.ISO-9797-M2.md";
    private const string Iso9797M3FileName = "Info.Padding.ISO-9797-M3.md";
    private const string NoneFileName = "Info.Padding.NONE.md";
    private const string Pkcs7FileName = "Info.Padding.PKCS-7.md";
    private const string TlsCbcFileName = "Info.Padding.TLS-CBC.md";
    private static readonly InfoDocumentId AnsiX923Id = InfoDocumentId.CreatePadding("ANSI-X923");
    private static readonly InfoDocumentId Iso10126Id = InfoDocumentId.CreatePadding("ISO-10126");
    private static readonly InfoDocumentId Iso7816Id = InfoDocumentId.CreatePadding("ISO-7816");
    private static readonly InfoDocumentId Iso9797M1Id = InfoDocumentId.CreatePadding("ISO-9797-M1");
    private static readonly InfoDocumentId Iso9797M2Id = InfoDocumentId.CreatePadding("ISO-9797-M2");
    private static readonly InfoDocumentId Iso9797M3Id = InfoDocumentId.CreatePadding("ISO-9797-M3");
    private static readonly InfoDocumentId NoneId = InfoDocumentId.CreatePadding("NONE");
    private static readonly InfoDocumentId Pkcs7Id = InfoDocumentId.CreatePadding("PKCS-7");
    private static readonly InfoDocumentId TlsCbcId = InfoDocumentId.CreatePadding("TLS-CBC");

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void AnsiX923Document_IsDeliveredAndResolvedByItsCanonicalUri()
    {
        const string uri = "cryptoscript-info://padding/ANSI-X923";
        var provider = new FileInfoDocumentationProvider();

        InfoDocumentUri.ToCanonicalString(AnsiX923Id).Should().Be(uri);
        InfoDocumentUri.TryParse(uri, out InfoDocumentId? parsedId).Should().BeTrue();
        parsedId.Should().Be(AnsiX923Id);
        InfoDocumentCatalog.TryGet(parsedId, out InfoDocumentCatalogEntry? entry)
            .Should().BeTrue();
        entry!.MarkdownFileName.Should().Be(AnsiX923FileName);

        string expected = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", AnsiX923FileName));
        provider.HasDocument(AnsiX923Id).Should().BeTrue();
        provider.TryGetDocument(AnsiX923Id, out string documentation).Should().BeTrue();
        documentation.Should().Be(expected);

        string overview = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Paddings.md"));
        overview.Should().Contain("[ANSI-X923](cryptoscript-info://padding/ANSI-X923)");
    }

    [Test]
    public void AnsiX923Document_StatesThePublicContractAndItsExampleExecutes()
    {
        string document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", AnsiX923FileName));

        document.Should().StartWith("# PADDING ANSI-X923");
        document.Should().Contain("#PAD:ANSI-X923");
        document.Should().Contain("`ANSIX923` is not a valid");
        document.Should().Contain("N = B - (L mod B)");
        document.Should().Contain("`N - 1` bytes with the value `0x00`");
        document.Should().Contain("fifteen `0x00` bytes followed by `0x10`");
        document.Should().Contain("seven `0x00` bytes followed by `0x08`");
        document.Should().Contain("historical ANSI X9.23 original");
        document.Should().Contain("does not attribute CryptoScript's concrete zero-filling");

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);
        examples.Should().ContainSingle();

        CryptoScriptProgram result = Execute(examples.Single());
        var ciphertext = result.Statements[3]
            .Should().BeOfType<StringVariableDeclaration>().Subject;
        var decrypted = result.Statements[4]
            .Should().BeOfType<StringVariableDeclaration>().Subject;

        ciphertext.Value.Should().BeEquivalentTo(
            "0x(DB9E61C7DA230568E1A5F0C0B83BDBD9)",
            options => options.IgnoringCase());
        decrypted.Value.Should().BeEquivalentTo(
            "0x(010203)", options => options.IgnoringCase());
    }

    [Test]
    public void Iso10126Document_IsDeliveredAndResolvedByItsCanonicalUri()
    {
        const string uri = "cryptoscript-info://padding/ISO-10126";
        var provider = new FileInfoDocumentationProvider();

        InfoDocumentUri.ToCanonicalString(Iso10126Id).Should().Be(uri);
        InfoDocumentUri.TryParse(uri, out InfoDocumentId? parsedId).Should().BeTrue();
        parsedId.Should().Be(Iso10126Id);
        InfoDocumentCatalog.TryGet(parsedId, out InfoDocumentCatalogEntry? entry)
            .Should().BeTrue();
        entry!.MarkdownFileName.Should().Be(Iso10126FileName);

        string expected = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Iso10126FileName));
        provider.HasDocument(Iso10126Id).Should().BeTrue();
        provider.TryGetDocument(Iso10126Id, out string documentation).Should().BeTrue();
        documentation.Should().Be(expected);

        string overview = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Paddings.md"));
        overview.Should().Contain("[ISO-10126](cryptoscript-info://padding/ISO-10126)");
    }

    [Test]
    public void Iso10126Document_StatesThePublicContractAndItsExampleExecutes()
    {
        string document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Iso10126FileName));

        document.Should().StartWith("# PADDING ISO-10126");
        document.Should().Contain("#PAD:ISO-10126");
        document.Should().Contain("`ISO10126` is not a registered");
        document.Should().Contain("N = B - (L mod B)");
        document.Should().Contain("`N - 1` cryptographically random bytes");
        document.Should().Contain("arbitrary contents in the filler octets");
        document.Should().Contain("does not require cryptographically random filler");
        document.Should().Contain("fifteen random bytes followed by `0x10`");
        document.Should().Contain("seven random bytes followed by `0x08`");
        document.Should().Contain("are not checked for particular values");
        document.Should().Contain("historical DEA/CBC context");

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);
        examples.Should().ContainSingle();

        CryptoScriptProgram result = Execute(examples.Single());
        var ciphertext = result.Statements[3]
            .Should().BeOfType<StringVariableDeclaration>().Subject;
        var decrypted = result.Statements[4]
            .Should().BeOfType<StringVariableDeclaration>().Subject;

        FormatConversions.ToByteArray(ciphertext.Value, ciphertext.ValueFormat)
            .Should().HaveCount(16);
        decrypted.Value.Should().BeEquivalentTo(
            "0x(010203)", options => options.IgnoringCase());
    }

    [Test]
    public void Iso7816Document_IsDeliveredAndResolvedByItsCanonicalUri()
    {
        const string uri = "cryptoscript-info://padding/ISO-7816";
        var provider = new FileInfoDocumentationProvider();

        InfoDocumentUri.ToCanonicalString(Iso7816Id).Should().Be(uri);
        InfoDocumentUri.TryParse(uri, out InfoDocumentId? parsedId).Should().BeTrue();
        parsedId.Should().Be(Iso7816Id);
        InfoDocumentCatalog.TryGet(parsedId, out InfoDocumentCatalogEntry? entry)
            .Should().BeTrue();
        entry!.MarkdownFileName.Should().Be(Iso7816FileName);

        string expected = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Iso7816FileName));
        provider.HasDocument(Iso7816Id).Should().BeTrue();
        provider.TryGetDocument(Iso7816Id, out string documentation).Should().BeTrue();
        documentation.Should().Be(expected);

        string overview = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Paddings.md"));
        overview.Should().Contain("[ISO-7816](cryptoscript-info://padding/ISO-7816)");
    }

    [Test]
    public void Iso7816Document_StatesThePublicContractAndItsExampleExecutes()
    {
        string document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Iso7816FileName));

        document.Should().StartWith("# PADDING ISO-7816");
        document.Should().Contain("#PAD:ISO-7816");
        document.Should().Contain("`ISO7816` is invalid");
        document.Should().Contain("N = B - (L mod B)");
        document.Should().Contain("`0x80` followed by `N - 1` bytes with the value `0x00`");
        document.Should().Contain("When `N = 1`, the padding consists only of `0x80`");
        document.Should().Contain("fifteen `0x00` bytes");
        document.Should().Contain("seven `0x00` bytes");
        document.Should().Contain("first non-zero byte reached must be `0x80`");
        document.Should().Contain("01 02 80 00");
        document.Should().Contain("byte-for-byte identical");
        document.Should().Contain("different normative contexts");
        document.Should().Contain("Secure Messaging");
        document.Should().Contain("Cryptographic checksum data element");

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);
        examples.Should().ContainSingle();

        CryptoScriptProgram result = Execute(examples.Single());
        var ciphertext = result.Statements[3]
            .Should().BeOfType<StringVariableDeclaration>().Subject;
        var decrypted = result.Statements[4]
            .Should().BeOfType<StringVariableDeclaration>().Subject;

        ciphertext.Value.Should().BeEquivalentTo(
            "0x(BDCA8D32BBE6732533C7FA16A9F0C3FC)",
            options => options.IgnoringCase());
        decrypted.Value.Should().BeEquivalentTo(
            "0x(010203)", options => options.IgnoringCase());
    }

    [Test]
    public void Iso9797M1Document_IsDeliveredAndResolvedByItsCanonicalUri()
    {
        const string uri = "cryptoscript-info://padding/ISO-9797-M1";
        var provider = new FileInfoDocumentationProvider();

        InfoDocumentUri.ToCanonicalString(Iso9797M1Id).Should().Be(uri);
        InfoDocumentUri.TryParse(uri, out InfoDocumentId? parsedId).Should().BeTrue();
        parsedId.Should().Be(Iso9797M1Id);
        InfoDocumentCatalog.TryGet(parsedId, out InfoDocumentCatalogEntry? entry)
            .Should().BeTrue();
        entry!.MarkdownFileName.Should().Be(Iso9797M1FileName);

        string expected = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Iso9797M1FileName));
        provider.HasDocument(Iso9797M1Id).Should().BeTrue();
        provider.TryGetDocument(Iso9797M1Id, out string documentation).Should().BeTrue();
        documentation.Should().Be(expected);

        string overview = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Paddings.md"));
        overview.Should().Contain("[ISO-9797-M1](cryptoscript-info://padding/ISO-9797-M1)");
    }

    [Test]
    public void Iso9797M1Document_StatesThePublicContractAndItsExampleExecutes()
    {
        string document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Iso9797M1FileName));

        document.Should().StartWith("# PADDING ISO-9797-M1");
        document.Should().Contain("#PAD:ISO-9797-M1");
        document.Should().Contain("`ISO9797M1` is parsed as a general identifier");
        document.Should().Contain("P(L,B) =");
        document.Should().Contain("B                 if L = 0");
        document.Should().Contain("0                 if L > 0 and L mod B = 0");
        document.Should().Contain("01 02 03 04 05 00 00 00");
        document.Should().Contain("no padding byte and no additional block");
        document.Should().Contain("AES produces sixteen `0x00` bytes");
        document.Should().Contain("Decrypt performs no padding removal");
        document.Should().Contain("01 02 00 00 00 00 00 00");
        document.Should().Contain("M2 begins its padding with `80`");
        document.Should().Contain("byte-for-byte identical to M2, not to M1");
        document.Should().Contain("prepends a complete block containing the original message length in bits");
        document.Should().Contain("defines Padding Method 1 in Section 6.3.2 as part of its MAC model");
        document.Should().Contain("supported encryption and MAC paths");
        document.Should().Contain("no claim is made that the amendment changed or left that rule unchanged");

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);
        examples.Should().ContainSingle();

        CryptoScriptProgram result = Execute(examples.Single());
        var ciphertext = result.Statements[3]
            .Should().BeOfType<StringVariableDeclaration>().Subject;
        var decrypted = result.Statements[4]
            .Should().BeOfType<StringVariableDeclaration>().Subject;

        ciphertext.Value.Should().BeEquivalentTo(
            "0x(ACD3C7AE20D0D53E7D22A92B13181A9F)",
            options => options.IgnoringCase());
        decrypted.Value.Should().BeEquivalentTo(
            "0x(01020300000000000000000000000000)",
            options => options.IgnoringCase());
    }

    [Test]
    public void Iso9797M2Document_IsDeliveredAndResolvedByItsCanonicalUri()
    {
        const string uri = "cryptoscript-info://padding/ISO-9797-M2";
        var provider = new FileInfoDocumentationProvider();

        InfoDocumentUri.ToCanonicalString(Iso9797M2Id).Should().Be(uri);
        InfoDocumentUri.TryParse(uri, out InfoDocumentId? parsedId).Should().BeTrue();
        parsedId.Should().Be(Iso9797M2Id);
        InfoDocumentCatalog.TryGet(parsedId, out InfoDocumentCatalogEntry? entry)
            .Should().BeTrue();
        entry!.MarkdownFileName.Should().Be(Iso9797M2FileName);

        string expected = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Iso9797M2FileName));
        provider.HasDocument(Iso9797M2Id).Should().BeTrue();
        provider.TryGetDocument(Iso9797M2Id, out string documentation).Should().BeTrue();
        documentation.Should().Be(expected);

        string overview = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Paddings.md"));
        overview.Should().Contain("[ISO-9797-M2](cryptoscript-info://padding/ISO-9797-M2)");
    }

    [Test]
    public void Iso9797M2Document_StatesThePublicContractAndItsExampleExecutes()
    {
        string document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Iso9797M2FileName));

        document.Should().StartWith("# PADDING ISO-9797-M2");
        document.Should().Contain("#PAD:ISO-9797-M2");
        document.Should().Contain("`ISO9797M2` is parsed as a general identifier");
        document.Should().Contain("N = B - (L mod B)");
        document.Should().Contain("`0x80`, followed by `N - 1` bytes with the value `0x00`");
        document.Should().Contain("With seven input bytes, `N = 1`");
        document.Should().Contain("complete additional block");
        document.Should().Contain("Empty input also becomes exactly one complete padding block");
        document.Should().Contain("first non-zero byte reached to be `0x80`");
        document.Should().Contain("01 02 80 00");
        document.Should().Contain("structural padding validation only");
        document.Should().Contain("byte-for-byte identical");
        document.Should().Contain("different normative contexts");
        document.Should().Contain("rejects `ISO-7816`");
        document.Should().Contain("default padding for DES3-RETAIL is `ISO-9797-M2`");
        document.Should().Contain("M1 appends only `00` bytes");
        document.Should().Contain("M3 additionally prepends a complete block");
        document.Should().Contain("defines Padding Method 2 in Section 6.3.3 as part of its MAC model");
        document.Should().Contain("supported encryption and MAC paths");
        document.Should().Contain("no claim is made that the amendment changed or left that rule unchanged");

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);
        examples.Should().ContainSingle();

        CryptoScriptProgram result = Execute(examples.Single());
        var ciphertext = result.Statements[3]
            .Should().BeOfType<StringVariableDeclaration>().Subject;
        var decrypted = result.Statements[4]
            .Should().BeOfType<StringVariableDeclaration>().Subject;

        ciphertext.Value.Should().BeEquivalentTo(
            "0x(BDCA8D32BBE6732533C7FA16A9F0C3FC)",
            options => options.IgnoringCase());
        decrypted.Value.Should().BeEquivalentTo(
            "0x(010203)", options => options.IgnoringCase());
    }

    [Test]
    public void Iso9797M3Document_IsDeliveredAndResolvedByItsCanonicalUri()
    {
        const string uri = "cryptoscript-info://padding/ISO-9797-M3";
        var provider = new FileInfoDocumentationProvider();

        InfoDocumentUri.ToCanonicalString(Iso9797M3Id).Should().Be(uri);
        InfoDocumentUri.TryParse(uri, out InfoDocumentId? parsedId).Should().BeTrue();
        parsedId.Should().Be(Iso9797M3Id);
        InfoDocumentCatalog.TryGet(parsedId, out InfoDocumentCatalogEntry? entry)
            .Should().BeTrue();
        entry!.MarkdownFileName.Should().Be(Iso9797M3FileName);

        string expected = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Iso9797M3FileName));
        provider.HasDocument(Iso9797M3Id).Should().BeTrue();
        provider.TryGetDocument(Iso9797M3Id, out string documentation).Should().BeTrue();
        documentation.Should().Be(expected);

        string overview = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Paddings.md"));
        overview.Should().Contain("[ISO-9797-M3](cryptoscript-info://padding/ISO-9797-M3)");
    }

    [Test]
    public void Iso9797M3Document_StatesThePublicContractAndItsExampleExecutes()
    {
        string document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", Iso9797M3FileName));

        Assert.Multiple(() =>
        {
            document.Should().StartWith("# PADDING ISO-9797-M3");
            document.Should().Contain("#PAD:ISO-9797-M3");
            document.Should().Contain("`ISO9797M3` is parsed as a general identifier");
            document.Should().Contain("Unknown parameter value : ISO9797M3");
            document.Should().Contain("length block || zero-padded data");
            document.Should().Contain("original message length in bits");
            document.Should().Contain("unsigned big-endian");
            document.Should().Contain("For DES3, `n = 64`");
            document.Should().Contain("For AES, `n = 128`");
            document.Should().Contain("rightmost 64 bits");
            document.Should().Contain("same way as Method 1");
            document.Should().Contain("Aligned, non-empty input receives no additional data block");
            document.Should().Contain("two 8-byte zero blocks");
            document.Should().Contain("two 16-byte zero blocks");
            document.Should().Contain("returns exactly the declared number of original bytes");
            document.Should().Contain("structural format and padding validation only");
            document.Should().Contain("0x(010200)   -> Encrypt -> Decrypt -> 0x(010200)");
            document.Should().Contain("0x(01020000) -> Encrypt -> Decrypt -> 0x(01020000)");
            document.Should().Contain("M1 has no length block");
            document.Should().Contain("M2 uses `80 00 ...` padding");
            document.Should().Contain("DES3-CBC-MAC");
            document.Should().Contain("DES3-RETAIL explicitly rejects M3");
            document.Should().Contain("M3 is not the default for any CryptoScript mechanism");
            document.Should().Contain("defines Padding Method 3 in Section 6.3.4 as part of its MAC model");
            document.Should().Contain("supported encryption and decryption paths");
            document.Should().Contain("no claim is made that the amendment changed or left that rule unchanged");
        });

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);
        examples.Should().ContainSingle();

        CryptoScriptProgram result = Execute(examples.Single());
        var ciphertext = result.Statements[3]
            .Should().BeOfType<StringVariableDeclaration>().Subject;
        var decrypted = result.Statements[4]
            .Should().BeOfType<StringVariableDeclaration>().Subject;

        ciphertext.Value.Should().BeEquivalentTo(
            "0x(162A3722741DD4C363DD19595518B73603BD3721388CA93069B7F22322B360F5)",
            options => options.IgnoringCase());
        decrypted.Value.Should().BeEquivalentTo(
            "0x(010203)", options => options.IgnoringCase());
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
    public void TlsCbcDocument_IsDeliveredAndResolvedByItsCanonicalUri()
    {
        const string uri = "cryptoscript-info://padding/TLS-CBC";
        var provider = new FileInfoDocumentationProvider();

        InfoDocumentUri.ToCanonicalString(TlsCbcId).Should().Be(uri);
        InfoDocumentUri.TryParse(uri, out InfoDocumentId? parsedId).Should().BeTrue();
        parsedId.Should().Be(TlsCbcId);
        InfoDocumentCatalog.TryGet(parsedId, out InfoDocumentCatalogEntry? entry)
            .Should().BeTrue();
        entry!.MarkdownFileName.Should().Be(TlsCbcFileName);

        string expected = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", TlsCbcFileName));
        provider.HasDocument(TlsCbcId).Should().BeTrue();
        provider.TryGetDocument(TlsCbcId, out string documentation).Should().BeTrue();
        documentation.Should().Be(expected);

        string overview = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Paddings.md"));
        overview.Should().Contain("[TLS-CBC](cryptoscript-info://padding/TLS-CBC)");
    }

    [Test]
    public void TlsCbcDocument_StatesThePublicContractAndItsExampleExecutes()
    {
        string document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "InfoDocs", TlsCbcFileName));

        Assert.Multiple(() =>
        {
            document.Should().StartWith("# PADDING TLS-CBC");
            document.Should().Contain("#PAD:TLS-CBC");
            document.Should().Contain("`TLSCBC` is parsed as a general identifier");
            document.Should().Contain("Unknown parameter value : TLSCBC");
            document.Should().Contain("padding || padding_length");
            document.Should().Contain("N = P + 1");
            document.Should().Contain("P = N - 1");
            document.Should().Contain("N = 1  ->  00");
            document.Should().Contain("01 02 03 04 05 02 02 02");
            document.Should().Contain("sixteen `0x0F` bytes");
            document.Should().Contain("Empty input becomes the same complete DES3 padding block");
            document.Should().Contain("Pad    -> generates minimal block-aligning padding");
            document.Should().Contain("Unpad  -> accepts minimal or extended TLS-conforming padding");
            document.Should().Contain("32 bytes of `0x1F`");
            document.Should().Contain("P = 255  -> 256 final FF bytes");
            document.Should().Contain("An ending such as `01 02 02` is invalid");
            document.Should().Contain("Invalid TLS-CBC padding bytes.");
            document.Should().Contain("TLS-CBC appends `N` bytes with value `N - 1`");
            document.Should().Contain("PKCS-7 appends `N` bytes with value `N`");
            document.Should().Contain("TLS 1.0");
            document.Should().Contain("TLS 1.1");
            document.Should().Contain("TLS 1.2");
            document.Should().Contain("TLS 1.3 does not use this classic CBC padding format");
            document.Should().Contain("does not create or process a complete TLS record");
            document.Should().Contain("does not automatically generate or manage");
            document.Should().Contain("AES-CBC encryption and decryption");
            document.Should().Contain("DES3-CBC encryption and decryption");
            document.Should().Contain("DES3-ECB merely reuses the byte format");
            document.Should().Contain("DES3-CBC-MAC through `Mac`");
            document.Should().Contain("CryptoScript CBC-MAC plus TLS-CBC byte padding");
            document.Should().Contain("DES3-RETAIL accepts only `ISO-9797-M1` and `ISO-9797-M2`");
            document.Should().Contain("not the default for any CryptoScript mechanism");
            document.Should().Contain("Padding validation alone does not establish authenticity or integrity");
            document.Should().Contain("rfc/rfc2246.html#section-6.2.3.2");
            document.Should().Contain("rfc/rfc4346.html#section-6.2.3.2");
            document.Should().Contain("rfc/rfc5246.html#section-6.2.3.2");
            document.Should().Contain("rfc/rfc8446.html#section-5.2");
        });

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);
        examples.Should().ContainSingle();

        CryptoScriptProgram result = Execute(examples.Single());
        var ciphertext = result.Statements[3]
            .Should().BeOfType<StringVariableDeclaration>().Subject;
        var decrypted = result.Statements[4]
            .Should().BeOfType<StringVariableDeclaration>().Subject;

        ciphertext.Value.Should().BeEquivalentTo(
            "0x(85D4DDE2744135C1BCBFE68B4505AD36)",
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
