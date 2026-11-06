using CryptoScript.CryptoAlgorithm;
using CryptoScript.Documentation;
using CryptoScript.Model;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvArpcDocumentationTests
{
    [SetUp]
    public void SetUp() => EmvAcFixtures.Reset();

    [TestCase("MAC-EMV-ARPC-METHOD-1", CryptoScriptLexer.M_MAC_EMV_ARPC_METHOD_1, "#ARC")]
    [TestCase("MAC-EMV-ARPC-METHOD-2", CryptoScriptLexer.M_MAC_EMV_ARPC_METHOD_2, "#CSU")]
    public void RegistryFactoryLexerAndParameterContractsAgree(string mechanism, int token, string required)
    {
        Assert.That(MechanismRegistry.TryGet(mechanism, out var entry), Is.True);
        Assert.That(entry!.StrictMechanismParameterContract, Is.True);
        Assert.That(entry.SupportedFunctions, Is.EquivalentTo(new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Mac }));
        Assert.That(AlgorithmFactory.Create(mechanism).GetType().Name, Is.EqualTo(mechanism.Replace('-', '_')));
        var lexer = new CryptoScriptLexer(new Antlr4.Runtime.AntlrInputStream(mechanism));
        Assert.That(lexer.NextToken().Type, Is.EqualTo(CryptoScriptLexer.MECHANISM));
        Assert.That(lexer.Vocabulary.GetLiteralName(token), Is.EqualTo("'" + mechanism + "'"));
        Assert.That(CryptoScriptParser.DefaultVocabulary.GetLiteralName(token), Is.EqualTo("'" + mechanism + "'"));
        foreach (var metadata in entry.FunctionMetadata.Values)
        {
            Assert.That(metadata.AdditionalNamedParameterHandling, Is.EqualTo(AdditionalNamedParameterHandling.None));
            Assert.That(metadata.Parameters.Single(p => p.Name == required).IsRequired, Is.True);
            var names = metadata.Parameters.Where(p => p.Kind == MechanismParameterKind.NamedParameter).Select(p => p.Name).ToArray();
            var expected = mechanism == EmvArpcFixtures.Method1 ? new[] { "#ARC" } : new[] { "#CSU", "#ADATA", "#VARIANT" };
            Assert.That(names, Is.EquivalentTo(metadata.Function == CryptoScriptFunction.Mac ? expected.Prepend("#MECH") : expected));
        }
        Assert.That(entry.FunctionMetadata[CryptoScriptFunction.Mac].Parameters.Single(p => p.Name == "arqc").AcceptedInputForms,
            Is.EquivalentTo(new[] { MechanismParameterInputForm.HexLiteral, MechanismParameterInputForm.VariableReference }));
    }

    [TestCase("#ARC", CryptoScriptLexer.P_ARC)]
    [TestCase("#CSU", CryptoScriptLexer.P_CSU)]
    public void NewNamedParameterTokensAndRegistryAgree(string name, int token)
    {
        Assert.That(ParameterRegistry.TryGet(name, out _), Is.True);
        var lexer = new CryptoScriptLexer(new Antlr4.Runtime.AntlrInputStream(name));
        Assert.That(lexer.NextToken().Type, Is.EqualTo(CryptoScriptLexer.PARAM_TYPE));
        Assert.That(CryptoScriptParser.DefaultVocabulary.GetLiteralName(token), Is.EqualTo("'" + name + "'"));
        Assert.That(lexer.Vocabulary.GetLiteralName(token), Is.EqualTo("'" + name + "'"));
    }

    [TestCase("MAC-EMV-ARPC-METHOD-1")]
    [TestCase("MAC-EMV-ARPC-METHOD-2")]
    public void CatalogProviderUriAndInfoUseDeployedDocuments(string mechanism)
    {
        var id = InfoDocumentId.CreateMechanism(mechanism);
        Assert.That(InfoDocumentCatalog.TryGet(id, out var entry), Is.True);
        Assert.That(entry!.MarkdownFileName, Is.EqualTo("Info.Mech." + mechanism + ".md"));
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocumentation(mechanism, out string byName), Is.True);
        Assert.That(provider.TryGetDocument(id, out string byId), Is.True);
        Assert.That(byName, Is.EqualTo(Read(mechanism)));
        Assert.That(byId, Is.EqualTo(byName));
        Assert.That(InfoDocumentUri.TryParse(InfoDocumentUri.ToCanonicalString(id), out var roundTrip), Is.True);
        Assert.That(roundTrip, Is.EqualTo(id));
        string? displayed = null;
        void Capture(string value) => displayed = value;
        OutputOperations.InfoEvent += Capture;
        try { EmvAcFixtures.Execute("Info(" + mechanism + ")"); }
        finally { OutputOperations.InfoEvent -= Capture; }
        Assert.That(displayed, Is.EqualTo(byName));
    }

    [TestCase("MAC-EMV-ARPC-METHOD-1", 0, "arpc", "FC16C28C668DDC7C")]
    [TestCase("MAC-EMV-ARPC-METHOD-1", 1, "arpc", "FC16C28C668DDC7C")]
    [TestCase("MAC-EMV-ARPC-METHOD-2", 0, "arpc", "B7E6B69E")]
    [TestCase("MAC-EMV-ARPC-METHOD-2", 1, "iad", "B7E6B69E00000000")]
    public void AllPublishedExamplesActuallyExecute(string mechanism, int example, string id, string expected)
    {
        string script = Read(mechanism).Split("```text")[example + 1].Split("```")[0];
        EmvAcFixtures.Execute(script);
        EmvArpcFixtures.AssertResult(id, expected, expected.Length / 2);
    }

    [TestCase("MAC-EMV-ARPC-METHOD-1", "8.2.1", "35")]
    [TestCase("MAC-EMV-ARPC-METHOD-2", "8.2.2", "36")]
    public void DocumentationDisclosesBinaryContractsPolicyAndEvidenceLimits(string mechanism, string section, string footnote)
    {
        string document = Read(mechanism);
        foreach (string fragment in new[] { section, "footnote " + footnote, "Secret TDEA", "Secret AES", "genuine",
            "Session KEY", "HEX", "MAC-EMV-AC-VERIFY", "SemanticErrorException", "not rolled back",
            "zeroized", "Linux/Unix/FIPS", "not EMVCo product certification", "characterization values", "Immutable strings" })
            Assert.That(document, Does.Contain(fragment).IgnoreCase, fragment);
        if (mechanism == EmvArpcFixtures.Method2)
            foreach (string fragment in new[] { "8-16", "not BER-TLV", "opaque", "0x()", "#VARIANT:\"IAD\"" })
                Assert.That(document, Does.Contain(fragment), fragment);
    }

    private static string Read(string mechanism) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Mech." + mechanism + ".md"));
}
