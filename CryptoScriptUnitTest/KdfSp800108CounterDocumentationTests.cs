using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Text.RegularExpressions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KdfSp800108CounterDocumentationTests
{
    private const string Mechanism = "KDF-SP800-108-COUNTER";

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void InfoResolvesDeployedPageAndHmacExampleExecutes()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string? displayed = null;
        void Capture(string text) => displayed = text;
        OutputOperations.InfoEvent += Capture;
        try { Execute($"Info({Mechanism})"); }
        finally { OutputOperations.InfoEvent -= Capture; }

        Assert.That(displayed, Is.EqualTo(document));
        Assert.That(document, Does.StartWith($"# MECHANISM {Mechanism}"));
        Execute(ExtractExample(document, "## HMAC-SHA256 Example", "## AES-CMAC Example"));

        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("derivedKey");
        Assert.Multiple(() =>
        {
            Assert.That(key.Value,
                Is.EqualTo("0x(508BE685D92997294C12712641077442382A77FD41A6F3D0A10CBB805EAEA7A0)")
                    .IgnoreCase);
            Assert.That(key.KeySize, Is.EqualTo("256"));
            Assert.That(key.Mechanism, Is.Empty);
            Assert.That(key.DerivationMechanism, Is.EqualTo(Mechanism));
        });
    }

    [Test]
    public void AesCmacExampleExecutes()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        Execute(ExtractExample(document, "## AES-CMAC Example", "## Result Metadata"));

        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("cmacDerivedKey");
        Assert.That(key.Value,
            Is.EqualTo("0x(64EC377C7A14A3D931E7BFD71840A493BC6012B38EC96C68A048053D0D35705B)")
                .IgnoreCase);
    }

    [Test]
    public void PageDocumentsInterfaceParametersPrfsAndEncoding()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string[] required =
        {
            "Derive(PARAM, KEY, DATA)", "#MECH", "#PRF", "#OUTLEN", "#COUNTER", "#LABEL",
            "[i]r || Label || 00 || Context || [L]32", "unsigned big-endian", "32-bit big-endian",
            "n = ceil(L / h)", "n > 2^r - 1", "leftmost L bits", "defaults to 32 bits",
            "4,294,967,288 bits", "unsigned 32-bit integer", "ceil(L / h) <= 2^r - 1",
            "65280 bits", "65536 bits", "K(1)", "K(2)", "not an official NIST CAVP or ACVP vector",
            "### Grammar and Parser", "### Parameter Evaluation", "### Derivation and Cryptographic Validation"
        };

        foreach (string value in required)
            Assert.That(document, Does.Contain(value), $"Missing documentation text: {value}");

        var expectedPrfs = new Dictionary<string, int>
        {
            ["HMAC-SHA1"] = 160,
            ["HMAC-SHA224"] = 224,
            ["HMAC-SHA256"] = 256,
            ["HMAC-SHA384"] = 384,
            ["HMAC-SHA512"] = 512,
            ["HMAC-SHA512-224"] = 224,
            ["HMAC-SHA512-256"] = 256,
            ["HMAC-SHA3-224"] = 224,
            ["HMAC-SHA3-256"] = 256,
            ["HMAC-SHA3-384"] = 384,
            ["HMAC-SHA3-512"] = 512,
            ["AES-CMAC"] = 128
        };

        string table = document.Split("## Supported PRFs", 2)[1].Split("## KIN Requirements", 2)[0];
        var documentedPrfs = Regex.Matches(table,
                @"^\s*\|\s*`(?<prf>[^`]+)`\s*\|\s*(?<bits>\d+)\s+bits\s*\|\s*$",
                RegexOptions.Multiline)
            .ToDictionary(match => match.Groups["prf"].Value,
                match => int.Parse(match.Groups["bits"].Value));

        Assert.That(documentedPrfs, Is.EquivalentTo(expectedPrfs));
    }

    [Test]
    public void PageDocumentsKinFormatsErrorsScopeAndMetadata()
    {
        string document = ReadInfoDocument($"Info.Mech.{Mechanism}.md");
        string[] required =
        {
            "at least one byte", "16 bytes / 128 bits", "24 bytes / 192 bits",
            "32 bytes / 256 bits", "0x(010203)", "b64(AQID)", "\"ctx\"", "empty Context",
            "DES3-CMAC", "KMAC", "Feedback Mode", "Double-Pipeline Iteration Mode",
            "FixedInputData", "K(0)", "does not append", "DerivationMechanism",
            "DUKPT-AES-INITIAL-KEY", "NIST Special Publication 800-108 Revision 1 Update 1"
        };

        foreach (string value in required)
            Assert.That(document, Does.Contain(value), $"Missing documentation text: {value}");
    }

    [Test]
    public void DocumentedLabelAndContextFormatsRepresentTheSameUtf8Bytes()
    {
        string[] representations = { "0x(C3A4)", "b64(w6Q=)", "\"ä\"" };
        var results = new List<string>();
        Execute("KEY formatKin = GenerateKey(HMAC-SHA256, " +
                "0x(000102030405060708090A0B0C0D0E0F))");

        for (int index = 0; index < representations.Length; index++)
        {
            string representation = representations[index];
            Execute($"PARAM formatKdf{index} = Parameters({Mechanism}, #PRF:HMAC-SHA256, " +
                    $"#OUTLEN:128, #COUNTER:32, #LABEL:{representation}) " +
                    $"KEY formatResult{index} = Derive(formatKdf{index}, formatKin, {representation})");

            var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get($"formatResult{index}");
            results.Add(key.Value);
        }

        Assert.Multiple(() =>
        {
            Assert.That(results[1], Is.EqualTo(results[0]).IgnoreCase,
                "Base64 must decode to the same bytes as hexadecimal data.");
            Assert.That(results[2], Is.EqualTo(results[0]).IgnoreCase,
                "The UTF-8 string ä must encode as the bytes C3 A4.");
        });
    }

    [Test]
    public void EmptyLabelAndContextExampleExecutes()
    {
        Execute("KEY emptyKin = GenerateKey(HMAC-SHA256, 0x(000102030405060708090A0B0C0D0E0F)) " +
                $"PARAM emptyKdf = Parameters({Mechanism}, #PRF:HMAC-SHA256, #OUTLEN:256, #COUNTER:8) " +
                "KEY emptyResult = Derive(emptyKdf, emptyKin, \"\")");

        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("emptyResult");
        Assert.That(FormatConversions.HexStringToByteArray(key.Value), Has.Length.EqualTo(32));
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
            Assert.That(parameters, Does.Contain("KDF-SP800-108-COUNTER parameter contract"));
            Assert.That(parameters, Does.Contain("8/16/24/32 bits, default 32"));
            Assert.That(parameters, Does.Contain("DES3-CMAC and KMAC are not supported"));
        });
    }

    private static string ReadInfoDocument(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", name);
        Assert.That(File.Exists(path), Is.True, $"Missing deployed documentation: {name}");
        return File.ReadAllText(path);
    }

    private static string ExtractExample(string document, string start, string end)
    {
        string section = document.Split(start, 2)[1].Split(end, 2)[0];
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
