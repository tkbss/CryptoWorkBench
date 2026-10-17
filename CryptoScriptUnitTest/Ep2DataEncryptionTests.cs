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
public class Ep2DataEncryptionTests
{
    private const string Mechanism = "EP2-DATA-ENCRYPTION";
    private const string Key = "0123456789ABCDEF23456789ABCDEF01";
    private const string Nonce = "0123456789ABCDEF23456789";
    private const string Plain = "6799998900000200051D2512220080810790";
    private const string Cipher = "BC689340D7866D4EF5C0C35A4844BAAB0288";
    private static readonly string[] Parameters = { "#NONCE:0x(" + Nonce + ")", "#OFFSET:0x(06)", "#DIRECTION:\"SEND\"" };

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
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero, script);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False, script);
        var runner = new CryptoScriptRunner();
        runner.Execute(tree);
        return runner;
    }

    private static VariableDeclaration Variable(string name) => VariableDictionary.Instance().Get(name);
    private static string Prefix(bool direct = false) =>
        $"KEY k=GenerateKey(AES-ECB,0x({Key})) VAR data=0x({Plain}) " +
        (direct ? $"PARAM p=#MECH:{Mechanism} {string.Join(" ", Parameters)} " :
            $"PARAM p=Parameters({Mechanism},{string.Join(",", Parameters)}) ");
    private static AlgorithmCallArguments Arguments() => new(new[] { "p", "k", "data" }
        .Select(id => new AlgorithmCallArgument(Variable(id).Value, Variable(id), ResolvedCallArgumentKind.Variable)));

    [TestCase(false)]
    [TestCase(true)]
    public void NormativeVectorCorePublicApiAndFullRunner(bool decrypt)
    {
        string input = decrypt ? Cipher : Plain;
        string expected = decrypt ? Plain : Cipher;
        Assert.That(Convert.ToHexString(Ep2DataEncryptionPrimitive.CounterBlock(Convert.FromHexString(Nonce), 6, 0)),
            Is.EqualTo("0123456789ABCDEF2345678906000000"));
        Assert.That(Convert.ToHexString(Ep2DataEncryptionPrimitive.Transform(Convert.FromHexString(Key),
            Convert.FromHexString(Nonce), 6, "SEND", Convert.FromHexString(input))), Is.EqualTo(expected));
        foreach (bool direct in new[] { false, true })
        foreach (bool literal in new[] { false, true })
        {
            Reset(); Run(Prefix(direct));
            Variable("data").Value = "0x(" + input + ")";
            string function = decrypt ? "Decrypt" : "Encrypt";
            var runner = Run($"VAR result={function}(p,k,{(literal ? "0x(" + input + ")" : "data")})");
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

    [TestCase(0)] [TestCase(1)] [TestCase(15)] [TestCase(16)] [TestCase(17)]
    [TestCase(31)] [TestCase(32)] [TestCase(33)] [TestCase(4097)]
    public void LengthsAndCounterCarryMatchIndependentEcbOracle(int length)
    {
        byte[] key = Convert.FromHexString(Key), nonce = Convert.FromHexString(Nonce);
        byte[] input = Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();
        byte[] original = input.ToArray();
        // Build the entire ECB input independently, using fixed-width hexadecimal block numbers.
        byte[] blocks = Enumerable.Range(0, (length + 15) / 16).SelectMany(i =>
            Convert.FromHexString(Nonce + "06" + i.ToString("X6"))).ToArray();
        using var aes = Aes.Create(); aes.Key = key;
        byte[] stream = aes.EncryptEcb(blocks, PaddingMode.None);
        byte[] expected = input.Select((b, i) => (byte)(b ^ stream[i])).ToArray();
        byte[] output = Ep2DataEncryptionPrimitive.Transform(key, nonce, 6, "SEND", input);
        Assert.That(output, Is.EqualTo(expected));
        Assert.That(Ep2DataEncryptionPrimitive.Transform(key, nonce, 6, "SEND", output), Is.EqualTo(input));
        Assert.That(input, Is.EqualTo(original));
        Assert.That(Convert.ToHexString(key), Is.EqualTo(Key));
        Assert.That(Convert.ToHexString(nonce), Is.EqualTo(Nonce));
    }

    [TestCase(0U, "000000")] [TestCase(255U, "0000FF")] [TestCase(256U, "000100")]
    [TestCase(65535U, "00FFFF")] [TestCase(65536U, "010000")] [TestCase(16777215U, "FFFFFF")]
    public void CounterBoundariesPreserveNonceAndOffset(uint counter, string suffix)
    {
        Assert.That(Convert.ToHexString(Ep2DataEncryptionPrimitive.CounterBlock(Convert.FromHexString(Nonce), 7, counter)),
            Is.EqualTo(Nonce + "07" + suffix));
    }

    [Test]
    public void CapacityBoundaryWithoutAllocatingPayload()
    {
        long max = 268435456;
        foreach (long length in new[] { 0, max - 16, max - 1, max })
            Assert.DoesNotThrow(() => Ep2DataEncryptionPrimitive.ValidateDataLength(length));
        foreach (long length in new[] { -1, max + 1, long.MaxValue })
            Assert.Throws<ArgumentException>(() => Ep2DataEncryptionPrimitive.ValidateDataLength(length));
        Assert.Throws<ArgumentException>(() => Ep2DataEncryptionPrimitive.CounterBlock(new byte[12], 0, 16777216));
        Assert.Throws<ArgumentException>(() => Ep2DataEncryptionPrimitive.CounterBlock(new byte[12], 0, uint.MaxValue));
    }

    [TestCase("SEND", 7)] [TestCase("RECEIVE", 1)]
    public void CompleteOffsetTableAndRfu(string direction, int maximum)
    {
        var algorithm = new Ep2DataEncryption();
        for (int offset = 0; offset <= 255; offset++)
        {
            string[] parameters = { Parameters[0], "#OFFSET:0x(" + offset.ToString("X2") + ")", "#DIRECTION:\"" + direction + "\"" };
            if (offset <= maximum)
            {
                var p = algorithm.GenerateParameters(Mechanism, parameters);
                Run($"KEY k{offset}=GenerateKey(AES-ECB,0x({Key})) PARAM p{offset}=Parameters({Mechanism},{string.Join(",", parameters)}) " +
                    $"VAR e{offset}=Encrypt(p{offset},k{offset},0x({Plain})) VAR d{offset}=Decrypt(p{offset},k{offset},e{offset})");
                Assert.That(Variable("d" + offset).Value, Is.EqualTo("0x(" + Plain + ")").IgnoreCase);
            }
            else
            {
                Assert.Throws<ArgumentException>(() => algorithm.GenerateParameters(Mechanism, parameters));
                Assert.Throws<ArgumentException>(() => Ep2DataEncryptionPrimitive.Transform(new byte[16], new byte[12], (byte)offset, direction, Array.Empty<byte>()));
            }
        }
    }

    [Test]
    public void RejectMalformedMissingAdditionalAndDuplicateParameters()
    {
        var algorithm = new Ep2DataEncryption();
        foreach (int position in Enumerable.Range(0, 3))
            Assert.Throws<FunctionContractException>(() => algorithm.GenerateParameters(Mechanism, Parameters.Where((_, i) => i != position).ToArray()));
        foreach (string extra in new[] { Parameters[0], Parameters[1], Parameters[2], "#MECH:" + Mechanism,
            "#COUNTER:0x(000000)", "#IV:0x(00)", "#PAD:NONE", "#MACLEN:8", "#VARIANT:ENCRYPTION" })
        foreach (bool direct in new[] { false, true })
        {
            Reset();
            string declaration = direct ? $"PARAM p=#MECH:{Mechanism} {string.Join(" ", Parameters)} {extra}" :
                $"PARAM p=Parameters({Mechanism},{string.Join(",", Parameters)},{extra})";
            Assert.Throws<SemanticErrorException>(() => Run(declaration), declaration);
        }
        foreach (string value in new[] { "0x(00)", "0x(00000000000000000000000000)", "b64(AA==)", "\"abcdefghijkl\"", "0x(GG)", "0x(0)", "0x()" })
            Assert.Throws<ArgumentException>(() => algorithm.GenerateParameters(Mechanism, new[] { "#NONCE:" + value, Parameters[1], Parameters[2] }));
        foreach (string value in new[] { "0x()", "0x(0006)", "0x(0)", "0x(GG)", "6", "\"06\"" })
            Assert.Throws<ArgumentException>(() => algorithm.GenerateParameters(Mechanism, new[] { Parameters[0], "#OFFSET:" + value, Parameters[2] }));
        foreach (string value in new[] { "SEND", "\"send\"", "\"RECV\"", "\"\"", "0x(53454E44)", "b64(U0VORA==)" })
            Assert.Throws<ArgumentException>(() => algorithm.GenerateParameters(Mechanism, new[] { Parameters[0], Parameters[1], "#DIRECTION:" + value }));
        Assert.Throws<ArgumentException>(() => Ep2DataEncryptionPrimitive.ValidateOffset(0, "invalid"));
        Assert.Throws<ArgumentException>(() => algorithm.GenerateParameters("AES-CTR", Parameters));
    }

    [TestCase(false)] [TestCase(true)]
    public void StructuredAndLegacyContracts(bool decrypt)
    {
        Run(Prefix());
        var algorithm = new Ep2DataEncryption();
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
            $"Parameters({Mechanism},{string.Join(",", Parameters)}),k,data", Mechanism + ",k,data", "p,k", "p,k,data,data", "serialized,k,data" })
        {
            Reset(); Run(Prefix() + $"VAR serialized=\"#MECH:{Mechanism}\"");
            Assert.Throws<SemanticErrorException>(() => Run($"VAR result={function}({args}) VAR after=0x(AA)"), args);
            Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        }
        Reset(); Run(Prefix() + "VAR text=\"text\" VAR base64=b64(AA==)");
        Assert.Throws<SemanticErrorException>(() => Run($"{function}(p,k,text)"));
        Assert.Throws<SemanticErrorException>(() => Run($"{function}(p,k,base64)"));
    }

    [Test]
    public void InvalidKeyLengthsKnownSizesAndNonceLengthsBlock()
    {
        Run(Prefix()); var key = (KeyVariableDeclaration)Variable("k");
        foreach (int length in new[] { 0, 1, 15, 17, 24, 32 })
        {
            key.KeyValue = "0x(" + new string('0', length * 2) + ")";
            Assert.Throws<ArgumentException>(() => new Ep2DataEncryption().Encrypt(Arguments()));
            Assert.Throws<ArgumentException>(() => Ep2DataEncryptionPrimitive.Transform(new byte[length], new byte[12], 0, "SEND", Array.Empty<byte>()));
        }
        key.KeyValue = "0x(GG)";
        Assert.Throws<ArgumentException>(() => new Ep2DataEncryption().Encrypt(Arguments()));
        foreach (int length in new[] { 0, 11, 13, 16 })
            Assert.Throws<ArgumentException>(() => Ep2DataEncryptionPrimitive.Transform(new byte[16], new byte[length], 0, "SEND", Array.Empty<byte>()));
        var wrongSize = new KeyVariableDeclaration { KeyValue = "0x(" + Key + ")", KeySize = "256" };
        var args = Arguments().Arguments.ToArray(); args[1] = new(wrongSize.KeyValue, wrongSize, ResolvedCallArgumentKind.Variable);
        Assert.Throws<ArgumentException>(() => new Ep2DataEncryption().Encrypt(new AlgorithmCallArguments(args)));
    }

    [TestCase(false)] [TestCase(true)]
    public void KeyIdentityDiagnosticsAndTechnicalFailure(bool decrypt)
    {
        Run(Prefix() + $"KEY other=GenerateKey(AES-ECB,0x({Key}))");
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
        key.KeyValue = "0x(11)";
        runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(ParserBuilder.StringBuild($"{function}(p,k,data)").program()));
        Assert.That(runner.Diagnostics.Count(x => x.Severity == SemanticDiagnosticSeverity.Warning), Is.EqualTo(2));
        Assert.That(runner.Diagnostics.Count(x => x.Severity == SemanticDiagnosticSeverity.Error), Is.EqualTo(1));
    }

    [Test]
    public void EncryptionSessionVariantFeedsProfileWithoutMetadataMutation()
    {
        var runner = Run(Prefix() +
            "PARAM kd=Parameters(KDF-EP2-SESSION,#SALT:0x(0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345),#VARIANT:ENCRYPTION) " +
            "KEY derived=Derive(kd,k,\"\") VAR encrypted=Encrypt(p,derived,data) VAR result=Decrypt(p,derived,encrypted)");
        var key = (KeyVariableDeclaration)Variable("derived");
        Assert.That(key.KeyValue, Is.EqualTo("0x(29DDFF143885D21CD2425D1FDDA2C229)").IgnoreCase);
        Assert.That(Variable("result").Value, Is.EqualTo("0x(" + Plain + ")").IgnoreCase);
        Assert.That(runner.Diagnostics, Is.Empty);
        string before = key.Serialize();
        Assert.That(Run("VAR again=Encrypt(p,derived,data)").Diagnostics, Is.Empty);
        Assert.That(key.Serialize(), Is.EqualTo(before));
    }

    [Test]
    public void UnsupportedDirectAndRuntimeOperationsReject()
    {
        var algorithm = new Ep2DataEncryption(); string[] empty = Array.Empty<string>();
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

    [Test]
    public void GenericAesCtrRetainsNist192BitKeyAnd32BitCounterContract()
    {
        Run("KEY k=GenerateKey(AES-CTR,0x(8E73B0F7DA0E6452C810F32B809079E562F8EAD2522C6B7B)) " +
            "PARAM p=Parameters(AES-CTR,#NONCE:0x(F0F1F2F3F4F5F6F7F8F9FAFB),#COUNTER:0x(FCFDFEFF)) " +
            "VAR result=Encrypt(p,k,0x(6BC1BEE22E409F96E93D7E117393172A)) VAR clear=Decrypt(p,k,result)");
        Assert.That(Variable("result").Value, Is.EqualTo("0x(1ABC932417521CA24F2B0459FE7E6E0B)").IgnoreCase);
        Assert.That(Variable("clear").Value, Is.EqualTo("0x(6BC1BEE22E409F96E93D7E117393172A)").IgnoreCase);
        foreach (string extra in new[] { "#OFFSET:0x(00)", "#DIRECTION:\"SEND\"" })
            Assert.Throws<SemanticErrorException>(() => Run($"PARAM bad=Parameters(AES-CTR,{extra})"));
    }

    [Test]
    public void DocumentationExamplesExecuteAndParameterVariablesResolve()
    {
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocumentation(Mechanism, out string markdown), Is.True);
        var examples = Regex.Matches(markdown, @"```cryptoscript\s*([\s\S]*?)```");
        Assert.That(examples.Count, Is.EqualTo(2));
        Run(examples[0].Groups[1].Value);
        Assert.That(Variable("cipher").Value, Is.EqualTo("0x(" + Cipher + ")").IgnoreCase);
        Assert.That(Variable("restored").Value, Is.EqualTo("0x(" + Plain + ")").IgnoreCase);
        Reset(); Run(examples[1].Groups[1].Value);
        Reset();
        Run($"VAR nonce=0x({Nonce}) VAR offset=0x(06) VAR direction=\"SEND\" " +
            $"KEY k=GenerateKey(AES-ECB,0x({Key})) PARAM p=Parameters({Mechanism},#NONCE:nonce,#OFFSET:offset,#DIRECTION:direction) " +
            $"VAR result=Encrypt(p,k,0x({Plain}))");
        Assert.That(Variable("result").Value, Is.EqualTo("0x(" + Cipher + ")").IgnoreCase);
    }

    [Test]
    public void DirectParameterDeclarationsRejectMissingAndMalformedValues()
    {
        foreach (int position in Enumerable.Range(0, 3))
            Assert.Throws<SemanticErrorException>(() => Run($"PARAM bad=#MECH:{Mechanism} " +
                string.Join(" ", Parameters.Where((_, i) => i != position))));
        foreach (string[] values in new[] {
            new[] { "#NONCE:0x(00)", Parameters[1], Parameters[2] },
            new[] { Parameters[0], "#OFFSET:0x(08)", Parameters[2] },
            new[] { Parameters[0], Parameters[1], "#DIRECTION:\"RECEIVE\"" },
            new[] { Parameters[0], Parameters[1], "#DIRECTION:\"invalid\"" }
        })
            Assert.Throws<SemanticErrorException>(() => Run($"PARAM bad=#MECH:{Mechanism} " + string.Join(" ", values)));
    }

    [TestCase(false)] [TestCase(true)]
    public void MatchingOrUnspecifiedMetadataProduceNoWarning(bool decrypt)
    {
        Run(Prefix()); var key = (KeyVariableDeclaration)Variable("k");
        key.Usage = KeyUsagePolicy.Restricted(decrypt ? KeyUsage.Decrypt : KeyUsage.Encrypt);
        string function = decrypt ? "Decrypt" : "Encrypt";
        Assert.That(Run($"VAR result={function}(p,k,data)").Diagnostics, Is.Empty);
        key.KeyType = KeyType.Secret(KeyAlgorithm.Unknown);
        Assert.That(Run($"VAR unknown={function}(p,k,data)").Diagnostics, Is.Empty);
    }
}
