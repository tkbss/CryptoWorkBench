using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DukptTdeaWorkingKeyDocumentationTests
{
    private const string Mechanism = "DUKPT-TDEA-WORKING-KEY";

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
            Assert.That(workingKey.Value, Is.EqualTo("0x(448d3f076d8304036a55a3d7e0055a78)"));
            Assert.That(workingKey.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Tdea)));
            Assert.That(workingKey.KeySizeInBits, Is.EqualTo(new KeySize(128)));
            Assert.That(workingKey.DerivationMechanism, Is.EqualTo(Mechanism));
            Assert.That(workingKey.Usage,
                Is.EqualTo(KeyUsagePolicy.Restricted(KeyUsage.Encrypt | KeyUsage.Decrypt)));
        });
    }

    [Test]
    public void PageDocumentsEveryUsageVariantAndCompleteTypedPolicy()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string[] requiredMappings =
        {
            "| `PIN` | PIN encryption | `Restricted(PinEncrypt)` |",
            "| `MAC-REQUEST` | Message authentication, request | `Restricted(MacGenerate \\| MacVerify)` |",
            "| `MAC-RESPONSE` | Message authentication, response | `Restricted(MacGenerate \\| MacVerify)` |",
            "| `MAC-BOTH` | Same DUKPT variant as `MAC-REQUEST` | `Restricted(MacGenerate \\| MacVerify)` |",
            "| `DATA-REQUEST` | Data encryption, request | `Restricted(Encrypt \\| Decrypt)` |",
            "| `DATA-RESPONSE` | Data encryption, response | `Restricted(Encrypt \\| Decrypt)` |",
            "| `DATA-BOTH` | Same DUKPT variant as `DATA-REQUEST` | `Restricted(Encrypt \\| Decrypt)` |"
        };

        foreach (string mapping in requiredMappings)
            Assert.That(document, Does.Contain(mapping), $"Missing usage mapping: {mapping}");

        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain(
                "Request and response identify the TDEA DUKPT derivation direction"));
            Assert.That(document, Does.Contain(
                "They do not mean that a request key only generates a MAC or encrypts data while a response key only verifies a MAC or decrypts data."));
            Assert.That(document, Does.Contain(
                "CryptoScript does not currently enforce this `KeyUsagePolicy` generally across all cryptographic operations."));
        });
    }

    [Test]
    public void PageDocumentsHostCounterNrgkpAndDataOwfContract()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");

        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("10 bytes / 80 bits"));
            Assert.That(document, Does.Contain("21 least-significant bits"));
            Assert.That(document, Does.Contain("Non-Reversible Key Generation Process (NRKGP)"));
            Assert.That(document, Does.Contain("not exposed as a CryptoScript result"));
            Assert.That(document, Does.Contain("Counter `0` is invalid"));
            Assert.That(document, Does.Contain("including values with more than ten set bits"));
            Assert.That(document, Does.Contain("terminal-side counter advancement"));
            Assert.That(document, Does.Contain("Do not adjust DES parity"));
            Assert.That(document, Does.Contain("encrypting both 8-byte halves"));
            Assert.That(document, Does.Contain("not equivalent to merely XORing"));
        });
    }

    [Test]
    public void PageDocumentsFixedOutputMetadataAndUnsupportedParameters()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");

        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("| Algorithm | `Tdea` |"));
            Assert.That(document, Does.Contain("| Material | `Secret` |"));
            Assert.That(document, Does.Contain("| Key Size | `128` bits |"));
            Assert.That(document, Does.Contain($"| Derivation | `{Mechanism}` |"));
            Assert.That(document, Does.Contain(
                "does not accept `#KEYTYPE`, `#OUTLEN` or `#COUNTER`"));
            Assert.That(document, Does.Contain("does not implement terminal Future Key Registers"));
            Assert.That(document, Does.Contain("No Key Check Value (KCV) is calculated"));
        });
    }

    [Test]
    public void CentralDocumentationListsMechanismAndParameterContract()
    {
        string mechanisms = ReadInfoDocument("Info.Mechanisms.md");
        string parameters = ReadInfoDocument("Info.Parameters.md");

        Assert.Multiple(() =>
        {
            Assert.That(mechanisms, Does.Contain($"- {Mechanism} :"));
            Assert.That(parameters, Does.Contain($"        - {Mechanism}"));
            Assert.That(parameters, Does.Contain($"- {Mechanism} requires USAGE in addition to MECH."));
            Assert.That(parameters, Does.Contain(
                "Request/response denotes DUKPT derivation direction, not a Generate/Verify or Encrypt/Decrypt permission split."));
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
            .Where(line => line.StartsWith("KEY ") || line.StartsWith("PARAM ")));
    }

    private static void Execute(string script)
    {
        var context = ParserBuilder.StringBuild(script).program();
        Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        new CryptoScriptRunner().Execute(context);
    }
}
