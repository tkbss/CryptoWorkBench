using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CryptoScript.Documentation;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.EP2;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Ep2PanReceiptEncryptionTests
{
    private const string Mechanism = "EP2-PAN-RECEIPT-ENCRYPTION";
    private const string Key = "D37FF3ECC3F2EAC21EF3C968A9DE2934";
    private const string TerminalKey = "0123456789ABCDEF23456789ABCDEF01";
    private const string Dol = "1322042006015445524D3132333412345678A000000111";
    private const string Plain = "1234567890123456";
    private const string Cipher = "3E3F25CCEE9B49A90EA87134D3B18222";

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
        var runner = new CryptoScriptRunner(); runner.Execute(tree); return runner;
    }
    private static VariableDeclaration Variable(string id) => VariableDictionary.Instance().Get(id);
    private static string Prefix(bool direct = false) =>
        $"KEY k=GenerateKey(AES-ECB,0x({Key})) VAR data=0x({Plain}) " +
        (direct ? $"PARAM p=#MECH:{Mechanism} " : $"PARAM p=Parameters({Mechanism}) ");
    private static AlgorithmCallArguments Arguments() => new(new[] { "p", "k", "data" }.Select(id =>
        new AlgorithmCallArgument(Variable(id).Value, Variable(id), ResolvedCallArgumentKind.Variable)));

    [TestCase(false)] [TestCase(true)]
    public void IndependentAesVectorThroughCorePublicApiAndRunner(bool decrypt)
    {
        string input = decrypt ? Cipher : Plain, expected = decrypt ? Plain : Cipher;
        Assert.That(Convert.ToHexString(new Iso7816Padding().Pad(Convert.FromHexString(Plain))),
            Is.EqualTo("12345678901234568000000000000000"));
        Assert.That(Convert.ToHexString(Ep2PanReceiptEncryptionPrimitive.Transform(
            Convert.FromHexString(Key), Convert.FromHexString(input), !decrypt)), Is.EqualTo(expected));
        foreach (bool direct in new[] { false, true })
        foreach (bool literal in new[] { false, true })
        {
            Reset(); Run(Prefix(direct)); Variable("data").Value = "0x(" + input + ")";
            var runner = Run($"VAR result={(decrypt ? "Decrypt" : "Encrypt")}(p,k,{(literal ? "0x(" + input + ")" : "data")})");
            Assert.That(Variable("result").Value, Is.EqualTo("0x(" + expected + ")").IgnoreCase);
            Assert.That(runner.Diagnostics, Is.Empty);
            var invocation = new OperationInvocation(new[] { "p", "k", "data" }.Select(id =>
                new ResolvedCallArgument(Variable(id).Value, ResolvedCallArgumentKind.Variable, Variable(id))));
            var operations = new CryptoOperations();
            var result = decrypt ? operations.Decrypt(invocation) : operations.Encrypt(invocation);
            Assert.That(result.Value, Is.EqualTo("0x(" + expected + ")").IgnoreCase);
            Assert.That(result, Is.TypeOf<StringVariableDeclaration>());
            Assert.That(result.ValueFormat, Is.EqualTo(FormatConversions.HEX));
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(15)] [TestCase(16)]
    [TestCase(17)] [TestCase(31)] [TestCase(32)] [TestCase(33)] [TestCase(1024)]
    public void BoundaryLengthsFullPaddingBlockAndByteExactRoundtrip(int length)
    {
        byte[] key = Convert.FromHexString(Key);
        byte[] input = Enumerable.Range(0, length).Select(i => new byte[] { 0, 0x80, 0xFF, 0, 1 }[i % 5]).ToArray();
        byte[] original = input.ToArray();
        // Independently specify the padded bytes; do not use the production padding helper as oracle.
        byte[] padded = input.Concat(new byte[] { 0x80 }).Concat(new byte[15 - length % 16]).ToArray();
        using var aes = Aes.Create(); aes.Key = key;
        byte[] cipher = Ep2PanReceiptEncryptionPrimitive.Transform(key, input, true);
        Assert.That(cipher.Length, Is.EqualTo((length / 16 + 1) * 16));
        Assert.That(aes.DecryptEcb(cipher, PaddingMode.None), Is.EqualTo(padded));
        Assert.That(Ep2PanReceiptEncryptionPrimitive.Transform(key, cipher, false), Is.EqualTo(input));
        Assert.That(Ep2PanReceiptEncryptionPrimitive.Transform(key, input, true), Is.EqualTo(cipher));
        Assert.That(input, Is.EqualTo(original));
        Assert.That(Convert.ToHexString(key), Is.EqualTo(Key));
    }

    [TestCase("00000000000000000000000000000000")]
    [TestCase("11111111111111111111111111111111")]
    [TestCase("12345678901234567F00000000000000")]
    [TestCase("12345678901234568000000100000000")]
    [TestCase("8000000000000000000000000000000000000000000000000000000000000000")]
    public void InvalidPaddingRejectsWithoutLeakingOrReturningPlaintext(string malformed)
    {
        byte[] key = Convert.FromHexString(Key);
        using var aes = Aes.Create(); aes.Key = key;
        byte[] cipher = aes.EncryptEcb(Convert.FromHexString(malformed), PaddingMode.None);
        var error = Assert.Throws<CryptographicException>(() => Ep2PanReceiptEncryptionPrimitive.Transform(key, cipher, false));
        Assert.That(error!.ToString(), Does.Not.Contain(Key).And.Not.Contain(Plain).And.Not.Contain(malformed));
        Run(Prefix());
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(ParserBuilder.StringBuild(
            $"VAR result=Decrypt(p,k,0x({Convert.ToHexString(cipher)})) VAR after=0x(AA)").program()));
        Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        Assert.That(string.Join(" ", runner.Diagnostics.Select(x => x.Message)),
            Does.Not.Contain(Key).And.Not.Contain(Plain).And.Not.Contain(Convert.ToHexString(cipher)));
    }

    [Test]
    public void InvalidKeyAndCiphertextLengthsAndKnownSizeMetadataReject()
    {
        Run(Prefix()); var key = (KeyVariableDeclaration)Variable("k");
        foreach (int length in new[] { 0, 1, 15, 17, 24, 32 })
        foreach (bool encrypt in new[] { false, true })
        {
            key.Value = "0x(" + new string('0', length * 2) + ")";
            Assert.Throws<ArgumentException>(() => Ep2PanReceiptEncryptionPrimitive.Transform(new byte[length], new byte[16], encrypt));
            var algorithm = new Ep2PanReceiptEncryption();
            Assert.Throws<ArgumentException>(() => { if (encrypt) algorithm.Encrypt(Arguments()); else algorithm.Decrypt(Arguments()); });
        }
        foreach (int length in new[] { 0, 1, 15, 17, 31, 33 })
            Assert.Throws<ArgumentException>(() => Ep2PanReceiptEncryptionPrimitive.Transform(Convert.FromHexString(Key), new byte[length], false));
        key.Value = "0x(GG)";
        Assert.Throws<ArgumentException>(() => new Ep2PanReceiptEncryption().Encrypt(Arguments()));
        var wrongSize = new KeyVariableDeclaration { Value = "0x(" + Key + ")", KeySize = "256" };
        var args = Arguments().Arguments.ToArray(); args[1] = new(wrongSize.Value, wrongSize, ResolvedCallArgumentKind.Variable);
        Assert.Throws<ArgumentException>(() => new Ep2PanReceiptEncryption().Encrypt(new AlgorithmCallArguments(args)));
    }

    [Test]
    public void OnlyMatchingMechAllowedAtDeclarationGenerationAndUse()
    {
        foreach (string extra in new[] { "#MECH:" + Mechanism, "#MECH:AES-ECB", "#PAD:ISO-7816", "#PAD:NONE",
            "#IV:0x(00)", "#NONCE:0x(00)", "#COUNTER:0x(00)", "#OFFSET:0x(00)", "#DIRECTION:\"SEND\"", "#MACLEN:8" })
        foreach (bool direct in new[] { false, true })
        {
            Reset();
            string declaration = direct ? $"PARAM p=#MECH:{Mechanism} {extra}" : $"PARAM p=Parameters({Mechanism},{extra})";
            Assert.Throws<SemanticErrorException>(() => Run(declaration), declaration);
            Assert.That(VariableDictionary.Instance().Contains("p"), Is.False);
        }
        var algorithm = new Ep2PanReceiptEncryption();
        Assert.Throws<ArgumentException>(() => algorithm.GenerateParameters("AES-ECB"));
        Run(Prefix()); var p = (ParameterVariableDeclaration)Variable("p");
        p.GetParameters().Remove("#MECH");
        Assert.Throws<ArgumentException>(() => algorithm.Encrypt(Arguments()));
        p.Mechanism = "AES-ECB";
        Assert.Throws<ArgumentException>(() => algorithm.Encrypt(Arguments()));
        p.Mechanism = Mechanism; p.SetParameter("#PAD", "NONE");
        Assert.Throws<FunctionContractException>(() => algorithm.Encrypt(Arguments()));
    }

    [Test]
    public void NormativeKdfOutputFeedsReceiptEncryptionAndRetainsUnknownMetadata()
    {
        var runner = Run($"KEY terminal=GenerateKey(HMAC-SHA256,0x({TerminalKey})) " +
            $"PARAM kd=Parameters(KDF-EP2-PAN-RECEIPT-TRX) KEY trx=Derive(kd,terminal,0x({Dol})) " +
            $"PARAM p=Parameters({Mechanism}) VAR encrypted=Encrypt(p,trx,0x({Plain})) VAR restored=Decrypt(p,trx,encrypted)");
        var key = (KeyVariableDeclaration)Variable("trx");
        Assert.That(key.Value, Is.EqualTo("0x(" + Key + ")").IgnoreCase);
        Assert.That(key.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Unknown)));
        Assert.That(Variable("encrypted").Value, Is.EqualTo("0x(" + Cipher + ")").IgnoreCase);
        Assert.That(Variable("restored").Value, Is.EqualTo("0x(" + Plain + ")").IgnoreCase);
        Assert.That(runner.Diagnostics, Is.Empty);
        string before = key.Serialize();
        Assert.That(Run($"VAR again=Encrypt(p,trx,0x({Plain}))").Diagnostics, Is.Empty);
        Assert.That(Variable("trx"), Is.SameAs(key)); Assert.That(key.Serialize(), Is.EqualTo(before));
    }

    [Test]
    public void GenericAesEcbStillUsesItsExistingUnpaddedMultiBlockContract()
    {
        Run("KEY k=GenerateKey(AES-ECB,0x(2B7E151628AED2A6ABF7158809CF4F3C)) PARAM p=Parameters(AES-ECB) " +
            "VAR d=0x(6BC1BEE22E409F96E93D7E117393172AAE2D8A571E03AC9C9EB76FAC45AF8E51) " +
            "VAR result=Encrypt(p,k,d) VAR restored=Decrypt(p,k,result)");
        Assert.That(Variable("result").Value, Is.EqualTo("0x(3AD77BB40D7A3660A89ECAF32466EF97F5D3D58503B9699DE785895A96FDBAAF)").IgnoreCase);
        Assert.That(Variable("restored").Value, Is.EqualTo(Variable("d").Value).IgnoreCase);
        Assert.Throws<SemanticErrorException>(() => Run("Encrypt(p,k,0x(00))"));
    }

    [Test]
    public void PublishedDocumentationExampleExecutes()
    {
        Assert.That(new FileInfoDocumentationProvider().TryGetDocumentation(Mechanism, out string markdown), Is.True);
        var examples = Regex.Matches(markdown, @"```cryptoscript\s*([\s\S]*?)```");
        Assert.That(examples.Count, Is.EqualTo(1));
        Assert.That(Run(examples[0].Groups[1].Value).Diagnostics, Is.Empty);
        Assert.That(Variable("cipher").Value, Is.EqualTo("0x(" + Cipher + ")").IgnoreCase);
        Assert.That(Variable("restored").Value, Is.EqualTo("0x(" + Plain + ")").IgnoreCase);
    }

    [TestCase(false)] [TestCase(true)]
    public void StructuredAndLegacyContracts(bool decrypt)
    {
        Run(Prefix());
        if (decrypt) Variable("data").Value = "0x(" + Cipher + ")";
        var algorithm = new Ep2PanReceiptEncryption();
        StringVariableDeclaration Call(AlgorithmCallArguments a) => decrypt ? algorithm.Decrypt(a) : algorithm.Encrypt(a);
        var valid = Arguments().Arguments.ToArray();
        Assert.DoesNotThrow(() => Call(new(valid)));
        Assert.Throws<ArgumentException>(() => Call(new(valid.Take(2))));
        Assert.Throws<ArgumentException>(() => Call(new(valid.Concat(new[] { valid[2] }))));
        for (int i = 0; i < 3; i++)
        foreach (var replacement in new[] {
            new AlgorithmCallArgument(valid[i].Value),
            new AlgorithmCallArgument(valid[i].Value, valid[i].SourceVariable, ResolvedCallArgumentKind.NestedFunctionCall),
            new AlgorithmCallArgument(valid[i].Value, valid[i].SourceVariable, ResolvedCallArgumentKind.Variable) { HasReconstructedSourceVariable = true },
            valid[(i + 1) % 3]
        })
        {
            var invalid = valid.ToArray(); invalid[i] = replacement;
            Assert.Throws<ArgumentException>(() => Call(new(invalid)));
        }
        string[] values = valid.Select(a => a.Value!).ToArray();
        Assert.Throws<ArgumentException>(() => algorithm.Encrypt(values));
        Assert.Throws<ArgumentException>(() => algorithm.Decrypt(values));
        Assert.Throws<ArgumentException>(() => new CryptoOperations().Encrypt(values));
        Assert.Throws<ArgumentException>(() => new CryptoOperations().Decrypt(values));
        foreach (string malformed in new[] { "0x(0)", "0x(GG)", "\"private input\"", "b64(AA==)" })
        {
            var invalid = valid.ToArray(); invalid[2] = new(malformed, null, ResolvedCallArgumentKind.HexLiteral);
            var error = Assert.Throws<ArgumentException>(() => Call(new(invalid)));
            Assert.That(error!.Message, Does.Not.Contain(malformed));
        }
        var p = (ParameterVariableDeclaration)Variable("p");
        p.SetParameter("#MECH", "AES-CTR");
        Assert.Throws<ArgumentException>(() => Call(new(valid)));
    }

    [TestCase(false)] [TestCase(true)]
    public void RunnerRejectsWrongArgumentForms(bool decrypt)
    {
        string function = decrypt ? "Decrypt" : "Encrypt";
        foreach (string args in new[] { "p,data,data", "p,0x(" + Key + "),data", "k,p,data", "p,k,k", "p,k,p",
            "p,k,\"text\"", "p,k,b64(AA==)", "p,k,1", "p,k,Encrypt(p,k,data)", "p,GenerateKey(AES-ECB,128),data",
            $"Parameters({Mechanism}),k,data", Mechanism + ",k,data", "p,k", "p,k,data,data", "serialized,k,data" })
        {
            Reset(); Run(Prefix() + $"VAR serialized=\"#MECH:{Mechanism}\"");
            Assert.Throws<SemanticErrorException>(() => Run($"VAR result={function}({args}) VAR after=0x(AA)"), args);
            Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        }
        Reset(); Run(Prefix() + "VAR text=\"text\" VAR base64=b64(AA==)");
        Assert.Throws<SemanticErrorException>(() => Run($"{function}(p,k,text)"));
        Assert.Throws<SemanticErrorException>(() => Run($"{function}(p,k,base64)"));
    }

    [TestCase(false)] [TestCase(true)]
    public void KeyIdentityDiagnosticsAndTechnicalFailure(bool decrypt)
    {
        Run(Prefix() + $"KEY other=GenerateKey(AES-ECB,0x({Key}))");
        if (decrypt) Variable("data").Value = "0x(" + Cipher + ")";
        var key = (KeyVariableDeclaration)Variable("k");
        key.KeyType = KeyType.Secret(KeyAlgorithm.Hmac);
        key.Usage = KeyUsagePolicy.Restricted(decrypt ? KeyUsage.Encrypt : KeyUsage.Decrypt);
        string before = key.Serialize();
        string function = decrypt ? "Decrypt" : "Encrypt";
        var runner = Run($"VAR result={function}(p,k,data) VAR clean={function}(p,other,data)");
        Assert.That(Variable("result").Value, Is.EqualTo(Variable("clean").Value));
        Assert.That(Variable("k"), Is.SameAs(key));
        Assert.That(key.Serialize(), Is.EqualTo(before));
        Assert.That(runner.Diagnostics.Select(x => x.DiagnosticCode), Is.EqualTo(new[] {
            SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict }));
        Assert.That(runner.Diagnostics.All(x => x.Severity == SemanticDiagnosticSeverity.Warning && x.Identifier == "k"), Is.True);
        Assert.That(string.Join(" ", runner.Diagnostics.Select(x => x.Message)), Does.Not.Contain(Key).And.Not.Contain(Plain));
        key.Value = "0x(11)";
        runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(ParserBuilder.StringBuild($"{function}(p,k,data)").program()));
        Assert.That(runner.Diagnostics.Count(x => x.Severity == SemanticDiagnosticSeverity.Warning), Is.EqualTo(2));
        Assert.That(runner.Diagnostics.Count(x => x.Severity == SemanticDiagnosticSeverity.Error), Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void MatchingOrUnspecifiedMetadataProduceNoWarning(bool decrypt)
    {
        Run(Prefix()); var key = (KeyVariableDeclaration)Variable("k");
        if (decrypt) Variable("data").Value = "0x(" + Cipher + ")";
        key.Usage = KeyUsagePolicy.Restricted(decrypt ? KeyUsage.Decrypt : KeyUsage.Encrypt);
        string function = decrypt ? "Decrypt" : "Encrypt";
        Assert.That(Run($"VAR result={function}(p,k,data)").Diagnostics, Is.Empty);
        key.KeyType = KeyType.Secret(KeyAlgorithm.Unknown);
        Assert.That(Run($"VAR unknown={function}(p,k,data)").Diagnostics, Is.Empty);
    }

    [Test]
    public void UnsupportedDirectAndRuntimeOperationsReject()
    {
        var algorithm = new Ep2PanReceiptEncryption(); string[] empty = Array.Empty<string>();
        Assert.Throws<NotSupportedException>(() => algorithm.Hash(empty));
        Assert.Throws<NotSupportedException>(() => algorithm.Mac(empty));
        Assert.Throws<NotSupportedException>(() => algorithm.Wrap(empty));
        Assert.Throws<NotSupportedException>(() => algorithm.Unwrap(empty));
        Assert.Throws<NotSupportedException>(() => algorithm.Derive(new AlgorithmCallArguments(Array.Empty<AlgorithmCallArgument>())));
        Assert.Throws<NotSupportedException>(() => algorithm.GenerateKey(Mechanism, "128"));
        Assert.Throws<NotSupportedException>(() => algorithm.GenerateBlockHeader(empty));
        Assert.Throws<NotSupportedException>(() => algorithm.GenerateBlockHeader(Mechanism));
        Run(Prefix());
        foreach (string call in new[] { "Hash(p,data)", "Mac(p,k,data)", "Wrap(p,k,data)", "Unwrap(p,k,data)", "Derive(p,k,data)", $"GenerateKey({Mechanism},128)" })
            Assert.Throws<SemanticErrorException>(() => Run(call), call);
        var entry = MechanismRegistry.Entries.Single(e => e.CanonicalName == Mechanism);
        Assert.That(entry.SupportedFunctions, Is.EquivalentTo(new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Encrypt, CryptoScriptFunction.Decrypt }));
    }

}
