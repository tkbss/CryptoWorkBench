using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Operations;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Tr31HeaderAuthorityTests
{
    private VariableDeclaration[] previous = [];
    [SetUp]
    public void Setup()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }
    [TearDown]
    public void Restore()
    {
        VariableDictionary.Instance().Clear();
        foreach (var v in previous) VariableDictionary.Instance().Add(v);
    }

    public static IEnumerable<TestCaseData> Answers()
    {
        foreach (var v in Tr31VersionDTransportVectors.All.Where(v => v.Algorithm is 'A' or 'T'))
            foreach (var a in Enum.GetValues<KeyAlgorithm>())
                yield return new TestCaseData(v, a).SetName($"HeaderAuthority_{v}_{a}");
    }

    private static KeyVariableDeclaration Key(string id, string hex, KeyAlgorithm a) => new()
    {
        Id = id, Value = "0x(" + hex + ")",
        ValueFormat = FormatConversions.HEX, Type = new CryptoTypeKey(),
        KeySizeInBits = new KeySize(hex.Length * 4),
        KeyType = a is KeyAlgorithm.Rsa or KeyAlgorithm.Ec ? KeyType.Public(a) : KeyType.Secret(a),
        Usage = KeyUsagePolicy.Restricted(KeyUsage.MacVerify)
    };

    private static (ParameterVariableDeclaration P, KeyVariableDeclaration K, KeyVariableDeclaration T)
        Inputs(Tr31VersionDTransportVector v, KeyAlgorithm a)
    {
        var p = new ParameterVariableDeclaration { Id = "p", Mechanism = "WRAP-AES-TR31" };
        p.SetParameter("BLKH", '"' + v.Header + '"');
        p.SetParameter("RND", "0x(" + v.Random + ")");
        var k = Key("kbpk", v.Kbpk, KeyAlgorithm.Aes);
        k.Usage = KeyUsagePolicy.Restricted(KeyUsage.WrapKey | KeyUsage.UnwrapKey);
        var t = Key("transported", v.Key, a);
        // Conflicting stored legacy header must never select the productive profile.
        t.KeyAttributes = [new OptionalBlock { ID = "HDR", Data = v.Algorithm == 'A' ? "D0112D0TB00E0000" : "D0144D0AB00E0000" }];
        foreach (VariableDeclaration item in new VariableDeclaration[] { p, k, t })
            VariableDictionary.Instance().Add(item);
        return (p, k, t);
    }

    private static AlgorithmCallArguments Args(ParameterVariableDeclaration p, KeyVariableDeclaration k,
        VariableDeclaration t, ISemanticDiagnosticSink? sink = null) => new(new[]
        {
            new AlgorithmCallArgument(p.Value, p, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(k.Value, k, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(t.Value, t, ResolvedCallArgumentKind.Variable)
        }, sink);

    [TestCaseSource(nameof(Answers))]
    public void ActualHeaderControlsCompleteIndependentAnswersRegardlessOfMetadata(object input, KeyAlgorithm declared)
    {
        var v = (Tr31VersionDTransportVector)input;
        var (p, k, t) = Inputs(v, declared);
        string beforeK = k.Serialize(), beforeT = t.Serialize(), beforeP = p.Value;
        var expected = v.Algorithm == 'A' ? KeyAlgorithm.Aes : KeyAlgorithm.Tdea;
        bool conflict = declared != KeyAlgorithm.Unknown && declared != expected;
        var sink = new SemanticDiagnosticSink();
        var algorithm = new WrapAESTR31();
        Assert.That(algorithm.Wrap(Args(p, k, t, sink)).Value, Is.EqualTo(v.Composite).IgnoreCase);
        CheckWarnings(sink.GetSnapshot(), conflict);
        Assert.That(algorithm.Wrap(Args(p, k, t)).Value, Is.EqualTo(v.Composite).IgnoreCase);
        Assert.That(algorithm.Wrap([p.Value, "kbpk", "transported"]).Value, Is.EqualTo(v.Composite).IgnoreCase);

        var invocationSink = new SemanticDiagnosticSink();
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument(p.Value, ResolvedCallArgumentKind.Variable, p),
            new ResolvedCallArgument(k.Value, ResolvedCallArgumentKind.Variable, k),
            new ResolvedCallArgument(t.Value, ResolvedCallArgumentKind.Variable, t)
        }, invocationSink);
        Assert.That(OperationFactory.CreateInvocationOperation("Wrap")(invocation).Value, Is.EqualTo(v.Composite).IgnoreCase);
        CheckWarnings(invocationSink.GetSnapshot(), conflict);
        // Equal bytes with different declarations must not replace the original SourceVariable.
        VariableDictionary.Instance().Add(Key("decoy", v.Key, expected));
        var identitySink = new SemanticDiagnosticSink();
        Assert.That(algorithm.Wrap(Args(p, k, t, identitySink)).Value, Is.EqualTo(v.Composite).IgnoreCase);
        CheckWarnings(identitySink.GetSnapshot(), conflict);
        Assert.Throws<ArgumentException>(() => algorithm.Wrap([p.Value, k.Value, t.Value]));

        var runner = new CryptoScriptRunner();
        runner.Execute(ParserBuilder.StringBuild("VAR output=Wrap(p,kbpk,transported) VAR after=0x(01)").program());
        Assert.That(VariableDictionary.Instance().Get("output").Value, Is.EqualTo(v.Composite).IgnoreCase);
        CheckWarnings(runner.Diagnostics, conflict);
        foreach (var d in runner.Diagnostics)
        {
            Assert.That(d.Location!.OperationId, Is.EqualTo(1));
            Assert.That(d.Location.ExecutionId, Is.Not.Null.And.Not.Empty);
            Assert.That(d.Location.Mechanism, Is.EqualTo("WRAP-AES-TR31"));
        }
        // The existing composite reader cannot parse header-only Optional Blocks.
        // Preserve that reader here; full wire form covers all supported optional fixtures.
        var readable = v.Header.Length == 16 ? new[] { v.Composite, '"' + v.Wire + '"' } : new[] { '"' + v.Wire + '"' };
        foreach (string block in readable)
        {
            var recovered = algorithm.Unwrap(Args(p, k, new StringVariableDeclaration { Value = block }));
            Assert.That(recovered.Value, Is.EqualTo("0x(" + v.Key + ")").IgnoreCase);
            Assert.That(recovered.KeySizeInBits.Bits, Is.EqualTo(v.Key.Length * 4));
            Assert.That(recovered.KeyType.Algorithm, Is.EqualTo(expected));
        }
        Assert.That(k.Serialize(), Is.EqualTo(beforeK));
        Assert.That(t.Serialize(), Is.EqualTo(beforeT));
        Assert.That(p.Value, Is.EqualTo(beforeP));
    }

    private static void CheckWarnings(IEnumerable<SemanticError> errors, bool conflict)
    {
        var all = errors.ToArray();
        Assert.That(all, Has.Length.EqualTo(conflict ? 1 : 0));
        foreach (var d in all)
        {
            Assert.That(d.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyTypeConflict));
            Assert.That(d.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
            Assert.That(d.Identifier, Is.EqualTo("transported"));
            Assert.That(d.Location!.Identifier, Is.EqualTo("transported"));
        }
    }

    [TestCase('A', 0)] [TestCase('A', 15)] [TestCase('A', 17)]
    [TestCase('A', 25)] [TestCase('A', 31)] [TestCase('A', 33)]
    [TestCase('T', 15)] [TestCase('T', 17)] [TestCase('T', 25)] [TestCase('T', 32)]
    public void InvalidActualMaterialLengthsAreNeverTruncatedOrFilled(char a, int length)
    {
        var (p, k, t) = Inputs(Tr31VersionDTransportVectors.All.First(v => v.Algorithm == a), KeyAlgorithm.Unknown);
        t.Value = "0x(" + new string('1', length * 2) + ")";
        Assert.Throws<ArgumentException>(() => new WrapAESTR31().Wrap(Args(p, k, t)));
    }

    [TestCase("0x(001)")] [TestCase("0x(00GG)")] [TestCase("0x(0011]")]
    [TestCase("001Z")] [TestCase("b64(AAAA)")] [TestCase(" 202122232425262728292A2B2C2D2E2F")]
    public void InvalidMaterialEncodingFails(string material)
    {
        var (p, k, t) = Inputs(Tr31VersionDTransportVectors.All[0], KeyAlgorithm.Tdea);
        t.Value = material;
        var sink = new SemanticDiagnosticSink();
        Assert.Throws<ArgumentException>(() => new WrapAESTR31().Wrap(Args(p, k, t, sink)));
        Assert.That(sink.GetSnapshot(), Is.Empty);
    }

    [Test]
    public void KnownMetadataMustMatchActualBytesWhileUnknownSizeUsesActualLength()
    {
        var v = Tr31VersionDTransportVectors.All[0];
        var (p, k, t) = Inputs(v, KeyAlgorithm.Unknown);
        t.Value = "0x(" + v.Key + "3031323334353637)";
        Assert.That(Assert.Throws<ArgumentException>(() => new WrapAESTR31().Wrap(Args(p, k, t)))!.Message,
            Does.Contain("size metadata"));
        var legacy = KeyVariableDeclaration.Deserialize("{\"Mechanism\":\"WRAP-AES-TR31\"}");
        legacy.Value = v.Key; // Preserve raw hexadecimal host input; size is genuinely unknown.
        Assert.That(new WrapAESTR31().Wrap(Args(p, k, legacy)).Value, Is.EqualTo(v.Composite).IgnoreCase);
        Assert.That(legacy.KeySizeInBits.IsKnown, Is.False);
    }

    [TestCase("D0144D0HB00E0000")] [TestCase("D0144D0RB00E0000")]
    [TestCase("D0144D0DB00E0000")] [TestCase("D0144D0ZB00E0000")]
    [TestCase("D0144D0 B00E0000")] [TestCase("D0144D0aB00E0000")]
    public void UnsupportedNewWriteProfilesFailExplicitly(string header)
    {
        var (p, k, t) = Inputs(Tr31VersionDTransportVectors.All[0], KeyAlgorithm.Aes);
        p.SetParameter("BLKH", '"' + header + '"');
        Assert.Throws<NotSupportedException>(() => new WrapAESTR31().Wrap(Args(p, k, t)));
    }

    [Test]
    public void MissingHeaderCannotFallBackToKeyMetadata()
    {
        var (p, k, t) = Inputs(Tr31VersionDTransportVectors.All[0], KeyAlgorithm.Aes);
        p.GetParameters().Remove("#BLKH");
        Assert.Throws<NotSupportedException>(() => new WrapAESTR31().Wrap(Args(p, k, t)));
    }

    [TestCase(6, null)] [TestCase(6, "AA")]
    [TestCase(8, null)] [TestCase(8, "AA")]
    public void TdeaRandomFillerFallbackPreservesMaterial(int vectorIndex, string? random)
    {
        var v = Tr31VersionDTransportVectors.All[vectorIndex];
        var (p, k, t) = Inputs(v, KeyAlgorithm.Aes); // Conflicting declaration does not change T layout.
        p.SetParameter("RND", random is null ? "" : "0x(" + random + ")");
        var algorithm = new WrapAESTR31();
        var block = algorithm.Wrap(Args(p, k, t));
        Assert.That(TR31String.FromString(block.Value).Cryptogram, Has.Length.EqualTo(32));
        Assert.That(algorithm.Unwrap(Args(p, k, block)).Value, Is.EqualTo("0x(" + v.Key + ")").IgnoreCase);
    }

    [TestCase("D")] [TestCase("D0144D0A")]
    [TestCase("D+144D0AB00E0000")] [TestCase("D0144D0AB00E 000")]
    [TestCase("D0144D0AB00E0100")] [TestCase("D0144D0AB00E0000KS04")]
    [TestCase("D0156D0AB00E0100KS0D12345678")]
    [TestCase("D0156D0AB00E0100KSZZ12345678")]
    [TestCase("D0156D0AB00E0100KS C12345678")]
    [TestCase("D0156D0AB00E0100KSC 12345678")]
    [TestCase("D0170D0AB00E0100KS0002 01A1234567890123456")]
    [TestCase("D0170D0AB00E0100KS0002001 1234567890123456")]
    [TestCase("D0156D0AB00E0100KS0312345678")]
    [TestCase("D0156D0AB00E0100KS0002ZZZZ")]
    [TestCase("D0156D0AB00E0100KS0C1234567é")]
    public void MalformedHeadersAndOptionalBlocksFail(string header)
    {
        var (p, k, t) = Inputs(Tr31VersionDTransportVectors.All[0], KeyAlgorithm.Aes);
        p.SetParameter("BLKH", '"' + header + '"');
        Assert.Throws<ArgumentException>(() => new WrapAESTR31().Wrap(Args(p, k, t)));
    }

    [Test]
    public void RoleWarningsRemainDistinctAndSurviveLaterLengthError()
    {
        var (p, k, t) = Inputs(Tr31VersionDTransportVectors.All[0], KeyAlgorithm.Tdea);
        k.KeyType = KeyType.Secret(KeyAlgorithm.Hmac);
        k.Usage = KeyUsagePolicy.Restricted(KeyUsage.Decrypt);
        p.SetParameter("BLKH", "\"D0112D0AB00E0000\"");
        var sink = new SemanticDiagnosticSink();
        Assert.Throws<ArgumentException>(() => new WrapAESTR31().Wrap(Args(p, k, t, sink)));
        Assert.That(sink.GetSnapshot().Select(d => (d.Identifier, d.DiagnosticCode)), Is.EqualTo(new[]
        {
            ("kbpk", SemanticDiagnosticCode.KeyTypeConflict),
            ("kbpk", SemanticDiagnosticCode.KeyUsageConflict),
            ("transported", SemanticDiagnosticCode.KeyTypeConflict)
        }));
    }

    [Test]
    public void OnePhysicalKeyDoesNotProduceDuplicateAesTypeWarnings()
    {
        var (p, k, _) = Inputs(Tr31VersionDTransportVectors.All[0], KeyAlgorithm.Aes);
        k.KeyType = KeyType.Secret(KeyAlgorithm.Hmac);
        var sink = new SemanticDiagnosticSink();
        new WrapAESTR31().Wrap(Args(p, k, k, sink));
        Assert.That(sink.GetSnapshot(), Has.Count.EqualTo(1));
        Assert.That(sink.GetSnapshot()[0].Identifier, Is.EqualTo("kbpk"));
    }

    [TestCase(false)] [TestCase(true)]
    public void AuthenticationFailureNeverReturnsKeyOrTransportWarnings(bool tamperHeader)
    {
        var v = Tr31VersionDTransportVectors.All[0];
        var (p, k, _) = Inputs(v, KeyAlgorithm.Aes);
        string wire = tamperHeader ? v.Wire[..7] + 'T' + v.Wire[8..] : v.Wire[..^1] + (v.Wire[^1] == '0' ? '1' : '0');
        VariableDictionary.Instance().Add(new StringVariableDeclaration { Id = "damaged", Value = '"' + wire + '"' });
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(
            ParserBuilder.StringBuild("KEY result=Unwrap(p,kbpk,damaged)").program()));
        Assert.That(VariableDictionary.Instance().GetVariables().Any(x => x.Id == "result"), Is.False);
        Assert.That(runner.Diagnostics.Any(d => d.DiagnosticCode == SemanticDiagnosticCode.KeyTypeConflict), Is.False);
        Assert.That(runner.Diagnostics.Any(d => d.Message.Contains("MAC verification failed")), Is.True);
    }
}
