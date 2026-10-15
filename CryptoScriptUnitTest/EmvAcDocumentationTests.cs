using CryptoScript.CryptoAlgorithm;
using CryptoScript.Documentation;
using CryptoScript.Model;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvAcDocumentationTests
{
    [SetUp]
    public void SetUp() => EmvAcFixtures.Reset();

    [TestCase("MAC-EMV-AC", CryptoScriptLexer.M_MAC_EMV_AC)]
    [TestCase("MAC-EMV-AC-VERIFY", CryptoScriptLexer.M_MAC_EMV_AC_VERIFY)]
    public void RegistryFactoryLexerAndStrictMetadataAgree(string mechanism, int token)
    {
        Assert.That(MechanismRegistry.TryGet(mechanism, out var entry), Is.True);
        Assert.That(entry!.StrictMechanismParameterContract, Is.True);
        Assert.That(entry.SupportedFunctions, Is.EquivalentTo(new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Mac }));
        Assert.That(MechanismList.Instance.Mechanisms.Count(name => name == mechanism), Is.EqualTo(1));
        Assert.That(AlgorithmFactory.Create(mechanism).GetType().Name, Is.EqualTo(mechanism.Replace('-', '_')));
        var lexer = new CryptoScriptLexer(new Antlr4.Runtime.AntlrInputStream(mechanism));
        Assert.That(lexer.Vocabulary.GetLiteralName(token), Is.EqualTo("'" + mechanism + "'"));
        Assert.That(CryptoScriptParser.DefaultVocabulary.GetLiteralName(token), Is.EqualTo("'" + mechanism + "'"));
        Assert.That(lexer.NextToken().Type, Is.EqualTo(CryptoScriptLexer.MECHANISM));
        foreach (var metadata in entry.FunctionMetadata.Values)
        {
            Assert.That(metadata.AdditionalNamedParameterHandling, Is.EqualTo(AdditionalNamedParameterHandling.None));
            Assert.That(metadata.Parameters.All(p => p.DefaultKind == MechanismParameterDefaultKind.None), Is.True);
            Assert.That(metadata.Parameters.Where(p => p.Kind == MechanismParameterKind.NamedParameter).Select(p => p.Name),
                Is.EquivalentTo(mechanism.EndsWith("VERIFY")
                    ? metadata.Function == CryptoScriptFunction.Mac ? new[] { "#MECH", "#EXPECTED" } : new[] { "#EXPECTED" }
                    : metadata.Function == CryptoScriptFunction.Mac ? new[] { "#MECH" } : Array.Empty<string>()));
        }
        Assert.That(entry.FunctionMetadata[CryptoScriptFunction.Mac].Parameters.Single(p => p.Name == "transactionData").AcceptedInputForms,
            Is.EquivalentTo(new[] { MechanismParameterInputForm.HexLiteral, MechanismParameterInputForm.VariableReference }));
    }

    [Test]
    public void ExpectedTokenAndParameterDefinitionAgree()
    {
        Assert.That(CryptoScriptLexer.P_EXPECTED, Is.EqualTo(CryptoScriptParser.P_EXPECTED));
        var lexer = new CryptoScriptLexer(new Antlr4.Runtime.AntlrInputStream("#EXPECTED"));
        Assert.That(lexer.NextToken().Type, Is.EqualTo(CryptoScriptLexer.PARAM_TYPE));
        Assert.That(lexer.Vocabulary.GetLiteralName(CryptoScriptLexer.P_EXPECTED), Is.EqualTo("'#EXPECTED'"));
        Assert.That(ParameterRegistry.TryGet("#EXPECTED", out var entry), Is.True);
        Assert.That(entry!.Description, Does.Contain("eight-byte").And.Contain("MAC-EMV-AC-VERIFY"));
    }

    [TestCase("MAC-EMV-AC")]
    [TestCase("MAC-EMV-AC-VERIFY")]
    public void CatalogProviderNavigationAndInfoUseDeployedPage(string mechanism)
    {
        var id = InfoDocumentId.CreateMechanism(mechanism);
        Assert.That(InfoDocumentCatalog.TryGet(id, out var entry), Is.True);
        Assert.That(entry!.MarkdownFileName, Is.EqualTo("Info.Mech." + mechanism + ".md"));
        string document = Read(mechanism);
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocumentation(mechanism, out string byName), Is.True);
        Assert.That(provider.TryGetDocument(id, out string byId), Is.True);
        Assert.That(byName, Is.EqualTo(document));
        Assert.That(byId, Is.EqualTo(document));
        Assert.That(InfoDocumentUri.TryParse(InfoDocumentUri.ToCanonicalString(id), out var roundTrip), Is.True);
        Assert.That(roundTrip, Is.EqualTo(id));
        string? displayed = null;
        void Capture(string text) => displayed = text;
        OutputOperations.InfoEvent += Capture;
        try { EmvAcFixtures.Execute("Info(" + mechanism + ")"); }
        finally { OutputOperations.InfoEvent -= Capture; }
        Assert.That(displayed, Is.EqualTo(document));
    }

    [TestCase("MAC-EMV-AC", "ac", "0x(D3A78E1517C22F70)")]
    [TestCase("MAC-EMV-AC-VERIFY", "verified", "0x(01)")]
    public void DeployedExampleExecutesFullSessionHierarchy(string mechanism, string resultName, string expected)
    {
        string example = Read(mechanism).Split("```text")[1].Split("```")[0];
        EmvAcFixtures.Execute(example);
        Assert.That(CryptoScript.Variables.VariableDictionary.Instance().Get(resultName).Value, Is.EqualTo(expected).IgnoreCase);
    }

    [TestCase("MAC-EMV-AC")]
    [TestCase("MAC-EMV-AC-VERIFY")]
    public void DocumentationStatesContractsAndEvidenceLimits(string mechanism)
    {
        string document = Read(mechanism);
        foreach (string fragment in new[] { "eight-byte", "Algorithm 3", "CMAC", "genuine", "nested", "KeyUsage",
            "zeroized", "CDOL/TLV", "APDU", "ARPC", "Secure Messaging", "Linux/Unix", "FIPS",
            "not EMV product certification", "characterization values", "0x()" })
            Assert.That(document, Does.Contain(fragment).IgnoreCase, fragment);
        if (mechanism.EndsWith("VERIFY"))
            foreach (string fragment in new[] { "SemanticErrorException", "actually stops", "not rolled back", "AST provenance", "duplicate" })
                Assert.That(document, Does.Contain(fragment).IgnoreCase, fragment);
        var parameterOverview = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Parameters.md"));
        Assert.That(parameterOverview, Does.Contain("EXPECTED").And.Contain(mechanism));
    }

    private static string Read(string mechanism) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Mech." + mechanism + ".md"));

    [TestCase("MAC-EMV-AC")]
    [TestCase("MAC-EMV-AC-VERIFY")]
    [Category("EmvArpcDocumentationLinks")]
    public void AcPagesDiscloseArpcAvailabilityAndResolveBothNavigationLinks(string mechanism)
    {
        string document = Read(mechanism);
        Assert.That(document, Does.Not.Contain("No ARPC mechanism").And.Not.Contain("ARPC mechanism, Secure Messaging"));
        foreach (string text in new[] { "does not compute ARPC", "After successful verification", "stops script execution",
                     "does not enforce prior verification", "Secure Messaging", "EMV transaction processing" })
            Assert.That(document, Does.Contain(text));
        var provider = new FileInfoDocumentationProvider();
        foreach (string target in new[] { "MAC-EMV-ARPC-METHOD-1", "MAC-EMV-ARPC-METHOD-2" })
        {
            string link = "cryptoscript-info://mechanism/" + target;
            Assert.That(document, Does.Contain("](" + link + ")"));
            Assert.That(InfoDocumentUri.TryParse(link, out var id), Is.True);
            Assert.That(id, Is.EqualTo(InfoDocumentId.CreateMechanism(target)));
            Assert.That(InfoDocumentCatalog.TryGet(id!, out var entry), Is.True);
            Assert.That(entry!.MarkdownFileName, Is.EqualTo("Info.Mech." + target + ".md"));
            Assert.That(provider.TryGetDocument(id!, out string content), Is.True);
            Assert.That(content, Is.EqualTo(Read(target)));
        }
    }
}
