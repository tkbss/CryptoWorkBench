using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using Algorithm = CryptoScript.CryptoAlgorithm.CryptoAlgorithm;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class GenerateKeyContractTests
{
    private static readonly string[] Routes = ["Runner", "Invocation", "Operations", "Factory", "Legacy"];
    private static IEnumerable<MechanismRegistryEntry> Generators =>
        MechanismRegistry.Entries.Where(e => e.Supports(CryptoScriptFunction.GenerateKey));

    [SetUp]
    public void Reset() => EmvAcFixtures.Reset();

    public static IEnumerable<TestCaseData> GenerationCases()
    {
        foreach (var entry in Generators)
        {
            int[] sizes = entry.GeneratedKeyAlgorithm switch
            {
                KeyAlgorithm.Aes => [128, 192, 256],
                KeyAlgorithm.Tdea => [128, 192],
                _ => [8, 136, 512]
            };
            foreach (int bits in sizes)
                foreach (string route in Routes)
                    foreach (bool import in new[] { false, true })
                        yield return new TestCaseData(entry, bits, route, import)
                            .SetName($"GenerateKey_{entry.CanonicalName}_{bits}_{route}_{(import ? "import" : "random")}");
        }
    }

    private static OperationInvocation Invocation(string mechanism, string size) => new(new[]
    {
        new ResolvedCallArgument(mechanism, ResolvedCallArgumentKind.Mechanism),
        new ResolvedCallArgument(size, size.StartsWith("0x(", StringComparison.Ordinal)
            ? ResolvedCallArgumentKind.HexLiteral : ResolvedCallArgumentKind.OtherLiteral)
    });

    private static KeyVariableDeclaration Generate(string mechanism, string size, string route)
    {
        VariableDeclaration result;
        if (route == "Runner")
        {
            var parser = ParserBuilder.StringBuild($"KEY generated=GenerateKey({mechanism},{size})");
            var runner = new CryptoScriptRunner();
            var program = runner.Execute(parser.program());
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            Assert.That(runner.SemanticErrors, Is.Empty);
            result = program.Statements.Single() as VariableDeclaration
                ?? throw new AssertionException("GenerateKey must return a variable.");
        }
        else result = route switch
        {
            "Invocation" => new CryptoOperations().GenerateKey(Invocation(mechanism, size)),
            "Operations" => new CryptoOperations().GenerateKey(mechanism, size),
            "Factory" => AlgorithmFactory.Create(mechanism).GenerateKey(mechanism, size),
            "Legacy" => OperationFactory.CreateOperation("GenerateKey")([mechanism, size]),
            _ => throw new ArgumentException("Unknown test route.")
        };
        Assert.That(result, Is.TypeOf<KeyVariableDeclaration>());
        return (KeyVariableDeclaration)result;
    }

    [TestCaseSource(nameof(GenerationCases))]
    public void AllApprovedMechanismsPreserveTypeLengthBytesAndFamilyCompatibility(
        MechanismRegistryEntry entry, int bits, string route, bool import)
    {
        // Alternating bytes exercise external TDEA parity properties without repair.
        string bytes = Convert.ToHexString(Enumerable.Range(0, bits / 8).Select(i => (byte)i).ToArray());
        string supplied = "0x(" + bytes + ")";
        var key = Generate(entry.CanonicalName, import ? supplied : bits.ToString(), route);
        Assert.That(key.KeyType, Is.EqualTo(KeyType.Secret(entry.GeneratedKeyAlgorithm!.Value)));
        Assert.That(key.KeyType.MaterialKind, Is.EqualTo(KeyMaterialKind.Secret));
        Assert.That(key.KeySizeInBits, Is.EqualTo(new KeySize(bits)));
        Assert.That(key.KeySize, Is.EqualTo(bits.ToString()));
        Assert.That(key.Type, Is.TypeOf<CryptoTypeKey>());
        Assert.That(key.ValueFormat, Is.EqualTo(FormatConversions.HEX));
        Assert.That(FormatConversions.HexStringToByteArray(key.Value), Has.Length.EqualTo(bits / 8));
        if (import) Assert.That(key.Value, Is.EqualTo(supplied));
        if (!import && entry.GeneratedKeyAlgorithm == KeyAlgorithm.Tdea)
            Assert.That(TripleDES.IsWeakKey(FormatConversions.HexStringToByteArray(key.Value)), Is.False);

        var restored = KeyVariableDeclaration.Deserialize(key.Serialize());
        Assert.That(restored.KeyType, Is.EqualTo(key.KeyType));
        Assert.That(restored.KeySizeInBits, Is.EqualTo(key.KeySizeInBits));
        Assert.That(restored.Value, Is.EqualTo(key.Value));

        // Use the generated key with another compatible mechanism. Modes and HMAC
        // digest names do not become a permanent restriction on this KEY.
        string target = entry.GeneratedKeyAlgorithm switch
        {
            KeyAlgorithm.Aes => entry.CanonicalName == "AES-CMAC" ? "AES-ECB" : "AES-CMAC",
            KeyAlgorithm.Tdea => entry.CanonicalName == "DES3-RETAIL" ? "DES3-ECB" : "DES3-RETAIL",
            _ => entry.CanonicalName == "HMAC-SHA256" ? "HMAC-SHA512" : "HMAC-SHA256"
        };
        var algorithm = AlgorithmFactory.Create(target);
        var p = algorithm.GenerateParameters(target);
        if (target.EndsWith("ECB", StringComparison.Ordinal)) p.SetParameter("PAD", "NONE");
        var sink = new SemanticDiagnosticSink();
        var args = new AlgorithmCallArguments(new[]
        {
            new AlgorithmCallArgument(p.Value, p, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(key.Value, key, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument("0x(00112233445566778899AABBCCDDEEFF)")
        }, sink);
        string before = key.Serialize();
        var used = target.EndsWith("ECB", StringComparison.Ordinal) ? algorithm.Encrypt(args) : algorithm.Mac(args);
        Assert.That(used.Value, Is.Not.Empty);
        Assert.That(sink.GetSnapshot().Where(d => d.Type == "KeyMetadata"), Is.Empty);
        Assert.That(key.Serialize(), Is.EqualTo(before));
    }

    [Test]
    public void RegistryAndConcreteFamiliesHaveOneUnambiguousImmutableMapping()
    {
        Assert.That(Generators.Count(), Is.EqualTo(23));
        Assert.That(Generators.Count(e => e.GeneratedKeyAlgorithm == KeyAlgorithm.Aes), Is.EqualTo(8));
        Assert.That(Generators.Count(e => e.GeneratedKeyAlgorithm == KeyAlgorithm.Tdea), Is.EqualTo(4));
        Assert.That(Generators.Count(e => e.GeneratedKeyAlgorithm == KeyAlgorithm.Hmac), Is.EqualTo(11));
        foreach (var entry in MechanismRegistry.Entries)
        {
            Assert.That(entry.GeneratedKeyAlgorithm.HasValue, Is.EqualTo(entry.Supports(CryptoScriptFunction.GenerateKey)));
            if (!entry.Supports(CryptoScriptFunction.GenerateKey)) continue;
            KeyAlgorithm expected = AlgorithmFactory.Create(entry.CanonicalName) switch
            {
                CryptoScript.CryptoAlgorithm.AES.AES => KeyAlgorithm.Aes,
                CryptoScript.CryptoAlgorithm.DES3.DES3 => KeyAlgorithm.Tdea,
                CryptoScript.CryptoAlgorithm.HMAC.HMAC => KeyAlgorithm.Hmac,
                _ => throw new AssertionException("Unreviewed GenerateKey implementation.")
            };
            Assert.That(entry.GeneratedKeyAlgorithm, Is.EqualTo(expected));
        }
        Assert.That(typeof(MechanismRegistryEntry).GetProperty(nameof(MechanismRegistryEntry.GeneratedKeyAlgorithm))!.SetMethod, Is.Null);
    }

    [TestCase(null, true)]
    [TestCase(KeyAlgorithm.Unknown, true)]
    [TestCase((KeyAlgorithm)999, true)]
    [TestCase(KeyAlgorithm.Rsa, true)]
    [TestCase(KeyAlgorithm.Ec, true)]
    [TestCase(KeyAlgorithm.Aes, false)]
    public void RegistryRejectsMissingUndefinedOrInapplicableGenerationAlgorithm(KeyAlgorithm? algorithm, bool supports) =>
        Assert.Throws<ArgumentException>(() => new MechanismRegistryEntry("TEST", "Test", "test.md",
            supports ? [CryptoScriptFunction.GenerateKey] : [], generatedKeyAlgorithm: algorithm));

    public static IEnumerable<TestCaseData> ForbiddenCases()
    {
        foreach (string mechanism in MechanismRegistry.Entries
            .Where(e => !e.Supports(CryptoScriptFunction.GenerateKey)).Select(e => e.CanonicalName).Append("UNKNOWN-MECHANISM"))
            foreach (string route in Routes)
                yield return new TestCaseData(mechanism, route);
    }

    [TestCaseSource(nameof(ForbiddenCases))]
    public void UnsupportedAndUnknownMechanismsNeverProduceAKey(string mechanism, string route)
    {
        Assert.That(() => Generate(mechanism, "128", route), Throws.Exception);
        Assert.That(VariableDictionary.Instance().Contains("generated"), Is.False);
    }

    [Test]
    public void ConcreteAlgorithmsRejectEveryRegistryEntryOutsideTheirApprovedFamily()
    {
        foreach (var implementation in new (Algorithm Instance, KeyAlgorithm Algorithm)[]
        {
            (new CryptoScript.CryptoAlgorithm.AES.AES(), KeyAlgorithm.Aes),
            (new CryptoScript.CryptoAlgorithm.DES3.DES3(), KeyAlgorithm.Tdea),
            (new CryptoScript.CryptoAlgorithm.HMAC.HMAC(), KeyAlgorithm.Hmac)
        })
        {
            foreach (var entry in MechanismRegistry.Entries.Where(e => e.GeneratedKeyAlgorithm != implementation.Algorithm))
                Assert.That(() => implementation.Instance.GenerateKey(entry.CanonicalName, "128"),
                    Throws.Exception, entry.CanonicalName);
            Assert.That(() => implementation.Instance.GenerateKey("UNKNOWN-MECHANISM", "128"), Throws.Exception);
        }
    }

    [TestCase("AES-ECB")]
    [TestCase("DES3-ECB")]
    [TestCase("HMAC-SHA256")]
    public void PrefixAndCaseNormalizationAgreeAcrossPublicDirectRoutes(string mechanism)
    {
        foreach (string route in Routes.Where(r => r != "Runner"))
            Assert.That(Generate("#mech:" + mechanism.ToLowerInvariant(), "128", route).KeyType.Algorithm,
                Is.EqualTo(MechanismRegistry.Entries.Single(e => e.CanonicalName == mechanism).GeneratedKeyAlgorithm));
    }

    [Test]
    public void AllBaseImplementationsRejectRatherThanReturningEmptyKeys()
    {
        foreach (var algorithm in new Algorithm[]
            { new(), new SymmetricCryptoAlgorithm(), new AsymmtricCryptoAlgorithm(), new CryptoScript.CryptoAlgorithm.Rsa() })
        {
            Assert.Throws<NotSupportedException>(() => algorithm.GenerateKey("AES-ECB", "128"));
            Assert.Throws<NotSupportedException>(() => algorithm.GenerateKey("KDF-HKDF", "128"));
        }
    }

    [TestCase(" 128")]
    [TestCase("+128")]
    [TestCase("128 ")]
    public void AcceptedDirectAesSizeSpellingsHaveCanonicalTypedLength(string size)
    {
        foreach (string route in Routes.Where(r => r != "Runner"))
        {
            var key = Generate("AES-ECB", size, route);
            Assert.That(key.KeySize, Is.EqualTo("128"));
            Assert.That(key.KeySizeInBits, Is.EqualTo(new KeySize(128)));
            Assert.That(FormatConversions.HexStringToByteArray(key.Value), Has.Length.EqualTo(16));
        }
    }

    public static IEnumerable<TestCaseData> InvalidInputCases()
    {
        foreach (string mechanism in new[] { "AES-ECB", "DES3-ECB", "HMAC-SHA256" })
        {
            string validHex = new('0', 32);
            foreach (string value in new[] { "0", "7", "129", "-8", "0x()", "0x(GG)", "0x(001)",
                "0x(" + validHex, "0x(" + validHex + "]", "0x(" + validHex + ")junk" })
                foreach (string route in Routes.Where(r => r != "Runner"))
                    yield return new TestCaseData(mechanism, value, route);
        }
    }

    [TestCaseSource(nameof(InvalidInputCases))]
    public void InvalidLengthOrIncompleteHexNeverProducesAKey(string mechanism, string value, string route) =>
        Assert.That(() => Generate(mechanism, value, route), Throws.Exception);

    [TestCase("AES-ECB", 64)]
    [TestCase("AES-ECB", 127)]
    [TestCase("AES-ECB", 512)]
    [TestCase("DES3-ECB", 64)]
    [TestCase("DES3-ECB", 256)]
    [TestCase("HMAC-SHA256", 0)]
    [TestCase("HMAC-SHA256", 7)]
    public void InvalidLengthsAlsoFailThroughTheRunner(string mechanism, int bits)
    {
        var error = Assert.Throws<SemanticErrorException>(() => Generate(mechanism, bits.ToString(), "Runner"));
        Assert.That(error!.SemanticError.FunctionName, Is.EqualTo("GenerateKey"));
        Assert.That(VariableDictionary.Instance().Contains("generated"), Is.False);
    }

    [TestCase("0x(00000000000000000000000000000000)")]
    [TestCase("0x(000000000000000000000000000000000000000000000000)")]
    [TestCase("0x(0022446688AACCEEFFDDBB9977553311)")]
    public void ExternalWeakOrInvalidParityTdeaBytesAreNeverRepaired(string supplied)
    {
        foreach (var entry in Generators.Where(e => e.GeneratedKeyAlgorithm == KeyAlgorithm.Tdea))
            foreach (string route in Routes)
                Assert.That(Generate(entry.CanonicalName, supplied, route).Value, Is.EqualTo(supplied));
    }

    [Test]
    public void DocumentationTracksRegistryPermissionsAlgorithmsAndExactFilenames()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "InfoDocs");
        var exactNames = Directory.EnumerateFiles(directory).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in MechanismRegistry.Entries)
        {
            Assert.That(exactNames.Contains(entry.DocumentationFileName), Is.True, entry.CanonicalName);
            string document = File.ReadAllText(Path.Combine(directory, entry.DocumentationFileName));
            bool documented = Regex.IsMatch(document, @"^\|\s*GenerateKey\s*\|", RegexOptions.Multiline);
            Assert.That(documented, Is.EqualTo(entry.Supports(CryptoScriptFunction.GenerateKey)), entry.CanonicalName);
            if (!documented) continue;
            Assert.That(document, Does.Contain("### Key Generation and Import"));
            Assert.That(document, Does.Contain($"Secret({entry.GeneratedKeyAlgorithm})"));
            Assert.That(document, Does.Contain("random").And.Contain("import"));
            Assert.That(document, Does.Contain("does not restrict"));
        }
    }
}
