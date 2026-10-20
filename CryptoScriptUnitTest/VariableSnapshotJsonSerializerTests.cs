using CryptoScript;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Model;
using CryptoScript.SecretSharing;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class VariableSnapshotJsonSerializerTests
{
    private readonly VariableSnapshotJsonSerializer serializer = new();
    private static KeyVariableDeclaration Key(string id = "key") => new()
    {
        Id = id, Type = new CryptoTypeKey(), ValueFormat = FormatConversions.HEX,
        Value = "0x(000102030405060708090a0B0c0D0e0F)", KeySizeInBits = new KeySize(128),
        KeyType = KeyType.Secret(KeyAlgorithm.Aes),
        Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt | KeyUsage.WrapKey | KeyUsage.UnwrapKey),
        DerivationMechanism = "KDF-HKDF",
        KeyAttributes = [new() { ID = "LB", Length = 9, Data = "label" },
            new() { ID = "KS", Length = 42, Data = "preserve legacy length" },
            new() { ID = null, Length = 0, Data = null }]
    };
    private static BlockHeaderVariableDeclaration Header(int? expected = null)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("KBVID", "D"), new("KEYU", "D0"), new("ALGO", "A"), new("MODEU", "B"), new("EXP", "E"),
            new("OPTID", "LB"), new("OPTBD", "\"label;,#:\\\"\"")
        };
        if (expected.HasValue) fields.Add(new("KBLEN", expected.Value.ToString()));
        return new() { Id = "header", ValueFormat = "", Definition = Tr31HeaderDefinition.Create(fields) };
    }
    private static ParameterVariableDeclaration Parameter(string id, BlockHeaderVariableDeclaration header)
    {
        var p = new ParameterVariableDeclaration { Id = id, Mechanism = "WRAP-AES-TR31" };
        p.SetParameter("#BLKH", header.Id);
        p.SetParameter("RND", "0x(0000000000000000000000000000)");
        p.RecordExplicitParameter("#BLKH");
        var result = ParameterVariableDeclaration.FromSnapshot(p.Mechanism, p.GetParameters(),
            p.ExplicitParameterNames, p.Value, header);
        result.Id = id;
        return result;
    }
    private T RoundTrip<T>(T value) where T : VariableDeclaration =>
        (T)serializer.Deserialize(serializer.Serialize([value])).Single();
    private static void Common(VariableDeclaration before, VariableDeclaration after)
    {
        Assert.That(after, Is.Not.SameAs(before));
        Assert.That(after.Id, Is.EqualTo(before.Id));
        Assert.That(after.GetType(), Is.EqualTo(before.GetType()));
        Assert.That(after.Type?.GetType(), Is.EqualTo(before.Type?.GetType()));
        Assert.That(after.Type?.Id, Is.EqualTo(before.Type?.Id));
        Assert.That(after.Type?.Text, Is.EqualTo(before.Type?.Text));
        Assert.That(after.Text, Is.EqualTo(before.Text));
        Assert.That(after.ValueFormat, Is.EqualTo(before.ValueFormat));
        Assert.That(after.Value, Is.EqualTo(before.Value));
    }

    [TestCase("HEX_STRING", "0x(aAbB00)")]
    [TestCase("BASE64_STRING", "b64(AQIDBA==)")]
    [TestCase("NORMAL_STRING", "\"héllo\\n世界\"")]
    [TestCase("JSON_STRING", "{\"a\": 1}")]
    [TestCase("PARAM_STRING", "#MECH:AES-CBC")]
    [TestCase("PATH_STRING", "C:\\x\\..\\y")]
    [TestCase("TR31_STRING", "\"D0112D0AB00E0000\"0x(00)0x(00)")]
    [TestCase("", "000123")]
    [TestCase("", "-12.3400")]
    [TestCase("", "")]
    public void VarPreservesEveryFormatAndExactText(string format, string value)
    {
        var source = new StringVariableDeclaration { Id = "data", ValueFormat = format, Value = value, GMAC = "0x(aBCd)" };
        source.Type!.Id = "custom display id";
        var restored = RoundTrip(source);
        Common(source, restored);
        Assert.That(restored.GMAC, Is.EqualTo(source.GMAC));
    }

    [TestCase("C:\\folder\\..\\x\\file.bin")]
    [TestCase("\\\\host\\share\\a")]
    [TestCase("/tmp/a/../b")]
    public void PathDoesNotNormalizeOrRequireExistence(string value)
    {
        var source = new PathVariableDeclaration { Id = "path", ValueFormat = FormatConversions.PATH, Value = value };
        Common(source, RoundTrip(source)); // null Type is a valid legacy state
        source.Type = new CryptoTypePath();
        Common(source, RoundTrip(source));
    }

    [Test]
    public void KeyPreservesAllMetadataAndFullCheckValuesAndRemainsUsable()
    {
        var source = Key();
        source.KeySize = "00128";
        string before = source.Serialize();
        var json = JObject.Parse(serializer.Serialize([source]));
        var meta = json["variables"]![0]!["metadata"]!;
        Assert.That(meta["kcv"]!.Value<string>(), Is.EqualTo("0x(c6a13b37878f5b826f4f8162a1c8d879)"));
        Assert.That(meta.ToString(), Does.Not.Contain("preserve legacy length"));
        var restored = (KeyVariableDeclaration)serializer.Deserialize(json.ToString()).Single();
        Common(source, restored);
        Assert.That(restored.KeyType, Is.EqualTo(source.KeyType));
        Assert.That(restored.Usage, Is.EqualTo(source.Usage));
        Assert.That(restored.KeySizeInBits, Is.EqualTo(source.KeySizeInBits));
        Assert.That(restored.KeySize, Is.EqualTo(source.KeySize));
        Assert.That(restored.DerivationMechanism, Is.EqualTo(source.DerivationMechanism));
        Assert.That(restored.KeyAttributes.Select(a => (a.ID, a.Length, a.Data)),
            Is.EqualTo(source.KeyAttributes.Select(a => (a.ID, a.Length, a.Data))));
        Assert.That(new[] { restored.Kcv, restored.Kcvc, restored.Kcvk },
            Is.EqualTo(new[] { source.Kcv, source.Kcvc, source.Kcvk }));
        var material = FormatConversions.ToByteArray(restored.Value, restored.ValueFormat);
        Assert.That(FormatConversions.ByteArrayToHexString(AES_ECB.EncryptNoPadding(material, new byte[16])),
            Is.EqualTo(restored.Kcv));
        restored.KeyAttributes[0].Data = "changed";
        Assert.That(source.Serialize(), Is.EqualTo(before));
    }

    [TestCase(KeyAlgorithm.Aes, "BASE64_STRING")]
    [TestCase(KeyAlgorithm.Tdea, "HEX_STRING")]
    [TestCase(KeyAlgorithm.Hmac, "NORMAL_STRING")]
    [TestCase(KeyAlgorithm.Unknown, "HEX_STRING")]
    [TestCase(KeyAlgorithm.Aes, "")]
    public void KeyEncodingsAndUnavailableChecks(KeyAlgorithm algorithm, string format)
    {
        var source = Key();
        source.KeyType = KeyType.Secret(algorithm);
        source.ValueFormat = format;
        if (format == FormatConversions.B64) source.Value = "b64(AAECAwQFBgcICQoLDA0ODw==)";
        if (format == FormatConversions.STR) source.Value = "\"0123456789abcdef\"";
        var restored = RoundTrip(source);
        Common(source, restored);
        Assert.That(restored.Kcv, Is.EqualTo(source.Kcv));
        if (algorithm is KeyAlgorithm.Hmac or KeyAlgorithm.Unknown)
            Assert.That(JObject.Parse(serializer.Serialize([source]))["variables"]![0]!["metadata"]!["kcv"]!.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void ParameterRestoresGeneratedChangedAndExplicitStateWithoutSetters()
    {
        var source = AESDefaultParameters.GenerateDefaultCBCParameters("AES-CBC");
        source.Id = "paramdata";
        source.RecordExplicitParameter("PAD");
        source.SetParameter("PAD", "NONE");
        source.RecordExplicitParameter("PAD"); // provenance may contain repeats
        source.SetParameter("IV", "0x(000102030405060708090a0b0c0d0e0f)");
        var original = source.GetParameters().ToArray();
        var restored = RoundTrip(source);
        Common(source, restored);
        Assert.That(restored.Mechanism, Is.EqualTo(source.Mechanism));
        Assert.That(restored.GetParameters().ToArray(), Is.EqualTo(original));
        Assert.That(restored.ExplicitParameterNames, Is.EqualTo(source.ExplicitParameterNames));
        restored.SetParameter("IV", "0x(01)");
        Assert.That(source.GetParameters().ToArray(), Is.EqualTo(original));
        // Direct dictionary changes and cached display text are independent runtime state.
        source.GetParameters()["#IV"] = "0x(02)";
        Common(source, RoundTrip(source));
        var empty = new ParameterVariableDeclaration { Id = "empty" };
        Assert.That(RoundTrip(empty).GetParameters(), Is.Empty);
    }

    [TestCase(null)]
    [TestCase(144)]
    public void HeaderPreservesDefinitionLengthPaddingAndImmutability(int? expected)
    {
        var source = Header(expected);
        var restored = RoundTrip(source);
        Common(source, restored);
        Assert.That(restored.Definition!.Attributes.ToArray(), Is.EqualTo(source.Definition!.Attributes.ToArray()));
        Assert.That(restored.Definition.OptionalBlocks.Select(b => (b.ID, b.Length, b.Data)),
            Is.EqualTo(source.Definition.OptionalBlocks.Select(b => (b.ID, b.Length, b.Data))));
        Assert.That(restored.Definition.ExpectedLength, Is.EqualTo(expected));
        Assert.That(restored.Definition.IsLengthResolved, Is.False);
        Assert.That(restored.Definition.OptionalBlocks.Last().ID, Is.EqualTo("PB"));
        Assert.Throws<InvalidOperationException>(() => restored.Value = "alter");
        restored.Definition.OptionalBlocks[0].Data = "alter";
        Assert.That(restored.Value, Is.EqualTo(source.Value));
        Assert.That(restored.Definition.SerializeForWrap(32), Is.EqualTo(source.Definition.SerializeForWrap(32)));
    }

    [Test]
    public void LegacyHeaderPreservesRawValue()
    {
        var source = new BlockHeaderVariableDeclaration { Id = "legacy", ValueFormat = FormatConversions.STR, Value = "\"D0112D0AB00E0000\"" };
        var restored = RoundTrip(source);
        Common(source, restored);
        Assert.That(restored.Definition, Is.Null);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SharedHeaderIdentitySurvivesRootOrderOrEmbedding(bool exportHeader)
    {
        var h = Header();
        var first = Parameter("first", h);
        var second = Parameter("second", h);
        VariableDeclaration[] roots = exportHeader ? [first, second, h] : [first, second];
        string before = h.Value;
        var json = serializer.Serialize(roots);
        var restored = serializer.Deserialize(json);
        Assert.That(restored, Has.Count.EqualTo(roots.Length));
        var a = (ParameterVariableDeclaration)restored[0];
        var b = (ParameterVariableDeclaration)restored[1];
        Assert.That(a.HeaderReference, Is.SameAs(b.HeaderReference));
        Assert.That(a.HeaderReference, Is.Not.SameAs(h));
        if (exportHeader) Assert.That(a.HeaderReference, Is.SameAs(restored[2]));
        Assert.That(JObject.Parse(json)["dependencies"]!.Count(), Is.EqualTo(exportHeader ? 0 : 1));
        Assert.That(serializer.Serialize(restored), Is.EqualTo(json));
        Assert.That(h.Value, Is.EqualTo(before));
        Assert.That(first.HeaderReference, Is.SameAs(h));
    }

    [Test]
    public void ReconstructedKeyParameterAndHeaderCanWrapAndUnwrap()
    {
        var h = Header();
        var p = Parameter("paramdata", h);
        var kbpk = Key("kbpk");
        var transported = Key("transported");
        var restored = serializer.Deserialize(serializer.Serialize([p, kbpk, transported]));
        var algorithm = new WrapAESTR31();
        AlgorithmCallArguments Args(VariableDeclaration value) => new(new[]
        {
            new AlgorithmCallArgument(restored[0].Value, restored[0], ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(restored[1].Value, restored[1], ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(value.Value, value, ResolvedCallArgumentKind.Variable)
        });
        var wrapped = algorithm.Wrap(Args(restored[2]));
        var recovered = algorithm.Unwrap(Args(wrapped));
        Assert.That(recovered.Value, Is.EqualTo(transported.Value).IgnoreCase);
        Assert.That(((ParameterVariableDeclaration)restored[0]).HeaderReference!.Definition!.IsLengthResolved, Is.False);
    }

    [TestCase(SecretDescriptor.Split, null, "SEPARATE-X", 3)]
    [TestCase(SecretDescriptor.Split, null, "X-Y", 1)]
    [TestCase(SecretDescriptor.Shamir, SecretDescriptor.Profile11B, "Y-X", 3)]
    [TestCase(SecretDescriptor.Shamir, SecretDescriptor.Profile11D, "X-Y", 2)]
    [TestCase(SecretDescriptor.Shamir, SecretDescriptor.Profile11B, "X-Y", 0)]
    public void SecretRetainsAllSharesAndOriginalMetadata(string mechanism, string? profile, string format, int count)
    {
        byte[] key = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");
        var metadata = new SecretKeyMetadata(KeyType.Secret(KeyAlgorithm.Aes),
            KeyUsagePolicy.Restricted(KeyUsage.DeriveKey | KeyUsage.MacGenerate), "KDF-HKDF",
            [new("LB", 7, "abc"), new("KS", 99, "sensitive"), new(null, 0, null)]);
        var full = SecretSharingEngine.Share(new(mechanism, 3, mechanism == SecretDescriptor.Split ? 3 : 2, 128, profile), key, format, metadata);
        var set = new SecretSet(full.Descriptor, full.Shares.Reverse().Take(count), format, metadata);
        var source = new SecretVariableDeclaration(set) { Id = "secret", ValueFormat = "" };
        var restored = RoundTrip(source);
        Common(source, restored);
        var actual = restored.SecretSet;
        Assert.That(actual.Descriptor.Mechanism, Is.EqualTo(set.Descriptor.Mechanism));
        Assert.That(actual.Descriptor.Parts, Is.EqualTo(3));
        Assert.That(actual.Descriptor.MinParts, Is.EqualTo(set.Descriptor.MinParts));
        Assert.That(actual.Descriptor.KeySizeBits, Is.EqualTo(128));
        Assert.That(actual.Descriptor.Profile, Is.EqualTo(profile));
        Assert.That(actual.PreferredShareFormat, Is.EqualTo(format));
        Assert.That(actual.Shares.Select(s => s.X), Is.EqualTo(set.Shares.Select(s => s.X)));
        Assert.That(actual.Shares.Select(s => s.Value), Is.EqualTo(set.Shares.Select(s => s.Value)));
        Assert.That(actual.OriginalKeyMetadata!.KeyType, Is.EqualTo(metadata.KeyType));
        Assert.That(actual.OriginalKeyMetadata.Usage, Is.EqualTo(metadata.Usage));
        Assert.That(actual.OriginalKeyMetadata.DerivationMechanism, Is.EqualTo(metadata.DerivationMechanism));
        Assert.That(actual.OriginalKeyMetadata.Attributes, Is.EqualTo(metadata.Attributes));
        if (count >= set.Descriptor.MinParts) Assert.That(SecretSharingEngine.Restore(actual), Is.EqualTo(key));
        else Assert.Throws<ArgumentException>(() => SecretSharingEngine.Restore(actual));
    }

    [Test]
    public void MixedContainerIsDetachedAndDoesNotUseGlobalVariablesOrAvalonia()
    {
        var globals = VariableDictionary.Instance().GetVariables().ToArray();
        var h = Header();
        VariableDeclaration[] source =
        [
            new StringVariableDeclaration { Id = "data", Value = "123" }, Key(),
            Parameter("paramdata", h), new PathVariableDeclaration { Id = "path", Value = "X:\\missing" }, h,
            new SecretVariableDeclaration(new(new(SecretDescriptor.Split, 2, 2, 8), [new(1, new byte[] { 42 })])) { Id = "secret" }
        ];
        var json = serializer.Serialize(source);
        var restored = serializer.Deserialize(json);
        Assert.That(restored, Has.Count.EqualTo(6));
        for (int i = 0; i < source.Length; i++) Common(source[i], restored[i]);
        Assert.That(serializer.Serialize(restored), Is.EqualTo(json));
        Assert.That(VariableDictionary.Instance().GetVariables().ToArray(), Is.EqualTo(globals));
        Assert.That(typeof(VariableSnapshotJsonSerializer).Assembly.GetReferencedAssemblies()
            .Any(a => a.Name!.Contains("Avalonia")), Is.False);
    }

    private void Reject(Action<JObject> change, VariableDeclaration? source = null)
    {
        var json = JObject.Parse(serializer.Serialize([source ?? Key()]));
        change(json);
        Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json.ToString()));
    }

    [Test]
    public void RejectsMissingUnknownWronglyTypedAndConflictingFields()
    {
        Reject(j => j["format"] = "other");
        Reject(j => j["version"] = 2);
        Reject(j => j["version"] = "1");
        Reject(j => j.Remove("dependencies"));
        Reject(j => j["unexpected"] = 1);
        Reject(j => j["$type"] = "System.IO.FileInfo");
        Reject(j => j["Variables"] = j["variables"]!.DeepClone());
        Reject(j => j["variables"]![0]!["identifier"] = "bad_name");
        Reject(j => j["variables"]![0]!["identifier"] = "KEY");
        Reject(j => j["variables"]![0]!["identifier"] = 123);
        Reject(j => j["variables"]![0]!["metadata"]!["type"] = "UNKNOWN");
        Reject(j => j["variables"]![0]!["metadata"]!["payloadKind"] = "scalar");
        Reject(j => ((JObject)j["variables"]![0]!["metadata"]!).Remove("kcv"));
        Reject(j => j["variables"]![0]!["metadata"]!["kcv"] = "0x(000000)");
        Reject(j => j["variables"]![0]!["metadata"]!["keySizeInBits"] = 256);
        Reject(j => j["variables"]![0]!["metadata"]!["keyType"]!["materialKind"] = "Public");
        Reject(j => j["variables"]![0]!["metadata"]!["usage"]!["allowedUsages"] = 1024);
        Reject(j => j["variables"]![0]!["value"]!["material"] = "0x(0G)");
        Reject(j => j["variables"]![0]!["value"]!["material"] = "0x(01)");
        Reject(j => ((JArray)j["variables"]!).Add(j["variables"]![0]!.DeepClone()));
    }

    [TestCase("{\"format\":\"CryptoScriptVariables\",\"version\":1,\"version\":1,\"variables\":[],\"dependencies\":[]}")]
    [TestCase("{'format':'CryptoScriptVariables','version':1,'variables':[],'dependencies':[]}")]
    [TestCase("{\"format\":\"CryptoScriptVariables\",\"version\":1,\"variables\":[],\"dependencies\":[],}")]
    [TestCase("{\"format\":\"CryptoScriptVariables\",\"version\":1,\"variables\":[],\"dependencies\":[]} {}")]
    [TestCase("null")]
    [TestCase("[]")]
    public void RejectsNonStrictJson(string json) =>
        Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json));

    [Test]
    public void RejectsNestedDuplicatesIncludingEscapedPropertyNames()
    {
        string json = serializer.Serialize([Key()]);
        json = json.Replace("\"material\":", "\"material\":\"bad\", \"mater\\u0069al\":", StringComparison.Ordinal);
        Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json));
    }

    [Test]
    public void RejectsInvalidReferencesAndHeaderDefinition()
    {
        var p = Parameter("paramdata", Header());
        Reject(j => j["variables"]![0]!["value"]!["headerReference"] = "missing", p);
        Reject(j => j["variables"]![0]!["value"]!["headerReference"] = "o1", p);
        Reject(j => j["dependencies"] = new JArray(), p);
        Reject(j => j["variables"]![0]!["value"]!["headerReference"] = JValue.CreateNull(), p);
        Reject(j => j["dependencies"]![0]!["value"]!["definition"]!["expectedLength"] = 128, p);
        Reject(j => j["dependencies"]![0]!["value"]!["definition"]!["optionalBlocks"]![0]!["length"] = 999, p);
        Reject(j => j["dependencies"]![0]!["value"]!["definition"]!["attributes"]![0]!["value"] = "Z", p);
        Reject(j => j["variables"]![0]!["value"]!["parameters"]![0]!["name"] = "#UNKNOWN", p);
        Reject(j => j["variables"]![0]!["metadata"]!["mechanism"] = "AES-CBC", p);
    }

    [Test]
    public void RejectsInvalidSecretSharesWithoutPublishingAnything()
    {
        var source = new SecretVariableDeclaration(new(new(SecretDescriptor.Split, 2, 2, 8),
            [new(1, new byte[] { 1 }), new(2, new byte[] { 2 })])) { Id = "secret" };
        var globals = VariableDictionary.Instance().GetVariables().ToArray();
        Reject(j => j["variables"]![0]!["value"]!["shares"]![1]!["x"] = 1, source);
        Reject(j => j["variables"]![0]!["value"]!["shares"]![1]!["x"] = 0, source);
        Reject(j => j["variables"]![0]!["value"]!["shares"]![1]!["bytes"] = "AQID", source);
        Reject(j => j["variables"]![0]!["value"]!["descriptor"]!["minParts"] = 1, source);
        Assert.That(VariableDictionary.Instance().GetVariables().ToArray(), Is.EqualTo(globals));
    }

    [Test]
    public void JsonGlobalSettingsCannotEnablePolymorphismOrChangeSchema()
    {
        var old = JsonConvert.DefaultSettings;
        string expected = serializer.Serialize([Key()]);
        try
        {
            JsonConvert.DefaultSettings = () => new() { TypeNameHandling = TypeNameHandling.All, NullValueHandling = NullValueHandling.Ignore };
            Assert.That(serializer.Serialize(serializer.Deserialize(expected)), Is.EqualTo(expected));
        }
        finally { JsonConvert.DefaultSettings = old; }
    }

    [TestCase(KeyAlgorithm.Rsa, KeyMaterialKind.Public)]
    [TestCase(KeyAlgorithm.Rsa, KeyMaterialKind.Private)]
    [TestCase(KeyAlgorithm.Ec, KeyMaterialKind.Public)]
    [TestCase(KeyAlgorithm.Ec, KeyMaterialKind.Private)]
    public void AsymmetricKeyMetadataUsesMathematicalSizeInsteadOfDerLength(KeyAlgorithm algorithm, KeyMaterialKind kind)
    {
        using System.Security.Cryptography.AsymmetricAlgorithm generator = algorithm == KeyAlgorithm.Rsa
            ? System.Security.Cryptography.RSA.Create(2048)
            : System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        byte[] bytes = kind == KeyMaterialKind.Private ? generator.ExportPkcs8PrivateKey() : generator.ExportSubjectPublicKeyInfo();
        var source = new KeyVariableDeclaration
        {
            Id = "asymmetric", Type = new CryptoTypeKey(), Value = Convert.ToBase64String(bytes),
            ValueFormat = FormatConversions.B64, KeyType = new(algorithm, kind),
            KeySizeInBits = new KeySize(generator.KeySize)
        };
        var restored = RoundTrip(source);
        Common(source, restored);
        Assert.That(restored.KeyType, Is.EqualTo(source.KeyType));
        Assert.That(restored.KeySizeInBits, Is.EqualTo(source.KeySizeInBits));
        Assert.That(restored.Kcv, Is.Null);
        Assert.That(restored.Kcvc, Is.Null);
        Assert.That(restored.Kcvk, Is.Null);
        Reject(j => j["variables"]![0]!["metadata"]!["keyType"]!["algorithm"] =
            algorithm == KeyAlgorithm.Rsa ? "Ec" : "Rsa", source);
        Reject(j => j["variables"]![0]!["metadata"]!["keyType"]!["materialKind"] =
            kind == KeyMaterialKind.Private ? "Public" : "Private", source);
    }

    [TestCase("AA==")]
    [TestCase("MAA=")]
    [TestCase("MAMCAQA=")]
    [TestCase("////////////")]
    public void MalformedAsymmetricMaterialHasControlledErrors(string material)
    {
        Reject(j =>
        {
            var meta = j["variables"]![0]!["metadata"]!;
            meta["keyType"]!["algorithm"] = "Rsa";
            meta["keyType"]!["materialKind"] = "Public";
            meta["format"] = FormatConversions.B64;
            meta["kcv"] = meta["kcvc"] = meta["kcvk"] = JValue.CreateNull();
            j["variables"]![0]!["value"]!["material"] = material;
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DistinctSameNamedAndAnonymousDependenciesAreNotMerged(bool anonymous)
    {
        var a = Header();
        var b = Header();
        if (anonymous) a.Id = b.Id = "";
        var roots = serializer.Deserialize(serializer.Serialize([Parameter("first", a), Parameter("second", b)]));
        Assert.That(((ParameterVariableDeclaration)roots[0]).HeaderReference,
            Is.Not.SameAs(((ParameterVariableDeclaration)roots[1]).HeaderReference));
        Assert.That(roots, Has.Count.EqualTo(2));
        Assert.That(((ParameterVariableDeclaration)roots[0]).HeaderReference!.Id, Is.EqualTo(a.Id));
    }

    [Test]
    public void GeneratedNoncesAndPostOperationChangesStayExact()
    {
        var p = AESDefaultParameters.GenerateDefaultGCMParameters("AES-GCM");
        p.Id = "nonceparam";
        var before = p.GetParameters().ToArray();
        var first = RoundTrip(p);
        Assert.That(first.GetParameters().ToArray(), Is.EqualTo(before));
        p.SetParameter("NONCE", "0x(00112233445566778899aabb)");
        p.SetParameter("ADATA", "\"changed authentication data\"");
        var second = RoundTrip(p);
        Assert.That(second.GetParameters().ToArray(), Is.EqualTo(p.GetParameters().ToArray()));
        Assert.That(second.ExplicitParameterNames, Is.Empty);
    }

    [Test]
    public void ParameterContractsRejectMalformedValuesWithoutPublishing()
    {
        var p = new ParameterVariableDeclaration { Id = "paramdata", Mechanism = "KDF-EMV-MASTER-A" };
        p.SetParameter("PSN", "\"00\"");
        Reject(j => j["variables"]![0]!["value"]!["parameters"]![1]!["value"] = "\"xx\"", p);
        Reject(j => j["variables"]![0]!["value"]!["parameters"]![1]!["name"] = "#OFFSET", p);
        Reject(j => ((JArray)j["variables"]![0]!["value"]!["parameters"]!).Add(
            new JObject { ["name"] = "#MECH", ["value"] = "KDF-EMV-MASTER-A" }), p);
    }

    [Test]
    public void EveryDtoFieldIsRequiredEvenWhenItsValueMayBeNull()
    {
        var h = Header();
        var set = new SecretSet(new(SecretDescriptor.Shamir, 3, 2, 8), [new(2, new byte[] { 2 })],
            originalKeyMetadata: new(KeyType.Secret(KeyAlgorithm.Hmac), KeyUsagePolicy.Unspecified, "",
                [new("LB", 5, "a")]));
        var original = JObject.Parse(serializer.Serialize([
            Key(), Parameter("paramdata", h), h, new SecretVariableDeclaration(set) { Id = "secret" },
            new StringVariableDeclaration { Id = "data" }, new PathVariableDeclaration { Id = "path" }]));
        var properties = original.Descendants().OfType<JProperty>().Select(p => p.Path).ToArray();
        foreach (string path in properties)
        {
            var copy = (JObject)original.DeepClone();
            ((JProperty)copy.SelectToken(path)!.Parent!).Remove();
            Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(copy.ToString()), path);
        }
    }

    [Test]
    public void MalformedTailCannotPublishEarlierValidEntriesOrExposeMaterial()
    {
        var globals = VariableDictionary.Instance().GetVariables().ToArray();
        var key = Key();
        string before = key.Serialize();
        var document = JObject.Parse(serializer.Serialize([key, new StringVariableDeclaration { Id = "last" }]));
        document["variables"]![1]!["metadata"]!["format"] = "INVALID";
        var error = Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(document.ToString()))!;
        Assert.That(error.Message, Does.Not.Contain(key.Value));
        Assert.That(error.InnerException, Is.Null);
        Assert.That(key.Serialize(), Is.EqualTo(before));
        Assert.That(VariableDictionary.Instance().GetVariables().ToArray(), Is.EqualTo(globals));
    }

    [Test]
    public void RuntimeProducedReferenceGraphIsCapturedWithoutChangingTheGlobalStore()
    {
        var saved = VariableDictionary.Instance().GetVariables().ToArray();
        try
        {
            VariableDictionary.Instance().Clear();
            var parser = ParserBuilder.StringBuild(
                "TR31H h=CreateHeader(KBVID:D,KEYU:D0,ALGO:A,MODEU:B,EXP:E) " +
                "PARAM p=#MECH:WRAP-AES-TR31 #BLKH:h PARAM q=#MECH:WRAP-AES-TR31 #BLKH:h");
            new CryptoScriptRunner().Execute(parser.program());
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            var original = VariableDictionary.Instance().GetVariables().ToArray();
            var copy = serializer.Deserialize(serializer.Serialize(original));
            for (int i = 0; i < original.Length; i++) Common(original[i], copy[i]);
            Assert.That(((ParameterVariableDeclaration)copy[1]).HeaderReference, Is.SameAs(copy[0]));
            Assert.That(((ParameterVariableDeclaration)copy[2]).HeaderReference, Is.SameAs(copy[0]));
            Assert.That(VariableDictionary.Instance().GetVariables().ToArray(), Is.EqualTo(original));
        }
        finally
        {
            VariableDictionary.Instance().Clear();
            foreach (var value in saved) VariableDictionary.Instance().Add(value);
        }
    }

    [Test]
    public void NullsRangesAndPayloadTypesAreStrict()
    {
        Reject(j => j["variables"] = JValue.CreateNull());
        Reject(j => j["variables"]![0] = JValue.CreateNull());
        Reject(j => j["variables"]![0]!["metadata"] = JValue.CreateNull());
        Reject(j => j["variables"]![0]!["value"] = JValue.CreateNull());
        Reject(j => j["variables"]![0]!["metadata"]!["keySizeInBits"] = -1);
        Reject(j => j["variables"]![0]!["metadata"]!["keySizeInBits"] = 128.0);
        Reject(j => j["variables"]![0]!["metadata"]!["usage"]!["allowedUsages"] = long.MaxValue);
        Reject(j => j["variables"]![0]!["value"]!["attributes"]![0] = JValue.CreateNull());
        Reject(j => j["variables"]![0]!["value"]!["attributes"]![0]!["length"] = "9");
        Reject(j => j["variables"]![0]!["value"] = new JObject(),
            new StringVariableDeclaration { Id = "data" });
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize(null!));
        Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null!));
        Assert.That(serializer.Deserialize(serializer.Serialize([])), Is.Empty);
    }

    [TestCase("VAR")]
    [TestCase("PATH")]
    [TestCase("KEY")]
    [TestCase("PARAM")]
    [TestCase("TR31H")]
    [TestCase("SECRET")]
    public void StatementAndTypeSourceTextStayInsideProtectedPayload(string type)
    {
        VariableDeclaration variable = type switch
        {
            "VAR" => new StringVariableDeclaration { Id = "data", Value = "42" },
            "PATH" => new PathVariableDeclaration { Id = "path", Value = "C:\\private", Type = new CryptoTypePath() },
            "KEY" => Key(),
            "PARAM" => Parameter("paramdata", Header()),
            "TR31H" => Header(),
            _ => new SecretVariableDeclaration(new(new(SecretDescriptor.Split, 2, 2, 8), [])) { Id = "secret" }
        };
        variable.Text = "source containing sensitive material 0x(012345)";
        variable.Type!.Text = "type source with sensitive material 0x(abcdef)";
        var document = JObject.Parse(serializer.Serialize([variable]));
        var entry = document["variables"]![0]!;
        Assert.That(entry["metadata"]!["payloadKind"]!.Value<string>(), Is.EqualTo("structured"));
        Assert.That(entry["metadata"]!.ToString(), Does.Not.Contain("sensitive"));
        Assert.That(entry["value"]!["source"]!["text"]!.Value<string>(), Is.EqualTo(variable.Text));
        Assert.That(entry["value"]!["source"]!["typeText"]!.Value<string>(), Is.EqualTo(variable.Type.Text));
        var restored = serializer.Deserialize(document.ToString()).Single();
        Common(variable, restored);
        Assert.That(serializer.Serialize([restored]), Is.EqualTo(document.ToString()));
        Reject(j => ((JObject)j["variables"]![0]!["value"]!).Remove("source"), variable);
        Reject(j => j["variables"]![0]!["value"]!["source"]!["typeText"] = JValue.CreateNull(), variable);
    }

    [TestCase("3012300D06092A864886F70D0101010500030100", "Public")]
    [TestCase("3014020100300D06092A864886F70D01010105000400", "Private")]
    public void MissingNestedRsaObjectsCannotEscapeAsLibraryExceptions(string hex, string kind)
    {
        Reject(j =>
        {
            var meta = j["variables"]![0]!["metadata"]!;
            meta["keyType"]!["algorithm"] = "Rsa";
            meta["keyType"]!["materialKind"] = kind;
            meta["keySize"] = "";
            meta["keySizeInBits"] = 0;
            meta["format"] = FormatConversions.B64;
            meta["kcv"] = meta["kcvc"] = meta["kcvk"] = JValue.CreateNull();
            j["variables"]![0]!["value"]!["material"] = Convert.ToBase64String(Convert.FromHexString(hex));
        });
    }
}
