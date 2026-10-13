using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.Documentation;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvMasterOptionCDocumentationTests
{
    private const string Mechanism = "KDF-EMV-MASTER-C";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [Test]
    public void RegistryFactoryLexerAndStrictParameterMetadataAgree()
    {
        Assert.That(AlgorithmFactory.Create(Mechanism), Is.TypeOf<KDF_EMV_MASTER_C>());
        Assert.That(MechanismRegistry.TryGet(Mechanism, out var entry), Is.True);
        Assert.That(entry!.StrictMechanismParameterContract, Is.True);
        Assert.That(MechanismList.Instance.Mechanisms.Count(name => name == Mechanism), Is.EqualTo(1));
        var lexer = new CryptoScriptLexer(new Antlr4.Runtime.AntlrInputStream(Mechanism));
        Assert.That(lexer.Vocabulary.GetLiteralName(CryptoScriptLexer.M_KDF_EMV_MASTER_C), Is.EqualTo("'" + Mechanism + "'"));
        Assert.That(CryptoScriptLexer.M_KDF_EMV_MASTER_C, Is.EqualTo(CryptoScriptParser.M_KDF_EMV_MASTER_C));
        Assert.That(lexer.NextToken().Type, Is.EqualTo(CryptoScriptLexer.MECHANISM));
        Assert.That(entry.FunctionMetadata.Keys, Is.EquivalentTo(new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Derive }));
        foreach (var metadata in entry.FunctionMetadata.Values)
        {
            Assert.That(metadata.AdditionalNamedParameterHandling, Is.EqualTo(AdditionalNamedParameterHandling.None));
            var psn = metadata.Parameters.Single(parameter => parameter.Name == "#PSN");
            Assert.That(psn.IsRequired, Is.False);
            Assert.That(psn.DefaultValue, Is.EqualTo("\"00\""));
            Assert.That(psn.AcceptedInputForms, Is.EqualTo(new[] { MechanismParameterInputForm.StringLiteral }));
        }
        var derive = entry.FunctionMetadata[CryptoScriptFunction.Derive];
        Assert.That(derive.Parameters.Single(parameter => parameter.Name == "pan").AcceptedInputForms,
            Is.EquivalentTo(new[] { MechanismParameterInputForm.StringLiteral, MechanismParameterInputForm.VariableReference }));
        Assert.That(derive.Parameters.Single(parameter => parameter.Name == "issuerMasterKey").ValueConstraint,
            Does.Contain("Secret AES").And.Contain("16, 24 or 32"));
    }

    [Test]
    public void CatalogProviderNavigationAndInfoUseTheDeployedPage()
    {
        var id = InfoDocumentId.CreateMechanism(Mechanism);
        Assert.That(InfoDocumentCatalog.TryGet(id, out var entry), Is.True);
        Assert.That(entry!.MarkdownFileName, Is.EqualTo("Info.Mech." + Mechanism + ".md"));
        string document = ReadDocument();
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocumentation(Mechanism, out string byName), Is.True);
        Assert.That(byName, Is.EqualTo(document));
        Assert.That(provider.TryGetDocument(id, out string byId), Is.True);
        Assert.That(byId, Is.EqualTo(document));
        string? displayed = null;
        void Capture(string text) => displayed = text;
        OutputOperations.InfoEvent += Capture;
        try { Execute("Info(" + Mechanism + ")"); }
        finally { OutputOperations.InfoEvent -= Capture; }
        Assert.That(displayed, Is.EqualTo(document));
    }

    [Test]
    public void DeployedExampleExecutesTheCompleteAesKeyHierarchy()
    {
        string example = ReadDocument().Split("```text")[1].Split("```")[0];
        Execute(example);
        var icc = (KeyVariableDeclaration)VariableDictionary.Instance().Get("ICC");
        var session = (KeyVariableDeclaration)VariableDictionary.Instance().Get("sessionKey");
        Assert.That(icc.KeyValue, Is.EqualTo("0x(35C006583E2B417207AE5B166C0EA6EF)").IgnoreCase);
        Assert.That(icc.DerivationMechanism, Is.EqualTo(Mechanism));
        Assert.That(session.KeyValue, Is.EqualTo("0x(1E955EB21AFFA73F01DBED4D41BD25F4)").IgnoreCase);
        Assert.That(session.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Aes)));
        Assert.That(session.KeySizeInBits, Is.EqualTo(new KeySize(128)));
        Assert.That(session.DerivationMechanism, Is.EqualTo("KDF-EMV-AC-SESSION"));
    }

    [Test]
    public void PageDocumentsActualContractsConformanceAndEvidenceLimits()
    {
        string document = ReadDocument();
        foreach (string fragment in new[]
        {
            "A1.4.3", "page 140", "Bulletin 162", "13 October 2026",
            "not EMV product certification", "not official EMVCo conformance vectors",
            "provisional CryptoScript input contract", "Secret AES", "16, 24 and 32",
            "full bitwise complement of all 16 bytes", "No DES Odd Parity correction",
            "nested function results are rejected", "normal string VAR", "no implicit",
            "Duplicate PSN or MECH", "serialized legacy PARAMs", "before lossy legacy parsing",
            "KeyUsage Unspecified", "Linux/Unix", "FIPS", "Secure Messaging",
            "provider-fault injection", "not guaranteed to be atomic"
        })
            Assert.That(document, Does.Contain(fragment).IgnoreCase, fragment);
        string parameters = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Parameters.md"));
        Assert.That(parameters, Does.Contain("KDF-EMV-MASTER-A, KDF-EMV-MASTER-B and KDF-EMV-MASTER-C"));
        Assert.That(document, Does.Not.Contain("verification pending"));
    }

    private static string ReadDocument() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Mech." + Mechanism + ".md"));

    private static void Execute(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        new CryptoScriptRunner().Execute(context);
    }
}
