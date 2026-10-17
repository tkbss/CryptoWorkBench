using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.EP2;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Ep2MacTests
{
    private const string Key = "0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345";
    private const string Data = "11112222333344445555";
    private const string Message = "EP2-MESSAGE-MAC";
    private const string Certificate = "EP2-TRANSACTION-CERTIFICATE";

    [SetUp]
    public void Reset()
    {
        VariableDictionary.Instance().Clear();
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.SyntaxErrorOccured = false;
    }

    private static CryptoScriptRunner Run(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var tree = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        var runner = new CryptoScriptRunner();
        runner.Execute(tree);
        return runner;
    }

    private static string Prefix(string mechanism, bool direct = false) =>
        $"KEY k=GenerateKey(HMAC-SHA256,0x({Key})) VAR d=0x({Data}) " +
        (direct ? $"PARAM p=#MECH:{mechanism} " : $"PARAM p=Parameters({mechanism}) ");

    private static string Value(string name = "result") => VariableDictionary.Instance().Get(name).Value;

    [TestCase(Message, "96B632C95451623B")]
    [TestCase(Certificate, "6FD9A4429C6348AA")]
    public void OfficialVectorsThroughCoreAndPublicRunner(string mechanism, string expected)
    {
        Assert.That(Convert.ToHexString(Ep2MacPrimitive.Compute(Convert.FromHexString(Key),
            Convert.FromHexString(Data), mechanism == Message)), Is.EqualTo(expected));
        foreach (bool direct in new[] { false, true })
        foreach (bool literal in new[] { false, true })
        {
            Reset();
            var runner = Run(Prefix(mechanism, direct) + $"VAR result=Mac(p,k,{(literal ? "0x(" + Data + ")" : "d")})");
            Assert.That(Value(), Is.EqualTo($"0x({expected})").IgnoreCase);
            Assert.That(VariableDictionary.Instance().Get("result"), Is.TypeOf<StringVariableDeclaration>());
            Assert.That(runner.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void BoundaryLengthsEmptyBytesAndRepeatedCallsMatchIndependentDotNetOracle()
    {
        byte[] key = Convert.FromHexString(Key);
        foreach (int length in new[] { 0, 1, 31, 32, 55, 56, 63, 64, 65, 127, 128, 129, 1024 })
        {
            byte[] data = Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();
            foreach (bool prehash in new[] { false, true })
            {
                byte[] expected = HMACSHA256.HashData(key, prehash ? SHA256.HashData(data) : data)[..8];
                for (int call = 0; call < 3; call++)
                    Assert.That(Ep2MacPrimitive.Compute(key, data, prehash), Is.EqualTo(expected));
            }
            Assert.That(data, Is.EqualTo(Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray()));
        }
        Assert.That(Convert.ToHexString(key), Is.EqualTo(Key));
    }

    [TestCase("3C613E203C2F613E")]
    [TestCase("3C613E0A3C2F613E")]
    [TestCase("3C613E0D0A3C2F613E")]
    [TestCase("003C612F3E00")]
    public void MessageInputIsByteExact(string bytes)
    {
        Run(Prefix(Message) + $"VAR result=Mac(p,k,0x({bytes}))");
        var expected = HMACSHA256.HashData(Convert.FromHexString(Key), SHA256.HashData(Convert.FromHexString(bytes)))[..8];
        Assert.That(Value(), Is.EqualTo($"0x({Convert.ToHexString(expected)})").IgnoreCase);
    }

    [TestCase(Message)]
    [TestCase(Certificate)]
    public void StrictStructuredArgumentsAndLegacyCalls(string mechanism)
    {
        Run(Prefix(mechanism));
        var algorithm = AlgorithmFactory.Create(mechanism);
        AlgorithmCallArgument Ref(string id) => new(Value(id), VariableDictionary.Instance().Get(id), ResolvedCallArgumentKind.Variable);
        var valid = new[] { Ref("p"), Ref("k"), Ref("d") };
        Assert.DoesNotThrow(() => algorithm.Mac(new AlgorithmCallArguments(valid)));
        Assert.Throws<ArgumentException>(() => algorithm.Mac(valid.Select(a => a.Value!).ToArray()));
        Assert.Throws<ArgumentException>(() => algorithm.Mac(new AlgorithmCallArguments(valid.Take(2))));
        Assert.Throws<ArgumentException>(() => algorithm.Mac(new AlgorithmCallArguments(new[] { valid[1], valid[0], valid[2] })));
        for (int position = 0; position < 3; position++)
        {
            foreach (var replacement in new[] {
                new AlgorithmCallArgument(valid[position].Value),
                new AlgorithmCallArgument(valid[position].Value, valid[position].SourceVariable, ResolvedCallArgumentKind.NestedFunctionCall),
                new AlgorithmCallArgument(valid[position].Value, valid[position].SourceVariable, ResolvedCallArgumentKind.Variable) { HasReconstructedSourceVariable = true }
            })
            {
                var invalid = valid.ToArray(); invalid[position] = replacement;
                Assert.Throws<ArgumentException>(() => algorithm.Mac(new AlgorithmCallArguments(invalid)));
            }
        }
        foreach (string malformed in new[] { "0x(0)", "0x(GG)", "00", "\"secret-data\"" })
        {
            var invalid = valid.ToArray(); invalid[2] = new(malformed, null, ResolvedCallArgumentKind.HexLiteral);
            var error = Assert.Throws<ArgumentException>(() => algorithm.Mac(new AlgorithmCallArguments(invalid)));
            Assert.That(error!.Message, Does.Not.Contain(malformed));
        }
        var empty = valid.ToArray(); empty[2] = new("0x()", null, ResolvedCallArgumentKind.HexLiteral);
        Assert.DoesNotThrow(() => algorithm.Mac(new AlgorithmCallArguments(empty)));
        var key = (KeyVariableDeclaration)valid[1].SourceVariable!;
        foreach (int size in new[] { 0, 16, 24, 31, 33 })
        {
            key.KeyValue = "0x(" + new string('0', size * 2) + ")";
            Assert.Throws<ArgumentException>(() => algorithm.Mac(new AlgorithmCallArguments(valid)));
            Assert.Throws<ArgumentException>(() => Ep2MacPrimitive.Compute(new byte[size], Array.Empty<byte>(), false));
        }
        key.KeyValue = "0x(GG)";
        Assert.Throws<ArgumentException>(() => algorithm.Mac(new AlgorithmCallArguments(valid)));
    }

    [TestCase(Message)]
    [TestCase(Certificate)]
    public void PublicRunnerRejectsWrongArgumentFormsAndStops(string mechanism)
    {
        foreach (string call in new[] {
            "Mac(p, d, d)", "Mac(p, 0x(" + Key + "), d)", "Mac(p,k,k)", "Mac(p,k,p)",
            "Mac(p,k,\"text\")", "Mac(p,k,b64(AA==))", "Mac(p,k,1)", "Mac(k,p,d)",
            "Mac(p,k,Mac(p,k,d))", "Mac(p,GenerateKey(HMAC-SHA256,256),d)",
            $"Mac(Parameters({mechanism}),k,d)", $"Mac({mechanism},k,d)", "Mac(p,k)", "Mac(p,k,d,d)"
        })
        {
            Reset();
            var parser = ParserBuilder.StringBuild(Prefix(mechanism) + $"VAR result={call} VAR after=0x(AA)");
            var tree = parser.program();
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero, call);
            Assert.Throws<SemanticErrorException>(() => new CryptoScriptRunner().Execute(tree), call);
            Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        }
    }

    [TestCase(Message)]
    [TestCase(Certificate)]
    public void ParameterAndFunctionContracts(string mechanism)
    {
        foreach (string extra in new[] { "#MECH:" + mechanism, "#HASH:HASH-SHA256", "#MACLEN:8", "#PAD:NONE", "#IV:0x(00)", "#SALT:0x(00)", "#VARIANT:TC" })
        foreach (bool direct in new[] { false, true })
        {
            Reset();
            string declaration = direct ? $"PARAM q=#MECH:{mechanism} {extra} " : $"PARAM q=Parameters({mechanism},{extra}) ";
            Assert.Throws<SemanticErrorException>(() => Run(Prefix(mechanism) + declaration + "VAR result=Mac(q,k,d)"), extra);
        }
        Reset(); Run(Prefix(mechanism));
        var p = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        var entry = MechanismRegistry.Entries.Single(e => e.CanonicalName == mechanism);
        Assert.That(entry.SupportedFunctions, Is.EquivalentTo(new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Mac }));
        foreach (var function in Enum.GetValues<CryptoScriptFunction>().Except(entry.SupportedFunctions))
            Assert.Throws<FunctionContractException>(() => MechanismParameterContractValidator.Validate(mechanism, function, p));
        p.SetParameter("#MECH", mechanism == Message ? Certificate : Message);
        Assert.Throws<SemanticErrorException>(() => Run("VAR result=Mac(p,k,d)"));
    }

    [TestCase(Message)]
    [TestCase(Certificate)]
    public void OriginalKeyMetadataWarningsAreNonBlockingAndNotDuplicated(string mechanism)
    {
        Run(Prefix(mechanism) + $"KEY other=GenerateKey(HMAC-SHA256,0x({Key}))");
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("k");
        key.KeyType = KeyType.Secret(KeyAlgorithm.Aes);
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt);
        string before = key.Serialize();
        var runner = Run("VAR result=Mac(p,k,d) VAR clean=Mac(p,other,d)");
        Assert.That(Value(), Is.EqualTo(Value("clean")));
        Assert.That(key.Serialize(), Is.EqualTo(before));
        Assert.That(runner.Diagnostics.Select(x => x.DiagnosticCode), Is.EqualTo(new[] {
            SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict }));
        Assert.That(runner.Diagnostics.All(x => x.Severity == SemanticDiagnosticSeverity.Warning && x.Identifier == "k"), Is.True);
        Assert.That(string.Join(" ",runner.Diagnostics.Select(x => x.Message)), Does.Not.Contain(Key).And.Not.Contain(Data));
        key.KeyValue = "0x(11)";
        var parser = ParserBuilder.StringBuild("VAR failed=Mac(p,k,d)");
        runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(parser.program()));
        Assert.That(runner.Diagnostics.Count(x => x.Severity == SemanticDiagnosticSeverity.Warning), Is.EqualTo(2));
        Assert.That(runner.Diagnostics.Count(x => x.Severity == SemanticDiagnosticSeverity.Error), Is.EqualTo(1));
    }

    [TestCase(Message)]
    [TestCase(Certificate)]
    public void UnsupportedPublicOperationsRejectInsteadOfReturningEmptyValues(string mechanism)
    {
        foreach (string call in new[] { "Hash(p,d)", "Derive(p,k,d)", "Encrypt(p,k,d)",
            "Decrypt(p,k,d)", "Wrap(p,k,d)", "Unwrap(p,k,d)", $"GenerateKey({mechanism},256)" })
        {
            Reset(); Run(Prefix(mechanism));
            Assert.Throws<SemanticErrorException>(() => Run(call), call);
        }
    }

    [TestCase(Message)]
    [TestCase(Certificate)]
    public void SizeMetadataNonHexVarsSerializedParametersAndRepeatedRuntimeCalls(string mechanism)
    {
        Run(Prefix(mechanism));
        Run("VAR a=Mac(p,k,d) VAR b=Mac(p,k,d)");
        Assert.That(Value("a"), Is.EqualTo(Value("b")));
        Run("VAR text=\"data\" VAR base64=b64(AA==)");
        Assert.Throws<SemanticErrorException>(() => Run("Mac(p,k,text)"));
        Assert.Throws<SemanticErrorException>(() => Run("Mac(p,k,base64)"));
        // A string-valued VAR must not be upgraded into a genuine PARAM reference.
        Run($"VAR serialized=\"#MECH:{mechanism}\"");
        Assert.Throws<SemanticErrorException>(() => Run("Mac(serialized,k,d)"));
        var original = (KeyVariableDeclaration)VariableDictionary.Instance().Get("k");
        var mismatch = new KeyVariableDeclaration {
            Value=original.Value, KeyValue=original.KeyValue, ValueFormat=FormatConversions.HEX,
            KeySize="128", Type=new CryptoTypeKey()
        };
        var parameters = VariableDictionary.Instance().Get("p");
        var args = new AlgorithmCallArguments(new[] {
            new AlgorithmCallArgument(parameters.Value,parameters,ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(mismatch.Value,mismatch,ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument("0x(00)",null,ResolvedCallArgumentKind.HexLiteral)
        });
        Assert.Throws<ArgumentException>(() => AlgorithmFactory.Create(mechanism).Mac(args));
        original.KeyType = KeyType.Secret(KeyAlgorithm.Unknown);
        Assert.That(Run("VAR unknown=Mac(p,k,d)").Diagnostics, Is.Empty);
    }

    [TestCase(Message)]
    [TestCase(Certificate)]
    public void DirectUnsupportedAlgorithmOperationsThrow(string mechanism)
    {
        var algorithm = AlgorithmFactory.Create(mechanism);
        string[] values = Array.Empty<string>();
        Assert.Throws<NotSupportedException>(() => algorithm.Hash(values));
        Assert.Throws<NotSupportedException>(() => algorithm.Encrypt(values));
        Assert.Throws<NotSupportedException>(() => algorithm.Decrypt(values));
        Assert.Throws<NotSupportedException>(() => algorithm.Wrap(values));
        Assert.Throws<NotSupportedException>(() => algorithm.Unwrap(values));
        Assert.Throws<NotSupportedException>(() => algorithm.GenerateKey(mechanism,"256"));
        Assert.Throws<NotSupportedException>(() => algorithm.Derive(new AlgorithmCallArguments(Array.Empty<AlgorithmCallArgument>())));
    }

    [TestCase("TC", Certificate, "CDA5C89A6B4F073779AB3B882C5CDFEFE756621E2FD4C4AC46C9FFCB9915C5CC")]
    [TestCase("MAC-SEND", Message, "58CA2B7D3B3517F06F6DDEE849E086E209896BC0EB19C365B30F63C281B6B3E5")]
    [TestCase("MAC-RECEIVE", Message, "9D1B46684B2F286665CF2DDE45C49F13066F97C955BEFA948DA5D49BEE22865D")]
    public void SessionKdfOutputsRemainUsableWithoutInventedProvenance(string variant, string mechanism, string derived)
    {
        var runner = Run($"KEY source=GenerateKey(HMAC-SHA256,0x(0123456789ABCDEF23456789ABCDEF01)) " +
            $"PARAM kd=Parameters(KDF-EP2-SESSION,#SALT:0x({Key}),#VARIANT:{variant}) " +
            $"KEY k=Derive(kd,source,\"\") PARAM p=Parameters({mechanism}) VAR result=Mac(p,k,0x({Data}))");
        var input = Convert.FromHexString(Data);
        var expected = HMACSHA256.HashData(Convert.FromHexString(derived), mechanism == Message ? SHA256.HashData(input) : input)[..8];
        Assert.That(Value(), Is.EqualTo($"0x({Convert.ToHexString(expected)})").IgnoreCase);
        Assert.That(runner.Diagnostics, Is.Empty);
        Assert.That(((KeyVariableDeclaration)VariableDictionary.Instance().Get("k")).KeyType.Algorithm, Is.EqualTo(KeyAlgorithm.Unknown));
    }

    [TestCase(Message, "96B632C95451623B")]
    [TestCase(Certificate, "6FD9A4429C6348AA")]
    public void DeployedDocumentationNavigationAndExecutableExamples(string mechanism, string expected)
    {
        string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "InfoDocs", $"Info.Mech.{mechanism}.md");
        string document = File.ReadAllText(path);
        MechanismDocumentationContract.AssertRequiredSections(document);
        string? shown = null;
        void Capture(string text) => shown = text;
        OutputOperations.InfoEvent += Capture;
        try { Run($"Info({mechanism})"); }
        finally { OutputOperations.InfoEvent -= Capture; }
        Assert.That(shown, Is.EqualTo(document));
        foreach (string script in MechanismDocumentationContract.ExtractExecutableExamples(document))
        {
            Reset(); Run(script);
            Assert.That(Value(), Is.EqualTo($"0x({expected})").IgnoreCase);
        }
        Assert.That(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"InfoDocs","Info.Mechanisms.md")), Does.Contain(mechanism));
        Assert.That(AntlrLanguageMetadata.GetMechanisms(), Does.Contain(mechanism));
    }
}
