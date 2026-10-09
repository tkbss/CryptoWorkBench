using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvAcSessionDocumentationTests
{
    private const string Mechanism = "KDF-EMV-AC-SESSION";

    [SetUp]
    public void SetUp()
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
        try
        {
            Execute($"Info({Mechanism})");
        }
        finally
        {
            OutputOperations.InfoEvent -= Capture;
        }

        Assert.That(displayed, Is.EqualTo(document));
        Execute(ExtractExample(document));

        var result = (KeyVariableDeclaration)VariableDictionary.Instance().Get("sessionKey");
        Assert.Multiple(() =>
        {
            Assert.That(result.Value,
                Is.EqualTo("0x(89F7B697A028A93345BE7A409665B9A4)").IgnoreCase);
            Assert.That(result.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Aes)));
            Assert.That(result.KeySizeInBits, Is.EqualTo(new KeySize(128)));
            Assert.That(result.Usage, Is.EqualTo(KeyUsagePolicy.Unspecified));
            Assert.That(result.DerivationMechanism, Is.EqualTo(Mechanism));
        });
    }

    [Test]
    public void PageDocumentsContractSecurityScopeAndNormativeUncertainty()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string[] requiredSections =
        {
            "## Purpose",
            "## Conformance Status",
            "## Supported Master Keys",
            "## ATC Input",
            "## Parameter Contract",
            "## Derivation Algorithms",
            "## Example Usage",
            "## Result KEY Metadata",
            "## Invalid Inputs",
            "## Security and Scope",
            "## Reference"
        };

        foreach (string section in requiredSections)
            Assert.That(document, Does.Contain(section), $"Missing {section}");

        Assert.Multiple(() =>
        {
            Assert.That(document, Does.StartWith($"# MECHANISM {Mechanism}"));
            Assert.That(document, Does.Contain("exactly two binary bytes"));
            Assert.That(document, Does.Contain("No padding or IV is used"));
            Assert.That(document, Does.Contain("not changed to enforce odd DES parity"));
            Assert.That(document, Does.Contain("leftmost 192 or 256 bits"));
            Assert.That(document, Does.Contain("No additional named parameters are supported"));
            Assert.That(document, Does.Contain("nested function result is rejected").IgnoreCase);
            Assert.That(document, Does.Contain("Duplicate `#MECH` declarations are rejected"));
            Assert.That(document, Does.Contain("final normative confirmation").IgnoreCase);
            Assert.That(document, Does.Contain("does not calculate an Application Cryptogram or ARPC"));
            Assert.That(document, Does.Contain("Errors describe the violated contract and do not include key material"));
        });
    }

    [Test]
    public void CentralDocumentationListsMechanismAndItsParameterContract()
    {
        string mechanisms = ReadInfoDocument("Info.Mechanisms.md");
        string parameters = ReadInfoDocument("Info.Parameters.md");

        Assert.Multiple(() =>
        {
            Assert.That(mechanisms, Does.Contain(
                $"- {Mechanism} : EMV Common Session Key Derivation for ATC-based Application Cryptogram and ARPC processing."));
            Assert.That(parameters, Does.Contain($"        - {Mechanism}"));
            Assert.That(parameters, Does.Contain(
                $"- {Mechanism} parameters contain only MECH."));
            Assert.That(parameters, Does.Contain("an exactly two-byte ATC"));
            Assert.That(parameters, Does.Contain("No additional named parameters are supported"));
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
        string section = document.Split("## Example Usage", 2)[1]
            .Split("## Result KEY Metadata", 2)[0];
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
