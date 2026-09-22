using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DukptTdeaInitialKeyDocumentationTests
{
    private const string Mechanism = "DUKPT-TDEA-INITIAL-KEY";

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
                Is.EqualTo("0x(6AC292FAA1315B4D858AB3A3D7D5933A)").IgnoreCase);
            Assert.That(initialKey.KeySize, Is.EqualTo("128"));
            Assert.That(initialKey.DerivationMechanism, Is.EqualTo(Mechanism));
        });
    }

    [Test]
    public void PageDocumentsContractAlgorithmAndOfficialVector()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string[] required =
        {
            "BDK + KSN -> Initial Key", "Derive(PARAM, KEY, DATA)",
            "#MECH:DUKPT-TDEA-INITIAL-KEY", "double-length TDEA",
            "exactly 16 bytes / 128 bits", "10-byte / 80-bit KSN",
            "21-bit Transaction Counter", "FFFF9876543210E00000", "FFFF9876543210E0",
            "C0C0C0C000000000C0C0C0C000000000", "C1E385A789ABCDEF3E1C7A5876543210",
            "6AC292FAA1315B4D858AB3A3D7D5933A", "InitialKey = IKL || IKR",
            "No IV and no padding"
        };

        foreach (string value in required)
            Assert.That(document, Does.Contain(value), $"Missing documentation text: {value}");
    }

    [Test]
    public void PageDocumentsLimitedScopeAndAesDukptDistinction()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string[] required =
        {
            "does not implement transaction key derivation", "Transaction Counter advancement",
            "future or intermediate key management", "PIN key variants", "MAC key variants",
            "data-encryption key variants", "device or key-register state management",
            "TR-31 key distribution", "DUKPT-AES-INITIAL-KEY", "64-bit Initial Key ID",
            "different standardized cryptographic derivation algorithms"
        };

        foreach (string value in required)
            Assert.That(document, Does.Contain(value), $"Missing documentation text: {value}");
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
            Assert.That(parameters, Does.Contain($"{Mechanism} parameters contain only MECH"));
            Assert.That(parameters, Does.Contain("16-byte double-length TDEA BDK"));
            Assert.That(parameters, Does.Contain("complete 80-bit KSN"));
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
