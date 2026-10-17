using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.EP2;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Documentation;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Ep2Tr31KeyBlockTests
{
    private const string Mechanism = "EP2-TR31-KEY-BLOCK";
    private const string Kbpk = "0123456789ABCDEF23456789ABCDEF01";
    private const string Material = "00112233445566778899AABBCCDDEEFF";
    private const string Filler = "E5EA76E4B74217D867F034AD99C1";
    private const string Header = "D011299HX00N0000";
    private const string Kbek = "04A20656E2F4FF9CB37D1131AE2DE276";
    private const string Kbak = "5107101DB9D158DE994A9A8C93DA3FDC";
    private const string Confidential = "008000112233445566778899AABBCCDDEEFFE5EA76E4B74217D867F034AD99C1";
    private const string Cipher = "BF671CBAAF2250BA849EB05E2A2BBF060B1B021B99ABD7D3DB5DF4BCE553E777";
    private const string Mac = "659CC2356DF2F011D8EAA1375B0D4334";
    private const string Wire = Header + Cipher + Mac;
    private static byte[] Hex(string value) => Convert.FromHexString(value);

    [SetUp]
    public void Reset()
    {
        VariableDictionary.Instance().Clear();
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.SyntaxErrorOccured = false;
    }
    private static VariableDeclaration Variable(string id) => VariableDictionary.Instance().Get(id);
    private static CryptoScriptRunner Run(string script)
    {
        var parser = ParserBuilder.StringBuild(script); var tree = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        var runner = new CryptoScriptRunner(); runner.Execute(tree); return runner;
    }
    private static string Prefix(bool direct = false, string header = Header) =>
        $"KEY kbpk=GenerateKey(AES-ECB,0x({Kbpk})) KEY material=GenerateKey(HMAC-SHA256,0x({Material})) " +
        (direct ? $"PARAM p=#MECH:{Mechanism} #BLKH:\"{header}\" #RND:0x({Filler}) " :
            $"PARAM p=Parameters({Mechanism},#BLKH:\"{header}\",#RND:0x({Filler})) ") +
        $"PARAM u=Parameters({Mechanism}) VAR wire=\"{Wire}\" ";
    private static AlgorithmCallArguments Args(params string[] names) => new(names.Select(id =>
        new AlgorithmCallArgument(Variable(id).Value, Variable(id), ResolvedCallArgumentKind.Variable)));
    private static string ToWire(StringVariableDeclaration block)
    {
        var tr31 = TR31String.FromString(block.Value);
        return tr31.Block + Convert.ToHexString(tr31.Cryptogram) + Convert.ToHexString(tr31.Mac);
    }

    [Test]
    public void NormativeVectorAndIndependentlyFixedIntermediateValues()
    {
        Assert.That(Wire.Length, Is.EqualTo(112));
        Assert.That(WrapAESTR31.DeriveKey(Hex(Kbpk), WrapAESTR31.EncryptionDerivationData(128), 128), Is.EqualTo(Hex(Kbek)));
        Assert.That(WrapAESTR31.DeriveKey(Hex(Kbpk), WrapAESTR31.MACDerivationData(128), 128), Is.EqualTo(Hex(Kbak)));
        var data = Tr31ConfidentialData.Create(Hex(Material), 16, 0, Hex(Filler));
        Assert.That(data.KeyLength, Is.EqualTo(Hex("0080")));
        Assert.That(data.ObfuscationPadding, Is.Empty);
        Assert.That(data.CipherBlockPadding, Is.EqualTo(Hex(Filler)));
        Assert.That(data.ToArray(), Is.EqualTo(Hex(Confidential)));
        Assert.That(WrapAESTR31.ComputeMAC([.. Encoding.ASCII.GetBytes(Header), .. Hex(Confidential)], Hex(Kbak)), Is.EqualTo(Hex(Mac)));
        Assert.That(WrapAESTR31.EncryptNoPadding(Hex(Kbek), Hex(Mac), Hex(Confidential)), Is.EqualTo(Hex(Cipher)));
        using var aes = Aes.Create(); aes.Key = Hex(Kbek);
        Assert.That(aes.DecryptCbc(Hex(Cipher), Hex(Mac), PaddingMode.None), Is.EqualTo(Hex(Confidential)));
        Assert.That(Ep2Tr31KeyBlockPrimitive.Wrap(Hex(Kbpk), Hex(Material), Header, Hex(Filler)), Is.EqualTo(Wire));
        Assert.That(Ep2Tr31KeyBlockPrimitive.Unwrap(Hex(Kbpk), Wire), Is.EqualTo(Hex(Material)));
    }

    [TestCase(Header)] [TestCase("D01129AHG00N0000")] [TestCase("D01129BHG00N0000")]
    public void EveryProfileThroughRunnerPublicApiAndBothSerializations(string header)
    {
        foreach (bool direct in new[] { false, true })
        {
            Reset(); Run(Prefix(direct, header));
            Assert.That(Run("VAR block=Wrap(p,kbpk,material) KEY restored=Unwrap(u,kbpk,block)").Diagnostics, Is.Empty);
            var block = (StringVariableDeclaration)Variable("block");
            string wire = ToWire(block);
            Assert.That(wire.Length, Is.EqualTo(112)); Assert.That(wire[..16], Is.EqualTo(header));
            if (header == Header) Assert.That(wire, Is.EqualTo(Wire));
            Assert.That(Variable("restored").Value, Is.EqualTo("0x(" + Material + ")").IgnoreCase);
            var key = (KeyVariableDeclaration)Variable("restored");
            Assert.That(key.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Hmac)));
            Assert.That(key.KeySize, Is.EqualTo("128")); Assert.That(key.KeyAttributes.Single().Data, Is.EqualTo(header));
            Variable("wire").Value = "\"" + wire + "\"";
            Assert.That(new Ep2Tr31KeyBlock().Unwrap(Args("u", "kbpk", "wire")).KeyValue, Is.EqualTo(key.KeyValue));
            var operations = new CryptoOperations();
            OperationInvocation Invocation(params string[] ids) => new(ids.Select(id =>
                new ResolvedCallArgument(Variable(id).Value, ResolvedCallArgumentKind.Variable, Variable(id))));
            Assert.That(ToWire((StringVariableDeclaration)operations.Wrap(Invocation("p", "kbpk", "material"))), Is.EqualTo(wire));
            Assert.That(operations.Unwrap(Invocation("u", "kbpk", "block")).Value, Is.EqualTo(key.Value));
        }
    }

    public static IEnumerable<string> InvalidHeaders()
    {
        for (int i = 0; i < 16; i++) yield return Header[..i] + (Header[i] == '0' ? '1' : '0') + Header[(i + 1)..];
        foreach (string header in new[] { "", Header[..15], Header + "0", "D01129CHG00N0000", "D01129AHX00N0000", "D011299HG00N0000",
            "D01129BHG00N0100", "D011299AX00N0000", "D011299HX01N0000", "D011299HX00E0000", "D011299HX00N0020" }) yield return header;
    }
    [TestCaseSource(nameof(InvalidHeaders))]
    public void RejectsAllNonProfileHeaderFields(string header)
    {
        Assert.Throws<ArgumentException>(() => Ep2Tr31KeyBlockPrimitive.Wrap(Hex(Kbpk), Hex(Material), header, Hex(Filler)));
        Assert.Throws<ArgumentException>(() => Ep2Tr31KeyBlockPrimitive.Unwrap(Hex(Kbpk), header + Cipher + Mac));
        Assert.Throws<SemanticErrorException>(() => Run($"PARAM p=Parameters({Mechanism},#BLKH:\"{header}\")"));
    }

    [Test]
    public void AuthenticationFailureNeverReleasesKeyOrLeaksSecret()
    {
        foreach (int position in new[] { 16, 48, 79, 80, 96, 111 })
        {
            string changed = Wire[..position] + (Wire[position] == '0' ? '1' : '0') + Wire[(position + 1)..];
            Assert.Throws<CryptographicException>(() => Ep2Tr31KeyBlockPrimitive.Unwrap(Hex(Kbpk), changed));
            Reset(); Run(Prefix()); Variable("wire").Value = "\"" + changed + "\"";
            var runner = new CryptoScriptRunner();
            Assert.Throws<SemanticErrorException>(() => runner.Execute(ParserBuilder.StringBuild("KEY result=Unwrap(u,kbpk,wire) VAR after=0x(AA)").program()));
            Assert.That(VariableDictionary.Instance().Contains("result"), Is.False);
            Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
            string messages = string.Join(" ", runner.Diagnostics.Select(d => d.Message));
            foreach (string secret in new[] { Kbpk, Material, Confidential, changed }) Assert.That(messages, Does.Not.Contain(secret));
        }
        Assert.Throws<CryptographicException>(() => Ep2Tr31KeyBlockPrimitive.Unwrap(new byte[16], Wire));
        // Change to a different *valid* profile: structural validation passes, header authentication must fail.
        Assert.Throws<CryptographicException>(() => Ep2Tr31KeyBlockPrimitive.Unwrap(Hex(Kbpk), "D01129AHG00N0000" + Cipher + Mac));
    }

    [TestCase("0000")] [TestCase("0081")] [TestCase("0180")] [TestCase("00C0")] [TestCase("0100")]
    public void AuthenticatedWrongKeyLengthIsRejected(string length)
    {
        byte[] data = Hex(length + Confidential[4..]);
        byte[] mac = WrapAESTR31.ComputeMAC([.. Encoding.ASCII.GetBytes(Header), .. data], Hex(Kbak));
        using var aes = Aes.Create(); aes.Key = Hex(Kbek);
        string wire = Header + Convert.ToHexString(aes.EncryptCbc(data, mac, PaddingMode.None)) + Convert.ToHexString(mac);
        Assert.Throws<CryptographicException>(() => Ep2Tr31KeyBlockPrimitive.Unwrap(Hex(Kbpk), wire));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(15)] [TestCase(17)] [TestCase(24)] [TestCase(32)]
    public void WrongKeySizesAtEitherPositionReject(int length)
    {
        Assert.Throws<ArgumentException>(() => Ep2Tr31KeyBlockPrimitive.Wrap(new byte[length], Hex(Material), Header));
        Assert.Throws<ArgumentException>(() => Ep2Tr31KeyBlockPrimitive.Wrap(Hex(Kbpk), new byte[length], Header));
        Assert.Throws<ArgumentException>(() => Ep2Tr31KeyBlockPrimitive.Unwrap(new byte[length], Wire));
        Run(Prefix());
        foreach (string id in new[] { "kbpk", "material" })
        {
            var key = (KeyVariableDeclaration)Variable(id); string original = key.KeyValue;
            key.KeyValue = "0x(" + new string('0', length * 2) + ")";
            Assert.Throws<ArgumentException>(() => new Ep2Tr31KeyBlock().Wrap(Args("p", "kbpk", "material")));
            key.KeyValue = original;
        }
    }

    [Test]
    public void FillerContractAndNoSilentReplacement()
    {
        foreach (int length in new[] { 0, 1, 13, 15, 16, 30 })
            Assert.Throws<ArgumentException>(() => Ep2Tr31KeyBlockPrimitive.Wrap(Hex(Kbpk), Hex(Material), Header, new byte[length]));
        Run(Prefix()); var p = (ParameterVariableDeclaration)Variable("p");
        foreach (string invalid in new[] { "0x()", "0x(00)", "0x(GG)", "0x(0)", "", "\"E5EA76E4B74217D867F034AD99C1\"" })
        {
            p.SetParameter("#RND", invalid);
            Assert.Catch<Exception>(() => new Ep2Tr31KeyBlock().Wrap(Args("p", "kbpk", "material")));
        }
        p.GetParameters().Remove("#RND");
        string wire = ToWire(new Ep2Tr31KeyBlock().Wrap(Args("p", "kbpk", "material")));
        Assert.That(Ep2Tr31KeyBlockPrimitive.Unwrap(Hex(Kbpk), wire), Is.EqualTo(Hex(Material)));
        using var aes = Aes.Create(); aes.Key = Hex(Kbek);
        byte[] clear = aes.DecryptCbc(Hex(wire[16..80]), Hex(wire[80..]), PaddingMode.None);
        Assert.That(clear.Length, Is.EqualTo(32)); Assert.That(clear[..18], Is.EqualTo(Hex(Confidential)[..18]));
        // No probabilistic uniqueness assertion; validate shape and round-trip for omitted randomness.
    }

    [Test]
    public void CompleteRepresentationsOnlyAndExactConfidentialLength()
    {
        Run(Prefix());
        foreach (string wire in new[] { Wire[..^1], Wire + "0", Wire + "00", Wire[..16], Wire.Replace("BF67", "ZZ67"),
            Header + Cipher[..32] + Mac, Header + Cipher + Cipher[..32] + Mac, "D0144" + Wire[5..] })
        {
            Assert.Throws<ArgumentException>(() => Ep2Tr31KeyBlockPrimitive.Unwrap(Hex(Kbpk), wire));
            Variable("wire").Value = "\"" + wire + "\"";
            Assert.Throws<ArgumentException>(() => new Ep2Tr31KeyBlock().Unwrap(Args("u", "kbpk", "wire")));
        }
        Run("VAR block=Wrap(p,kbpk,material)");
        var block = (StringVariableDeclaration)Variable("block"); string valid = block.Value;
        foreach (string malformed in new[] { valid + "00", valid[..^1], valid.Replace("0x(", "0X("), valid.Replace(Cipher, Cipher[..32], StringComparison.OrdinalIgnoreCase),
            valid.Replace(Mac, Mac[..16], StringComparison.OrdinalIgnoreCase), "\"" + Header + "\"", "0x(" + Cipher + Mac + ")" })
        {
            block.Value = malformed;
            Assert.Throws<ArgumentException>(() => new Ep2Tr31KeyBlock().Unwrap(Args("u", "kbpk", "block")));
        }
    }

    [Test]
    public void ParameterContractsAtDeclarationAndUse()
    {
        foreach (bool direct in new[] { false, true })
        foreach (string extras in new[] { "#RND:0x(00)", "#BLKH:\"bad\"", "#IV:0x(00)", "#PAD:NONE",
            "#MECH:" + Mechanism, "#MECH:AES-ECB", "#BLKH:\"" + Header + "\",#BLKH:\"" + Header + "\"",
            "#RND:0x(" + Filler + "),#RND:0x(" + Filler + ")" })
        {
            Reset();
            string script = direct ? $"PARAM p=#MECH:{Mechanism} {extras.Replace(',', ' ')}" : $"PARAM p=Parameters({Mechanism},{extras})";
            Assert.Throws<SemanticErrorException>(() => Run(script), script);
        }
        Reset(); Run(Prefix()); var algorithm = new Ep2Tr31KeyBlock();
        Assert.Throws<FunctionContractException>(() => algorithm.Wrap(Args("u", "kbpk", "material")));
        Assert.Throws<FunctionContractException>(() => algorithm.Unwrap(Args("p", "kbpk", "wire")));
        var u = (ParameterVariableDeclaration)Variable("u"); u.SetParameter("#BLKH", "\"D01129AHG00N0000\"");
        Assert.Throws<ArgumentException>(() => algorithm.Unwrap(Args("u", "kbpk", "wire")));
        u.SetParameter("#BLKH", "\"" + Header + "\"");
        Assert.DoesNotThrow(() => algorithm.Unwrap(Args("u", "kbpk", "wire")));
        u.SetParameter("#MECH", "AES-ECB");
        Assert.Throws<ArgumentException>(() => algorithm.Unwrap(Args("u", "kbpk", "wire")));
    }

    [TestCase(false)] [TestCase(true)]
    public void ObjectContractsAndUnsupportedOperations(bool unwrap)
    {
        Run(Prefix()); var algorithm = new Ep2Tr31KeyBlock();
        var valid = (unwrap ? Args("u", "kbpk", "wire") : Args("p", "kbpk", "material")).Arguments.ToArray();
        void Call(AlgorithmCallArguments args) { if (unwrap) algorithm.Unwrap(args); else algorithm.Wrap(args); }
        Assert.DoesNotThrow(() => Call(new(valid)));
        Assert.Throws<ArgumentException>(() => Call(new(valid.Take(2))));
        Assert.Throws<ArgumentException>(() => Call(new(valid.Append(valid[2]))));
        for (int i = 0; i < 3; i++)
        foreach (var replacement in new[] { new AlgorithmCallArgument(valid[i].Value),
            new AlgorithmCallArgument(valid[i].Value, valid[i].SourceVariable, ResolvedCallArgumentKind.NestedFunctionCall),
            new AlgorithmCallArgument(valid[i].Value, valid[i].SourceVariable, ResolvedCallArgumentKind.Variable) { HasReconstructedSourceVariable = true },
            valid[(i + 1) % 3] })
        {
            if (!unwrap && i == 1 && ReferenceEquals(replacement, valid[2])) continue; // Both are genuine 128-bit KEYs; metadata only warns.
            var invalid = valid.ToArray(); invalid[i] = replacement;
            Assert.Catch<Exception>(() => Call(new(invalid)));
        }
        Assert.Throws<NotSupportedException>(() => algorithm.Wrap(valid.Select(a => a.Value!).ToArray()));
        Assert.Throws<NotSupportedException>(() => algorithm.Unwrap(valid.Select(a => a.Value!).ToArray()));
        Assert.Throws<ArgumentException>(() => new CryptoOperations().Wrap("p", "kbpk", "material"));
        Assert.Throws<ArgumentException>(() => new CryptoOperations().Unwrap("u", "kbpk", "wire"));
        Assert.Throws<NotSupportedException>(() => algorithm.Encrypt([]));
        Assert.Throws<NotSupportedException>(() => algorithm.Decrypt([]));
        Assert.Throws<NotSupportedException>(() => algorithm.Mac([]));
        Assert.Throws<NotSupportedException>(() => algorithm.Hash([]));
        Assert.Throws<NotSupportedException>(() => algorithm.GenerateBlockHeader(Array.Empty<string>()));
        Assert.Throws<NotSupportedException>(() => algorithm.GenerateBlockHeader(Mechanism));
        Assert.Throws<NotSupportedException>(() => algorithm.GenerateKey(Mechanism, "128"));
        Assert.Throws<NotSupportedException>(() => algorithm.Derive(new AlgorithmCallArguments([])));
        Assert.Throws<ArgumentException>(() => algorithm.GenerateParameters("AES-ECB"));
    }

    [Test]
    public void MetadataWarningsPreserveObjectsAndDoNotInferRolesFromBytes()
    {
        Run(Prefix());
        var kbpk = (KeyVariableDeclaration)Variable("kbpk"); var material = (KeyVariableDeclaration)Variable("material");
        kbpk.KeyType = KeyType.Secret(KeyAlgorithm.Tdea); kbpk.Usage = KeyUsagePolicy.Restricted(KeyUsage.Encrypt);
        material.KeyType = KeyType.Secret(KeyAlgorithm.Aes);
        string beforeKbpk = kbpk.Serialize(), beforeMaterial = material.Serialize();
        var runner = Run("VAR block=Wrap(p,kbpk,material)");
        Assert.That(ToWire((StringVariableDeclaration)Variable("block")), Is.EqualTo(Wire));
        Assert.That(runner.Diagnostics.Select(d => d.DiagnosticCode), Is.EqualTo(new[] { SemanticDiagnosticCode.KeyTypeConflict,
            SemanticDiagnosticCode.KeyUsageConflict, SemanticDiagnosticCode.KeyTypeConflict }));
        Assert.That(kbpk.Serialize(), Is.EqualTo(beforeKbpk)); Assert.That(material.Serialize(), Is.EqualTo(beforeMaterial));
        Assert.That(Variable("kbpk"), Is.SameAs(kbpk)); Assert.That(Variable("material"), Is.SameAs(material));
        runner = Run("KEY restored=Unwrap(u,kbpk,block)");
        Assert.That(runner.Diagnostics.Count, Is.EqualTo(2));
        kbpk.KeyType = KeyType.Secret(KeyAlgorithm.Aes); kbpk.Usage = KeyUsagePolicy.Unspecified;
        runner = Run("VAR same=Wrap(p,kbpk,kbpk)");
        Assert.That(runner.Diagnostics.Count, Is.EqualTo(1)); // Same object has two different roles, H conflicts once.
        material.KeyValue = kbpk.KeyValue; material.KeyType = KeyType.Secret(KeyAlgorithm.Hmac);
        Assert.That(Run("VAR equal=Wrap(p,kbpk,material)").Diagnostics, Is.Empty);
        foreach (string id in new[] { "kbpk", "material" })
        {
            var key = (KeyVariableDeclaration)Variable(id);
            var args = Args("p", "kbpk", "material").Arguments.ToArray();
            var wrongSize = new KeyVariableDeclaration { KeyValue = key.KeyValue, KeySize = "256" };
            args[id == "kbpk" ? 1 : 2] = new(wrongSize.KeyValue, wrongSize, ResolvedCallArgumentKind.Variable);
            Assert.Throws<ArgumentException>(() => new Ep2Tr31KeyBlock().Wrap(new AlgorithmCallArguments(args)));
            key.KeyValue = "0x(GG)";
            Assert.Throws<ArgumentException>(() => new Ep2Tr31KeyBlock().Wrap(Args("p", "kbpk", "material")));
            key.KeyValue = "0x(" + Kbpk + ")";
        }
    }

    [Test]
    public void ParameterVariablesAndFullWireThroughRunner()
    {
        Run(Prefix());
        Assert.That(Run($"VAR header=\"{Header}\" VAR filler=0x({Filler}) " +
            $"PARAM variableParams=Parameters({Mechanism},#BLKH:header,#RND:filler) " +
            "VAR block=Wrap(variableParams,kbpk,material) KEY restored=Unwrap(u,kbpk,wire)").Diagnostics, Is.Empty);
        Assert.That(ToWire((StringVariableDeclaration)Variable("block")), Is.EqualTo(Wire));
        Assert.That(Variable("restored").Value, Is.EqualTo("0x(" + Material + ")").IgnoreCase);
    }

    [Test]
    public void RunnerRejectsNestedSerializedAndWrongPositionArguments()
    {
        foreach (string call in new[] {
            "Wrap(p,material,p)", "Wrap(kbpk,p,material)", "Wrap(p,kbpk,wire)",
            "Wrap(p,GenerateKey(AES-ECB,128),material)", "Wrap(p,kbpk,GenerateKey(HMAC-SHA256,128))",
            $"Wrap(Parameters({Mechanism},#BLKH:\"{Header}\"),kbpk,material)",
            "Wrap(serialized,kbpk,material)", "Unwrap(u,kbpk,Wrap(p,kbpk,material))",
            "Unwrap(u,kbpk,material)", "Unwrap(u,wire,wire)", "Unwrap(serialized,kbpk,wire)",
            "Encrypt(u,kbpk,wire)", "Decrypt(u,kbpk,wire)", "Mac(u,kbpk,wire)", "Derive(u,kbpk,wire)" })
        {
            Reset(); Run(Prefix() + $"VAR serialized=\"#MECH:{Mechanism}\"");
            Assert.Throws<SemanticErrorException>(() => Run(call + " VAR after=0x(AA)"), call);
            Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        }
    }

    [Test]
    public void DocumentationExamplesAndSessionKeyEncryptionIntegration()
    {
        Assert.That(new FileInfoDocumentationProvider().TryGetDocumentation(Mechanism, out string markdown), Is.True);
        var examples = Regex.Matches(markdown, @"```cryptoscript\s*([\s\S]*?)```");
        Assert.That(examples.Count, Is.EqualTo(2));
        for (int i = 0; i < examples.Count; i++)
        {
            Reset(); Assert.That(Run(examples[i].Groups[1].Value).Diagnostics, Is.Empty);
            Assert.That(Variable("restored").Value, Is.EqualTo("0x(" + Material + ")").IgnoreCase);
            if (i == 0) Assert.That(ToWire((StringVariableDeclaration)Variable("block")), Is.EqualTo(Wire));
            else
            {
                var kbpk = (KeyVariableDeclaration)Variable("kbpk");
                Assert.That(kbpk.KeyType.Algorithm, Is.EqualTo(KeyAlgorithm.Unknown));
                Assert.That(kbpk.KeySize, Is.EqualTo("128"));
            }
        }
    }
}
