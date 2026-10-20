using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class AesDes3SemanticDiagnosticsTests
{
    private const string Data = "0x(00112233445566778899AABBCCDDEEFF)";
    private const string NormalTdea = "0123456789ABCDEFFEDCBA9876543210";
    private static readonly string[] Modes = { "ECB", "CBC", "CBC-MAC", "CMAC", "RETAIL" };

    [SetUp]
    public void Setup() => VariableDictionary.Instance().Clear();

    private static KeyVariableDeclaration Key(string hex, KeyAlgorithm algorithm) => new()
    {
        Id = "key", Type = new CryptoTypeKey(), Value = "0x(" + hex + ")",
        ValueFormat = FormatConversions.HEX, KeyType = KeyType.Secret(algorithm), KeySizeInBits = new KeySize(hex.Length * 4)
    };

    private static ParameterVariableDeclaration Parameters(string mechanism, string padding = "NONE")
    {
        var p = AlgorithmFactory.Create(mechanism).GenerateParameters(mechanism);
        p.SetParameter("PAD", mechanism == "DES3-RETAIL" ? "ISO-9797-M2" : padding);
        if (mechanism.EndsWith("CBC", StringComparison.Ordinal))
            p.SetParameter("IV", mechanism.StartsWith("AES", StringComparison.Ordinal)
                ? "0x(00000000000000000000000000000000)" : "0x(1234567890ABCDEF)");
        return p;
    }

    private static VariableDeclaration Call(string function, ParameterVariableDeclaration parameters,
        KeyVariableDeclaration key, string data, ISemanticDiagnosticSink? sink)
    {
        return OperationFactory.CreateInvocationOperation(function)(new OperationInvocation(new[]
        {
            new ResolvedCallArgument(parameters.Value, ResolvedCallArgumentKind.Variable, parameters),
            new ResolvedCallArgument(key.Value, ResolvedCallArgumentKind.Variable, key),
            new ResolvedCallArgument(data, ResolvedCallArgumentKind.HexLiteral)
        }, sink)) ?? throw new AssertionException("Cryptographic operations must return a variable.");
    }

    private static CryptoScriptProgram Execute(CryptoScriptRunner runner, string text)
    {
        var parser = ParserBuilder.StringBuild(text);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        return runner.Execute(context);
    }

    [TestCase("AES-ECB", "Encrypt", KeyAlgorithm.Aes)]
    [TestCase("AES-ECB", "Decrypt", KeyAlgorithm.Aes)]
    [TestCase("AES-CMAC", "Mac", KeyAlgorithm.Aes)]
    [TestCase("DES3-ECB", "Encrypt", KeyAlgorithm.Tdea)]
    [TestCase("DES3-ECB", "Decrypt", KeyAlgorithm.Tdea)]
    [TestCase("DES3-CMAC", "Mac", KeyAlgorithm.Tdea)]
    public void MetadataConflictsWarnInOrderWithoutChangingBytesResultsOrKey(string mechanism, string function, KeyAlgorithm algorithm)
    {
        var key = Key(algorithm == KeyAlgorithm.Aes ? "000102030405060708090A0B0C0D0E0F" : NormalTdea, algorithm);
        var p = Parameters(mechanism);
        string expected = Call(function, p, key, Data, null).Value;
        key.KeyType = KeyType.Secret(algorithm == KeyAlgorithm.Aes ? KeyAlgorithm.Tdea : KeyAlgorithm.Aes);
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.DeriveKey);
        string snapshot = key.Serialize();
        var sink = new SemanticDiagnosticSink();
        Assert.That(Call(function, p, key, Data, sink).Value, Is.EqualTo(expected));
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(new[]
            { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict }));
        foreach (var diagnostic in sink.GetSnapshot())
        {
            Assert.That(diagnostic.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
            Assert.That(diagnostic.Location, Is.EqualTo(new DiagnosticLocation(
                FunctionName: function, Mechanism: mechanism, Identifier: "key")));
            Assert.That(diagnostic.Message, Does.Not.Contain(key.Value));
        }
        Assert.That(key.Serialize(), Is.EqualTo(snapshot));
        Assert.That(Call(function, p, key, Data, null).Value, Is.EqualTo(expected));
        Assert.That(AlgorithmFactory.Create(mechanism) switch
        {
            CryptoScript.CryptoAlgorithm.AES.AES aes => function switch
            { "Encrypt" => aes.Encrypt(new[] { p.Value, key.Value, Data }).Value,
              "Decrypt" => aes.Decrypt(new[] { p.Value, key.Value, Data }).Value,
              _ => aes.Mac(new[] { p.Value, key.Value, Data }).Value },
            DES3 des => function switch
            { "Encrypt" => des.Encrypt(new[] { p.Value, key.Value, Data }).Value,
              "Decrypt" => des.Decrypt(new[] { p.Value, key.Value, Data }).Value,
              _ => des.Mac(new[] { p.Value, key.Value, Data }).Value },
            _ => throw new InvalidOperationException()
        }, Is.EqualTo(expected));
    }

    [TestCase("AES-ECB", KeyAlgorithm.Aes)]
    [TestCase("DES3-ECB", KeyAlgorithm.Tdea)]
    public void UnknownAndUnspecifiedHaveNoWarningsAndIdenticalValuesKeepSourceIdentity(string mechanism, KeyAlgorithm algorithm)
    {
        var key = Key(algorithm == KeyAlgorithm.Aes ? "000102030405060708090A0B0C0D0E0F" : NormalTdea, KeyAlgorithm.Unknown);
        var sameBytes = Key(algorithm == KeyAlgorithm.Aes ? "000102030405060708090A0B0C0D0E0F" : NormalTdea, KeyAlgorithm.Hmac);
        sameBytes.Usage = KeyUsagePolicy.Restricted(KeyUsage.MacVerify);
        var sink = new SemanticDiagnosticSink();
        var p = Parameters(mechanism);
        string expected = Call("Encrypt", p, key, Data, sink).Value;
        Assert.That(sink.GetSnapshot(), Is.Empty);
        Assert.That(Call("Encrypt", p, sameBytes, Data, sink).Value, Is.EqualTo(expected));
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(new[]
            { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict }));
        Assert.That(key.KeyType.Algorithm, Is.EqualTo(KeyAlgorithm.Unknown));
    }

    [TestCase("AES-ECB", KeyAlgorithm.Tdea)]
    [TestCase("DES3-ECB", KeyAlgorithm.Aes)]
    public void RunnerKeepsResultsAndNestedDiagnosticContexts(string mechanism, KeyAlgorithm declared)
    {
        var runner = new CryptoScriptRunner();
        Execute(runner, $"KEY k=GenerateKey({mechanism},0x({NormalTdea})) PARAM p=Parameters({mechanism},#PAD:NONE)");
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("k");
        key.KeyType = KeyType.Secret(declared);
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.DeriveKey);
        string before = key.Serialize();
        var program = Execute(runner, $"VAR output=Encrypt(p,k,Encrypt(p,k,{Data})) VAR later=0x(01)");
        Assert.That(program.Diagnostics.Select(d => d.DiagnosticCode), Is.EqualTo(new[]
        {
            SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict,
            SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict
        }));
        Assert.That(program.Diagnostics.Select(d => d.Location!.OperationId), Is.EqualTo(new[] { 2, 2, 1, 1 }));
        Assert.That(program.Diagnostics.Select(d => d.Location!.ParentOperationId), Is.EqualTo(new int?[] { 1, 1, null, null }));
        Assert.That(program.Diagnostics.Select(d => d.Location!.Identifier), Is.All.EqualTo("k"));
        Assert.That(program.Diagnostics.Select(d => d.Location!.Mechanism), Is.All.EqualTo(mechanism));
        Assert.That(program.Diagnostics.Select(d => d.Location!.ExecutionId).Distinct().Count(), Is.EqualTo(1));
        Assert.That(runner.SemanticErrors, Is.Empty);
        Assert.That(VariableDictionary.Instance().Contains("output"), Is.True);
        Assert.That(VariableDictionary.Instance().Contains("later"), Is.True);
        Assert.That(key.Serialize(), Is.EqualTo(before));
    }

    // Independently generated with OpenSSL 3.6.1 (not the production Bouncy Castle backend).
    // Data=00112233445566778899AABBCCDDEEFF, CBC IV=1234567890ABCDEF;
    // CBC-MAC IV=0/PAD=NONE, CMAC=64 bits, RETAIL=ISO9797-M2/MACLEN=8.
    // RETAIL reference: DES-CBC(K1), then D(K2)/E(K3); OpenSSL EDE(K,K,K)
    // supplies single DES without requiring its unavailable legacy provider.
    [TestCase(NormalTdea, "31A7364CAC91CA39C0489F69BEC54FA2", "C47BC6E91A9D566F649D750BCE1CE988", "B72A829449FFEE5A", "7D5D18D1CCC332D2", "FC9071E696F5D9D3")]
    [TestCase(NormalTdea + "0123456789ABCDEF", "31A7364CAC91CA39C0489F69BEC54FA2", "C47BC6E91A9D566F649D750BCE1CE988", "B72A829449FFEE5A", "7D5D18D1CCC332D2", "FC9071E696F5D9D3")]
    [TestCase("00000000000000000000000000000000", "74F4AE777AA431E89CA4757B2414F4D7", "48CA95272AAA6221900CA4022703644D", "28A4FB55B39DA61A", "82A4376EF3784E17", "DA7B09625F90764C")]
    [TestCase("010101010101010101010101010101010101010101010101", "74F4AE777AA431E89CA4757B2414F4D7", "48CA95272AAA6221900CA4022703644D", "28A4FB55B39DA61A", "82A4376EF3784E17", "DA7B09625F90764C")]
    [TestCase("0101010101010101FEDCBA9876543210", "98B67B95E03A2B1405EFE6D39D3C887F", "FD9B97F6550D97EA95AA27B0054508B1", "308952BC4360F9A5", "C64D3E6FA4E828B6", "3211EC0FD739D053")]
    [TestCase("01FE01FE01FE01FEFEDCBA9876543210", "8CB29EA208819A221A6F05F4F7864588", "172EBD15E1853D3C269CE6CD3E0A5980", "27E7A34A9037422A", "9053597B2645A044", "A220344DAFBE37B3")]
    [TestCase("0123456789ABCDEF0123456789ABCDEF0011223344556677", "E4FF10812363072DCDBCA2DB5A6E62BD", "59E26BCCE5321FCB60801C3D50038007", "AB7532A621210CDF", "A3386770346DB495", "BDB52E9346088798")]
    [TestCase("0123456789ABCDEFFEDCBA9876543210FEDCBA9876543210", "CADB6782EE2B4823EDD076666F6AE130", "EA2F68F28421D42F2DB5F0501CA9AD0F", "FC5B6B357742E3A1", "0DCFA676D2713C5F", "0FC757C5FB3870AE")]
    public void AllFiveModesMatchIndependentOpenSslReferencesIncludingParityVariants(string hex,
        string ecb, string cbc, string cbcMac, string cmac, string retail)
    {
        var expected = new[] { ecb, cbc, cbcMac, cmac, retail };
        for (int i = 0; i < Modes.Length; i++)
        {
            string mechanism = "DES3-" + (Modes[i] == "CBC-MAC" ? "CBC" : Modes[i]);
            string function = i < 2 ? "Encrypt" : "Mac";
            var p = Parameters(mechanism);
            var key = Key(hex, KeyAlgorithm.Tdea);
            string snapshot = key.Serialize();
            var sink = new SemanticDiagnosticSink();
            Assert.That(Call(function, p, key, Data, sink).Value, Is.EqualTo("0x(" + expected[i] + ")").IgnoreCase, Modes[i]);
            Assert.That(key.Serialize(), Is.EqualTo(snapshot));
            var codes = sink.GetSnapshot().Select(d => d.DiagnosticCode).ToArray();
            Assert.That(codes.Distinct().Count(), Is.EqualTo(codes.Length));
            if (i < 2)
                Assert.That(Call("Decrypt", p, key, "0x(" + expected[i] + ")", null).Value, Is.EqualTo(Data).IgnoreCase);
            byte[] flipped = Convert.FromHexString(hex).Select(b => (byte)(b ^ 1)).ToArray();
            var variant = Key(Convert.ToHexString(flipped), KeyAlgorithm.Tdea);
            Assert.That(Call(function, p, variant, Data, null).Value, Is.EqualTo("0x(" + expected[i] + ")").IgnoreCase);
        }
    }

    [TestCase(NormalTdea, false, false, "")]
    [TestCase(NormalTdea + "0123456789ABCDEF", false, false, "")]
    [TestCase("0022446688AACCEEFFDDBB9977553311", true, false, "K1")]
    [TestCase("0022446688AACCEEFFDDBB99775533110022446688AACCEE", true, false, "K3")]
    [TestCase("0101010101010101FEDCBA9876543210", false, true, "known weak DES component K1")]
    [TestCase("01FE01FE01FE01FEFEDCBA9876543210", false, true, "semi-weak DES component K1")]
    [TestCase("00000000000000000000000000000000", true, true, "K1 = K2")]
    [TestCase("0123456789ABCDEF0123456789ABCDEF0011223344556677", true, true, "K1 = K2")]
    [TestCase("0123456789ABCDEFFEDCBA9876543210FEDCBA9876543210", false, true, "K2 = K3")]
    public void QualityCategoriesAreAccurateAggregatedAndDoNotMutate(string hex, bool parity, bool weak, string detail)
    {
        var key = Convert.FromHexString(hex);
        var original = (byte[])key.Clone();
        var sink = new SemanticDiagnosticSink();
        Des3KeyQuality.Report(key, "external", "Mac", "DES3-CMAC", sink);
        Assert.That(key, Is.EqualTo(original));
        Assert.That(sink.GetSnapshot().Count(d => d.DiagnosticCode == SemanticDiagnosticCode.DesParityViolation), Is.EqualTo(parity ? 1 : 0));
        Assert.That(sink.GetSnapshot().Count(d => d.DiagnosticCode == SemanticDiagnosticCode.WeakDes3Key), Is.EqualTo(weak ? 1 : 0));
        if (detail.Length > 0) Assert.That(string.Join(" ", sink.GetSnapshot().Select(d => d.Message)), Does.Contain(detail));
        foreach (var warning in sink.GetSnapshot())
        {
            Assert.That(warning.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
            Assert.That(warning.Location, Is.EqualTo(new DiagnosticLocation(FunctionName: "Mac", Mechanism: "DES3-CMAC", Identifier: "external")));
            Assert.That(warning.Message, Does.Not.Contain(hex));
        }
    }

    [TestCase("DES3-ECB")]
    [TestCase("DES3-CBC")]
    public void WeakKeyFallbackPreservesAllPaddingContracts(string mechanism)
    {
        var key = Key(new string('0', 32), KeyAlgorithm.Tdea);
        foreach (string padding in new[] { "NONE", "PKCS-7", "ANSI-X923", "ISO-10126", "ISO-7816", "ISO-9797-M1", "ISO-9797-M2", "ISO-9797-M3", "TLS-CBC" })
        {
            var p = Parameters(mechanism, padding);
            string cipher = Call("Encrypt", p, key, Data, null).Value;
            Assert.That(Call("Decrypt", p, key, cipher, null).Value, Is.EqualTo(Data).IgnoreCase, padding);
        }
        var invalid = Parameters(mechanism, "ANSI-X923");
        // Deliberately encrypt a block that has a non-zero ANSI padding byte before its count.
        string malformed = Call("Encrypt", Parameters(mechanism), key, "0x(0000000000010003)", null).Value;
        Assert.Throws<CryptographicException>(() => Call("Decrypt", invalid, key, malformed, null));
    }

    [TestCase("AES-ECB", 15)]
    [TestCase("AES-ECB", 17)]
    [TestCase("DES3-ECB", 8)]
    [TestCase("DES3-CBC", 17)]
    [TestCase("DES3-CMAC", 8)]
    [TestCase("DES3-RETAIL", 32)]
    public void InvalidKeyLengthsRemainBlocking(string mechanism, int length)
    {
        var key = Key(new string('0', length * 2), KeyAlgorithm.Unknown);
        var p = Parameters(mechanism);
        Action operation = () => Call(mechanism.EndsWith("CMAC") || mechanism.EndsWith("RETAIL") ? "Mac" : "Encrypt", p, key, Data, new SemanticDiagnosticSink());
        if (mechanism.StartsWith("AES", StringComparison.Ordinal)) Assert.Throws<CryptographicException>(operation);
        else Assert.Throws<ArgumentException>(operation);
    }

    [Test]
    public void ImportPreservesWeakBytesButSharedStrictPrimitivesAndRandomGenerationRemainStrict()
    {
        var des = new DES3();
        var key = des.GenerateKey("DES3-ECB", "0x(00000000000000000000000000000000)");
        Assert.That(key.Value, Is.EqualTo("0x(00000000000000000000000000000000)"));
        Assert.Throws<ArgumentException>(() => new DES3_ECB().ModeEncryption(Parameters("DES3-ECB"), key,
            new StringVariableDeclaration { Value = Data, ValueFormat = FormatConversions.HEX }));
        Assert.Throws<ArgumentException>(() => DES3.ValidateUsableKey(new byte[16]));
        Assert.Throws<CryptographicException>(() => DES3_ECB.EncryptNoPadding(new byte[16], new byte[8]));
        Assert.Throws<ArgumentException>(() => DES3_RETAIL.ComputeMac(new byte[16], new byte[8], "ISO-9797-M2", 8));
        foreach (string size in new[] { "128", "192" })
        {
            var generated = des.GenerateKey("DES3-ECB", size);
            Assert.That(TripleDES.IsWeakKey(FormatConversions.ToByteArray(generated.Value, generated.ValueFormat)), Is.False);
        }
    }

    [TestCase("AES-GCM", "0x(000000000000000000000000)")]
    [TestCase("AES-CCM", "0x(00000000000000000000000000)")]
    public void MetadataWarningsDoNotBypassAeadAuthentication(string mechanism, string nonce)
    {
        var key = Key("000102030405060708090A0B0C0D0E0F", KeyAlgorithm.Tdea);
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.DeriveKey);
        var p = AlgorithmFactory.Create(mechanism).GenerateParameters(mechanism, new[] { "#NONCE:" + nonce });
        string ciphertext = Call("Encrypt", p, key, Data, null).Value;
        byte[] tampered = FormatConversions.ToByteArray(ciphertext, FormatConversions.HEX);
        tampered[^1] ^= 1;
        var sink = new SemanticDiagnosticSink();
        Assert.Throws<SemanticErrorException>(() => Call("Decrypt", p, key,
            FormatConversions.ByteArrayToHexString(tampered), sink));
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(new[]
            { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict }));
    }

    [TestCase("AES-ECB")]
    [TestCase("DES3-ECB")]
    [TestCase("DES3-CBC")]
    public void MetadataWarningsCannotMakeInvalidFormatsOrDataSuccessful(string mechanism)
    {
        var key = Key(NormalTdea, KeyAlgorithm.Hmac);
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.DeriveKey);
        var p = Parameters(mechanism);
        var sink = new SemanticDiagnosticSink();
        Assert.Throws<ArgumentException>(() => Call("Encrypt", p, key, "0x(01)", sink));
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.KeyTypeConflict));
        key.Value = "0x(GG)";
        Assert.That(() => Call("Encrypt", p, key, Data, null), Throws.Exception);
    }

    [TestCase("DES3-ECB", "IV", "0x(00)", "Encrypt")]
    [TestCase("DES3-CBC", "IV", "0x(00)", "Encrypt")]
    [TestCase("DES3-CBC", "PAD", "invalid", "Encrypt")]
    [TestCase("DES3-CBC", "PAD", "ISO-10126", "Mac")]
    [TestCase("DES3-CBC", "MACLEN", "3", "Mac")]
    [TestCase("DES3-RETAIL", "MACLEN", "9", "Mac")]
    [TestCase("DES3-RETAIL", "PAD", "NONE", "Mac")]
    [TestCase("DES3-CMAC", "PAD", "NONE", "Encrypt")]
    [TestCase("DES3-RETAIL", "PAD", "ISO-9797-M2", "Decrypt")]
    public void WeakKeysCannotBypassTechnicalParameterContracts(string mechanism, string parameter, string value, string function)
    {
        var key = Key(new string('0', 32), KeyAlgorithm.Tdea);
        var p = Parameters(mechanism);
        p.SetParameter(parameter, value);
        var sink = new SemanticDiagnosticSink();
        if ((mechanism == "DES3-CMAC" || mechanism == "DES3-RETAIL") && function != "Mac")
        {
            Assert.Throws<FunctionContractException>(() => Call(function, p, key, Data, sink));
            Assert.That(sink.GetSnapshot(), Is.Empty); // Rejected before the algorithm is invoked.
            return;
        }
        Assert.Throws<ArgumentException>(() => Call(function, p, key, Data, sink));
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Does.Contain(SemanticDiagnosticCode.WeakDes3Key));
    }

    [Test]
    public void RunnerWeakImportThenOperationReportsAllFourCategoriesWithoutMutation()
    {
        var runner = new CryptoScriptRunner();
        var imported = Execute(runner, "KEY k=GenerateKey(DES3-ECB,0x(00000000000000000000000000000000)) PARAM p=Parameters(DES3-ECB,#PAD:NONE)");
        Assert.That(imported.Diagnostics, Is.Empty); // Import has no sink-aware signature.
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("k");
        key.KeyType = KeyType.Secret(KeyAlgorithm.Aes);
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.DeriveKey);
        string snapshot = key.Serialize();
        var result = Execute(runner, $"VAR output=Encrypt(p,k,{Data})");
        Assert.That(VariableDictionary.Instance().Get("output").Value,
            Is.EqualTo("0x(74F4AE777AA431E89CA4757B2414F4D7)").IgnoreCase);
        Assert.That(result.Diagnostics.Select(d => d.DiagnosticCode), Is.EqualTo(new[]
        {
            SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict,
            SemanticDiagnosticCode.DesParityViolation, SemanticDiagnosticCode.WeakDes3Key
        }));
        Assert.That(key.Serialize(), Is.EqualTo(snapshot));
        Assert.That(result.Diagnostics.Select(d => d.Location!.Mechanism), Is.All.EqualTo("DES3-ECB"));
        Assert.That(result.Diagnostics.Select(d => d.Location!.Identifier), Is.All.EqualTo("k"));
    }

    [TestCase("DES3-CMAC")]
    [TestCase("DES3-RETAIL")]
    public void DirectDes3FacadeAlsoRejectsEncryptionWithMacOnlyMechanisms(string mechanism)
    {
        var key = Key(NormalTdea, KeyAlgorithm.Tdea);
        var p = Parameters(mechanism);
        var args = new AlgorithmCallArguments(new[]
        {
            new AlgorithmCallArgument(p.Value, p, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(key.Value, key, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(Data, kind: ResolvedCallArgumentKind.HexLiteral)
        });
        Assert.Throws<ArgumentException>(() => new DES3().Encrypt(args));
        Assert.Throws<ArgumentException>(() => new DES3().Decrypt(args));
    }

    // R01 PUBLIC TEST VECTORS ONLY: generated independently with OpenSSL 3.6.1.
    // Both keys below are rejected by .NET TripleDES.IsWeakKey and reduce to DES
    // with public component key 0123456789ABCDEF: K1=K2 (16B), K2=K3 (24B).
    // Raw binary stdin/stdout commands, applied to the explicit padded bytes:
    //   openssl enc -des-ede-ecb  -K <16B key> -nopad -nosalt
    //   openssl enc -des-ede-cbc  -K <16B key> -iv <IV> -nopad -nosalt
    //   openssl enc -des-ede3-ecb -K <24B key> -nopad -nosalt
    //   openssl enc -des-ede3-cbc -K <24B key> -iv <IV> -nopad -nosalt
    // Every deterministic row was generated for BOTH representations and compared.
    // IV: ECB absent, CBC=1234567890ABCDEF, CBC-MAC=0000000000000000.
    // CBC-MAC is the last 8 ciphertext bytes; MACLEN 4..8 selects its leftmost bytes.
    // Columns: padding | original plaintext | explicitly padded plaintext | ECB | CBC | full CBC-MAC.
    // Padding was materialized outside CryptoScript: PKCS7=count repeated; X923=zeros/count;
    // M2/7816=80/zeros; M1=zeros to a positive block multiple; M3=64-bit BE bit length
    // followed by M1-padded data; TLS=(count-1) repeated. NONE accepts positive aligned data.
    // There is no CryptoScript/BouncyCastle reference calculation or external CLI dependency
    // during tests. .NET single DES independently verifies the raw OpenSSL results.
    private const string PublicDesComponent = "0123456789ABCDEF";
    private static readonly string[] PublicDegenerateKeys =
    {
        "0123456789ABCDEF0123456789ABCDEF",
        "0123456789ABCDEFFEDCBA9876543210FEDCBA9876543210"
    };

    private static readonly string[] PublicPaddingReferences =
    {
        "NONE|0011223344556677|0011223344556677|CADB6782EE2B4823|EA2F68F28421D42F|CADB6782EE2B4823",
        "NONE|00112233445566778899AABBCCDDEEFF|00112233445566778899AABBCCDDEEFF|CADB6782EE2B4823EDD076666F6AE130|EA2F68F28421D42F2DB5F0501CA9AD0F|FC5B6B357742E3A1",
        "PKCS-7||0808080808080808|086F9A1D74C94D4E|C21106448C1E13C5|086F9A1D74C94D4E",
        "PKCS-7|00112233445566|0011223344556601|09400DFAC91E720C|03A257D64CE34CAE|09400DFAC91E720C",
        "PKCS-7|0011223344556677|00112233445566770808080808080808|CADB6782EE2B4823086F9A1D74C94D4E|EA2F68F28421D42F824C1E76B332B881|7DC0D604A4CAA82A",
        "PKCS-7|00112233445566778899AABBCCDDEEFF|00112233445566778899AABBCCDDEEFF0808080808080808|CADB6782EE2B4823EDD076666F6AE130086F9A1D74C94D4E|EA2F68F28421D42F2DB5F0501CA9AD0FE110EB25224FF434|40C96E2DB23C72A2",
        "PKCS-7|00112233445566778899AABBCCDDEEFF00|00112233445566778899AABBCCDDEEFF0007070707070707|CADB6782EE2B4823EDD076666F6AE1307A2DEEE2A1E36331|EA2F68F28421D42F2DB5F0501CA9AD0FD7F7F1A50B3EB789|CA0CDF55303D9766",
        "ANSI-X923||0000000000000008|9E3CDF76C5625E28|73B5528325D99F4A|9E3CDF76C5625E28",
        "ANSI-X923|00112233445566|0011223344556601|09400DFAC91E720C|03A257D64CE34CAE|09400DFAC91E720C",
        "ANSI-X923|0011223344556677|00112233445566770000000000000008|CADB6782EE2B48239E3CDF76C5625E28|EA2F68F28421D42F190FE5A64ED5A868|A4B24AAC6E4129FB",
        "ANSI-X923|00112233445566778899AABBCCDDEEFF|00112233445566778899AABBCCDDEEFF0000000000000008|CADB6782EE2B4823EDD076666F6AE1309E3CDF76C5625E28|EA2F68F28421D42F2DB5F0501CA9AD0FF33911E10EA4A5C4|0A89D9E407420EFB",
        "ANSI-X923|00112233445566778899AABBCCDDEEFF00|00112233445566778899AABBCCDDEEFF0000000000000007|CADB6782EE2B4823EDD076666F6AE1300CC4626D624C765E|EA2F68F28421D42F2DB5F0501CA9AD0F880F7747E680B3CB|60B7A95F776E9387",
        "ISO-7816||8000000000000000|CAEE534C523E1E79|B5624C0880D2EF3F|CAEE534C523E1E79",
        "ISO-7816|00112233445566|0011223344556680|953C6AA09A29341C|58185B800B70D398|953C6AA09A29341C",
        "ISO-7816|0011223344556677|00112233445566778000000000000000|CADB6782EE2B4823CAEE534C523E1E79|EA2F68F28421D42F338BECA586D83A72|91CA697F9742232A",
        "ISO-7816|00112233445566778899AABBCCDDEEFF|00112233445566778899AABBCCDDEEFF8000000000000000|CADB6782EE2B4823EDD076666F6AE130CAEE534C523E1E79|EA2F68F28421D42F2DB5F0501CA9AD0F5D1AB0F22D705BFF|0FC757C5FB3870AE",
        "ISO-7816|00112233445566778899AABBCCDDEEFF00|00112233445566778899AABBCCDDEEFF0080000000000000|CADB6782EE2B4823EDD076666F6AE130933A2CCFA7E60EB0|EA2F68F28421D42F2DB5F0501CA9AD0FF4C3385B56630EED|831DFB2EA7CFCB02",
        "ISO-9797-M1||0000000000000000|D5D44FF720683D0D|BD661569AE874E25|D5D44FF720683D0D",
        "ISO-9797-M1|00112233445566|0011223344556600|8E5D1537272A9FEC|BD305794AF41652F|8E5D1537272A9FEC",
        "ISO-9797-M1|0011223344556677|0011223344556677|CADB6782EE2B4823|EA2F68F28421D42F|CADB6782EE2B4823",
        "ISO-9797-M1|00112233445566778899AABBCCDDEEFF|00112233445566778899AABBCCDDEEFF|CADB6782EE2B4823EDD076666F6AE130|EA2F68F28421D42F2DB5F0501CA9AD0F|FC5B6B357742E3A1",
        "ISO-9797-M1|00112233445566778899AABBCCDDEEFF00|00112233445566778899AABBCCDDEEFF0000000000000000|CADB6782EE2B4823EDD076666F6AE130D5D44FF720683D0D|EA2F68F28421D42F2DB5F0501CA9AD0F1A48269B6AA9F90D|AC773E9B706C61BD",
        "ISO-9797-M2||8000000000000000|CAEE534C523E1E79|B5624C0880D2EF3F|CAEE534C523E1E79",
        "ISO-9797-M2|00112233445566|0011223344556680|953C6AA09A29341C|58185B800B70D398|953C6AA09A29341C",
        "ISO-9797-M2|0011223344556677|00112233445566778000000000000000|CADB6782EE2B4823CAEE534C523E1E79|EA2F68F28421D42F338BECA586D83A72|91CA697F9742232A",
        "ISO-9797-M2|00112233445566778899AABBCCDDEEFF|00112233445566778899AABBCCDDEEFF8000000000000000|CADB6782EE2B4823EDD076666F6AE130CAEE534C523E1E79|EA2F68F28421D42F2DB5F0501CA9AD0F5D1AB0F22D705BFF|0FC757C5FB3870AE",
        "ISO-9797-M2|00112233445566778899AABBCCDDEEFF00|00112233445566778899AABBCCDDEEFF0080000000000000|CADB6782EE2B4823EDD076666F6AE130933A2CCFA7E60EB0|EA2F68F28421D42F2DB5F0501CA9AD0FF4C3385B56630EED|831DFB2EA7CFCB02",
        "ISO-9797-M3||00000000000000000000000000000000|D5D44FF720683D0DD5D44FF720683D0D|BD661569AE874E255D976A504786581F|5661E9804FE87B77",
        "ISO-9797-M3|00112233445566|00000000000000380011223344556600|C7FD73F95D76C6028E5D1537272A9FEC|E50911B7BA016081F51BD81EEE550F8E|D906073FAA774574",
        "ISO-9797-M3|0011223344556677|00000000000000400011223344556677|91902B708858CF71CADB6782EE2B4823|AF695ECF4ACEF3C927EA8F2AD3C8A770|3A161808B6A97099",
        "ISO-9797-M3|00112233445566778899AABBCCDDEEFF|000000000000008000112233445566778899AABBCCDDEEFF|1311ADA3732AB2C4CADB6782EE2B4823EDD076666F6AE130|CFD7403E20DE6EFC8CCEAC1EF05399D03FF9C710B23E76B5|DA3DCD9A1BFF9DF5",
        "ISO-9797-M3|00112233445566778899AABBCCDDEEFF00|000000000000008800112233445566778899AABBCCDDEEFF0000000000000000|A5F791024F9EEA70CADB6782EE2B4823EDD076666F6AE130D5D44FF720683D0D|E87CF16FC64ADDC26FAA640C811B6C1AB2C7534E4CE575CF35A6971014D50EBA|15E1F380294E6C9A",
        "TLS-CBC||0707070707070707|8A87D41C5D3CAEAD|C558C8D559829D02|8A87D41C5D3CAEAD",
        "TLS-CBC|00112233445566|0011223344556600|8E5D1537272A9FEC|BD305794AF41652F|8E5D1537272A9FEC",
        "TLS-CBC|0011223344556677|00112233445566770707070707070707|CADB6782EE2B48238A87D41C5D3CAEAD|EA2F68F28421D42FDF08082C6E49AE26|78200969080771C8",
        "TLS-CBC|00112233445566778899AABBCCDDEEFF|00112233445566778899AABBCCDDEEFF0707070707070707|CADB6782EE2B4823EDD076666F6AE1308A87D41C5D3CAEAD|EA2F68F28421D42F2DB5F0501CA9AD0F1F91D21F81FCA9FF|30E005E348BDB89D",
        "TLS-CBC|00112233445566778899AABBCCDDEEFF00|00112233445566778899AABBCCDDEEFF0006060606060606|CADB6782EE2B4823EDD076666F6AE130F46A70557B2B56BF|EA2F68F28421D42F2DB5F0501CA9AD0F98D9396F1BA60AA6|D003E332349D6ABC",
    };

    private static string Hex(string hex) => "0x(" + hex + ")";
    private static string ScriptInput(string hex) => hex.Length == 0 ? "\"\"" : Hex(hex);

    private static KeyVariableDeclaration FallbackKey(string hex)
    {
        byte[] bytes = Convert.FromHexString(hex);
        Assert.That(TripleDES.IsWeakKey(bytes), Is.True, "The test must select the production fallback predicate.");
        using TripleDES strict = TripleDES.Create();
        Assert.Throws<CryptographicException>(() => strict.Key = bytes,
            "The same key must be rejected by the ordinary .NET backend.");
        return Key(hex, KeyAlgorithm.Tdea);
    }

    // Independent oracle: algebraic cancellation of the EDE stages leaves single DES.
    // Never calls CryptoScript's padding/cipher helpers or Bouncy Castle.
    private static byte[] ReferenceSingleDes(string mechanism, byte[] input, bool encrypt, bool zeroIv = false)
    {
        using DES reference = DES.Create();
        reference.Key = Convert.FromHexString(PublicDesComponent);
        reference.Mode = mechanism == "DES3-ECB" ? CipherMode.ECB : CipherMode.CBC;
        reference.Padding = PaddingMode.None;
        reference.IV = Convert.FromHexString(zeroIv ? "0000000000000000" : "1234567890ABCDEF");
        using ICryptoTransform transform = encrypt ? reference.CreateEncryptor() : reference.CreateDecryptor();
        return transform.TransformFinalBlock(input, 0, input.Length);
    }

    private static IEnumerable<TestCaseData> DeterministicFallbackCases()
    {
        foreach (string key in PublicDegenerateKeys)
        foreach (string mechanism in new[] { "DES3-ECB", "DES3-CBC" })
        foreach (string row in PublicPaddingReferences)
        {
            string[] fields = row.Split('|');
            yield return new TestCaseData(key, mechanism, fields[0], fields[1], fields[2],
                fields[mechanism == "DES3-ECB" ? 3 : 4])
                .SetName($"FallbackReference_{mechanism}_{key.Length / 2}B_{fields[0]}_{fields[1].Length / 2}B");
        }
    }

    [TestCaseSource(nameof(DeterministicFallbackCases))]
    public void FallbackDeterministicPaddingMatchesIndependentFullCiphertexts(string keyHex,
        string mechanism, string padding, string plainHex, string paddedHex, string expectedCipherHex)
    {
        var key = FallbackKey(keyHex);
        var parameters = Parameters(mechanism, padding);
        string snapshot = key.Serialize();
        var sink = new SemanticDiagnosticSink();
        string actual = Call("Encrypt", parameters, key, ScriptInput(plainHex), sink).Value;
        Assert.That(actual, Is.EqualTo(Hex(expectedCipherHex)).IgnoreCase);
        Assert.That(ReferenceSingleDes(mechanism, Convert.FromHexString(expectedCipherHex), false),
            Is.EqualTo(Convert.FromHexString(paddedHex)), "Independent verification of fixed OpenSSL reference.");
        // Method 1 cannot recover the original length and deliberately retains zero padding.
        string expectedClear = padding == "ISO-9797-M1" ? paddedHex : plainHex;
        Assert.That(Call("Decrypt", parameters, key, Hex(expectedCipherHex), null).Value,
            Is.EqualTo(Hex(expectedClear)).IgnoreCase);
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode),
            Is.EqualTo(new[] { SemanticDiagnosticCode.WeakDes3Key }));
        Assert.That(key.Serialize(), Is.EqualTo(snapshot));
    }

    private static IEnumerable<TestCaseData> CbcMacFallbackCases()
    {
        foreach (string key in PublicDegenerateKeys)
        foreach (string row in PublicPaddingReferences)
        for (int length = 4; length <= 8; length++)
        {
            string[] fields = row.Split('|');
            yield return new TestCaseData(key, fields[0], fields[1], fields[2], fields[5], length)
                .SetName($"FallbackCbcMac_{key.Length / 2}B_{fields[0]}_{fields[1].Length / 2}B_MAC{length}");
        }
    }

    [TestCaseSource(nameof(CbcMacFallbackCases))]
    public void FallbackCbcMacMatchesIndependentPaddingAndLeftTruncation(string keyHex, string padding,
        string plainHex, string paddedHex, string fullMacHex, int macLength)
    {
        var key = FallbackKey(keyHex);
        var parameters = Parameters("DES3-CBC", padding);
        parameters.SetParameter("MACLEN", macLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string snapshot = key.Serialize();
        var sink = new SemanticDiagnosticSink();
        byte[] independent = ReferenceSingleDes("DES3-CBC", Convert.FromHexString(paddedHex), true, zeroIv: true);
        Assert.That(Convert.ToHexString(independent.AsSpan(independent.Length - 8)),
            Is.EqualTo(fullMacHex), "Independent verification of the full fixed OpenSSL CBC-MAC.");
        Assert.That(Call("Mac", parameters, key, ScriptInput(plainHex), sink).Value,
            Is.EqualTo(Hex(fullMacHex[..(macLength * 2)])).IgnoreCase);
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode),
            Is.EqualTo(new[] { SemanticDiagnosticCode.WeakDes3Key }));
        Assert.That(key.Serialize(), Is.EqualTo(snapshot));
    }

    // Public OpenSSL vectors generated with the same raw commands above. Columns:
    // plaintext | explicit ISO10126 padded plaintext (arbitrary public filler) | ECB | CBC.
    // Filler bytes are NOT required to match on fresh encryption; only count/length matter.
    private static readonly string[] PublicIso10126References =
    {
        "|A1B2C3D4E5F60708|4F0C42511D0F88FC|D74F84372A38532B",
        "00112233445566|0011223344556601|09400DFAC91E720C|03A257D64CE34CAE",
        "0011223344556677|0011223344556677A1B2C3D4E5F60708|CADB6782EE2B48234F0C42511D0F88FC|EA2F68F28421D42FD809296294AA79BB",
        "00112233445566778899AABBCCDDEEFF|00112233445566778899AABBCCDDEEFFA1B2C3D4E5F60708|CADB6782EE2B4823EDD076666F6AE1304F0C42511D0F88FC|EA2F68F28421D42F2DB5F0501CA9AD0FDEC631A3BC4F7499",
        "00112233445566778899AABBCCDDEEFF00|00112233445566778899AABBCCDDEEFF00A1B2C3D4E5F607|CADB6782EE2B4823EDD076666F6AE130355F9382DBDB7282|EA2F68F28421D42F2DB5F0501CA9AD0FA515AAC91130195E",
    };

    private static IEnumerable<TestCaseData> Iso10126FallbackCases()
    {
        foreach (string key in PublicDegenerateKeys)
        foreach (string mechanism in new[] { "DES3-ECB", "DES3-CBC" })
        foreach (string row in PublicIso10126References)
        {
            string[] fields = row.Split('|');
            yield return new TestCaseData(key, mechanism, fields[0], fields[1],
                fields[mechanism == "DES3-ECB" ? 2 : 3])
                .SetName($"FallbackIso10126_{mechanism}_{key.Length / 2}B_{fields[0].Length / 2}B");
        }
    }

    [TestCaseSource(nameof(Iso10126FallbackCases))]
    public void FallbackIso10126AcceptsIndependentCiphertextsAndEmitsValidRandomPadding(
        string keyHex, string mechanism, string plainHex, string paddedHex, string fixedCipherHex)
    {
        var key = FallbackKey(keyHex);
        var parameters = Parameters(mechanism, "ISO-10126");
        string snapshot = key.Serialize();
        Assert.That(ReferenceSingleDes(mechanism, Convert.FromHexString(fixedCipherHex), false),
            Is.EqualTo(Convert.FromHexString(paddedHex)));
        Assert.That(Call("Decrypt", parameters, key, Hex(fixedCipherHex), null).Value,
            Is.EqualTo(Hex(plainHex)).IgnoreCase);
        byte[] encrypted = FormatConversions.ToByteArray(
            Call("Encrypt", parameters, key, ScriptInput(plainHex), null).Value, FormatConversions.HEX);
        byte[] independentlyDecrypted = ReferenceSingleDes(mechanism, encrypted, false);
        byte[] plain = Convert.FromHexString(plainHex);
        int count = 8 - plain.Length % 8;
        Assert.That(independentlyDecrypted.Length, Is.EqualTo(plain.Length + count));
        Assert.That(independentlyDecrypted.AsSpan(0, plain.Length).ToArray(), Is.EqualTo(plain));
        Assert.That(independentlyDecrypted[^1], Is.EqualTo((byte)count));
        // Deliberately no assertion on the randomly generated filler values.
        Assert.That(Call("Decrypt", parameters, key, Hex(Convert.ToHexString(encrypted)), null).Value,
            Is.EqualTo(Hex(plainHex)).IgnoreCase);
        Assert.That(key.Serialize(), Is.EqualTo(snapshot));
    }

    // Public negative vectors: raw decrypted block | OpenSSL ECB ciphertext | CBC ciphertext.
    // Count 00/09 is invalid for BOTH PKCS7 and ISO10126. 00 02 is invalid ONLY for
    // PKCS7: ISO10126 explicitly allows arbitrary filler, including that zero byte.
    private static readonly string[] PublicMalformedPaddingReferences =
    {
        "0011223344556600|8E5D1537272A9FEC|BD305794AF41652F",
        "0011223344556609|D34ACCC860AA6C64|708606F67C06FDDF",
        "0011223344550002|B192217688535DFD|3EC15FA8285ADCFD",
    };

    private static IEnumerable<TestCaseData> InvalidFallbackPaddingCases()
    {
        foreach (string key in PublicDegenerateKeys)
        foreach (string mechanism in new[] { "DES3-ECB", "DES3-CBC" })
        foreach (string row in PublicMalformedPaddingReferences)
        foreach (string padding in new[] { "PKCS-7", "ISO-10126" })
        {
            string[] fields = row.Split('|');
            if (padding == "ISO-10126" && fields[0].EndsWith("0002", StringComparison.Ordinal)) continue;
            yield return new TestCaseData(key, mechanism, padding, fields[0],
                fields[mechanism == "DES3-ECB" ? 1 : 2])
                .SetName($"FallbackInvalidPadding_{mechanism}_{key.Length / 2}B_{padding}_{fields[0]}");
        }
    }

    [TestCaseSource(nameof(InvalidFallbackPaddingCases))]
    public void FallbackRejectsActuallyInvalidIndependentPaddedCiphertexts(string keyHex, string mechanism,
        string padding, string rawHex, string cipherHex)
    {
        var key = FallbackKey(keyHex);
        Assert.That(ReferenceSingleDes(mechanism, Convert.FromHexString(cipherHex), false),
            Is.EqualTo(Convert.FromHexString(rawHex)));
        var sink = new SemanticDiagnosticSink();
        Assert.Throws<Org.BouncyCastle.Crypto.InvalidCipherTextException>(() =>
            Call("Decrypt", Parameters(mechanism, padding), key, Hex(cipherHex), sink));
        Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode),
            Is.EqualTo(new[] { SemanticDiagnosticCode.WeakDes3Key }));
    }

    [TestCase("DES3-ECB", "B192217688535DFD")]
    [TestCase("DES3-CBC", "3EC15FA8285ADCFD")]
    public void FallbackIso10126DoesNotMistakeArbitraryFillerForPkcs7(string mechanism, string cipherHex)
    {
        foreach (string keyHex in PublicDegenerateKeys)
            Assert.That(Call("Decrypt", Parameters(mechanism, "ISO-10126"), FallbackKey(keyHex),
                Hex(cipherHex), null).Value, Is.EqualTo("0x(001122334455)").IgnoreCase);
    }

    [TestCase("DES3-ECB")]
    [TestCase("DES3-CBC")]
    public void FallbackStillRejectsEmptyAndIncompleteDataAndUnknownPadding(string mechanism)
    {
        foreach (string keyHex in PublicDegenerateKeys)
        {
            var key = FallbackKey(keyHex);
            var none = Parameters(mechanism);
            foreach (string input in new[] { "\"\"", "0x(01)", "0x(00112233445566)", "0x(001122334455667788)" })
            {
                Assert.Throws<ArgumentException>(() => Call("Encrypt", none, key, input, null));
                Assert.Throws<ArgumentException>(() => Call("Decrypt", none, key, input, null));
            }
            Assert.Throws<ArgumentException>(() => Call("Encrypt", Parameters(mechanism, "invalid"), key, Data, null));
            Assert.Throws<ArgumentException>(() => Call("Decrypt", Parameters(mechanism, "invalid"), key, Data, null));
            if (mechanism == "DES3-CBC")
            {
                foreach (string iv in new[] { "0x()", "0x(00112233445566)", "0x(001122334455667788)" })
                {
                    var invalidIv = Parameters(mechanism);
                    invalidIv.SetParameter("IV", iv);
                    Assert.Throws<ArgumentException>(() => Call("Encrypt", invalidIv, key, Data, null));
                    Assert.Throws<ArgumentException>(() => Call("Decrypt", invalidIv, key, Data, null));
                }
                Assert.Throws<ArgumentException>(() => Call("Mac", Parameters(mechanism, "ISO-10126"), key, Data, null));
                Assert.Throws<ArgumentException>(() => Call("Mac", none, key, "\"\"", null));
                Assert.Throws<ArgumentException>(() => Call("Mac", none, key, "0x(01)", null));
            }
        }
    }

    [TestCase("DES3-ECB")]
    [TestCase("DES3-CBC")]
    public void FallbackTechnicalAbortRetainsWarningInRunnerAndStopsLaterStatements(string mechanism)
    {
        var runner = new CryptoScriptRunner();
        Execute(runner, $"KEY k=GenerateKey({mechanism},0x({PublicDegenerateKeys[0]})) PARAM p=Parameters({mechanism},#PAD:NONE)");
        Assert.Throws<SemanticErrorException>(() => Execute(runner,
            "VAR failed=Encrypt(p,k,0x(01)) VAR later=0x(01)"));
        Assert.That(runner.Diagnostics[0].DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.WeakDes3Key));
        Assert.That(runner.Diagnostics[0].Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
        Assert.That(runner.Diagnostics[0].Location!.StatementIndex, Is.EqualTo(1));
        Assert.That(runner.Diagnostics[0].Location!.OperationId, Is.EqualTo(1));
        Assert.That(runner.Diagnostics[0].Location!.FunctionName, Is.EqualTo("Encrypt"));
        Assert.That(runner.Diagnostics[0].Location!.Mechanism, Is.EqualTo(mechanism));
        Assert.That(runner.Diagnostics.Count(d => d.DiagnosticCode == SemanticDiagnosticCode.WeakDes3Key), Is.EqualTo(1));
        Assert.That(runner.Diagnostics[^1].Severity, Is.EqualTo(SemanticDiagnosticSeverity.Error));
        Assert.That(VariableDictionary.Instance().Contains("failed"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("later"), Is.False);
    }

    private static IEnumerable<TestCaseData> IsolatedParityByteCases()
    {
        foreach (string hex in new[] { NormalTdea, NormalTdea + "89ABCDEF01234567" })
        for (int offset = 0; offset < hex.Length / 2; offset++)
            yield return new TestCaseData(hex, offset).SetName($"IsolatedParity_{hex.Length / 2}B_Byte{offset}");
    }

    [TestCaseSource(nameof(IsolatedParityByteCases))]
    public void EveryKeyByteIsCheckedForParityWithoutChangingEffectiveCipherOrMetadata(string hex, int offset)
    {
        byte[] bytes = Convert.FromHexString(hex);
        bytes[offset] ^= 1; // Only the least significant DES parity bit at this one position.
        string changed = Convert.ToHexString(bytes);
        for (int mode = 0; mode < Modes.Length; mode++)
        {
            string mechanism = "DES3-" + (mode == 2 ? "CBC" : Modes[mode]);
            string function = mode < 2 ? "Encrypt" : "Mac";
            var key = Key(changed, KeyAlgorithm.Tdea);
            string snapshot = key.Serialize();
            var parameters = Parameters(mechanism);
            string expected = Call(function, parameters, Key(hex, KeyAlgorithm.Tdea), Data, null).Value;
            var sink = new SemanticDiagnosticSink();
            Assert.That(Call(function, parameters, key, Data, sink).Value, Is.EqualTo(expected), Modes[mode]);
            Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode),
                Is.EqualTo(new[] { SemanticDiagnosticCode.DesParityViolation }), Modes[mode]);
            Assert.That(sink.GetSnapshot()[0].Message, Does.Contain("K" + (offset / 8 + 1)));
            Assert.That(key.Serialize(), Is.EqualTo(snapshot));
            Assert.That(FormatConversions.ToByteArray(key.Value, key.ValueFormat), Is.EqualTo(bytes));
        }
    }

    [TestCase("0123456789ABCDEF0022446688AACCEE", true, true, "K1 = K2")]
    [TestCase("0123456789ABCDEF0022446688AACCEEFEDCBA9876543210", true, true, "K1 = K2")]
    [TestCase("0123456789ABCDEFFEDCBA9876543210FFDDBB9977553311", true, true, "K2 = K3")]
    [TestCase(NormalTdea + "0022446688AACCEE", true, false, "")]
    [TestCase("0123456789ABCDEF0223456789ABCDEFFEDCBA9876543210", false, false, "")]
    [TestCase("0123456789ABCDEFFEDCBA9876543210FDDCBA9876543210", false, false, "")]
    public void EffectiveEqualityIgnoresParityButNotMaterialBits(string hex, bool parity, bool degenerate, string detail)
    {
        // First three cases differ only in parity; K1=K3 control is not degeneration.
        // Last two change a non-parity bit as well (and retain odd parity in that byte).
        Assert.That(TripleDES.IsWeakKey(Convert.FromHexString(hex)), Is.EqualTo(degenerate));
        for (int mode = 0; mode < Modes.Length; mode++)
        {
            string mechanism = "DES3-" + (mode == 2 ? "CBC" : Modes[mode]);
            string function = mode < 2 ? "Encrypt" : "Mac";
            var key = Key(hex, KeyAlgorithm.Tdea);
            string snapshot = key.Serialize();
            var sink = new SemanticDiagnosticSink();
            Call(function, Parameters(mechanism), key, Data, sink);
            var expected = new List<SemanticDiagnosticCode>();
            if (parity) expected.Add(SemanticDiagnosticCode.DesParityViolation);
            if (degenerate) expected.Add(SemanticDiagnosticCode.WeakDes3Key);
            Assert.That(sink.GetSnapshot().Select(d => d.DiagnosticCode), Is.EqualTo(expected), Modes[mode]);
            if (degenerate) Assert.That(sink.GetSnapshot().Single(d => d.DiagnosticCode == SemanticDiagnosticCode.WeakDes3Key)
                .Message, Does.Contain(detail));
            Assert.That(key.Serialize(), Is.EqualTo(snapshot));
        }
    }

}
