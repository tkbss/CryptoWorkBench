using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DukptAesWorkingKeyDocumentationTests
{
    private const string Mechanism = "DUKPT-AES-WORKING-KEY";

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void InfoResolvesDeployedPageAndDocumentedExampleExecutes()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string? displayed = null;
        void Capture(string text) => displayed = text;
        OutputOperations.InfoEvent += Capture;
        try { Execute($"Info({Mechanism})"); }
        finally { OutputOperations.InfoEvent -= Capture; }

        Assert.That(displayed, Is.EqualTo(document));
        Assert.That(document, Does.StartWith($"# MECHANISM {Mechanism}"));
        Execute(ExtractExample(document));

        var workingKey = (KeyVariableDeclaration)VariableDictionary.Instance().Get("wk");
        Assert.Multiple(() =>
        {
            Assert.That(workingKey.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Aes)));
            Assert.That(workingKey.KeySizeInBits, Is.EqualTo(new KeySize(128)));
            Assert.That(workingKey.DerivationMechanism, Is.EqualTo(Mechanism));
            Assert.That(workingKey.Usage, Is.EqualTo(KeyUsagePolicy.Restricted(KeyUsage.Encrypt)));
        });
    }

    [Test]
    public void PageDocumentsEveryUsageAndItsTypedPolicy()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string[] requiredMappings =
        {
            "| `PIN` | PIN encryption key | `Restricted(PinEncrypt)` |",
            "| `MAC-GENERATE` | Generate message authentication codes | `Restricted(MacGenerate)` |",
            "| `MAC-VERIFY` | Verify message authentication codes | `Restricted(MacVerify)` |",
            "| `MAC-BOTH` | Generate and verify message authentication codes | `Restricted(MacGenerate \\| MacVerify)` |",
            "| `DATA-ENCRYPT` | Encrypt general data | `Restricted(Encrypt)` |",
            "| `DATA-DECRYPT` | Decrypt general data | `Restricted(Decrypt)` |",
            "| `DATA-BOTH` | Encrypt and decrypt general data | `Restricted(Encrypt \\| Decrypt)` |"
        };

        foreach (string mapping in requiredMappings)
            Assert.That(document, Does.Contain(mapping), $"Missing usage mapping: {mapping}");

        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("does not grant the generic `Encrypt` usage"));
            Assert.That(document, Does.Contain(
                "This page does not imply that every CryptoScript cryptographic operation currently enforces that metadata."));
        });
    }

    [Test]
    public void PageDocumentsEveryKeyTypeAndCompleteStrengthMatrix()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string[] keyTypes =
        {
            "TDEA-2", "TDEA-3", "AES-128", "AES-192", "AES-256",
            "HMAC-128", "HMAC-192", "HMAC-256"
        };
        foreach (string value in keyTypes)
            Assert.That(document, Does.Contain(value), $"Missing key-type documentation: {value}");

        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain(
                "| AES-128 | TDEA-2, TDEA-3, AES-128, HMAC-128 |"));
            Assert.That(document, Does.Contain(
                "| AES-192 | TDEA-2, TDEA-3, AES-128, AES-192, HMAC-128, HMAC-192 |"));
            Assert.That(document, Does.Contain(
                "| AES-256 | TDEA-2, TDEA-3, AES-128, AES-192, AES-256, HMAC-128, HMAC-192, HMAC-256 |"));
            Assert.That(document, Does.Contain("must not be stronger than its AES Initial Key"));
            Assert.That(document, Does.Contain("length of the generated HMAC keying material"));
            Assert.That(document, Does.Contain("do not select").And.Contain("hash algorithm"));
        });
    }

    [Test]
    public void PageDocumentsKsnHostAlgorithmAndResultMetadata()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string[] required =
        {
            "12 bytes / 96 bits", "64 bits / 8 bytes", "32 bits / 4 bytes",
            "most significant bit", "Intermediate Derivation Keys", "AES DUKPT KDF",
            "KeyType", "KeySizeInBits", "DerivationMechanism", "KeyUsagePolicy"
        };

        foreach (string value in required)
            Assert.That(document, Does.Contain(value), $"Missing derivation documentation: {value}");

        Assert.That(document, Does.Contain(
            "Intermediate Derivation Keys are internal values and are not exposed as a separate CryptoScript mechanism"));
    }

    [Test]
    public void CentralDocumentationListsMechanismAndParameterContract()
    {
        string mechanisms = ReadInfoDocument("Info.Mechanisms.md");
        string parameters = ReadInfoDocument("Info.Parameters.md");

        Assert.Multiple(() =>
        {
            Assert.That(mechanisms, Does.Contain($"- {Mechanism} :"));
            Assert.That(parameters, Does.Contain($"- {Mechanism}"));
            Assert.That(parameters, Does.Contain($"- {Mechanism} requires USAGE and KEYTYPE"));
            Assert.That(parameters, Does.Contain("PIN maps only to PinEncrypt, not to generic Encrypt"));
        });
    }

    private static string ReadInfoDocument(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", name);
        Assert.That(File.Exists(path), Is.True, $"Missing deployed documentation: {name}");
        return File.ReadAllText(path);
    }

    private static string ExtractExample(string document)
    {
        string section = document.Split("## Example Usage", 2)[1].Split("## Invalid Inputs", 2)[0];
        return string.Join(Environment.NewLine, section.Split('\n')
            .Select(line => line.TrimEnd('\r').TrimStart())
            .Where(line => line.StartsWith("KEY ") || line.StartsWith("PARAM ") ||
                           line.StartsWith("DUKPT-") || line.StartsWith("#USAGE:") ||
                           line.StartsWith("#KEYTYPE:") || line.StartsWith("workingParameters,") ||
                           line.StartsWith("initialKey,") || line.StartsWith("0x(")));
    }

    private static void Execute(string script)
    {
        var context = ParserBuilder.StringBuild(script).program();
        Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        new CryptoScriptRunner().Execute(context);
    }
}
