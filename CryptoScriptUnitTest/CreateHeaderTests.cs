using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Documentation;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class CreateHeaderTests
{
    private VariableDeclaration[] previous = [];
    private const string Basic = "KBVID:D,KEYU:D0,ALGO:A,MODEU:B,EXP:E";
    [SetUp] public void Save()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }
    [TearDown] public void Restore()
    {
        VariableDictionary.Instance().Clear();
        foreach (var item in previous) VariableDictionary.Instance().Add(item);
    }
    private static void Run(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var tree = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero, script);
        new CryptoScriptRunner().Execute(tree);
    }
    private static BlockHeaderVariableDeclaration Header(string fields = Basic)
    {
        Run($"TR31H header=CreateHeader({fields})");
        return (BlockHeaderVariableDeclaration)VariableDictionary.Instance().Get("header");
    }

    [Test]
    public void TypedStructuredHeaderDefaultsIdentityAndPrint()
    {
        var header = Header();
        Run("PARAM p=#MECH:WRAP-AES-TR31 #BLKH:header");
        var p = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        string? printed = null;
        void Capture(string s) => printed = s;
        OutputOperations.PrintEvent += Capture;
        try { Run("Print(header)"); }
        finally { OutputOperations.PrintEvent -= Capture; }
        Assert.Multiple(() =>
        {
            Assert.That(CryptoType.Parse("TR31H"), Is.TypeOf<CryptoTypeTR31Header>());
            Assert.That(header.Type, Is.TypeOf<CryptoTypeTR31Header>());
            Assert.That(header.Definition!.Attributes["KEYVN"], Is.EqualTo("00"));
            Assert.That(header.Definition.Attributes["KEYCTX"], Is.EqualTo("0"));
            Assert.That(header.Definition.IsLengthResolved, Is.False);
            Assert.That(p.HeaderReference, Is.SameAs(header));
            Assert.That(printed, Does.Contain("KBLEN:auto (unresolved)"));
            Assert.That(printed, Does.Not.Contain("D0000"));
        });
    }

    [Test]
    public void BracesUseCanonicalBuilderAndPreserveTextPunctuation()
    {
        Run("TR31H header={KBVID:D;KEYU:D0;ALGO:A;MODEU:B;EXP:E;OPTID:LB;OPTBD:\"a;b,c:#\";}");
        var header = (BlockHeaderVariableDeclaration)VariableDictionary.Instance().Get("header");
        Assert.That(header.Definition!.OptionalBlocks[0].Data, Is.EqualTo("a;b,c:#"));
    }

    [TestCase("D", "A", 128)]
    [TestCase("D", "A", 192)]
    [TestCase("D", "A", 256)]
    [TestCase("D", "T", 128)]
    [TestCase("D", "T", 192)]
    [TestCase("A", "T", 128)]
    [TestCase("A", "T", 192)]
    [TestCase("B", "T", 128)]
    [TestCase("B", "T", 192)]
    [TestCase("C", "T", 128)]
    [TestCase("C", "T", 192)]
    public void WrapAndBothUnwrapFormsWithOptionalBlocks(string version, string algorithm, int bits)
    {
        string wrapping = version == "D" ? "AES" : "DES3";
        string transported = algorithm == "A" ? "AES" : "DES3";
        foreach (string optional in new[] { "", ",OPTID:LB,OPTBD:\"Test\"", ",OPTID:LB,OPTBD:\"Test\",OPTID:WP,OPTBD:\"000\"" })
        {
            Header($"KBVID:{version},KEYU:D0,ALGO:{algorithm},MODEU:B,EXP:E{optional}");
            Run($"KEY kbpk=GenerateKey({wrapping}-CBC,{(version == "D" ? 256 : 192)}) " +
                $"KEY transportKey=GenerateKey({transported}-CBC,{bits}) " +
                $"PARAM p=#MECH:WRAP-{wrapping}-TR31 #BLKH:header");
            var h = (BlockHeaderVariableDeclaration)VariableDictionary.Instance().Get("header");
            var p = VariableDictionary.Instance().Get("p");
            string beforeH = h.Value, beforeP = p.Value;
            Run("VAR wrapped=Wrap(p,kbpk,transportKey) KEY recovered=Unwrap(p,kbpk,wrapped)");
            var block = TR31String.FromString(VariableDictionary.Instance().Get("wrapped").Value);
            string wire = block.Block + Convert.ToHexString(block.Cryptogram) + Convert.ToHexString(block.Mac);
            Assert.That(int.Parse(block.Block.Substring(1, 4)), Is.EqualTo(wire.Length));
            Assert.That(block.Block.Length % (version == "D" ? 16 : 8), Is.Zero);
            Assert.That(VariableDictionary.Instance().Get("recovered").Value,
                Is.EqualTo(VariableDictionary.Instance().Get("transportKey").Value).IgnoreCase);
            Run($"VAR wire=\"{wire}\" KEY recovered=Unwrap(p,kbpk,wire)");
            Assert.Multiple(() =>
            {
                Assert.That(VariableDictionary.Instance().Get("recovered").Value,
                    Is.EqualTo(VariableDictionary.Instance().Get("transportKey").Value).IgnoreCase);
                Assert.That(h.Value, Is.EqualTo(beforeH));
                Assert.That(p.Value, Is.EqualTo(beforeP));
                Assert.That(h.Definition!.IsLengthResolved, Is.False);
            });
        }
    }

    [Test]
    public void ReuseAcrossKeyLengthsDoesNotResolveOrMutateDefinition()
    {
        var h = Header();
        string before = h.Value;
        Run("KEY kbpk=GenerateKey(AES-CBC,256) PARAM p=#MECH:WRAP-AES-TR31 #BLKH:header");
        foreach (int bits in new[] { 128, 192, 256 })
            Run($"KEY transportKey=GenerateKey(AES-CBC,{bits}) VAR wrapped=Wrap(p,kbpk,transportKey) KEY recovered=Unwrap(p,kbpk,wrapped)");
        Assert.That(h.Value, Is.EqualTo(before));
        Assert.That(h.Definition!.ExpectedLength, Is.Null);
    }

    [TestCase("KBVID:D,ALGO:A,MODEU:B,EXP:E", "KEYU")]
    [TestCase(Basic + ",KEYU:D0", "Duplicate")]
    [TestCase(Basic + ",WHATEVER:0", "Unknown")]
    [TestCase("KBVID:Z,KEYU:D0,ALGO:A,MODEU:B,EXP:E", "KBVID")]
    [TestCase("KBVID:D,KEYU:ZZ,ALGO:A,MODEU:B,EXP:E", "KEYU")]
    [TestCase("KBVID:D,KEYU:D0,ALGO:H,MODEU:B,EXP:E", "ALGO")]
    [TestCase("KBVID:D,KEYU:D0,ALGO:A,MODEU:S,EXP:E", "combination")]
    [TestCase("KBVID:D,KEYU:D0,ALGO:A,MODEU:B,EXP:X", "EXP")]
    [TestCase("KBVID:B,KEYU:D0,ALGO:A,MODEU:B,EXP:E", "ALGO")]
    [TestCase("KBVID:C,KEYU:P2,ALGO:T,MODEU:G,EXP:E", "combination")]
    [TestCase(Basic + ",KEYCTX:3", "KEYCTX")]
    [TestCase(Basic + ",KEYVN:000", "KEYVN")]
    [TestCase(Basic + ",RSV:1", "RSV")]
    [TestCase(Basic + ",KBLEN:10000", "KBLEN")]
    [TestCase(Basic + ",NUMOPTB:1", "NUMOPTB")]
    [TestCase(Basic + ",OPTID:LB", "missing")]
    [TestCase(Basic + ",OPTBD:\"x\"", "preceding")]
    [TestCase(Basic + ",OPTID:LB,KEYVN:00,OPTBD:\"x\"", "immediately")]
    [TestCase(Basic + ",OPTID:LB,OPTBD:\"x\",OPTID:LB,OPTBD:\"y\"", "Duplicate")]
    [TestCase(Basic + ",OPTID:KC,OPTBD:0x(123456)", "KC")]
    [TestCase(Basic + ",OPTID:LB,OPTBD:0x(4142)", "text")]
    [TestCase(Basic + ",OPTID:AL,OPTBD:0x(0100)", "unsupported")]
    [TestCase(Basic + ",OPTID:ZZ,OPTBD:\"x\"", "unknown")]
    [TestCase(Basic + ",OPTID:TS,OPTBD:\"2026-02-30T12:00:00Z\"", "TS")]
    [TestCase(Basic + ",OPTID:PB,OPTBD:\"x\"", "align")]
    public void InvalidDefinitionsFailWithoutDeclaringVariable(string fields, string message)
    {
        var error = Assert.Throws<SemanticErrorException>(() => Header(fields));
        Assert.That(error!.SemanticError.Message, Does.Contain(message));
        Assert.That(VariableDictionary.Instance().Contains("header"), Is.False);
    }

    [TestCase("CreateHeader(KBVID D)")]
    [TestCase("CreateHeader(KBVID:D,)")]
    [TestCase("CreateHeader(OPTBD:0x(GG))")]
    public void InvalidSyntax(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.GreaterThan(0));
    }

    [Test]
    public void OptionalBlockLengthsPaddingAndDefensiveCopies()
    {
        var h = Header(Basic + ",OPTID:LB,OPTBD:\"" + new string('x', 252) + "\",OPTID:KP,OPTBD:0x(013F077B8FAA)");
        var d = h.Definition!;
        string wire = d.SerializeForWrap(48);
        Assert.That(wire[16..], Does.StartWith("LB00040106"));
        Assert.That(d.OptionalBlocks.Select(b => b.ID), Is.EqualTo(new[] { "LB", "KP", "PB" }));
        Assert.That(d.Attributes["NUMOPTB"], Is.EqualTo("03"));
        d.OptionalBlocks[0].Data = "changed";
        Assert.That(d.SerializeForWrap(48), Is.EqualTo(wire));
        Assert.That(TR31Block.FromString(wire + new string('0', 128)).OptionalBlocks![0].Data, Has.Length.EqualTo(252));
    }

    [Test]
    public void ExplicitExpectationsAndAuthenticatedComparison()
    {
        var d = Header(Basic + ",KBLEN:144,NUMOPTB:0").Definition!;
        string wire = d.SerializeForWrap(48);
        Assert.That(wire, Is.EqualTo("D0144D0AB00E0000"));
        Assert.Throws<ArgumentException>(() => d.SerializeForWrap(32));
        Assert.DoesNotThrow(() => d.ValidateAuthenticatedHeader(wire));
        Assert.Throws<ArgumentException>(() => d.ValidateAuthenticatedHeader("D0144D0AB00N0000"));
        Assert.Throws<ArgumentException>(() => d.SerializeForWrap(5000));
    }

    [Test]
    public void WrongExpectedLengthAndHeaderMismatchDoNotMutateInputs()
    {
        var h = Header(Basic + ",KBLEN:128");
        Run("KEY kbpk=GenerateKey(AES-CBC,256) KEY transportKey=GenerateKey(AES-CBC,128) PARAM p=#MECH:WRAP-AES-TR31 #BLKH:header");
        var values = VariableDictionary.Instance().GetVariables().ToDictionary(v => v.Id, v => v.Value);
        Assert.Throws<SemanticErrorException>(() => Run("VAR wrapped=Wrap(p,kbpk,transportKey)"));
        foreach (var pair in values) Assert.That(VariableDictionary.Instance().Get(pair.Key).Value, Is.EqualTo(pair.Value));
        Assert.That(VariableDictionary.Instance().Contains("wrapped"), Is.False);
        Header();
        Run("PARAM p=#MECH:WRAP-AES-TR31 #BLKH:header VAR wrapped=Wrap(p,kbpk,transportKey)");
        Header(Basic.Replace("EXP:E", "EXP:N"));
        Run("PARAM q=#MECH:WRAP-AES-TR31 #BLKH:header");
        Assert.Throws<SemanticErrorException>(() => Run("KEY bad=Unwrap(q,kbpk,wrapped)"));
    }

    [Test]
    public void UnknownReferencesWrongTypesAndEp2AreRejected()
    {
        Assert.Throws<SemanticErrorException>(() => Run("PARAM p=#MECH:WRAP-AES-TR31 #BLKH:missing"));
        Assert.Throws<ArgumentException>(() => Run("TR31H fake=\"D0144D0AB00E0000\""));
        Header();
        Assert.Throws<SemanticErrorException>(() => Run("PARAM p=#MECH:EP2-TR31-KEY-BLOCK #BLKH:header"));
    }

    [TestCase("tr31", "cryptoscript-info://overview/tr31")]
    [TestCase("CreateHeader", "cryptoscript-info://function/CreateHeader")]
    [TestCase("tr31attributes", "cryptoscript-info://overview/tr31attributes")]
    public void DocumentationIsDeployedAndInfoCallWorks(string topic, string uri)
    {
        Assert.That(InfoDocumentUri.TryParse(uri, out var id), Is.True);
        Assert.That(InfoDocumentUri.ToCanonicalString(id!), Is.EqualTo(uri));
        Assert.That(new FileInfoDocumentationProvider().TryGetDocument(id!, out string document), Is.True);
        string? actual = null;
        void Capture(string text) => actual = text;
        OutputOperations.InfoEvent += Capture;
        try { Run($"Info({topic})"); }
        finally { OutputOperations.InfoEvent -= Capture; }
        Assert.That(actual, Is.EqualTo(document));
    }

    [Test]
    public void DocumentationExamplesExecuteAndLinksResolve()
    {
        var provider = new FileInfoDocumentationProvider();
        foreach (var id in new[] { InfoDocumentId.CreateTr31Overview(), InfoDocumentId.CreateTr31Attributes(),
            InfoDocumentId.CreateFunction("CreateHeader") })
        {
            Assert.That(provider.TryGetDocument(id, out string document), Is.True);
            foreach (System.Text.RegularExpressions.Match example in System.Text.RegularExpressions.Regex.Matches(
                document, @"```cryptoscript\r?\n(.*?)```", System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                VariableDictionary.Instance().Clear();
                Run(example.Groups[1].Value);
            }
            foreach (System.Text.RegularExpressions.Match link in System.Text.RegularExpressions.Regex.Matches(
                document, @"cryptoscript-info://[^\s)]+"))
            {
                Assert.That(InfoDocumentUri.TryParse(link.Value, out var target), Is.True, link.Value);
                Assert.That(provider.TryGetDocument(target!, out _), Is.True, link.Value);
            }
        }
    }

    [Test]
    public void DocumentationCoversEveryMetadataFieldUsageAndOptionalId()
    {
        new FileInfoDocumentationProvider().TryGetDocument(InfoDocumentId.CreateTr31Attributes(), out string text);
        foreach (string field in Tr31AttributeRegistry.Fields)
            Assert.That(text, Does.Contain("| " + field + " |"));
        foreach (var (code, rule) in Tr31AttributeRegistry.KeyUsages)
            Assert.That(text, Does.Contain($"| {code} | {rule.Description} | {rule.Modes} | {rule.Versions} | {rule.Algorithms} |"));
        foreach (string id in Tr31AttributeRegistry.KnownOptionalIds)
            Assert.That(text, Does.Contain("| " + id + " |"));
    }

    [TestCase("BI", "0x(0112345678)", "B1", "A", "X")]
    [TestCase("BI", "0x(001234567812)", "B1", "T", "X")]
    [TestCase("IK", "0x(1234567812345678)", "B1", "A", "X")]
    [TestCase("KS", "0x(12345678901234600000)", "B1", "T", "X")]
    [TestCase("KC", "0x(0017EB36)", "D0", "T", "B")]
    [TestCase("KC", "0x(013F077B8FAA)", "D0", "A", "B")]
    [TestCase("KP", "\"013F077B8FAA\"", "D0", "A", "B")]
    [TestCase("PK", "0x(0017EB36)", "D0", "A", "B")]
    [TestCase("KV", "0x(0000)", "D0", "A", "B")]
    [TestCase("LB", "\"\"", "D0", "A", "B")]
    [TestCase("PA", "\"Example regional algorithm v1\"", "B3", "A", "X")]
    [TestCase("DA", "\"01P0TENM0TGN\"", "B3", "A", "X")]
    [TestCase("TC", "\"2026102012000012Z\"", "D0", "A", "B")]
    [TestCase("TS", "\"2026-10-20T12:00:00.12Z\"", "D0", "A", "B")]
    [TestCase("WP", "\"003\"", "D0", "A", "B")]
    [TestCase("PB", "\"000000000000\"", "D0", "A", "B")]
    public void EverySupportedOptionalStructure(string id, string data, string usage, string algorithm, string mode)
    {
        var h = Header($"KBVID:D,KEYU:{usage},ALGO:{algorithm},MODEU:{mode},EXP:E,OPTID:{id},OPTBD:{data}");
        Assert.That(h.Definition!.OptionalBlocks[0].ID, Is.EqualTo(id));
    }

    [TestCase("BI", "\"0212345678\"")]
    [TestCase("IK", "\"1234\"")]
    [TestCase("KS", "\"12345678901234600001\"")]
    [TestCase("KV", "\"0001\"")]
    [TestCase("WP", "\"004\"")]
    [TestCase("PK", "\"02AABBCC\"")]
    [TestCase("KP", "\"0017EB36\"")]
    [TestCase("DA", "\"01D0ASN\"")]
    [TestCase("TC", "\"20261020120000\"")]
    [TestCase("LB", "\"ümlaut\"")]
    public void InvalidOptionalStructures(string id, string data)
    {
        Assert.Throws<SemanticErrorException>(() => Header($"KBVID:D,KEYU:B1,ALGO:T,MODEU:X,EXP:E,OPTID:{id},OPTBD:{data}"));
    }

    [Test]
    public void HeaderSizeBoundaryAndExplicitPaddingAreChecked()
    {
        Assert.Throws<SemanticErrorException>(() => Header(Basic + ",OPTID:LB,OPTBD:\"" + new string('x', 9990) + "\""));
        Assert.Throws<SemanticErrorException>(() => Header(Basic + ",NUMOPTB:100"));
        Assert.Throws<SemanticErrorException>(() => Header(Basic + ",OPTID:PB,OPTBD:\"000000000000\",OPTID:LB,OPTBD:\"x\""));
        var d = Header(Basic + ",OPTID:LB,OPTBD:\"Test\",NUMOPTB:2").Definition!;
        Assert.That(d.Attributes["NUMOPTB"], Is.EqualTo("02"));
        Assert.That(d.SerializeForWrap(48), Does.EndWith("LB08TestPB080000"));
    }

    [Test]
    public void PrintableLabelDelimitersSurviveCompositeAndFullWireRoundtrip()
    {
        string label = "quote \" slash \\ embedded 0x(1234) : # ; ,";
        string literal = Newtonsoft.Json.JsonConvert.SerializeObject(label);
        Header(Basic + ",OPTID:LB,OPTBD:" + literal);
        Run("KEY kbpk=GenerateKey(AES-CBC,256) KEY transportKey=GenerateKey(AES-CBC,128) " +
            "PARAM p=#MECH:WRAP-AES-TR31 #BLKH:header VAR wrapped=Wrap(p,kbpk,transportKey) KEY recovered=Unwrap(p,kbpk,wrapped)");
        var block = TR31String.FromString(VariableDictionary.Instance().Get("wrapped").Value);
        string wire = block.Block + Convert.ToHexString(block.Cryptogram) + Convert.ToHexString(block.Mac);
        Run("VAR wire=" + Newtonsoft.Json.JsonConvert.SerializeObject(wire) + " KEY recovered=Unwrap(p,kbpk,wire)");
        var recovered = (KeyVariableDeclaration)VariableDictionary.Instance().Get("recovered");
        Assert.That(recovered.KeyAttributes!.Single(b => b.ID == "LB").Data, Is.EqualTo(label));
        Assert.That(recovered.Value, Is.EqualTo(VariableDictionary.Instance().Get("transportKey").Value).IgnoreCase);
    }

    [Test]
    public void BuilderReproducesExistingIndependentAesAndTdeaReferenceBytes()
    {
        foreach (var v in Tr31VersionDTransportVectors.All.Where(v => v.Header.Length == 16 && v.Algorithm is 'A' or 'T'))
        {
            Header($"KBVID:D,KEYU:D0,ALGO:{v.Algorithm},MODEU:B,EXP:E");
            Run($"KEY kbpk=GenerateKey(AES-CBC,0x({v.Kbpk})) " +
                $"KEY transportKey=GenerateKey({(v.Algorithm == 'A' ? "AES" : "DES3")}-CBC,0x({v.Key})) " +
                $"PARAM p=#MECH:WRAP-AES-TR31 #BLKH:header #RND:0x({v.Random}) VAR wrapped=Wrap(p,kbpk,transportKey)");
            Assert.That(VariableDictionary.Instance().Get("wrapped").Value, Is.EqualTo(v.Composite).IgnoreCase);
        }
        foreach (int i in new[] { 0, 3 })
        {
            var v = Tr31ReferenceVectors.All[i];
            Header($"KBVID:{v.Header[0]},KEYU:P0,ALGO:T,MODEU:E,EXP:E");
            Run($"KEY kbpk=GenerateKey(DES3-CBC,0x({v.Kbpk})) KEY transportKey=GenerateKey(DES3-CBC,0x({v.Key})) " +
                $"PARAM p=#MECH:WRAP-DES3-TR31 #BLKH:header #RND:0x({v.ObfuscationPadding}{v.CipherBlockPadding}) VAR wrapped=Wrap(p,kbpk,transportKey)");
            var block = TR31String.FromString(VariableDictionary.Instance().Get("wrapped").Value);
            Assert.That(block.Block + Convert.ToHexString(block.Cryptogram) + Convert.ToHexString(block.Mac), Is.EqualTo(v.CompleteBlock));
        }
    }

    [TestCase("D", "A")]
    [TestCase("B", "T")]
    public void TamperedOptionalDataAndMacFailAuthentication(string version, string algorithm)
    {
        string mech = version == "D" ? "AES" : "DES3";
        Header($"KBVID:{version},KEYU:D0,ALGO:{algorithm},MODEU:B,EXP:E,OPTID:LB,OPTBD:\"Test\"");
        Run($"KEY kbpk=GenerateKey({mech}-CBC,192) KEY transportKey=GenerateKey({mech}-CBC,128) " +
            $"PARAM p=#MECH:WRAP-{mech}-TR31 #BLKH:header VAR wrapped=Wrap(p,kbpk,transportKey)");
        var block = TR31String.FromString(VariableDictionary.Instance().Get("wrapped").Value);
        string wire = block.Block + Convert.ToHexString(block.Cryptogram) + Convert.ToHexString(block.Mac);
        foreach (int offset in new[] { 11, 20, wire.Length - 1 })
        {
            char[] changed = wire.ToCharArray();
            changed[offset] = changed[offset] == '0' ? '1' : '0';
            Run($"VAR altered=\"{new string(changed)}\"");
            Assert.Throws<SemanticErrorException>(() => Run("KEY recovered=Unwrap(p,kbpk,altered)"));
            Assert.That(VariableDictionary.Instance().Contains("recovered"), Is.False);
        }
        Run($"VAR trailing=\"{wire}00\"");
        Assert.Throws<SemanticErrorException>(() => Run("KEY recovered=Unwrap(p,kbpk,trailing)"));
        Assert.That(VariableDictionary.Instance().Contains("recovered"), Is.False);
    }
}
