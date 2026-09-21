using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DukptAesInitialKeyDocumentationTests
{
    private const string Mechanism = "DUKPT-AES-INITIAL-KEY";

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void InfoResolvesDeployedPageAndOfficialExampleExecutes()
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

        var initialKey = (KeyVariableDeclaration)VariableDictionary.Instance().Get("initialKey");
        Assert.Multiple(() =>
        {
            Assert.That(initialKey.Value,
                Is.EqualTo("0x(1273671EA26AC29AFA4D1084127652A1)").IgnoreCase);
            Assert.That(initialKey.KeySize, Is.EqualTo("128"));
            Assert.That(initialKey.DerivationMechanism, Is.EqualTo(Mechanism));
        });
    }

    [Test]
    public void PageDocumentsInterfaceParametersAndBinaryFormats()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");

        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("Derive(PARAM, KEY, DATA)"));
            Assert.That(document, Does.Contain("#MECH:DUKPT-AES-INITIAL-KEY"));
            Assert.That(document, Does.Contain("#OUTLEN"));
            Assert.That(document, Does.Contain("#PRF"));
            Assert.That(document, Does.Contain("#LABEL"));
            Assert.That(document, Does.Contain("#COUNTER"));
            Assert.That(document, Does.Contain("0x(1234567890123456)"));
            Assert.That(document, Does.Contain("b64(EjRWeJASNFY=)"));
            Assert.That(document, Does.Contain("\"12345678\""));
            Assert.That(document, Does.Contain("exactly eight bytes"));
        });
    }

    [TestCase("0x(1234567890123456)")]
    [TestCase("b64(EjRWeJASNFY=)")]
    [TestCase("\"12345678\"")]
    public void DocumentedIkidFormatsExecuteAsEightBytes(string ikid)
    {
        Execute("KEY formatBdk = GenerateKey(AES-ECB, " +
                "0x(FEDCBA9876543210F1F1F1F1F1F1F1F1)) " +
                $"PARAM formatDukpt = Parameters({Mechanism}) " +
                $"KEY formatInitialKey = Derive(formatDukpt, formatBdk, {ikid})");

        var initialKey = (KeyVariableDeclaration)VariableDictionary.Instance().Get("formatInitialKey");
        Assert.That(FormatConversions.HexStringToByteArray(initialKey.Value), Has.Length.EqualTo(16));
    }

    [Test]
    public void PageDocumentsBdkSizesDerivationDataAndDukptScope()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string[] required =
        {
            "AES-128", "AES-192", "AES-256", "16 bytes / 128 bits", "24 bytes / 192 bits",
            "32 bytes / 256 bits", "BDK", "IKID", "Initial Key", "Transaction Keys", "Working Keys",
            "01 || Counter || 8001 || Algorithm || KeyLength || IKID", "0002", "0003", "0004",
            "0080", "00C0", "0100", "AES-ECB", "No IV and no padding"
        };

        foreach (string value in required)
            Assert.That(document, Does.Contain(value), $"Missing documentation text: {value}");
    }

    [Test]
    public void CentralDocumentationListsMechanismAndFixedParameterContract()
    {
        string mechanisms = ReadInfoDocument("Info.Mechanisms.md");
        string parameters = ReadInfoDocument("Info.Parameters.md");

        Assert.Multiple(() =>
        {
            Assert.That(mechanisms, Does.Contain($"- {Mechanism} :"));
            Assert.That(parameters, Does.Contain($"- {Mechanism}"));
            Assert.That(parameters, Does.Contain($"{Mechanism} parameters contain only MECH"));
            Assert.That(parameters, Does.Contain("OUTLEN, PRF, LABEL and COUNTER are not supported"));
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
            .Select(line => line.TrimEnd('\r'))
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
