using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KdfDocumentationTests
{
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

    [Test]
    public void InfoResolvesDeployedKdfHkdfDocumentationAndExampleExecutes()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Mech.KDF-HKDF.md");
        Assert.That(File.Exists(path), Is.True, "Missing deployed documentation for KDF-HKDF");
        string document = File.ReadAllText(path);

        string? displayed = null;
        void Capture(string text) => displayed = text;
        OutputOperations.InfoEvent += Capture;
        try { Execute("Info(KDF-HKDF)"); }
        finally { OutputOperations.InfoEvent -= Capture; }

        Assert.That(displayed, Is.EqualTo(document));
        Assert.That(document, Does.StartWith("# MECHANISM KDF-HKDF"));
        string template = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "InfoDocs", "Info.Mech.AES-CBC.md"));
        Assert.That(Sections(document), Is.EqualTo(Sections(template)));

        string examples = string.Join(Environment.NewLine, document.Split('\n')
            .Where(line => line.StartsWith("KEY ") || line.StartsWith("PARAM ") || line.StartsWith("VAR ")));
        Assert.That(examples, Is.Not.Empty);
        Execute(examples);

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
    public void CentralDocumentationContainsKdfContract()
    {
        string mechanisms = ReadInfoDocument("Info.Mechanisms.md");
        string functions = ReadInfoDocument("Info.Functions.md");
        string parameters = ReadInfoDocument("Info.Parameters.md");

        Assert.That(mechanisms, Does.Contain("- KDF-HKDF :"));
        Assert.That(functions, Does.Contain("Derive(parameters, key, data)"));
        foreach (string hash in SupportedHashes)
            Assert.That(ReadInfoDocument("Info.Mech.KDF-HKDF.md"), Does.Contain(hash));
        Assert.Multiple(() =>
        {
            Assert.That(parameters, Does.Contain("- KDF-HKDF"));
            Assert.That(parameters, Does.Contain("- HASH:"));
            Assert.That(parameters, Does.Contain("- SALT:"));
            Assert.That(parameters, Does.Contain("- OUTLEN:"));
        });
    }

    private static string ReadInfoDocument(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InfoDocs", name));

    private static string[] Sections(string document) => document.Split('\n')
        .Where(line => line.StartsWith("## ")).Select(line => line.Trim()).ToArray();

    private static void Execute(string script)
    {
        var context = ParserBuilder.StringBuild(script).program();
        Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        new CryptoScriptRunner().Execute(context);
    }
}
