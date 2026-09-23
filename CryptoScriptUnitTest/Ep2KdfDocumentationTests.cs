using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Ep2KdfDocumentationTests
{
    private static readonly object[] MechanismExamples =
    {
        new object[]
        {
            "KDF-EP2-SESSION", "sessionVariant",
            "CDA5C89A6B4F073779AB3B882C5CDFEFE756621E2FD4C4AC46C9FFCB9915C5CC"
        },
        new object[]
        {
            "KDF-EP2-PAN-RECEIPT-TRX", "receiptTrx",
            "D37FF3ECC3F2EAC21EF3C968A9DE2934"
        },
        new object[]
        {
            "KDF-EP2-PAN-RECEIPT-TRM", "receiptTrm",
            "F9B286738F4A3848C20C7CB12D3886E7"
        },
        new object[]
        {
            "KDF-EP2-PAN-SURROGATE-TRX", "surrogateTrx",
            "AEA780CDFC3CDA67C0FD0D70D509C9B4C1DD1F40F0C05D922BFD3BC8A01E2E6E"
        }
    };

    private static readonly string[] RequiredSections =
    {
        "## Key Features", "## Data Flow", "## Functions", "## Input Requirements",
        "## Parameters", "## Example Usage", "## ep2 Reference"
    };

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCaseSource(nameof(MechanismExamples))]
    public void InfoResolvesDeployedPageAndDocumentedExampleMatchesReference(
        string mechanism, string resultId, string expected)
    {
        string document = ReadMechanismDocument(mechanism);
        Assert.That(document, Does.StartWith($"# MECHANISM {mechanism}"));
        foreach (string section in RequiredSections)
            Assert.That(document, Does.Contain(section), $"Missing {section} on {mechanism} page");

        string? displayed = null;
        void Capture(string text) => displayed = text;
        OutputOperations.InfoEvent += Capture;
        try { Execute($"Info({mechanism})"); }
        finally { OutputOperations.InfoEvent -= Capture; }
        Assert.That(displayed, Is.EqualTo(document));

        Execute(ExtractExamples(document));
        var result = (KeyVariableDeclaration)VariableDictionary.Instance().Get(resultId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo($"0x({expected})").IgnoreCase);
            Assert.That(result.Value, Is.EqualTo(result.KeyValue));
            Assert.That(result.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(result.DerivationMechanism, Is.EqualTo(mechanism));
        });
    }

    [Test]
    public void SessionPageDocumentsAllVariantMappingsAndFixedContract()
    {
        string document = ReadMechanismDocument("KDF-EP2-SESSION");
        string[] rows =
        {
            "| TC | 7184718471847184 | 32 Byte |",
            "| MAC-SEND | EB5AEB5AEB5AEB5A | 32 Byte |",
            "| MAC-RECEIVE | 9309930993099309 | 32 Byte |",
            "| ENCRYPTION | 0C7E0C7E0C7E0C7E | 16 Byte |",
            "| PIN | C5B1C5B1C5B1C5B1 | 16 Byte |",
            "| KEY-ENCRYPTION | 3FE73FE73FE73FE7 | 16 Byte |"
        };

        foreach (string row in rows)
            Assert.That(document, Does.Contain(row));
        Execute(ExtractExamples(document));
        var encryption = (KeyVariableDeclaration)VariableDictionary.Instance().Get("encryptionVariant");
        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("HKDF-Extract-SHA256"));
            Assert.That(document, Does.Contain("HKDF-Expand-SHA256"));
            Assert.That(document, Does.Contain("#SALT"));
            Assert.That(document, Does.Contain("#VARIANT"));
            Assert.That(document, Does.Contain("used directly and is not hashed"));
            Assert.That(document, Does.Contain("third Derive argument must be present and empty"));
            Assert.That(encryption.Value,
                Is.EqualTo("0x(29DDFF143885D21CD2425D1FDDA2C229)").IgnoreCase);
        });
    }

    [Test]
    public void ReceiptTransactionPageDocumentsHashedDolAndLeftTruncation()
    {
        string document = ReadMechanismDocument("KDF-EP2-PAN-RECEIPT-TRX");
        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("No Extract"));
            Assert.That(document, Does.Contain("SHA-256(DOL)"));
            Assert.That(document, Does.Contain("Expand produces 32 bytes"));
            Assert.That(document, Does.Contain("leftmost 16 bytes"));
            Assert.That(document, Does.Contain("rightmost 16 bytes are discarded"));
            Assert.That(document, Does.Contain("does not parse the DOL structure"));
        });
    }

    [Test]
    public void ReceiptTerminalPageDocumentsExtractHashedPropertiesAndLeftTruncation()
    {
        string document = ReadMechanismDocument("KDF-EP2-PAN-RECEIPT-TRM");
        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("HKDF-Extract-SHA256"));
            Assert.That(document, Does.Contain("HKDF-Expand-SHA256"));
            Assert.That(document, Does.Contain("#SALT must resolve to exactly 32 bytes"));
            Assert.That(document, Does.Contain("SHA-256(Terminal Properties)"));
            Assert.That(document, Does.Contain("key and salt are not pre-hashed"));
            Assert.That(document, Does.Contain("leftmost 16 bytes"));
        });
    }

    [Test]
    public void SurrogateTransactionPageDocumentsRawDolAndFullOutput()
    {
        string document = ReadMechanismDocument("KDF-EP2-PAN-SURROGATE-TRX");
        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("No Extract"));
            Assert.That(document, Does.Contain("raw DOL as info").IgnoreCase);
            Assert.That(document, Does.Contain("not pre-hashed"));
            Assert.That(document, Does.Contain("no truncation"));
            Assert.That(document, Does.Contain("complete 32-byte result"));
            Assert.That(document, Does.Contain("Primary and Secondary"));
        });
    }

    [Test]
    public void CentralDocumentationContainsAllEp2MechanismsAndDifferences()
    {
        string mechanisms = ReadInfoDocument("Info.Mechanisms.md");
        string parameters = ReadInfoDocument("Info.Parameters.md");
        foreach (object[] example in MechanismExamples)
        {
            string mechanism = (string)example[0];
            Assert.That(mechanisms, Does.Contain($"- {mechanism} :"));
            Assert.That(parameters, Does.Contain($"| {mechanism} |"));
        }

        Assert.Multiple(() =>
        {
            Assert.That(mechanisms, Does.Contain("| 8.11 | yes | variant constant | 16/32 Byte |"));
            Assert.That(mechanisms, Does.Contain("| 8.12 | no | SHA-256(DOL) | leftmost 16 of 32 Byte |"));
            Assert.That(mechanisms, Does.Contain("| 8.13 | yes | SHA-256(Terminal Properties) | leftmost 16 of 32 Byte |"));
            Assert.That(mechanisms, Does.Contain("| 8.14 | no | raw DOL | full 32 Byte |"));
            Assert.That(parameters, Does.Contain("VARIANT: Required only by KDF-EP2-SESSION"));
            Assert.That(parameters, Does.Contain("TC, MAC-SEND, MAC-RECEIVE, ENCRYPTION, PIN or KEY-ENCRYPTION"));
            Assert.That(parameters, Does.Contain("fix SHA-256"));
            Assert.That(parameters, Does.Contain("HASH and OUTLEN cannot be selected externally"));
        });
    }

    private static string ReadMechanismDocument(string mechanism) =>
        ReadInfoDocument($"Info.Mech.{mechanism}.md");

    private static string ReadInfoDocument(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", name);
        Assert.That(File.Exists(path), Is.True, $"Missing deployed documentation: {name}");
        return File.ReadAllText(path);
    }

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
