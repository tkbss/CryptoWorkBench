using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Operations;
using CryptoScript.SecretSharing;
using CryptoScript.Variables;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using System.Text.RegularExpressions;
using CryptoScript.Documentation;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class SecretSharingTests
{
    [SetUp] public void Reset()
    {
        VariableDictionary.Instance().Clear();
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.SyntaxErrorOccured = false;
        SyntaxErrorListner.ErrorMessage.Clear();
    }
    private static VariableDeclaration Get(string id) => VariableDictionary.Instance().Get(id);
    private static void Run(string script, bool fails = false)
    {
        var parser = ParserBuilder.StringBuild(script);
        var program = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero, script);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        var runner = new CryptoScriptRunner();
        if (fails) Assert.Throws<SemanticErrorException>(() => runner.Execute(program));
        else try { runner.Execute(program); }
            catch (SemanticErrorException e) { Assert.Fail(e.SemanticError?.Message ?? "Missing diagnostic"); }
    }

    // Independent carryless integer polynomial multiplication/reduction oracle.
    private static byte ReferenceMultiply(int a, int b, int polynomial)
    {
        int product = 0;
        for (int bit = 0; bit < 8; bit++) if ((b & (1 << bit)) != 0) product ^= a << bit;
        for (int bit = 14; bit >= 8; bit--) if ((product & (1 << bit)) != 0) product ^= polynomial << (bit - 8);
        return (byte)product;
    }
    [TestCase(0x11B)] [TestCase(0x11D)]
    public void FieldArithmeticExhaustivelyMatchesPolynomialOracle(int polynomial)
    {
        var gf = new Gf256(polynomial);
        for (int a = 0; a < 256; a++) for (int b = 0; b < 256; b++)
        {
            Assert.That(gf.Multiply((byte)a,(byte)b), Is.EqualTo(ReferenceMultiply(a,b,polynomial)));
            Assert.That(Gf256.Add((byte)a,(byte)b), Is.EqualTo((byte)(a^b)));
        }
        for (int a = 1; a < 256; a++) Assert.That(gf.Multiply((byte)a,gf.Inverse((byte)a)), Is.EqualTo(1));
        Assert.Throws<ArgumentException>(() => gf.Inverse(0));
    }

    [TestCase(SecretDescriptor.Profile11B)] [TestCase(SecretDescriptor.Profile11D)]
    public void ShamirAllSubsetsLengthsAndAdditionalShares(string profile)
    {
        foreach (int length in new[] { 1, 8, 16, 24, 32, 64 })
        {
            byte[] key = Enumerable.Range(0,length).Select(i => (byte)(i*127)).ToArray();
            var d = new SecretDescriptor(SecretDescriptor.Shamir, 6, 3, length*8, profile);
            var set = SecretSharingEngine.Share(d,key);
            for (int a = 0; a < 4; a++) for(int b=a+1;b<5;b++) for(int c=b+1;c<6;c++)
                Assert.That(SecretSharingEngine.Restore(new SecretSet(d,[set.Shares[c],set.Shares[a],set.Shares[b]])), Is.EqualTo(key));
            Assert.That(SecretSharingEngine.Restore(set),Is.EqualTo(key));
            Assert.Throws<ArgumentException>(()=>SecretSharingEngine.Restore(new SecretSet(d,set.Shares.Take(2))));
            var bad = set.Shares.Last().Value; bad[0]^=1;
            Assert.Throws<ArgumentException>(() => SecretSharingEngine.Restore(new SecretSet(d,set.Shares.Take(5).Append(new SecretShare(6,bad)))));
        }
    }
    [TestCase(2)] [TestCase(3)] [TestCase(17)] [TestCase(255)]
    public void XorRequiresEveryComponent(int count)
    {
        byte[] key = [0, 255, 128, 1, 4, 19];
        var d=new SecretDescriptor(SecretDescriptor.Split,count,count,key.Length*8);
        var set=SecretSharingEngine.Share(d,key);
        Assert.That(SecretSharingEngine.Restore(new SecretSet(d,set.Shares.Reverse())),Is.EqualTo(key));
        Assert.Throws<ArgumentException>(() => SecretSharingEngine.Restore(new SecretSet(d,set.Shares.Skip(1))));
    }
    [Test]
    public void ImmutableBoundariesAndInvalidContainers()
    {
        byte[] input=[1,2]; var share=new SecretShare(255,input); input[0]=0;
        var value=share.Value; value[0]=0; Assert.That(share.Value,Is.EqualTo(new byte[]{1,2}));
        var d=new SecretDescriptor(SecretDescriptor.Shamir,3,2,16);
        var list=new List<SecretShare>{share}; var set=new SecretSet(d,list); list.Clear();
        Assert.That(set.Shares,Has.Count.EqualTo(1));
        Assert.Throws<ArgumentException>(()=>new SecretSet(d,[share,share]));
        Assert.Throws<ArgumentException>(()=>new SecretSet(d,[new SecretShare(1,[0])]));
        Assert.Throws<ArgumentException>(()=>new SecretSet(d,[share],"bad"));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SecretShare(0,[1]));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SecretShare(256,[1]));
        Assert.Throws<ArgumentException>(()=>new SecretShare(1,[]));
        Assert.Throws<ArgumentException>(()=>new SecretDescriptor(SecretDescriptor.Split,3,2,8));
        Assert.Throws<ArgumentException>(()=>new SecretDescriptor(SecretDescriptor.Split,3,3,8,SecretDescriptor.Profile11B));
        Assert.Throws<ArgumentException>(()=>new SecretDescriptor(SecretDescriptor.Shamir,3,2,8,"AES"));
        Assert.Throws<ArgumentException>(()=>new SecretDescriptor(SecretDescriptor.Shamir,256,2,8));
        Assert.Throws<ArgumentException>(()=>new SecretDescriptor(SecretDescriptor.Shamir,2,1,8));
        Assert.Throws<ArgumentException>(()=>new SecretDescriptor(SecretDescriptor.Shamir,2,2,7));
        Assert.Throws<ArgumentException>(()=>SecretSharingEngine.Restore(new SecretSet(d,[])));
    }

    [TestCase("X-Y", "11B")] [TestCase("Y-X", "11B")] [TestCase("SEPARATE-X", "11B")]
    [TestCase("X-Y", "11D")] [TestCase("Y-X", "11D")] [TestCase("SEPARATE-X", "11D")]
    [TestCase("X-Y", "XOR")] [TestCase("Y-X", "XOR")] [TestCase("SEPARATE-X", "XOR")]
    public void ScriptAndPrintRoundTrip(string format,string profile)
    {
        string parameters=profile=="XOR" ? "SECRET_SPLIT, #PARTS: 5" : $"SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 3, #POLYNOM: \"SHAMIR-GF256-{profile}\"";
        Run($"KEY k=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F)) PARAM p=Parameters({parameters}, #SHAREFORMAT: \"{format}\") SECRET s=Share(p,k) KEY restored=Restore(s)");
        Assert.That(Get("restored").Value,Is.EqualTo(Get("k").Value).IgnoreCase);
        Assert.That(((KeyVariableDeclaration)Get("restored")).KeyType.Algorithm,Is.EqualTo(KeyAlgorithm.Aes));
        string? output=null; int events=0; void Capture(string text) {output=text;events++;}
        OutputOperations.PrintEvent+=Capture;
        try {Run("Print(s)");} finally {OutputOperations.PrintEvent-=Capture;}
        Assert.That(events,Is.EqualTo(1));
        Assert.That(output,Does.Contain("AvailableShares: 5").And.Contain("KeySizeBits: 128"));
        var source=(SecretVariableDeclaration)Get("s");
        foreach(var share in source.SecretSet.Shares) Assert.That(output,Does.Contain(Convert.ToHexString(share.Value)));
        string script=string.Join("\n",output!.Split('\n').Select(l=>l.TrimEnd('\r')).Where(l=>l.StartsWith("out: PARAM")||l.StartsWith("out: SECRET")).Select(l=>l[5..]));
        Run(script+" KEY external=Restore(importedSecret)");
        var external=(KeyVariableDeclaration)Get("external");
        Assert.That(external.Value,Is.EqualTo(Get("k").Value).IgnoreCase);
        Assert.That(external.KeyType,Is.EqualTo(KeyType.Secret(KeyAlgorithm.Unknown)));
        Assert.That(external.Usage,Is.EqualTo(KeyUsagePolicy.Unspecified));
        Assert.That(external.KeyAttributes,Is.Empty);
        Assert.That(external.KeySizeInBits.Bits,Is.EqualTo(128));
    }
    [Test]
    public void MetadataSnapshotIsIndependentAndRestoreNeedsNoParam()
    {
        Run("KEY k=GenerateKey(AES-ECB,128) PARAM p=Parameters(SECRET_SHAMIR,#PARTS:3,#MINPARTS:2)");
        var key=(KeyVariableDeclaration)Get("k"); key.Usage=new(KeyUsageMode.Restricted,KeyUsage.Encrypt);
        key.KeyAttributes.Add(new OptionalBlock{ID="AA",Length=4,Data="original"});
        Run("SECRET s=Share(p,k)");
        var source=(SecretVariableDeclaration)Get("s");
        Assert.That(source.SecretSet.Descriptor.Profile,Is.EqualTo(SecretDescriptor.Profile11B));
        key.KeyAttributes[0].Data="changed"; key.Usage=KeyUsagePolicy.Unspecified; key.KeyType=KeyType.Secret(KeyAlgorithm.Unknown);
        VariableDictionary.Instance().Clear();VariableDictionary.Instance().Add(source);
        Run("KEY restored=Restore(s)");
        var result=(KeyVariableDeclaration)Get("restored");
        Assert.That(result.KeyType.Algorithm,Is.EqualTo(KeyAlgorithm.Aes));
        Assert.That(result.Usage.AllowedUsages,Is.EqualTo(KeyUsage.Encrypt));
        Assert.That(result.KeyAttributes[0].Data,Is.EqualTo("original"));
        result.KeyAttributes[0].Data="again";Run("KEY second=Restore(s)");
        Assert.That(((KeyVariableDeclaration)Get("second")).KeyAttributes[0].Data,Is.EqualTo("original"));
    }
    [Test]
    public void LiteralKeysAndIncompleteImportAreSupported()
    {
        Run("KEY k=0x(00FF) PARAM p=#MECH:SECRET_SPLIT #PARTS:2 SECRET s=Share(p,k) KEY r=Restore(s)");
        Assert.That(Get("r").Value,Is.EqualTo("0x(00ff)"));
        Run("PARAM p=Parameters(SECRET_SHAMIR,#PARTS:5,#MINPARTS:3) SECRET s=Secret(p,0x(FFAA))");
        Assert.That(((SecretVariableDeclaration)Get("s")).SecretSet.Shares.Count,Is.EqualTo(1));
        Run("KEY r=Restore(s)",true);
    }

    [Test]
    public void Base64KeyMaterialUsesTheExistingBinaryKeyContract()
    {
        Run("KEY k=b64(AP8=) PARAM p=Parameters(SECRET_SPLIT,#PARTS:2) SECRET s=Share(p,k) KEY r=Restore(s)");
        Assert.That(Get("r").Value,Is.EqualTo("0x(00ff)"));
        Assert.That(Get("k").Value,Is.EqualTo("b64(AP8=)"));
    }

    [TestCase("#POLYNOM: \"AES\"")] [TestCase("#POLYNOM: \"SHAMIR-GF256-11b\"")]
    [TestCase("#POLYNOM: 123")] [TestCase("#SHAREFORMAT: \"XY\"")] [TestCase("#SHAREFORMAT: 1")]
    public void InvalidProfileAndFormatAreSyntaxErrors(string parameter)
    {
        var parser=ParserBuilder.StringBuild($"PARAM p=Parameters(SECRET_SHAMIR,#PARTS:3,#MINPARTS:2,{parameter})");
        parser.program(); Assert.That(parser.NumberOfSyntaxErrors,Is.GreaterThan(0));
    }
    [TestCase("SECRET_SPLIT,#PARTS:1")] [TestCase("SECRET_SPLIT,#PARTS:256")]
    [TestCase("SECRET_SPLIT,#PARTS:2,#MINPARTS:2")] [TestCase("SECRET_SPLIT,#PARTS:2,#POLYNOM:\"SHAMIR-GF256-11B\"")]
    [TestCase("SECRET_SHAMIR,#PARTS:3")] [TestCase("SECRET_SHAMIR,#PARTS:3,#MINPARTS:4")]
    [TestCase("SECRET_SHAMIR,#PARTS:3,#MINPARTS:1")] [TestCase("SECRET_SPLIT,#PARTS:2,#PARTS:3")]
    [TestCase("SECRET_SPLIT,#PARTS:2,#IV:0x(00)")]
    public void InvalidParameterCombinationsFail(string parameters) => Run($"PARAM p=Parameters({parameters})",true);
    [TestCase("0x(00AA),0x(02BB)")] [TestCase("0x(01AA),0x(01BB)")]
    [TestCase("0x(01AA),0x(02BBBB)")] [TestCase("0x(01),0x(02BB)")]
    [TestCase("0x(01A),0x(02BB)")] [TestCase("\"01AA\",0x(02BB)")]
    public void MalformedShareInputsFail(string shares)
    {
        Run("PARAM p=Parameters(SECRET_SHAMIR,#PARTS:3,#MINPARTS:2)");Run($"SECRET s=Secret(p,{shares})",true);
    }
    [TestCase("1,0x(AA),3")] [TestCase("0x(AA),1")] [TestCase("0,0x(AA)")]
    [TestCase("256,0x(AA)")] [TestCase("1,0x(AA),1,0x(BB)")]
    public void SeparateIndexRequiresStrictPairs(string shares)
    {
        Run("PARAM p=Parameters(SECRET_SHAMIR,#PARTS:3,#MINPARTS:2,#SHAREFORMAT:\"SEPARATE-X\")");
        Run($"SECRET s=Secret(p,{shares})",true);
    }

    private static string Evidence => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,"../../../../docs/architecture/evidence/G03"));
    [Test]
    public void OriginalJavaVectorsReconstructThroughParserInEveryFormat()
    {
        var groups=File.ReadAllLines(Path.Combine(Evidence,"java-vectors.txt")).Where(l=>!string.IsNullOrWhiteSpace(l)).Select(l=>l.Split('|')).GroupBy(f=>f[0]);
        Assert.That(groups.Count(),Is.EqualTo(2));
        foreach(var group in groups) foreach(string format in new[]{"X-Y","Y-X","SEPARATE-X"})
        {
            var rows=group.Reverse().ToArray();
            string shares=string.Join(",",rows.Select(f=> format=="X-Y" ? $"0x({int.Parse(f[4]):X2}{f[5]})" : format=="Y-X" ? $"0x({f[5]}{int.Parse(f[4]):X2})" : $"{f[4]},0x({f[5]})"));
            Run($"PARAM p=Parameters(SECRET_SHAMIR,#PARTS:255,#MINPARTS:3,#POLYNOM:\"SHAMIR-GF256-{group.Key}\",#SHAREFORMAT:\"{format}\") SECRET s=Secret(p,{shares}) KEY r=Restore(s)");
            Assert.That(Get("r").Value,Is.EqualTo($"0x({rows[0][1]})").IgnoreCase);
        }
    }
    [Test]
    public void ExportCSharpSharesForOriginalJavaVerification()
    {
        var lines=new List<string>();
        foreach(string profile in new[]{SecretDescriptor.Profile11B,SecretDescriptor.Profile11D})
        foreach(int length in new[]{1,16,24,32,64})
        {
            byte[] key=Enumerable.Range(0,length).Select(i=>(byte)(i*127)).ToArray();
            var set=SecretSharingEngine.Share(new(SecretDescriptor.Shamir,255,3,length*8,profile),key);
            foreach(int[] xs in new[]{new[]{255,17,3},new[]{128,1,3,17,255}})
                lines.Add(profile[^3..]+"|"+Convert.ToHexString(key)+"|"+string.Join("|",xs.Select(x=>$"{x}:{Convert.ToHexString(set.Shares[x-1].Value)}")));
        }
        File.WriteAllLines(Path.Combine(TestContext.CurrentContext.WorkDirectory,"g03-csharp-vectors.txt"),lines);
        Assert.That(lines,Has.Count.EqualTo(20));
    }

    [Test]
    public void ProgrammaticParametersAndTypedObjectIdentityAreEnforced()
    {
        Run("PARAM p=Parameters(SECRET_SHAMIR,#PARTS:3,#MINPARTS:2) KEY k=GenerateKey(AES-ECB,128)");
        var p=(ParameterVariableDeclaration)Get("p"); var k=(KeyVariableDeclaration)Get("k");
        var invocation=new OperationInvocation([new(p.Value,ResolvedCallArgumentKind.Variable,p),new(k.Value,ResolvedCallArgumentKind.Variable,k)]);
        VariableDictionary.Instance().Clear(); // Objects, not lookup by strings, remain authoritative.
        var secret=(SecretVariableDeclaration)SecretOperations.Share(invocation);
        Assert.That(SecretSharingEngine.Restore(secret.SecretSet),Is.EqualTo(FormatConversions.HexStringToByteArray(k.Value)));
        foreach(string bad in new[]{"\"AES\"","\"\"","SHAMIR-GF256-11B"})
        {
            p.SetParameter("#POLYNOM",bad);
            Assert.Throws<ArgumentException>(()=>SecretOperations.Share(invocation));
        }
        p.SetParameter("#POLYNOM","\"SHAMIR-GF256-11B\"");p.SetParameter("#SHAREFORMAT","bad");
        Assert.Throws<ArgumentException>(()=>SecretOperations.Share(invocation));
        Assert.Throws<ArgumentException>(()=>SecretOperations.Share(new OperationInvocation([new(p.Value,ResolvedCallArgumentKind.OtherLiteral),new(k.Value,ResolvedCallArgumentKind.OtherLiteral)])));
    }

    [Test]
    public void DocumentationLinksCaseAndExamplesAreExecutable()
    {
        var provider=new FileInfoDocumentationProvider();
        var entries=InfoDocumentCatalog.Entries.Where(e=>e.MarkdownFileName.Contains("SECRET",StringComparison.Ordinal)||
            e.DocumentId.Function is "Share" or "Secret" or "Restore").ToArray();
        Assert.That(entries,Has.Length.EqualTo(12));
        string directory=Path.Combine(AppContext.BaseDirectory,"InfoDocs");
        var exactNames=Directory.GetFiles(directory).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        foreach(var entry in entries)
        {
            Assert.That(exactNames.Contains(entry.MarkdownFileName),Is.True);
            Assert.That(provider.TryGetDocument(entry.DocumentId,out string text),Is.True);
            string uri=InfoDocumentUri.ToCanonicalString(entry.DocumentId);
            Assert.That(InfoDocumentUri.TryParse(uri,out var id),Is.True);Assert.That(id,Is.EqualTo(entry.DocumentId));
            foreach(Match link in Regex.Matches(text,@"cryptoscript-info://[^)\s]+"))
            {
                Assert.That(InfoDocumentUri.TryParse(link.Value,out var linked),Is.True,link.Value);
                Assert.That(provider.HasDocument(linked!),Is.True,link.Value);
            }
            foreach(Match example in Regex.Matches(text,@"```cryptoscript\s+(.*?)```",RegexOptions.Singleline))
            { Reset();Run(example.Groups[1].Value); }
        }
        provider.TryGetDocument(InfoDocumentId.CreateType("SECRET"),out string printDoc);
        foreach(Match block in Regex.Matches(printDoc,@"```text\s+(.*?)```",RegexOptions.Singleline))
        {
            string script=string.Join("\n",block.Groups[1].Value.Split('\n').Where(l=>l.StartsWith("out: PARAM")||l.StartsWith("out: SECRET")).Select(l=>l[5..]));
            Reset();Run(script+" KEY r=Restore(importedSecret)");
            Assert.That(Get("r").Value,Is.EqualTo("0x(aabb)"));
        }
    }

    [TestCase(SecretDescriptor.Profile11B)] [TestCase(SecretDescriptor.Profile11D)]
    public void ThresholdBoundaryZeroSecretAndHomomorphism(string profile)
    {
        var d=new SecretDescriptor(SecretDescriptor.Shamir,255,255,8,profile);
        var zero=SecretSharingEngine.Share(d,new byte[1]);
        Assert.That(SecretSharingEngine.Restore(zero),Is.EqualTo(new byte[1]));
        Assert.Throws<ArgumentException>(()=>SecretSharingEngine.Restore(new SecretSet(d,zero.Shares.Skip(1))));
        d=new SecretDescriptor(SecretDescriptor.Shamir,5,3,8,profile);
        var a=SecretSharingEngine.Share(d,new byte[]{0x55});var b=SecretSharingEngine.Share(d,new byte[]{0xAA});
        var sum=new SecretSet(d,a.Shares.Zip(b.Shares,(x,y)=>new SecretShare(x.X,new byte[]{(byte)(x.Value[0]^y.Value[0])})));
        Assert.That(SecretSharingEngine.Restore(sum),Is.EqualTo(new byte[]{255}));
        var gf=new Gf256(d.ReductionPolynomial!.Value);
        // For a fixed nonzero point and any secret, all 256 random coefficients give all 256 possible shares.
        foreach(int secret in new[]{0,1,128,255})
            Assert.That(Enumerable.Range(0,256).Select(c=>(byte)(secret^gf.Multiply((byte)c,17))).Distinct().Count(),Is.EqualTo(256));
    }

    [Test]
    public void RuntimeFailuresRedactMaterialAndCannotCreateFakeSecrets()
    {
        Run("SECRET s=0x(11223344)",true);
        Run("PARAM p=Parameters(SECRET_SHAMIR,#PARTS:3,#MINPARTS:2)");
        var parser=ParserBuilder.StringBuild("SECRET s=Secret(p,0x(001122334455))");
        var error=Assert.Throws<SemanticErrorException>(()=>new CryptoScriptRunner().Execute(parser.program()));
        Assert.That(error!.SemanticError!.FunctionCall,Is.EqualTo("Secret(<redacted>)"));
        Assert.That(error.SemanticError.Message,Does.Not.Contain("1122334455"));
        Run("PARAM bad=#MECH:SECRET_SPLIT #PARTS:2 #MINPARTS:2",true);
        Run("PARAM bad=#MECH:SECRET_SHAMIR #PARTS:3 #MINPARTS:4",true);
    }

    [TestCase(8)] [TestCase(16)] [TestCase(24)] [TestCase(32)] [TestCase(64)]
    public void ExternalKeyLengthNeverInfersAlgorithmOrUsage(int length)
    {
        string zeros=new('0',length*2), ones=new('F',length*2);
        Run($"PARAM p=Parameters(SECRET_SPLIT,#PARTS:2) SECRET s=Secret(p,0x(FF{zeros}),0x(80{ones})) KEY r=Restore(s)");
        var key=(KeyVariableDeclaration)Get("r");
        Assert.That(key.KeyType,Is.EqualTo(KeyType.Secret(KeyAlgorithm.Unknown)));
        Assert.That(key.Usage,Is.EqualTo(KeyUsagePolicy.Unspecified));
        Assert.That(key.KeyAttributes,Is.Empty);
        Assert.That(key.KeySizeInBits.Bits,Is.EqualTo(length*8));
    }

    [Test]
    public void SecretImportSyntaxDiagnosticsRedactBeforePublication(
        [Values("X-Y", "Y-X", "SEPARATE-X")] string format,
        [Values("comma", "invalid", "unfinished")] string damage)
    {
        string payload = format == "X-Y" ? "02DDEEFF" : format == "Y-X" ? "DDEEFF02" : "DDEEFF";
        string head = format == "SEPARATE-X" ? "SECRET s=Secret(p,1,0x(AABBCC),2" : "SECRET s=Secret(p,0x(01AABBCC)";
        head += damage == "comma" ? " " : ",";
        string tail = damage == "invalid" ? $"0x({payload}GG))" : damage == "unfinished" ? $"0x({payload}" : $"0x({payload}))";
        string script = $"PARAM p=Parameters(SECRET_SHAMIR,#PARTS:3,#MINPARTS:2,#SHAREFORMAT:\"{format}\")\r\n" + head + tail;
        var parser = ParserBuilder.StringBuild(script);
        parser.program();
        var errors = SyntaxErrorList.Instance().ToArray();
        Assert.That(errors, Is.Not.Empty);
        Assert.That(errors[0].Line, Is.EqualTo("2"));
        // With malformed HEX, ANTLR can first tokenize the leading 0 as INT;
        // its first parser diagnostic then points to x, not to the leading 0.
        Assert.That(errors[0].Column, Is.EqualTo((head.Length + (damage == "comma" ? 0 : 1)).ToString()));
        Assert.That(parser.TokenStream.TokenSource.InputStream.ToString(), Is.EqualTo(script));
        foreach (var error in errors)
        {
            Assert.That(error.Symbol, Does.Not.Contain("DDEEFF").And.Not.Contain("AABBCC"));
            Assert.That(error.Message, Does.Not.Contain("DDEEFF").And.Not.Contain("AABBCC"));
        }
        Assert.That(SyntaxErrorListner.ErrorMessage.ToString(), Does.Contain("Secret import").And.Not.Contain("DDEEFF").And.Not.Contain("AABBCC"));
        Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.True);
    }

    [TestCase("")]
    [TestCase("SECRET s=Secret(p,0x(01AA)) ")]
    [TestCase("Print(\"Secret(p,0x(AA)\") ")]
    [TestCase("SecretOther(p,0x(AA)) ")]
    public void SyntaxDiagnosticsOutsideSecretImportsRemainUnchanged(string prefix)
    {
        string script = prefix + "VAR v=0x(00) 0x(DDEEFF)";
        var parser = ParserBuilder.StringBuild(script);
        parser.program();
        var error = SyntaxErrorList.Instance().OfType<ParserError>().Single();
        Assert.That(error.Symbol, Is.EqualTo("0x(DDEEFF)"));
        Assert.That(error.Message, Does.Contain("0x(DDEEFF)").And.Not.Contain("redacted"));
        Assert.That(error.Line, Is.EqualTo("1"));
        Assert.That(error.Column, Is.EqualTo(script.IndexOf("0x(DDEEFF)", StringComparison.Ordinal).ToString()));
    }

    [TestCase(SecretDescriptor.Profile11B)] [TestCase(SecretDescriptor.Profile11D)]
    public void ExactlyThresholdDoesNotAuthenticateButAnyContradictoryExtraPointFails(string profile)
    {
        var d=new SecretDescriptor(SecretDescriptor.Shamir,5,3,8,profile);
        var set=SecretSharingEngine.Share(d,new byte[]{42});
        var changed=new SecretShare(set.Shares[0].X,new byte[]{(byte)(set.Shares[0].Value[0]^1)});
        var exact=new SecretSet(d,new[]{changed,set.Shares[1],set.Shares[2]});
        Assert.That(SecretSharingEngine.Restore(exact),Is.Not.EqualTo(new byte[]{42}));
        Assert.Throws<ArgumentException>(()=>SecretSharingEngine.Restore(new SecretSet(d,new[]{changed}.Concat(set.Shares.Skip(1)))));
    }

    [Test]
    public void SecretImportLexerFailurePreservesPositionWithoutPublishingMaterial()
    {
        string line = "SECRET s=Secret(p,0x(01AABBCC),0x(DDEEFF@))";
        ParserBuilder.StringBuild("\n" + line).program();
        var error = SyntaxErrorList.Instance().OfType<LexerError>().Single();
        Assert.That(error.Line, Is.EqualTo("2"));
        Assert.That(error.Column, Is.EqualTo(line.IndexOf('@').ToString()));
        Assert.That(error.Symbol, Is.EqualTo("<redacted>"));
        Assert.That(error.Message, Does.Contain("Secret import").And.Not.Contain("DDEEFF"));
        Assert.That(SyntaxErrorListner.ErrorMessage.ToString(), Does.Not.Contain("DDEEFF").And.Not.Contain("AABBCC"));
    }

    [TestCase("C:\\Secret(p,0x(AA)")]
    [TestCase("/Secret(p,0x(AA)")]
    [TestCase("\\\\server\\Secret(p,0x(AA)")]
    public void PathContentsDoNotIntroduceSecretDiagnosticScope(string path)
    {
        ParserBuilder.StringBuild($"PATH p={path}\nVAR v=0x(00) 0x(DDEEFF)").program();
        var error = SyntaxErrorList.Instance().OfType<ParserError>().Single();
        Assert.That(error.Symbol, Is.EqualTo("0x(DDEEFF)"));
        Assert.That(error.Line, Is.EqualTo("2"));
    }

    [TestCase("VAR v=\"bad\\q SECRET s=Secret(p,0x(01AABBCC) 0x(02DDEEFF))")]
    [TestCase("VAR v=\"bad\\z Print(Secret(p,0x(01AABBCC) 0x(02DDEEFF)))")]
    [TestCase("Secret(p,0x(01AA),0x(02BB))\nVAR v=\"bad\\q Secret(p,\n0x(01AABBCC)\n0x(02DDEEFF))")]
    [TestCase("VAR v=\"bad\\q SECRET s=Secret(p,\n0x(01AABBCC),0x(02DDEEFF")]
    public void LexerRecoveryCannotHideActualSecretCalls(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        parser.program();
        Assert.That(SyntaxErrorList.Instance().OfType<LexerError>(), Is.Not.Empty);
        Assert.That(SyntaxErrorList.Instance().OfType<ParserError>(), Is.Not.Empty);
        foreach (var error in SyntaxErrorList.Instance())
        {
            Assert.That(error.Symbol + error.Message, Does.Not.Contain("AABBCC").And.Not.Contain("DDEEFF"));
        }
        Assert.That(SyntaxErrorListner.ErrorMessage.ToString(),
            Does.Contain("Secret import").And.Not.Contain("AABBCC").And.Not.Contain("DDEEFF"));
        Assert.That(parser.TokenStream.TokenSource.InputStream.ToString(), Is.EqualTo(script));
    }

    [Test]
    public void LexerDiagnosticUsesItsAbsoluteFailureRegion(
        [Values("\n", "\r\n")] string newline, [Values(0, 1, 21, 50)] int column,
        [Values("SECRET s=Secret(p,0x(01AABBCC),0x(02DDEEFF))", "VAR v=0x(11223344)")] string firstLine)
    {
        string script = firstLine + newline + new string(' ', column) + "@";
        ParserBuilder.StringBuild(script).program();
        var error = SyntaxErrorList.Instance().OfType<LexerError>().Single();
        Assert.That(error.Line, Is.EqualTo("2"));
        Assert.That(error.Column, Is.EqualTo(column.ToString()));
        Assert.That(error.Symbol, Is.EqualTo("@"));
        Assert.That(error.Message, Is.EqualTo("token recognition error at: '@'"));
        Assert.That(SyntaxErrorListner.ErrorMessage.ToString(), Does.Not.Contain("AABBCC").And.Not.Contain("DDEEFF"));
    }

    [TestCase("VAR v=0x(11223344)\nVAR w=\"bad\\q", "\"bad\\q", 6)]
    [TestCase("VAR v=0x(11223344)\r\nVAR w=\"unfinished", "\"unfinished", 6)]
    [TestCase("VAR v=0x(11223344)\n@", "@", 0)]
    public void LexerDiagnosticCopiesOnlyConsumedFailureIncludingEof(string script, string expected, int column)
    {
        ParserBuilder.StringBuild(script).program();
        var error = SyntaxErrorList.Instance().OfType<LexerError>().Single();
        Assert.That(error.Symbol, Is.EqualTo(expected));
        Assert.That(error.Line, Is.EqualTo("2"));
        Assert.That(error.Column, Is.EqualTo(column.ToString()));
    }

    [TestCase("VAR v=\"Secret(p,0x(AABBCC))\" ")]
    [TestCase("VAR v=\"escaped\\\" Secret(p,0x(AABBCC))\" ")]
    [TestCase("Secret(p,0x(01AA),0x(02BB)) Print(\"Secret(p\") ")]
    public void ActualLexerStringTokensAndCompletedCallsDoNotExtendRedaction(string prefix)
    {
        ParserBuilder.StringBuild(prefix + "VAR w=0x(00) 0x(DDEEFF)").program();
        var error = SyntaxErrorList.Instance().OfType<ParserError>().Single();
        Assert.That(error.Symbol, Is.EqualTo("0x(DDEEFF)"));
        Assert.That(error.Message, Does.Contain("DDEEFF"));
    }

    [TestCase("VAR v=\"bad\\q SECRET s=Secret(p,0x(01AABBCC) 0x(02DDEEFF))", true)]
    [TestCase("SECRET s=Secret(p,0x(01AABBCC) 0x(02DDEEFF))", true)]
    [TestCase("SECRET s=Secret(p,0x(01AABBCC),0x(DDEEFF@))", true)]
    [TestCase("VAR v=0x(00) 0x(DDEEFF)", false)]
    public void FileParserRetainsConsoleDiagnosticsWithScopedRedaction(string script, bool sensitive)
    {
        string path = Path.GetTempFileName();
        var previous = Console.Error;
        using var output = new StringWriter();
        try
        {
            File.WriteAllText(path, script);
            Console.SetError(output);
            ParserBuilder.FileBuild(path).program();
            Assert.That(output.ToString(), Does.Contain("line 1:"));
            if (sensitive) Assert.That(output.ToString(), Does.Not.Contain("DDEEFF").And.Not.Contain("AABBCC").And.Contain("Secret import"));
            else Assert.That(output.ToString(), Does.Contain("DDEEFF"));
        }
        finally { Console.SetError(previous); File.Delete(path); }
    }
}
