using CryptoScript.CryptoAlgorithm;
using CryptoScript.Documentation;
using CryptoScript.Model;
using System.Collections.Frozen;
using System.Reflection;
using System.Text.RegularExpressions;

using Algorithm = CryptoScript.CryptoAlgorithm.CryptoAlgorithm;

namespace CryptoScriptUnitTest;

public class MechanismRegistryTests
{
    // Formatting tolerance is limited to Markdown list indentation, whitespace around
    // the list marker and colon, and trailing line whitespace.
    private static readonly Regex MechanismDescriptionLine = new(
        @"^\s*-\s+(?<name>[A-Z0-9-]+)\s+:\s+(?<description>\S(?:.*\S)?)\s*$",
        RegexOptions.CultureInvariant);

    private static readonly string[] RoadmapMechanisms =
    {
        "WRAP-AES", "WRAP-DES3", "RSA-PSS", "RSA-OAEP", "ECDSA"
    };

    [Test]
    public void ContainsExactlyFiftyThreeRegisteredMechanisms()
    {
        Assert.That(MechanismRegistry.Entries, Has.Count.EqualTo(53));
        Assert.That(
            MechanismRegistry.Entries.Select(entry => entry.CanonicalName),
            Is.EquivalentTo(MechanismList.Instance.Mechanisms));
    }

    [Test]
    public void EntriesHaveUniqueNamesAndDocumentationFiles()
    {
        string[] names = MechanismRegistry.Entries
            .Select(entry => entry.CanonicalName)
            .ToArray();
        string[] documentationFiles = MechanismRegistry.Entries
            .Select(entry => entry.DocumentationFileName)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                names.Distinct(StringComparer.Ordinal).ToArray(),
                Has.Length.EqualTo(names.Length));
            Assert.That(
                documentationFiles.Distinct(StringComparer.Ordinal).ToArray(),
                Has.Length.EqualTo(documentationFiles.Length));
        });
    }

    [Test]
    public void EntriesAreSortedByCanonicalNameAndReadOnly()
    {
        string[] names = MechanismRegistry.Entries
            .Select(entry => entry.CanonicalName)
            .ToArray();

        Assert.That(names, Is.EqualTo(names.OrderBy(name => name, StringComparer.Ordinal)));
        Assert.That(MechanismRegistry.Entries, Is.InstanceOf<IList<MechanismRegistryEntry>>());

        var list = (IList<MechanismRegistryEntry>)MechanismRegistry.Entries;
        Assert.Multiple(() =>
        {
            Assert.That(list.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => list.Add(
                new MechanismRegistryEntry(
                    "TEST",
                    "Test.",
                    "Info.Mech.TEST.md",
                    Array.Empty<CryptoScriptFunction>())));
        });
    }

    [Test]
    public void SupportedFunctionsAreCopiedIntoImmutableSets()
    {
        var source = new[] { CryptoScriptFunction.Encrypt };
        var entry = new MechanismRegistryEntry(
            "TEST",
            "Test.",
            "Info.Mech.TEST.md",
            source);

        source[0] = CryptoScriptFunction.Decrypt;

        Assert.Multiple(() =>
        {
            Assert.That(
                entry.SupportedFunctions,
                Is.InstanceOf<FrozenSet<CryptoScriptFunction>>());
            Assert.That(entry.SupportedFunctions, Is.EquivalentTo(new[]
            {
                CryptoScriptFunction.Encrypt
            }));
            Assert.That(
                typeof(MechanismRegistryEntry)
                    .GetProperty(nameof(MechanismRegistryEntry.SupportedFunctions))!
                    .SetMethod,
                Is.Null);
        });
    }

    [Test]
    public void SupportsPerformsAnExactTypedMembershipCheck()
    {
        foreach (MechanismRegistryEntry entry in MechanismRegistry.Entries)
        {
            foreach (CryptoScriptFunction function in Enum.GetValues<CryptoScriptFunction>())
            {
                Assert.That(
                    entry.Supports(function),
                    Is.EqualTo(entry.SupportedFunctions.Contains(function)),
                    $"{entry.CanonicalName}: {function}");
            }
        }
    }

    [Test]
    public void SupportedFunctionsMatchConcreteAlgorithmImplementations()
    {
        foreach (MechanismRegistryEntry entry in MechanismRegistry.Entries)
        {
            IReadOnlySet<CryptoScriptFunction> implementedFunctions =
                DiscoverImplementedFunctions(entry.CanonicalName);

            Assert.That(
                entry.SupportedFunctions,
                Is.EquivalentTo(implementedFunctions),
                entry.CanonicalName);
        }
    }

    [Test]
    public void HashKeyGenerationIsAnExplicitRejectionNotSupport()
    {
        foreach (MechanismRegistryEntry entry in MechanismRegistry.Entries.Where(
                     entry => entry.CanonicalName.StartsWith("HASH-", StringComparison.Ordinal)))
        {
            Algorithm algorithm = AlgorithmFactory.Create(entry.CanonicalName);

            Assert.Multiple(() =>
            {
                Assert.That(
                    entry.Supports(CryptoScriptFunction.GenerateKey),
                    Is.False,
                    entry.CanonicalName);
                Assert.Throws<ArgumentException>(
                    () => algorithm.GenerateKey(entry.CanonicalName, "256"),
                    entry.CanonicalName);
            });
        }
    }

    [TestCase("AES-CBC", CryptoScriptFunction.Mac)]
    [TestCase("AES-CBC-MAC", CryptoScriptFunction.Encrypt)]
    [TestCase("AES-CBC-MAC", CryptoScriptFunction.Decrypt)]
    [TestCase("AES-CMAC", CryptoScriptFunction.Encrypt)]
    [TestCase("AES-CMAC", CryptoScriptFunction.Decrypt)]
    [TestCase("DES3-ECB", CryptoScriptFunction.Mac)]
    [TestCase("DES3-CMAC", CryptoScriptFunction.Encrypt)]
    [TestCase("DES3-RETAIL", CryptoScriptFunction.Decrypt)]
    [TestCase("HASH-SHA256", CryptoScriptFunction.GenerateKey)]
    [TestCase("HMAC-SHA256", CryptoScriptFunction.Hash)]
    [TestCase("KDF-HKDF", CryptoScriptFunction.Encrypt)]
    [TestCase("WRAP-AES-TR31", CryptoScriptFunction.Parameters)]
    [TestCase("WRAP-DES3-TR31", CryptoScriptFunction.BlockHeader)]
    public void KnownUnsupportedFunctionCombinationsAreExcluded(
        string mechanism,
        CryptoScriptFunction function)
    {
        Assert.That(MechanismRegistry.TryGet(mechanism, out MechanismRegistryEntry? entry), Is.True);
        Assert.That(entry!.Supports(function), Is.False);
    }

    [Test]
    public void ExactSearchReturnsEveryRegisteredEntry()
    {
        foreach (MechanismRegistryEntry expected in MechanismRegistry.Entries)
        {
            bool found = MechanismRegistry.TryGet(
                expected.CanonicalName,
                out MechanismRegistryEntry? actual);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True, expected.CanonicalName);
                Assert.That(actual, Is.SameAs(expected), expected.CanonicalName);
            });
        }
    }

    [TestCase("aes-cbc")]
    [TestCase("AES-CBC ")]
    [TestCase("UNKNOWN")]
    [TestCase("")]
    [TestCase(null)]
    public void ExactSearchRejectsNonCanonicalOrUnknownNames(string? name)
    {
        Assert.That(MechanismRegistry.TryGet(name, out MechanismRegistryEntry? entry), Is.False);
        Assert.That(entry, Is.Null);
    }

    [Test]
    public void RoadmapMechanismsAreExcluded()
    {
        foreach (string mechanism in RoadmapMechanisms)
        {
            Assert.That(
                MechanismRegistry.Entries.Select(entry => entry.CanonicalName),
                Does.Not.Contain(mechanism));
            Assert.That(MechanismRegistry.TryGet(mechanism, out _), Is.False);
        }
    }

    [Test]
    public void DescriptionsMatchTheMechanismDocumentationIndex()
    {
        IReadOnlyDictionary<string, string> documentedDescriptions =
            ReadDocumentedMechanismDescriptions();

        Assert.That(documentedDescriptions, Has.Count.EqualTo(53));

        foreach (MechanismRegistryEntry entry in MechanismRegistry.Entries)
        {
            Assert.That(
                documentedDescriptions.TryGetValue(
                    entry.CanonicalName,
                    out string? documentedDescription),
                Is.True,
                $"Info.Mechanisms.md contains {entry.CanonicalName}");
            Assert.That(
                entry.Description,
                Is.EqualTo(documentedDescription),
                entry.CanonicalName);
        }
    }

    [Test]
    public void DocumentationFilesMatchTheDocumentationProviderMapping()
    {
        var documentationProvider = new FileInfoDocumentationProvider();
        string infoDocsDirectory = Path.Combine(AppContext.BaseDirectory, "InfoDocs");

        foreach (MechanismRegistryEntry entry in MechanismRegistry.Entries)
        {
            string canonicalFileName = $"Info.Mech.{entry.CanonicalName}.md";
            string registryDocumentPath = Path.Combine(
                infoDocsDirectory,
                entry.DocumentationFileName);

            bool providerFound = documentationProvider.TryGetDocumentation(
                entry.CanonicalName,
                out string providerDocumentation);

            Assert.Multiple(() =>
            {
                Assert.That(
                    entry.DocumentationFileName,
                    Is.EqualTo(canonicalFileName),
                    entry.CanonicalName);
                Assert.That(
                    File.Exists(registryDocumentPath),
                    Is.True,
                    $"{entry.CanonicalName} references {entry.DocumentationFileName}");
                Assert.That(
                    providerFound,
                    Is.True,
                    $"documentation provider maps {entry.CanonicalName}");
            });

            if (!File.Exists(registryDocumentPath) || !providerFound)
                continue;

            Assert.That(
                File.ReadAllText(registryDocumentPath),
                Is.EqualTo(providerDocumentation),
                $"{entry.DocumentationFileName} matches the provider mapping for {entry.CanonicalName}");
        }
    }

    private static IReadOnlyDictionary<string, string> ReadDocumentedMechanismDescriptions()
    {
        string mechanismsDocumentPath = Path.Combine(
            AppContext.BaseDirectory,
            "InfoDocs",
            "Info.Mechanisms.md");
        var descriptions = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string line in File.ReadLines(mechanismsDocumentPath))
        {
            Match match = MechanismDescriptionLine.Match(line);
            if (!match.Success)
                continue;

            string name = match.Groups["name"].Value;
            string description = match.Groups["description"].Value;
            if (!descriptions.TryAdd(name, description))
                throw new InvalidOperationException(
                    $"Info.Mechanisms.md contains duplicate mechanism {name}.");
        }

        return descriptions;
    }

    private static IReadOnlySet<CryptoScriptFunction> DiscoverImplementedFunctions(
        string mechanism)
    {
        Algorithm algorithm = AlgorithmFactory.Create(mechanism);
        var functions = new HashSet<CryptoScriptFunction>();

        if (HasConcreteAlgorithmOverride(
                algorithm,
                nameof(Algorithm.GenerateParameters),
                typeof(string)) &&
            HasConcreteAlgorithmOverride(
                algorithm,
                nameof(Algorithm.GenerateParameters),
                typeof(string),
                typeof(string[])))
        {
            functions.Add(CryptoScriptFunction.Parameters);
        }

        // HASH overrides GenerateKey only to reject it explicitly.
        if (algorithm is not CryptoScript.CryptoAlgorithm.HASH.HASH &&
            HasConcreteAlgorithmOverride(
                algorithm,
                nameof(Algorithm.GenerateKey),
                typeof(string),
                typeof(string)))
        {
            functions.Add(CryptoScriptFunction.GenerateKey);
        }

        if (algorithm is SymmetricCryptoAlgorithm symmetric)
        {
            EncryptionMode mode = symmetric.CreateMode(mechanism);
            AddModeFunction(
                functions,
                mode,
                nameof(EncryptionMode.ModeEncryption),
                CryptoScriptFunction.Encrypt);
            AddModeFunction(
                functions,
                mode,
                nameof(EncryptionMode.ModeDecryption),
                CryptoScriptFunction.Decrypt);
            AddModeFunction(
                functions,
                mode,
                nameof(EncryptionMode.ModeMac),
                CryptoScriptFunction.Mac);
        }

        AddAlgorithmFunction(
            functions,
            algorithm,
            nameof(Algorithm.Hash),
            CryptoScriptFunction.Hash);
        AddAlgorithmFunction(
            functions,
            algorithm,
            nameof(Algorithm.Derive),
            CryptoScriptFunction.Derive);
        AddAlgorithmFunction(
            functions,
            algorithm,
            nameof(Algorithm.Wrap),
            CryptoScriptFunction.Wrap);
        AddAlgorithmFunction(
            functions,
            algorithm,
            nameof(Algorithm.Unwrap),
            CryptoScriptFunction.Unwrap);

        try
        {
            Algorithm blockHeaderAlgorithm = AlgorithmFactory.Create($"BLOCKHEADER-{mechanism}");
            if (HasConcreteAlgorithmOverride(
                    blockHeaderAlgorithm,
                    nameof(Algorithm.GenerateBlockHeader),
                    typeof(string)))
            {
                functions.Add(CryptoScriptFunction.BlockHeader);
            }
        }
        catch (NotSupportedException)
        {
            // No internal BlockHeader dispatch exists for this mechanism.
        }

        return functions.ToFrozenSet();
    }

    private static void AddAlgorithmFunction(
        ISet<CryptoScriptFunction> functions,
        Algorithm algorithm,
        string methodName,
        CryptoScriptFunction function)
    {
        if (HasConcreteAlgorithmOverride(algorithm, methodName, typeof(string[])))
            functions.Add(function);
    }

    private static void AddModeFunction(
        ISet<CryptoScriptFunction> functions,
        EncryptionMode mode,
        string methodName,
        CryptoScriptFunction function)
    {
        MethodInfo method = mode.GetType().GetMethod(methodName) ??
            throw new InvalidOperationException($"Missing mode method {methodName}.");
        if (method.DeclaringType != typeof(EncryptionMode))
            functions.Add(function);
    }

    private static bool HasConcreteAlgorithmOverride(
        Algorithm algorithm,
        string methodName,
        params Type[] parameterTypes)
    {
        MethodInfo method = algorithm.GetType().GetMethod(methodName, parameterTypes) ??
            throw new InvalidOperationException($"Missing algorithm method {methodName}.");
        return method.DeclaringType != typeof(Algorithm) &&
               method.DeclaringType != typeof(SymmetricCryptoAlgorithm);
    }
}
