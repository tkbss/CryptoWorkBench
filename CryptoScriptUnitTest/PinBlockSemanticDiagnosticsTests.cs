using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.CryptoAlgorithm.PINBLOCK;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class PinBlockSemanticDiagnosticsTests
{
    private const string NormalTdea = "0123456789ABCDEFFEDCBA9876543210";
    private const string DegenerateTdea = "0123456789ABCDEF0123456789ABCDEF";
    private const string AesKey = "000102030405060708090A0B0C0D0E0F";
    private const string Pin = "01234";
    private const string Pan = "1234567890123456";
    private const string InvalidBlock = "Decrypted PIN block is invalid.";
    private static readonly string[] Mechanisms =
    {
        "WRAP-DES3-PINBLOCK-0", "WRAP-DES3-PINBLOCK-1", "WRAP-DES3-PINBLOCK-2",
        "WRAP-DES3-PINBLOCK-3", "WRAP-AES-PINBLOCK-4"
    };

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TearDown]
    public void TearDown() => VariableDictionary.Instance().Clear();

    private static string Hex(string value) => "0x(" + value + ")";
    private static KeyAlgorithm ExpectedAlgorithm(string mechanism) =>
        mechanism.Contains("AES", StringComparison.Ordinal) ? KeyAlgorithm.Aes : KeyAlgorithm.Tdea;

    private static KeyVariableDeclaration Key(string hex, KeyAlgorithm algorithm, KeyUsagePolicy? usage = null,
        string id = "k") => new()
    {
        Id = id, Type = new CryptoTypeKey(), Value = Hex(hex),
        ValueFormat = FormatConversions.HEX, KeyType = KeyType.Secret(algorithm),
        KeySizeInBits = new KeySize((ulong)hex.Length * 4),
        Usage = usage ?? KeyUsagePolicy.Restricted(KeyUsage.PinEncrypt)
    };

    private static StringVariableDeclaration Var(string value, string id = "data") => new()
    {
        Id = id, Value = value, ValueFormat = FormatConversions.ParseString(value), Type = new CryptoTypeVar()
    };

    private static ParameterVariableDeclaration Parameters(string mechanism, bool fixedFields = true, string id = "p")
    {
        var p = new ParameterVariableDeclaration { Id = id, Mechanism = mechanism };
        if (mechanism.EndsWith("-0") || mechanism.EndsWith("-3") || mechanism.EndsWith("-4"))
            p.SetParameter("PAN", Hex(Pan));
        if (fixedFields)
        {
            if (mechanism.EndsWith("-1")) p.SetParameter("TRANSACTION", "0x(012345678)");
            if (mechanism.EndsWith("-3")) p.SetParameter("FILL", "0x(ABCDEFABC)");
            if (mechanism.EndsWith("-4")) p.SetParameter("RANDOM", "0x(0011223344556677)");
        }
        return p;
    }

    private static string FixedCipher(string mechanism) => mechanism[^1] switch
    {
        '0' => "2D8F0FF09AFDCCD0", '1' => "429B597B94CB18A5",
        '2' => "978BB4AC07BBD2EB", '3' => "6694CAF82B1C2994",
        '4' => "6685B016B0CEC63CB7E0F2663C9551EC", _ => throw new ArgumentException()
    };

    private static AlgorithmCallArguments Arguments(ParameterVariableDeclaration p, VariableDeclaration key,
        VariableDeclaration data, ISemanticDiagnosticSink? sink) => new(new[]
    {
        new AlgorithmCallArgument(p.Value, p, ResolvedCallArgumentKind.Variable),
        new AlgorithmCallArgument(key.Value, key, ResolvedCallArgumentKind.Variable),
        new AlgorithmCallArgument(data.Value, data, ResolvedCallArgumentKind.Variable)
    }, sink);

    private static StringVariableDeclaration Call(string function, ParameterVariableDeclaration p,
        KeyVariableDeclaration key, StringVariableDeclaration data, ISemanticDiagnosticSink? sink, string route)
    {
        var args = Arguments(p, key, data, sink);
        Assert.That(args.Arguments[0].SourceVariable, Is.SameAs(p));
        Assert.That(args.Arguments[1].SourceVariable, Is.SameAs(key));
        var algorithm = AlgorithmFactory.Create(p.Mechanism);
        return route switch
        {
            "typed" => (StringVariableDeclaration)(function == "Wrap" ? algorithm.Wrap(args) : algorithm.Unwrap(args)),
            "dispatch" => (StringVariableDeclaration)(OperationFactory.CreateInvocationOperation(function)(
                new OperationInvocation(new[]
                {
                    new ResolvedCallArgument(p.Value, ResolvedCallArgumentKind.Variable, p),
                    new ResolvedCallArgument(key.Value, ResolvedCallArgumentKind.Variable, key),
                    new ResolvedCallArgument(data.Value, ResolvedCallArgumentKind.Variable, data)
                }, sink)) ?? throw new AssertionException("PIN block operations must return a variable.")),
            "legacy" => LegacyCall(algorithm, function, p, key, data),
            _ => throw new ArgumentException("Unknown test route.")
        };
    }

    private static StringVariableDeclaration LegacyCall(CryptoScript.CryptoAlgorithm.CryptoAlgorithm algorithm,
        string function, ParameterVariableDeclaration p, KeyVariableDeclaration key, StringVariableDeclaration data)
    {
        VariableDictionary.Instance().Add(p);
        VariableDictionary.Instance().Add(key);
        VariableDictionary.Instance().Add(data);
        return (StringVariableDeclaration)(function == "Wrap"
            ? algorithm.Wrap(new[] { p.Id, key.Id, data.Id })
            : algorithm.Unwrap(new[] { p.Id, key.Id, data.Id }));
    }

    private static IEnumerable<TestCaseData> MetadataCases()
    {
        foreach (string mechanism in Mechanisms)
        foreach (string function in new[] { "Wrap", "Unwrap" })
        foreach (string profile in new[] { "correct", "type", "usage", "both", "unknown", "unspecified",
                     "unknown-usage", "type-unspecified", "unknown-unspecified" })
            yield return new TestCaseData(mechanism, function, profile);
    }

    [TestCaseSource(nameof(MetadataCases))]
    public void MetadataWarningsPreserveResultsIdentityAndParameterStateAcrossPublicRoutes(
        string mechanism, string function, string profile)
    {
        foreach (string route in new[] { "typed", "dispatch", "no-sink", "legacy" })
        {
            VariableDictionary.Instance().Clear();
            var expectedAlgorithm = ExpectedAlgorithm(mechanism);
            bool wrongType = profile is "type" or "both" or "type-unspecified";
            bool wrongUsage = profile is "usage" or "both" or "unknown-usage";
            var algorithm = profile.StartsWith("unknown", StringComparison.Ordinal) ? KeyAlgorithm.Unknown :
                wrongType ? KeyAlgorithm.Hmac : expectedAlgorithm;
            var usage = profile.Contains("unspecified", StringComparison.Ordinal) ? KeyUsagePolicy.Unspecified :
                KeyUsagePolicy.Restricted(wrongUsage ? KeyUsage.Encrypt : KeyUsage.PinEncrypt);
            var key = Key(expectedAlgorithm == KeyAlgorithm.Aes ? AesKey : NormalTdea, algorithm, usage);
            var p = Parameters(mechanism);
            string keyBefore = key.Serialize(), parameterBefore = p.Value;
            var sink = route is "no-sink" or "legacy" ? null : new SemanticDiagnosticSink();
            var input = Var(Hex(function == "Wrap" ? Pin : FixedCipher(mechanism)));
            string result = Call(function, p, key, input, sink, route == "no-sink" ? "typed" : route).Value;
            Assert.That(result, Is.EqualTo(Hex(function == "Wrap" ? FixedCipher(mechanism) : Pin)).IgnoreCase,
                $"{mechanism} {function} {profile} {route}");
            var expected = new List<SemanticDiagnosticCode>();
            if (wrongType) expected.Add(SemanticDiagnosticCode.KeyTypeConflict);
            if (wrongUsage) expected.Add(SemanticDiagnosticCode.KeyUsageConflict);
            if (sink is not null)
            {
                Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(expected), route);
                foreach (var d in sink.GetSnapshot()) AssertWarning(d, function, mechanism, "k");
            }
            Assert.That(key.Serialize(), Is.EqualTo(keyBefore));
            Assert.That(p.Value, Is.EqualTo(parameterBefore), "Fixed fields must not change.");
        }
    }

    private static void AssertWarning(SemanticError d, string function, string mechanism, string identifier)
    {
        Assert.That(d.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
        Assert.That(d.Location!.FunctionName, Is.EqualTo(function));
        Assert.That(d.Location.Mechanism, Is.EqualTo(mechanism));
        Assert.That(d.Location.Identifier, Is.EqualTo(identifier));
    }

    // PUBLIC TEST VECTORS ONLY. OpenSSL 3.6.1 enc -des-ede-ecb (16B key) or
    // enc -des-ede3-ecb (24B key), -K <row key> -nopad -nosalt; binary stdin/out, no IV.
    // Independently assembled fields for PIN 01234:
    // F0=0501234FFFFFFFFF XOR 0000456789012345; F1=1501234012345678;
    // F2=2501234FFFFFFFFF; F3=3501234ABCDEFABC XOR 0000456789012345.
    // Thus PAN=1234567890123456, TRANSACTION=012345678, FILL=ABCDEFABC.
    // No production field codec or cipher generated these constants. No OpenSSL runtime dependency.
    // Columns: classification | public key | parity-warning | weak-warning | format | clear block | ciphertext.
    private static readonly string[] PublicReferences =
    {
        "normal16|0123456789ABCDEFFEDCBA9876543210|0|0|0|0501662876FEDCBA|2D8F0FF09AFDCCD0",
        "normal16|0123456789ABCDEFFEDCBA9876543210|0|0|1|1501234012345678|429B597B94CB18A5",
        "normal16|0123456789ABCDEFFEDCBA9876543210|0|0|2|2501234FFFFFFFFF|978BB4AC07BBD2EB",
        "normal16|0123456789ABCDEFFEDCBA9876543210|0|0|3|3501662D35DFD9F9|6694CAF82B1C2994",
        "normal24twoKey|0123456789ABCDEFFEDCBA98765432100123456789ABCDEF|0|0|0|0501662876FEDCBA|2D8F0FF09AFDCCD0",
        "normal24twoKey|0123456789ABCDEFFEDCBA98765432100123456789ABCDEF|0|0|1|1501234012345678|429B597B94CB18A5",
        "normal24twoKey|0123456789ABCDEFFEDCBA98765432100123456789ABCDEF|0|0|2|2501234FFFFFFFFF|978BB4AC07BBD2EB",
        "normal24twoKey|0123456789ABCDEFFEDCBA98765432100123456789ABCDEF|0|0|3|3501662D35DFD9F9|6694CAF82B1C2994",
        "parity|0023456789ABCDEFFEDCBA9876543210|1|0|0|0501662876FEDCBA|2D8F0FF09AFDCCD0",
        "parity|0023456789ABCDEFFEDCBA9876543210|1|0|1|1501234012345678|429B597B94CB18A5",
        "parity|0023456789ABCDEFFEDCBA9876543210|1|0|2|2501234FFFFFFFFF|978BB4AC07BBD2EB",
        "parity|0023456789ABCDEFFEDCBA9876543210|1|0|3|3501662D35DFD9F9|6694CAF82B1C2994",
        "weakComponent|0101010101010101FEDCBA9876543210|0|1|0|0501662876FEDCBA|5C590C6D8C6C423A",
        "weakComponent|0101010101010101FEDCBA9876543210|0|1|1|1501234012345678|DF2321A7CDF49355",
        "weakComponent|0101010101010101FEDCBA9876543210|0|1|2|2501234FFFFFFFFF|526641B53C989BA5",
        "weakComponent|0101010101010101FEDCBA9876543210|0|1|3|3501662D35DFD9F9|39F79D5BB0B6EF69",
        "semiWeakComponent|01FE01FE01FE01FEFEDCBA9876543210|0|1|0|0501662876FEDCBA|401E38A5F5E7BADD",
        "semiWeakComponent|01FE01FE01FE01FEFEDCBA9876543210|0|1|1|1501234012345678|C12FC97072AF38D9",
        "semiWeakComponent|01FE01FE01FE01FEFEDCBA9876543210|0|1|2|2501234FFFFFFFFF|824D1BD8831B3C0F",
        "semiWeakComponent|01FE01FE01FE01FEFEDCBA9876543210|0|1|3|3501662D35DFD9F9|99EF19E0781DA640",
        "degenerate16|0123456789ABCDEF0123456789ABCDEF|0|1|0|0501662876FEDCBA|961FCA772DF6066A",
        "degenerate16|0123456789ABCDEF0123456789ABCDEF|0|1|1|1501234012345678|F975D9E4123C5AB9",
        "degenerate16|0123456789ABCDEF0123456789ABCDEF|0|1|2|2501234FFFFFFFFF|FAD39DEC1F3696F0",
        "degenerate16|0123456789ABCDEF0123456789ABCDEF|0|1|3|3501662D35DFD9F9|1E62436B583CBB32",
        "degenerate24|0123456789ABCDEFFEDCBA9876543210FEDCBA9876543210|0|1|0|0501662876FEDCBA|961FCA772DF6066A",
        "degenerate24|0123456789ABCDEFFEDCBA9876543210FEDCBA9876543210|0|1|1|1501234012345678|F975D9E4123C5AB9",
        "degenerate24|0123456789ABCDEFFEDCBA9876543210FEDCBA9876543210|0|1|2|2501234FFFFFFFFF|FAD39DEC1F3696F0",
        "degenerate24|0123456789ABCDEFFEDCBA9876543210FEDCBA9876543210|0|1|3|3501662D35DFD9F9|1E62436B583CBB32",
        "zero16|00000000000000000000000000000000|1|1|0|0501662876FEDCBA|FBF6997C8F3A68E1",
        "zero16|00000000000000000000000000000000|1|1|1|1501234012345678|94A170680C5B5A40",
        "zero16|00000000000000000000000000000000|1|1|2|2501234FFFFFFFFF|3970ACE33AD6DA2B",
        "zero16|00000000000000000000000000000000|1|1|3|3501662D35DFD9F9|A2D93BBACC44C72A",
        "mixedParityDegenerate|0123456789ABCDEF0022446688AACCEE|1|1|0|0501662876FEDCBA|961FCA772DF6066A",
        "mixedParityDegenerate|0123456789ABCDEF0022446688AACCEE|1|1|1|1501234012345678|F975D9E4123C5AB9",
        "mixedParityDegenerate|0123456789ABCDEF0022446688AACCEE|1|1|2|2501234FFFFFFFFF|FAD39DEC1F3696F0",
        "mixedParityDegenerate|0123456789ABCDEF0022446688AACCEE|1|1|3|3501662D35DFD9F9|1E62436B583CBB32",
    };

    private static IEnumerable<TestCaseData> QualityCases()
    {
        foreach (string row in PublicReferences)
        foreach (string function in new[] { "Wrap", "Unwrap" })
        {
            string[] f = row.Split('|');
            yield return new TestCaseData(f[0], f[1], f[2] == "1", f[3] == "1", f[4], f[5], f[6], function)
                .SetName($"PinQuality_{f[0]}_Format{f[4]}_{function}");
        }
    }

    [TestCaseSource(nameof(QualityCases))]
    public void Des3QualityWarningsDoNotAlterIndependentReferenceResults(string classification, string keyHex,
        bool parity, bool weak, string format, string clearHex, string cipherHex, string function)
    {
        string mechanism = "WRAP-DES3-PINBLOCK-" + format;
        foreach (string route in new[] { "typed", "dispatch", "no-sink", "legacy" })
        {
            VariableDictionary.Instance().Clear();
            var key = Key(keyHex, KeyAlgorithm.Tdea);
            string before = key.Serialize();
            var p = Parameters(mechanism);
            string parameterBefore = p.Value;
            var sink = route is "no-sink" or "legacy" ? null : new SemanticDiagnosticSink();
            string result = Call(function, p, key, Var(Hex(function == "Wrap" ? Pin : cipherHex)), sink,
                route == "no-sink" ? "typed" : route).Value;
            Assert.That(result, Is.EqualTo(Hex(function == "Wrap" ? cipherHex : Pin)).IgnoreCase, classification);
            if (sink is not null)
            {
                var expected = new List<SemanticDiagnosticCode>();
                if (parity) expected.Add(SemanticDiagnosticCode.DesParityViolation);
                if (weak) expected.Add(SemanticDiagnosticCode.WeakDes3Key);
                Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(expected));
                foreach (var d in sink.GetSnapshot())
                {
                    AssertWarning(d, function, mechanism, "k");
                    Assert.That(d.Message, Does.Not.Contain(keyHex).And.Not.Contain(Hex(Pin)));
                }
            }
            if (classification is "degenerate16" or "degenerate24" or "mixedParityDegenerate")
            {
                byte[] rawKey = Convert.FromHexString(keyHex);
                Assert.That(TripleDES.IsWeakKey(rawKey), Is.True);
                using var strict = TripleDES.Create();
                Assert.Throws<CryptographicException>(() => strict.Key = rawKey);
                Assert.That(ReferenceDes(Convert.FromHexString(cipherHex), encrypt: false),
                    Is.EqualTo(Convert.FromHexString(clearHex)), "Independent .NET single-DES verification.");
            }
            Assert.That(key.Serialize(), Is.EqualTo(before));
            Assert.That(p.Value, Is.EqualTo(parameterBefore));
        }
    }

    private static byte[] ReferenceDes(byte[] input, bool encrypt)
    {
        using var des = DES.Create();
        des.Key = Convert.FromHexString("0123456789ABCDEF");
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.None;
        using var transform = encrypt ? des.CreateEncryptor() : des.CreateDecryptor();
        return transform.TransformFinalBlock(input, 0, input.Length);
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void RunnerKeepsNestedOperationContextsAndSeparatesExecutions(string mechanism)
    {
        var p = Parameters(mechanism);
        var key = Key(ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? AesKey : NormalTdea,
            KeyAlgorithm.Hmac, KeyUsagePolicy.Restricted(KeyUsage.Encrypt), "bad");
        var sameBytes = Key(ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? AesKey : NormalTdea,
            ExpectedAlgorithm(mechanism), id: "good");
        Assert.That(key.Value, Is.EqualTo(sameBytes.Value));
        VariableDictionary.Instance().Add(p); VariableDictionary.Instance().Add(key); VariableDictionary.Instance().Add(sameBytes);
        VariableDictionary.Instance().Add(Var(Hex(Pin), "pin"));
        VariableDictionary.Instance().Add(Var(Hex(FixedCipher(mechanism)), "block"));
        var runner = new CryptoScriptRunner();
        var first = Execute(runner, "Print(Wrap(p,bad,pin)) Print(Unwrap(p,bad,block)) VAR later=0x(01)");
        Assert.That(first.Diagnostics.Select(d => d.DiagnosticCode), Is.EqualTo(new[]
        {
            SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict,
            SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict
        }));
        Assert.That(first.Diagnostics.Select(d => d.Location!.OperationId), Is.EqualTo(new[] { 2, 2, 4, 4 }));
        Assert.That(first.Diagnostics.Select(d => d.Location!.ParentOperationId), Is.EqualTo(new int?[] { 1, 1, 3, 3 }));
        Assert.That(first.Diagnostics.Select(d => d.Location!.StatementIndex), Is.EqualTo(new[] { 1, 1, 2, 2 }));
        Assert.That(first.Diagnostics.Select(d => d.Location!.FunctionName), Is.EqualTo(new[] { "Wrap", "Wrap", "Unwrap", "Unwrap" }));
        Assert.That(first.Diagnostics.Select(d => d.Location!.Identifier), Is.All.EqualTo("bad"));
        Assert.That(first.Diagnostics.Select(d => d.Location!.Mechanism), Is.All.EqualTo(mechanism));
        Guid id = first.Diagnostics[0].Location!.ExecutionId!.Value;
        Assert.That(id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(first.Diagnostics.Select(d => d.Location!.ExecutionId), Is.All.EqualTo(id));
        Assert.That(runner.SemanticErrors, Is.Empty);
        Assert.That(VariableDictionary.Instance().Contains("later"), Is.True);
        var second = Execute(runner, "VAR clean=Wrap(p,good,pin)");
        Assert.That(second.Diagnostics, Is.Empty); Assert.That(runner.Diagnostics, Is.Empty);
        var third = Execute(runner, "VAR again=Unwrap(p,bad,block)");
        Assert.That(third.Diagnostics[0].Location!.ExecutionId, Is.Not.EqualTo(id));
        Assert.That(first.Diagnostics.Count, Is.EqualTo(4));
        Assert.That(sameBytes.KeyType.Algorithm, Is.EqualTo(ExpectedAlgorithm(mechanism)));
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void WarningsSurviveTechnicalAbortWithoutPartialParameterMutation(string mechanism)
    {
        var p = Parameters(mechanism, fixedFields: false);
        var key = Key(ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? AesKey : new string('0', 32),
            KeyAlgorithm.Hmac, KeyUsagePolicy.Restricted(KeyUsage.Encrypt));
        VariableDictionary.Instance().Add(p); VariableDictionary.Instance().Add(key);
        VariableDictionary.Instance().Add(Var("0x(123)", "shortpin"));
        string before = p.Value, keyBefore = key.Serialize();
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => Execute(runner, "VAR fail=Wrap(p,k,shortpin) VAR later=0x(01)"));
        var expected = new List<SemanticDiagnosticCode>
            { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict };
        if (ExpectedAlgorithm(mechanism) == KeyAlgorithm.Tdea)
        {
            expected.Add(SemanticDiagnosticCode.DesParityViolation); expected.Add(SemanticDiagnosticCode.WeakDes3Key);
        }
        Assert.That(runner.Diagnostics.Where(d => d.Severity == SemanticDiagnosticSeverity.Warning)
            .Select(d => d.DiagnosticCode), Is.EqualTo(expected));
        Assert.That(runner.Diagnostics.Take(expected.Count).Select(d => d.Location!.OperationId), Is.All.EqualTo(1));
        Assert.That(runner.Diagnostics.Take(expected.Count).Select(d => d.Location!.Identifier), Is.All.EqualTo("k"));
        Assert.That(runner.Diagnostics[^1].Severity, Is.EqualTo(SemanticDiagnosticSeverity.Error));
        Assert.That(p.Value, Is.EqualTo(before)); Assert.That(key.Serialize(), Is.EqualTo(keyBefore));
        Assert.That(VariableDictionary.Instance().Contains("later"), Is.False);
    }

    private static CryptoScriptProgram Execute(CryptoScriptRunner runner, string script)
    {
        var parser = ParserBuilder.StringBuild(script); var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        return runner.Execute(context);
    }

    [TestCase("WRAP-DES3-PINBLOCK-1")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    [TestCase("WRAP-AES-PINBLOCK-4")]
    public void WarningDoesNotBlockGeneratedOutputAndUnwrapRestoresOnlyOriginalParameter(string mechanism)
    {
        var original = Parameters(mechanism, fixedFields: false);
        var other = Parameters(mechanism, fixedFields: false, id: "other");
        string otherBefore = other.Value;
        var key = Key(ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? AesKey : DegenerateTdea,
            KeyAlgorithm.Hmac, KeyUsagePolicy.Restricted(KeyUsage.Encrypt));
        string keyBefore = key.Serialize();
        var algorithm = new PinBlockAlgorithm(mechanism,
            ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? PinBlockCipherFamily.Aes : PinBlockCipherFamily.Des3,
            _ => 0, count => Enumerable.Repeat((byte)0xA5, count).ToArray());
        var sink = new SemanticDiagnosticSink();
        var encrypted = algorithm.Wrap(Arguments(original, key, Var(Hex(Pin)), sink));
        string field = mechanism[^1] switch { '1' => "TRANSACTION", '3' => "FILL", _ => "RANDOM" };
        string expected = mechanism[^1] switch
            { '1' => "0x(000000000)", '3' => "0x(AAAAAAAAA)", _ => "0x(A5A5A5A5A5A5A5A5)" };
        Assert.That(original.GetParameter(field), Is.EqualTo(expected));
        original.SetParameter(field, "invalid-not-an-input-on-unwrap");
        var clear = algorithm.Unwrap(Arguments(original, key, encrypted, sink));
        Assert.That(clear.Value, Is.EqualTo(Hex(Pin)));
        Assert.That(original.GetParameter(field), Is.EqualTo(expected));
        Assert.That(other.Value, Is.EqualTo(otherBefore));
        Assert.That(key.Serialize(), Is.EqualTo(keyBefore));
        Assert.That(sink.GetSnapshot().Count(d => d.DiagnosticCode == SemanticDiagnosticCode.KeyTypeConflict), Is.EqualTo(2));
        Assert.That(sink.GetSnapshot().Count(d => d.DiagnosticCode == SemanticDiagnosticCode.KeyUsageConflict), Is.EqualTo(2));
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void InvalidPinDataRemainsBlockingAfterWarnings(string mechanism)
    {
        foreach (string invalid in new[] { "0x(123)", "0x(1234567890123)", "0x(12A4)", "\"01234\"" })
        {
            var p = Parameters(mechanism, fixedFields: false);
            var key = Key(ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? AesKey : DegenerateTdea, KeyAlgorithm.Hmac,
                KeyUsagePolicy.Restricted(KeyUsage.Encrypt));
            string before = p.Value; var sink = new SemanticDiagnosticSink();
            Assert.Throws<ArgumentException>(() => Call("Wrap", p, key, Var(invalid), sink, "typed"), invalid);
            Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.KeyTypeConflict));
            Assert.That(p.Value, Is.EqualTo(before));
        }
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void InvalidLengthsContractsAndVariableIdentityRemainBlocking(string mechanism)
    {
        var p = Parameters(mechanism); var key = Key(ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? AesKey : DegenerateTdea,
            KeyAlgorithm.Hmac, KeyUsagePolicy.Restricted(KeyUsage.Encrypt));
        string before = p.Value; var sink = new SemanticDiagnosticSink();
        foreach (int size in new[] { 0, 7, 9, 15, 17 })
        {
            if (size == (ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? 16 : 8)) continue;
            Assert.Throws<ArgumentException>(() => Call("Unwrap", p, key, Var(Hex(new string('0', size * 2))), sink, "typed"));
            Assert.That(p.Value, Is.EqualTo(before));
        }
        foreach (int size in new[] { 8, 15, 17, 33 })
        {
            var badKey = Key(new string('0', size * 2), KeyAlgorithm.Unknown);
            Assert.Throws<ArgumentException>(() => Call("Wrap", p, badKey, Var(Hex(Pin)), null, "typed"));
            Assert.Throws<ArgumentException>(() => Call("Unwrap", p, badKey, Var(Hex(FixedCipher(mechanism))), null, "typed"));
        }
        p.GetParameters()["#UNKNOWN"] = "test";
        Assert.Throws<FunctionContractException>(() => Call("Wrap", p, key, Var(Hex(Pin)), null, "typed"));
        p = Parameters(mechanism); p.SetParameter("PAD", "NONE");
        Assert.Throws<FunctionContractException>(() => Call("Wrap", p, key, Var(Hex(Pin)), null, "typed"));
        if (mechanism.EndsWith("-1") || mechanism.EndsWith("-3") || mechanism.EndsWith("-4"))
        {
            p = Parameters(mechanism, id: "");
            Assert.Throws<ArgumentException>(() => Call("Wrap", p, key, Var(Hex(Pin)), null, "typed"));
        }
        Assert.That(() => AlgorithmFactory.Create(mechanism).Wrap(new AlgorithmCallArguments(new[]
        {
            new AlgorithmCallArgument(Parameters(mechanism).Value, Parameters(mechanism)),
            new AlgorithmCallArgument(key.Value, key),
            new AlgorithmCallArgument(Hex(Pin), kind: ResolvedCallArgumentKind.HexLiteral)
        })), Throws.ArgumentException);
    }

    [TestCase("WRAP-DES3-PINBLOCK-0")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    [TestCase("WRAP-AES-PINBLOCK-4")]
    public void PanRequirementsRemainTechnicalAndAtomic(string mechanism)
    {
        foreach (string pan in new[] { "0x(123456789)", "0x(12345678901234567890)", "0x(12345678901A)" })
        foreach (string function in new[] { "Wrap", "Unwrap" })
        {
            var p = Parameters(mechanism, fixedFields: false);
            p.SetParameter("PAN", pan); string before = p.Value;
            var key = Key(ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? AesKey : DegenerateTdea, KeyAlgorithm.Hmac);
            var sink = new SemanticDiagnosticSink();
            Assert.Throws<ArgumentException>(() => Call(function, p, key,
                Var(Hex(function == "Wrap" ? Pin : FixedCipher(mechanism))), sink, "typed"));
            Assert.That(sink.GetSnapshot()[0].DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyTypeConflict));
            Assert.That(p.Value, Is.EqualTo(before));
        }
        var missing = Parameters(mechanism); missing.GetParameters().Remove("#PAN");
        Assert.Throws<FunctionContractException>(() => Call("Wrap", missing,
            Key(ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? AesKey : DegenerateTdea, KeyAlgorithm.Hmac),
            Var(Hex(Pin)), null, "typed"));
    }

    [TestCaseSource(nameof(Mechanisms))]
    public void WeakOrMisdeclaredKeyCannotMakeInvalidDecodedPinFieldsValid(string mechanism)
    {
        var p = Parameters(mechanism);
        var key = Key(ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? AesKey : DegenerateTdea, KeyAlgorithm.Hmac,
            KeyUsagePolicy.Restricted(KeyUsage.Encrypt));
        int format = mechanism[^1] - '0';
        foreach (string defect in new[] { "format", "length", "digits", "fill" })
        {
            if (format == 1 && defect == "fill") continue; // Every hex transaction nibble is permitted.
            string field = format == 4 ? "4501234AAAAAAAAA0011223344556677" :
                format switch { 0 => "0501234FFFFFFFFF", 1 => "1501234012345678",
                    2 => "2501234FFFFFFFFF", _ => "3501234ABCDEFABC" };
            char[] chars = field.ToCharArray();
            if (defect == "format") chars[0] = '9';
            if (defect == "length") chars[1] = '3';
            if (defect == "digits") chars[3] = 'A';
            if (defect == "fill") chars[7] = '0';
            byte[] raw = Convert.FromHexString(new string(chars));
            byte[] encrypted;
            if (format == 4) encrypted = ReferenceAesFormat4(raw);
            else
            {
                if (format is 0 or 3) raw = Xor(raw, Convert.FromHexString("0000456789012345"));
                encrypted = ReferenceDes(raw, encrypt: true);
            }
            var sink = new SemanticDiagnosticSink(); string before = p.Value;
            var error = Assert.Throws<FormatException>(() => Call("Unwrap", p, key,
                Var(Hex(Convert.ToHexString(encrypted))), sink, "typed"), defect);
            Assert.That(error!.Message, Is.EqualTo(InvalidBlock));
            Assert.That(p.Value, Is.EqualTo(before));
            Assert.That(sink.GetSnapshot()[0].DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyTypeConflict));
        }
    }

    private static byte[] ReferenceAesFormat4(byte[] pinField)
    {
        using var aes = Aes.Create(); aes.Key = Convert.FromHexString(AesKey);
        aes.Mode = CipherMode.ECB; aes.Padding = PaddingMode.None;
        using var transform = aes.CreateEncryptor();
        byte[] first = transform.TransformFinalBlock(pinField, 0, pinField.Length);
        byte[] mixed = Xor(first, Convert.FromHexString("41234567890123456000000000000000"));
        return transform.TransformFinalBlock(mixed, 0, mixed.Length);
    }

    [TestCase("0")]
    [TestCase("1")]
    [TestCase("2")]
    [TestCase("3")]
    public void SuccessfulWeakKeyOperationsReportAllFourCategoriesExactlyOnceInOrder(string format)
    {
        string[] reference = PublicReferences.Select(row => row.Split('|'))
            .Single(row => row[0] == "zero16" && row[4] == format);
        string mechanism = "WRAP-DES3-PINBLOCK-" + format;
        foreach (string function in new[] { "Wrap", "Unwrap" })
        {
            var key = Key(reference[1], KeyAlgorithm.Aes, KeyUsagePolicy.Restricted(KeyUsage.Encrypt));
            var p = Parameters(mechanism); string before = key.Serialize(), parameterBefore = p.Value;
            var sink = new SemanticDiagnosticSink();
            string actual = Call(function, p, key, Var(Hex(function == "Wrap" ? Pin : reference[6])), sink, "dispatch").Value;
            Assert.That(actual, Is.EqualTo(Hex(function == "Wrap" ? reference[6] : Pin)).IgnoreCase);
            Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(new[]
            {
                SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict,
                SemanticDiagnosticCode.DesParityViolation, SemanticDiagnosticCode.WeakDes3Key
            }));
            foreach (var diagnostic in sink.GetSnapshot())
            {
                AssertWarning(diagnostic, function, mechanism, "k");
                Assert.That(diagnostic.Message, Does.Not.Contain(reference[1]));
            }
            Assert.That(key.Serialize(), Is.EqualTo(before)); Assert.That(p.Value, Is.EqualTo(parameterBefore));
        }
    }

    [TestCase("WRAP-DES3-PINBLOCK-1")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    [TestCase("WRAP-AES-PINBLOCK-4")]
    public void GeneratedFieldFailureAfterWarningsDoesNotPartiallyMutateParameters(string mechanism)
    {
        var p = Parameters(mechanism, fixedFields: false);
        var key = Key(ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? AesKey : DegenerateTdea,
            KeyAlgorithm.Hmac, KeyUsagePolicy.Restricted(KeyUsage.Encrypt));
        string before = p.Value, keyBefore = key.Serialize();
        int generated = 0;
        var algorithm = new PinBlockAlgorithm(mechanism,
            ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes ? PinBlockCipherFamily.Aes : PinBlockCipherFamily.Des3,
            limit => generated++ == 0 ? 0 : limit,
            _ => new byte[7]);
        var sink = new SemanticDiagnosticSink();
        Assert.Throws<InvalidOperationException>(() => algorithm.Wrap(Arguments(p, key, Var(Hex(Pin)), sink)));
        Assert.That(p.Value, Is.EqualTo(before)); Assert.That(key.Serialize(), Is.EqualTo(keyBefore));
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(
            ExpectedAlgorithm(mechanism) == KeyAlgorithm.Aes
                ? new[] { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict }
                : new[] { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict, SemanticDiagnosticCode.WeakDes3Key }));
    }

    private static byte[] Xor(byte[] left, byte[] right) => left.Zip(right, (a, b) => (byte)(a ^ b)).ToArray();

    [TestCase("000102030405060708090A0B0C0D0E0F", "6685B016B0CEC63CB7E0F2663C9551EC")]
    [TestCase("000102030405060708090A0B0C0D0E0F1011121314151617", "3D82F10B6E406770B509275DE21981D2")]
    [TestCase("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "E229CDA360D0A794E2B8846E0FC505B0")]
    public void AesFormat4KeepsAllKeySizesAndExactExistingReferenceResults(string hex, string cipher)
    {
        var p = Parameters("WRAP-AES-PINBLOCK-4");
        var key = Key(hex, KeyAlgorithm.Tdea, KeyUsagePolicy.Restricted(KeyUsage.Encrypt));
        var sink = new SemanticDiagnosticSink();
        Assert.That(Call("Wrap", p, key, Var(Hex(Pin)), sink, "typed").Value, Is.EqualTo(Hex(cipher)).IgnoreCase);
        Assert.That(Call("Unwrap", p, key, Var(Hex(cipher)), sink, "typed").Value, Is.EqualTo(Hex(Pin)));
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(new[]
        {
            SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict,
            SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict
        }));
    }

    [Test]
    public void PinBlockOptInIsSingleBlockOnlyAndSharedPrimitivesRemainStrict()
    {
        byte[] weak = Convert.FromHexString(DegenerateTdea), block = Convert.FromHexString("2501234FFFFFFFFF");
        byte[] copy = (byte[])block.Clone(), keyCopy = (byte[])weak.Clone();
        Assert.That(DES3_ECB.EncryptPinBlockForAnalysis(weak, block), Is.EqualTo(Convert.FromHexString("FAD39DEC1F3696F0")));
        Assert.That(DES3_ECB.DecryptPinBlockForAnalysis(weak, Convert.FromHexString("FAD39DEC1F3696F0")), Is.EqualTo(block));
        Assert.That(block, Is.EqualTo(copy)); Assert.That(weak, Is.EqualTo(keyCopy));
        Assert.Throws<CryptographicException>(() => DES3_ECB.EncryptNoPadding(weak, block));
        Assert.Throws<CryptographicException>(() => DES3_ECB.DecryptNoPadding(weak, block));
        Assert.Throws<ArgumentException>(() => DES3.ValidateUsableKey(weak));
        Assert.Throws<CryptographicException>(() => DES3_CBC.EncryptNoPadding(weak, new byte[8], block));
        Assert.Throws<ArgumentException>(() => DES3_RETAIL.ComputeMac(weak, block, "ISO-9797-M2", 8));
        foreach (int length in new[] { 0, 7, 9, 16 })
        {
            Assert.Throws<ArgumentException>(() => DES3_ECB.EncryptPinBlockForAnalysis(weak, new byte[length]));
            Assert.Throws<ArgumentException>(() => DES3_ECB.DecryptPinBlockForAnalysis(weak, new byte[length]));
        }
        foreach (int length in new[] { 0, 8, 15, 17, 25 })
            Assert.Throws<ArgumentException>(() => DES3_ECB.EncryptPinBlockForAnalysis(new byte[length], block));
        Assert.Throws<ArgumentNullException>(() => DES3_ECB.EncryptPinBlockForAnalysis(null!, block));
        Assert.Throws<ArgumentNullException>(() => DES3_ECB.DecryptPinBlockForAnalysis(weak, null!));
        Assert.Throws<ArgumentException>(() => KeyUsagePolicy.Restricted(KeyUsage.Encrypt).EnsureAllows(KeyUsage.PinEncrypt));
        Assert.That(new SemanticDiagnosticSink().GetSnapshot(), Is.Empty);
    }
}
