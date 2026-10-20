using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Operations;
using CryptoScript.Variables;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KeyCheckValueTests
{
    // Frozen independent OpenSSL 3.6.1 vectors. ECB: enc -<cipher> -K <key> -nopad.
    // CMAC: mac -macopt cipher:<cipher>-CBC -macopt hexkey:<key> -in zero.bin CMAC.
    // zero.bin is exactly one block; self.bin is the first block of the key.
    public static readonly object[][] Vectors =
    [
        [KeyAlgorithm.Aes, "000102030405060708090A0B0C0D0E0F", "C6A13B37878F5B826F4F8162A1C8D879", "BE7ED6AE787DD5544C5561C7DCD06ABB", "0A940BB5416EF045F1C39458C653EA5A"],
        [KeyAlgorithm.Aes, "000102030405060708090A0B0C0D0E0F1011121314151617", "916251821C73A522C396D62738019607", "D4FFB80EBF6C3A89C1AB5278C6949F6D", "0060BFFE46834BB8DA5CF9A61FF220AE"],
        [KeyAlgorithm.Aes, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "F29000B62A499FD0A9F39A6ADD2E7780", "377822C09348AD3BCF3F1987BCF89B4A", "5A6E045708FB7196F02E553D02C3A692"],
        [KeyAlgorithm.Tdea, "0123456789ABCDEFFEDCBA9876543210", "08D7B4FB629D0885", "0A8245866490475B", "1A4D672DCA6CB335"],
        [KeyAlgorithm.Tdea, "0123456789ABCDEFFEDCBA98765432100011223344556677", "CBE6A76F9E351C6F", "CFB1A5FA2E56BD18", "372C1D08A4F057BA"]
    ];

    private VariableDeclaration[] previous = [];
    [SetUp] public void Save()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }
    [TearDown] public void Restore()
    {
        VariableDictionary.Instance().Clear();
        foreach (var key in previous) VariableDictionary.Instance().Add(key);
    }
    private static KeyVariableDeclaration Key(KeyAlgorithm algorithm = KeyAlgorithm.Aes,
        string hex = "000102030405060708090A0B0C0D0E0F") => new()
    {
        Id = "original", Type = new CryptoTypeKey(), Value = $"0x({hex})", ValueFormat = FormatConversions.HEX,
        KeyType = KeyType.Secret(algorithm), KeySizeInBits = new KeySize(hex.Length * 4)
    };
    private static CryptoScriptProgram Run(string script, CryptoScriptRunner? runner = null)
    {
        var parser = ParserBuilder.StringBuild(script);
        var program = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        try { return (runner ?? new CryptoScriptRunner()).Execute(program); }
        catch (SemanticErrorException error) { Assert.Fail(error.SemanticError?.Message ?? error.Message); throw; }
    }
    private static KeyVariableDeclaration Get(string id) => (KeyVariableDeclaration)VariableDictionary.Instance().Get(id);

    [TestCaseSource(nameof(Vectors))]
    public void FullValuesMatchIndependentVectors(KeyAlgorithm algorithm, string material, string kcv, string kcvc, string kcvk)
    {
        var key = Key(algorithm, material);
        Assert.Multiple(() =>
        {
            Assert.That(key.Kcv, Is.EqualTo($"0x({kcv.ToLowerInvariant()})"));
            Assert.That(key.Kcvc, Is.EqualTo($"0x({kcvc.ToLowerInvariant()})"));
            Assert.That(key.Kcvk, Is.EqualTo($"0x({kcvk.ToLowerInvariant()})"));
        });
    }

    [TestCaseSource(nameof(Vectors))]
    public void SearchAllMethodsFullAndPrefixWithAndWithoutFilter(KeyAlgorithm algorithm, string material, string kcv, string kcvc, string kcvk)
    {
        VariableDictionary.Instance().Add(Key(algorithm, material));
        foreach (var (name, value) in new[] { ("KCV", kcv), ("KCVC", kcvc), ("KCVK", kcvk) })
        foreach (int length in new[] { 2, 6, value.Length })
        foreach (bool filter in new[] { true, false })
        {
            string prefix = $"0x({value[..length]})";
            var found = KeyCheckOperations.Check(filter ? [prefix, name] : [prefix]);
            Assert.That(found.Value, Is.EqualTo($"0x({material})"));
        }
    }

    [Test]
    public void CmacIsOneZeroBlockNotEmptyMessage()
    {
        // OpenSSL 3.6.1 AES-128 CMAC(empty) with key 000102...0F.
        Assert.That(Key().Kcvc, Is.Not.EqualTo("0x(97dd6e5a882cbd564c39ae7d1c5a31aa)"));
        Assert.That(Key().Kcvc, Is.EqualTo("0x(be7ed6ae787dd5544c5561c7dcd06abb)"));
    }

    [Test]
    public void LegacyEncodingAndJsonFieldsDoNotOverrideCalculatedValues()
    {
        var key = Key();
        key.ValueFormat = "";
        var json = JObject.Parse(key.Serialize());
        json["Kcv"] = "stale";
        var restored = KeyVariableDeclaration.Deserialize(json.ToString());
        Assert.That(restored.Kcv, Is.EqualTo("0x(c6a13b37878f5b826f4f8162a1c8d879)"));
        restored.ValueFormat = FormatConversions.B64;
        restored.Value = Convert.ToBase64String(Convert.FromHexString("000102030405060708090A0B0C0D0E0F"));
        Assert.That(restored.Kcv, Is.EqualTo(key.Kcv));
    }

    [Test]
    public void FailedRedeclarationRetainsExistingKeyAndLongInputCannotMatch()
    {
        var key = Key();
        VariableDictionary.Instance().Add(key);
        var program = Run("KEY original=Check(0x(C6A13B37878F5B826F4F8162A1C8D87900)) VAR after=0x(01)");
        Assert.That(program.Diagnostics.Single().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyCheckValueNotFound));
        Assert.That(Get("original"), Is.SameAs(key));
    }

    [TestCase(KeyAlgorithm.Hmac)]
    [TestCase(KeyAlgorithm.Unknown)]
    public void InapplicableAlgorithmNeverGuessedFromLength(KeyAlgorithm algorithm)
    {
        var key = Key(algorithm);
        Assert.That(new[] { key.Kcv, key.Kcvc, key.Kcvk }, Is.All.Null);
    }

    [Test]
    public void SerializationAndMutationsNeverLeaveStaleValues()
    {
        var key = Key();
        var json = JObject.Parse(key.Serialize());
        Assert.That(json.Properties().Any(p => p.Name.StartsWith("Kcv", StringComparison.OrdinalIgnoreCase)), Is.False);
        var copy = KeyVariableDeclaration.Deserialize(json.ToString());
        Assert.That(copy.Kcv, Is.EqualTo(key.Kcv));
        copy.Value = "0x(00000000000000000000000000000000)";
        Assert.That(copy.Kcv, Is.EqualTo("0x(66e94bd4ef8a2c3b884cfa59ca342b2e)"));
        copy.KeyType = KeyType.Secret(KeyAlgorithm.Tdea);
        Assert.That(copy.Kcv, Is.EqualTo("0x(8ca64de9c1b123a7)"));
        copy.KeyType = KeyType.Secret(KeyAlgorithm.Unknown);
        Assert.That(copy.Kcv, Is.Null);
        copy.KeyType = KeyType.Public(KeyAlgorithm.Rsa);
        Assert.That(copy.Kcv, Is.Null);
    }

    [TestCase("KCV", "C6A13B37878F5B826F4F8162A1C8D879")]
    [TestCase("kcvc", "BE7ED6AE78")]
    [TestCase("KcVk", "0A940B")]
    [TestCase(null, "BE7ED6AE")]
    [TestCase(null, "C6")]
    public void CheckCopiesExactlyOneCandidateAndItsMutableMetadata(string? method, string prefix)
    {
        var key = Key();
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt);
        key.DerivationMechanism = "test";
        key.KeyAttributes.Add(new OptionalBlock { ID = "KS", Data = "test", Length = 8 });
        VariableDictionary.Instance().Add(key);
        Run($"VAR prefix=0x({prefix}) KEY found=Check(prefix{(method is null ? "" : $",\"{method}\"")})");
        var copy = Get("found");
        Assert.That(copy, Is.Not.SameAs(key));
        Assert.That(copy.Value, Is.EqualTo(key.Value));
        Assert.That(copy.KeyType, Is.EqualTo(key.KeyType));
        Assert.That(copy.Usage, Is.EqualTo(key.Usage));
        Assert.That(copy.DerivationMechanism, Is.EqualTo(key.DerivationMechanism));
        Assert.That(copy.KeyAttributes[0], Is.Not.SameAs(key.KeyAttributes[0]));
        copy.KeyAttributes[0].Data = "changed";
        Assert.That(key.KeyAttributes[0].Data, Is.EqualTo("test"));
        Assert.That(key.Id, Is.EqualTo("original"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FailedSearchIsAnExecutionLocalErrorAndDoesNotDeclareOrStop(bool ambiguous)
    {
        if (ambiguous)
        {
            VariableDictionary.Instance().Add(Key());
            var duplicate = Key(); duplicate.Id = "duplicate"; VariableDictionary.Instance().Add(duplicate);
        }
        var runner = new CryptoScriptRunner();
        var program = Run("KEY missing=Check(0x(C6A13B)) VAR continued=0x(AA)", runner);
        Assert.That(VariableDictionary.Instance().Contains("missing"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("continued"), Is.True);
        var error = program.Diagnostics.Single();
        Assert.That(error.DiagnosticCode, Is.EqualTo(ambiguous ? SemanticDiagnosticCode.KeyCheckValueAmbiguous : SemanticDiagnosticCode.KeyCheckValueNotFound));
        Assert.That(error.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Error));
        Assert.That(error.Location!.FunctionName, Is.EqualTo("Check"));
        Assert.That(runner.SemanticErrors, Has.Count.EqualTo(1));
        Assert.That(Run("VAR next=0x(BB)", runner).Diagnostics, Is.Empty);
    }

    [Test]
    public void NestedFailedSearchDoesNotExecuteParentOrDeclare()
    {
        var program = Run("VAR bad=Compare(Check(0x(AA)),0x(AA)) VAR good=0x(01)");
        Assert.That(program.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(VariableDictionary.Instance().Contains("bad"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("good"), Is.True);
    }

    [Test]
    public void MultipleMatchingMethodsCountOnceAndFiltersAreExclusive()
    {
        VariableDictionary.Instance().Add(Key(KeyAlgorithm.Aes, new string('0', 32)));
        Run("KEY found=Check(0x(66e94b))"); // KCV == KCVK for a zero key.
        Assert.That(Get("found").Kcv, Is.EqualTo(Get("found").Kcvk));
        Assert.That(Run("KEY missing=Check(0x(66e94b),\"KCVC\")").Diagnostics.Single().DiagnosticCode,
            Is.EqualTo(SemanticDiagnosticCode.KeyCheckValueNotFound));
    }

    [TestCase("0x()", null)] [TestCase("0x(A)", null)] [TestCase("0x(GG)", null)]
    [TestCase("123456", null)] [TestCase("0x(AA)", "0")] [TestCase("0x(AA)", "unknown")]
    public void InvalidInputsAreRejected(string value, string? method) =>
        Assert.Throws<ArgumentException>(() => KeyCheckOperations.Check(method is null ? [value] : [value, method]));

    [Test]
    public void PrintUsesSourceObjectAndPreservesOrdinaryOutput()
    {
        var key = Key();
        var messages = new List<string>();
        void Capture(string text) => messages.Add(text);
        OutputOperations.PrintEvent += Capture;
        try
        {
            new OutputOperations().Print(new OperationInvocation([new ResolvedCallArgument("different value", ResolvedCallArgumentKind.Variable, key)]));
            new OutputOperations().Print(["ordinary"]);
            foreach (string label in new[] { "Identifier", "Type", "Value", "ValueFormat", "KeyType", "KeySizeInBits", "KeyUsage", "KeyPolicy", "DerivationMechanism", "KeyAttributes", "KCV", "KCVC", "KCVK" })
                Assert.That(messages[0], Does.Contain(label + ":"));
            foreach (string value in new[] { key.Value, key.Kcv!, key.Kcvc!, key.Kcvk! }) Assert.That(messages[0], Does.Contain(value));
            Assert.That(messages[0], Does.Contain("KeyAttributes: N/A"));
            Assert.That(messages[1], Is.EqualTo("out: ordinary"));
        }
        finally { OutputOperations.PrintEvent -= Capture; }
    }

    [Test]
    public void GenerationDerivationAndUnwrapExposeValuesWithoutOperationSpecificChanges()
    {
        Run("KEY random=GenerateKey(AES-ECB,192) KEY bdk=GenerateKey(AES-ECB,0x(FEDCBA9876543210F1F1F1F1F1F1F1F1)) PARAM p=Parameters(DUKPT-AES-INITIAL-KEY) KEY derived=Derive(p,bdk,0x(1234567890123456))");
        var v = Tr31ReferenceVectors.All[0];
        Run($"KEY kbpk=GenerateKey(DES3-ECB,0x({v.Kbpk})) PARAM tr=#MECH:WRAP-DES3-TR31 KEY unwrapped=Unwrap(tr,kbpk,\"{v.CompleteBlock}\")");
        foreach (string name in new[] { "random", "bdk", "derived", "unwrapped" })
        {
            var key = Get(name);
            Assert.That(new[] { key.Kcv, key.Kcvc, key.Kcvk }, Is.All.Not.Null);
            Assert.That(key.Kcv!.Length, Is.EqualTo(key.KeyType.Algorithm == KeyAlgorithm.Aes ? 36 : 20));
        }
        Run("KEY weak=GenerateKey(DES3-ECB,0x(00000000000000000000000000000000))");
        Assert.That(Get("weak").Kcv, Is.EqualTo("0x(8ca64de9c1b123a7)"));
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public void KeyPrintMarksEveryPhysicalLineWithoutExtraLines(string newline)
    {
        var key = Key();
        key.DerivationMechanism = "source" + newline + "detail";
        var messages = new List<string>();
        void Capture(string text) => messages.Add(text);
        OutputOperations.PrintEvent += Capture;
        try
        {
            new OutputOperations().Print(new OperationInvocation([
                new ResolvedCallArgument(key.Value, ResolvedCallArgumentKind.Variable, key)]));
            Assert.That(messages, Has.Count.EqualTo(1));
            string[] lines = messages[0].Split(Environment.NewLine);
            Assert.That(lines, Has.Length.EqualTo(15));
            Assert.That(lines.Count(line => line.StartsWith("out: Value:", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(lines.Any(line => line.StartsWith("out: KeyValue:", StringComparison.Ordinal)), Is.False);
            Assert.That(lines.All(line => line.StartsWith("out: ", StringComparison.Ordinal)), Is.True);
            Assert.That(lines, Does.Contain("out: DerivationMechanism: source"));
            Assert.That(lines, Does.Contain("out: detail"));
            Assert.That(lines, Does.Not.Contain("out: "));
            Assert.That(lines[^1], Is.EqualTo("out: KCVK: 0x(0a940bb5416ef045f1c39458c653ea5a)"));
        }
        finally { OutputOperations.PrintEvent -= Capture; }
    }

    [TestCaseSource(typeof(Tr31VersionDTransportVectors), nameof(Tr31VersionDTransportVectors.All))]
    public void AesProtectedTransportUsesPayloadAlgorithmAndPreservesMetadata(object data)
    {
        var vector = (Tr31VersionDTransportVector)data;
        Run($"KEY kbpk=GenerateKey(AES-ECB,0x({vector.Kbpk})) PARAM p=#MECH:WRAP-AES-TR31 KEY payload=Unwrap(p,kbpk,\"{vector.Wire}\")");
        var key = Get("payload");
        string before = key.Serialize();
        var values = new[] { key.Kcv, key.Kcvc, key.Kcvk };
        if (vector.Algorithm is 'A' or 'T')
        {
            Assert.That(values, Is.All.Not.Null);
            Assert.That(values.Select(value => value!.Length), Is.All.EqualTo(vector.Algorithm == 'A' ? 36 : 20));
        }
        else Assert.That(values, Is.All.Null);
        Assert.That(key.Serialize(), Is.EqualTo(before));
    }

    [TestCase("0x(AA)")]
    [TestCase("0x(GG000000000000000000000000000000)")]
    [TestCase("0x(000)")]
    [TestCase("0x(000102030405060708090A0B0C0D0E0F!")]
    public void MalformedMaterialHasNoApplicableValue(string value)
    {
        var key = Key(); key.Value = value;
        Assert.That(new[] { key.Kcv, key.Kcvc, key.Kcvk }, Is.All.Null);
    }
}
