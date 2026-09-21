using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KdfDocumentationTests
{
    private static readonly string[] HkdfMechanisms =
    {
        "KDF-HKDF", "HKDF-EXTRACT", "HKDF-EXPAND"
    };

    private static readonly string[] SupportedHashes =
    {
        "HASH-SHA1", "HASH-SHA224", "HASH-SHA256", "HASH-SHA384", "HASH-SHA512",
        "HASH-SHA512-224", "HASH-SHA512-256", "HASH-SHA3-224", "HASH-SHA3-256",
        "HASH-SHA3-384", "HASH-SHA3-512"
    };

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCaseSource(nameof(HkdfMechanisms))]
    public void InfoResolvesDeployedHkdfDocumentationAndExampleExecutes(string mechanism)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", $"Info.Mech.{mechanism}.md");
        Assert.That(File.Exists(path), Is.True, $"Missing deployed documentation for {mechanism}");
        string document = File.ReadAllText(path);

        string? displayed = null;
        void Capture(string text) => displayed = text;
        OutputOperations.InfoEvent += Capture;
        try { Execute($"Info({mechanism})"); }
        finally { OutputOperations.InfoEvent -= Capture; }

        Assert.That(displayed, Is.EqualTo(document));
        Assert.That(document, Does.StartWith($"# MECHANISM {mechanism}"));
        string template = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "InfoDocs", "Info.Mech.AES-CBC.md"));
        Assert.That(Sections(document), Is.EqualTo(Sections(template)));

        string examples = ExtractExamples(document);
        Assert.That(examples, Is.Not.Empty);
        Execute(examples);
    }

    [Test]
    public void KdfHkdfExampleDocumentsAndExecutesCompleteMode()
    {
        Execute(ExtractExamples(ReadInfoDocument("Info.Mech.KDF-HKDF.md")));

        var parameter = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("hkdf");
        Assert.Multiple(() =>
        {
            Assert.That(parameter.GetParameters(), Has.Count.EqualTo(4));
            Assert.That(parameter.GetParameter("MECH"), Is.EqualTo("KDF-HKDF"));
            Assert.That(parameter.GetParameter("HASH"), Is.EqualTo("HASH-SHA256"));
            Assert.That(parameter.GetParameter("SALT"), Is.EqualTo("0x(000102030405060708090A0B0C)"));
            Assert.That(parameter.GetParameter("OUTLEN"), Is.EqualTo("256"));
        });

        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("okm");
        Assert.Multiple(() =>
        {
            Assert.That(key.Type, Is.TypeOf<CryptoTypeKey>());
            Assert.That(key.Value, Is.EqualTo(key.KeyValue));
            Assert.That(key.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(FormatConversions.HexStringToByteArray(key.Value), Has.Length.EqualTo(32));
            Assert.That(key.KeySize, Is.EqualTo("256"));
            Assert.That(key.Mechanism, Is.Empty);
            Assert.That(key.DerivationMechanism, Is.EqualTo("KDF-HKDF"));
        });
    }

    [Test]
    public void ExtractExampleUsesEmptyInfoAndReturnsHashLengthPrk()
    {
        string document = ReadInfoDocument("Info.Mech.HKDF-EXTRACT.md");
        Assert.That(document, Does.Contain("Derive(extract, ikm, \"\")"));
        Execute(ExtractExamples(document));

        var prk = (KeyVariableDeclaration)VariableDictionary.Instance().Get("prk");
        Assert.Multiple(() =>
        {
            Assert.That(FormatConversions.HexStringToByteArray(prk.Value), Has.Length.EqualTo(32));
            Assert.That(prk.KeySize, Is.EqualTo("256"));
            Assert.That(prk.DerivationMechanism, Is.EqualTo("HKDF-EXTRACT"));
        });
    }

    [Test]
    public void ExpandExampleUsesHashLengthPrkAndReturnsRequestedOkm()
    {
        string document = ReadInfoDocument("Info.Mech.HKDF-EXPAND.md");
        Assert.That(document, Does.Contain("must be a PRK, not arbitrary input keying material"));
        Execute(ExtractExamples(document));

        var prk = (KeyVariableDeclaration)VariableDictionary.Instance().Get("prk");
        var okm = (KeyVariableDeclaration)VariableDictionary.Instance().Get("okm");
        Assert.Multiple(() =>
        {
            Assert.That(FormatConversions.HexStringToByteArray(prk.Value), Has.Length.EqualTo(32));
            Assert.That(FormatConversions.HexStringToByteArray(okm.Value), Has.Length.EqualTo(42));
            Assert.That(okm.KeySize, Is.EqualTo("336"));
            Assert.That(okm.DerivationMechanism, Is.EqualTo("HKDF-EXPAND"));
        });
    }

    [TestCaseSource(nameof(HkdfMechanisms))]
    public void EveryHkdfPageDocumentsSupportedHashesAndOutputUnits(string mechanism)
    {
        string document = ReadInfoDocument($"Info.Mech.{mechanism}.md");
        foreach (string hash in SupportedHashes)
            Assert.That(document, Does.Contain(hash), $"Missing {hash} on {mechanism} page");
        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("HMAC-*"));
            Assert.That(document, Does.Contain("HashLen"));
        });

        if (mechanism != "HKDF-EXTRACT")
        {
            Assert.Multiple(() =>
            {
                Assert.That(document, Does.Contain("#OUTLEN"));
                Assert.That(document, Does.Contain("bits"));
                Assert.That(document, Does.Contain("255 * HashLen bytes"));
            });
        }
    }

    [Test]
    public void CentralDocumentationContainsKdfContract()
    {
        string mechanisms = ReadInfoDocument("Info.Mechanisms.md");
        string functions = ReadInfoDocument("Info.Functions.md");
        string parameters = ReadInfoDocument("Info.Parameters.md");

        foreach (string mechanism in HkdfMechanisms)
            Assert.That(mechanisms, Does.Contain($"- {mechanism} :"));
        Assert.That(functions, Does.Contain("Derive(parameters, key, data)"));
        Assert.Multiple(() =>
        {
            Assert.That(parameters, Does.Contain("- KDF-HKDF"));
            Assert.That(parameters, Does.Contain("| HKDF-EXTRACT | Required | Optional | Not supported |"));
            Assert.That(parameters, Does.Contain("| HKDF-EXPAND | Required | Not supported | Required |"));
            Assert.That(parameters, Does.Contain("- HASH:"));
            Assert.That(parameters, Does.Contain("- SALT:"));
            Assert.That(parameters, Does.Contain("- OUTLEN:"));
            Assert.That(parameters, Does.Contain("specified in bits"));
        });
    }

    private static string ReadInfoDocument(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InfoDocs", name));

    private static string[] Sections(string document) => document.Split('\n')
        .Where(line => line.StartsWith("## ")).Select(line => line.Trim()).ToArray();

    private static string ExtractExamples(string document) =>
        string.Join(Environment.NewLine, document.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("KEY ") || line.StartsWith("PARAM ") || line.StartsWith("VAR ")));

    private static void Execute(string script)
    {
        var context = ParserBuilder.StringBuild(script).program();
        Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        new CryptoScriptRunner().Execute(context);
    }
}
