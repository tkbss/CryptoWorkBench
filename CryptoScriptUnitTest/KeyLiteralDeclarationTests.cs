using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KeyLiteralDeclarationTests
{
    private VariableDeclaration[] previous = null!;
    private string directory = null!;
    private static VariableDictionary Variables => VariableDictionary.Instance();
    private readonly VariableSnapshotJsonSerializer serializer = new();

    [SetUp]
    public void SetUp()
    {
        previous = Variables.GetVariables().ToArray();
        Variables.Clear();
        directory = Path.Combine(Path.GetTempPath(), "CryptoScript-K01-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown()
    {
        Variables.Clear();
        foreach (var variable in previous) Variables.Add(variable);
        Directory.Delete(directory, recursive: true);
    }

    private static CryptoScriptParser.ProgramContext Parse(string script)
    {
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.SyntaxErrorOccured = false;
        SyntaxErrorListner.ErrorMessage.Clear();
        var parser = ParserBuilder.StringBuild(script);
        var program = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        return program;
    }

    private static CryptoScriptProgram Run(string script)
    {
        try { return new CryptoScriptRunner().Execute(Parse(script)); }
        catch (SemanticErrorException e)
        {
            Assert.Fail(e.SemanticError?.Message ?? "Missing diagnostic");
            throw;
        }
    }

    private static KeyVariableDeclaration Key(string name = "key")
    {
        Assert.That(Variables.Get(name), Is.TypeOf<KeyVariableDeclaration>());
        return (KeyVariableDeclaration)Variables.Get(name);
    }

    private static void AssertUnknown(KeyVariableDeclaration key, string literal, int bytes)
    {
        Assert.Multiple(() =>
        {
            Assert.That(key.Type, Is.TypeOf<CryptoTypeKey>());
            Assert.That(key.Value, Is.EqualTo(literal));
            Assert.That(key.ValueFormat, Is.EqualTo(literal.StartsWith("0x(") ? FormatConversions.HEX : FormatConversions.B64));
            Assert.That(key.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Unknown)));
            Assert.That(key.KeySizeInBits.Bits, Is.EqualTo((ulong)bytes * 8));
            Assert.That(key.KeySize, Is.EqualTo((bytes * 8).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(key.Usage, Is.EqualTo(KeyUsagePolicy.Unspecified));
            Assert.That(key.DerivationMechanism, Is.Empty);
            Assert.That(key.KeyAttributes, Is.Empty);
            Assert.That(key.Kcv, Is.Null);
            Assert.That(key.Kcvc, Is.Null);
            Assert.That(key.Kcvk, Is.Null);
        });
    }

    [TestCase(1)] [TestCase(8)] [TestCase(16)] [TestCase(24)] [TestCase(32)] [TestCase(64)]
    public void HexDeclarationsUseTheCompleteLiteralWithoutInferringAnAlgorithm(int bytes)
    {
        // Include both leading zeros and mixed case; preserve spelling, not just bytes.
        string literal = "0x(" + string.Concat(Enumerable.Repeat("00aB", bytes / 2)) +
            (bytes % 2 == 1 ? "00" : "") + ")";
        var program = Run($"KEY key = {literal}");
        AssertUnknown(Key(), literal, bytes);
        Assert.That(program.Statements.Single(), Is.SameAs(Key()));
        Assert.That(Key().Text, Is.EqualTo("KEYkey=" + literal));
        Assert.That(program.Diagnostics, Is.Empty);
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(16)] [TestCase(24)] [TestCase(32)]
    public void Base64DeclarationsMeasureDecodedBytesIncludingPadding(int bytes)
    {
        byte[] material = Enumerable.Range(0, bytes).Select(i => (byte)(i * 7)).ToArray();
        string literal = "b64(" + Convert.ToBase64String(material) + ")";
        Run($"KEY key = {literal}");
        AssertUnknown(Key(), literal, bytes);
        Assert.That(FormatConversions.ToByteArray(Key().Value, Key().ValueFormat), Is.EqualTo(material));
    }

    [TestCase("0x(0)")] [TestCase("0x(0011223)")]
    [TestCase("b64(A)")] [TestCase("b64(AAAA=)")] [TestCase("b64(====)")]
    [TestCase("123")] [TestCase("\"password-sensitive\"")] [TestCase("\"\"")]
    [TestCase("\"0x(AABBCCDD)\"")] [TestCase("/k01-no-file-access")]
    [TestCase("\"A\"0x(AB)0x(CD)")] [TestCase("")]
    public void InvalidDeclarationsAreSanitizedAndNeverReplaceOrPublish(string literal)
    {
        Run("KEY key = 0x(00112233445566778899AABBCCDDEEFF) VAR untouched = \"original\"");
        var originals = Variables.GetVariables().ToArray();
        string before = new VariableSnapshotJsonSerializer().Serialize(originals);
        foreach (string name in new[] { "key", "newkey" })
        {
            var runner = new CryptoScriptRunner();
            var error = Assert.Throws<SemanticErrorException>(() =>
                runner.Execute(Parse($"KEY {name} = {literal}")))!;
            Assert.That(error.SemanticError!.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.InvalidDeclaration));
            Assert.That(error.SemanticError.Message,
                Is.EqualTo("KEY requires a valid nonempty HEX or Base64 literal containing whole bytes."));
            Assert.That(error.SemanticError.Value, Is.Empty);
            Assert.That(error.InnerException, Is.Null);
            Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(Variables.GetVariables().ToArray(), Is.EqualTo(originals));
            Assert.That(new VariableSnapshotJsonSerializer().Serialize(originals), Is.EqualTo(before));
        }
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [TestCase("0x(AbCd)")] [TestCase("b64(AAEC)")] [TestCase("\"unchanged\"")] [TestCase("123")]
    public void VarLiteralBehaviorIsUnchanged(string literal)
    {
        Run($"VAR data = {literal}");
        var data = Variables.Get("data");
        Assert.That(data, Is.TypeOf<StringVariableDeclaration>());
        Assert.That(data.Type, Is.TypeOf<CryptoTypeVar>());
        Assert.That(data.Value, Is.EqualTo(literal));
        Assert.That(data.ValueFormat, Is.EqualTo(FormatConversions.ParseString(literal)));
    }

    [TestCase(16, false)] [TestCase(24, false)] [TestCase(32, false)]
    [TestCase(16, true)] [TestCase(24, true)] [TestCase(32, true)]
    public void AesCbcRoundTripUsesUnknownKeyWithoutMetadataMutation(int bytes, bool base64)
    {
        byte[] material = Enumerable.Range(0, bytes).Select(i => (byte)i).ToArray();
        string literal = base64 ? "b64(" + Convert.ToBase64String(material) + ")" :
            "0x(" + Convert.ToHexString(material) + ")";
        Run($"KEY key = {literal} PARAM p = Parameters(AES-CBC) VAR data = 0x(000102030405060708090A0B0C0D0E0F)");
        var key = Key();
        string before = serializer.Serialize([key]);
        var program = Run("VAR encrypted = Encrypt(p, key, data) VAR decrypted = Decrypt(p, key, encrypted)");
        Assert.That(Variables.Get("decrypted").Value, Is.EqualTo(Variables.Get("data").Value).IgnoreCase);
        Assert.That(Key(), Is.SameAs(key));
        AssertUnknown(key, literal, bytes);
        Assert.That(serializer.Serialize([key]), Is.EqualTo(before));
        Assert.That(program.Diagnostics, Is.Empty);
    }

    [Test]
    public void AesEcbMatchesFips197KnownAnswer()
    {
        // FIPS 197 AES-128 example, also used in KeyMetadataDiagnosticTransportTests.
        const string literal = "0x(000102030405060708090A0B0C0D0E0F)";
        Run($"KEY key = {literal} PARAM p = Parameters(AES-ECB, #PAD:NONE)");
        string before = serializer.Serialize([Key()]);
        var result = Run("""
            VAR cipher = Encrypt(p, key, 0x(00112233445566778899AABBCCDDEEFF))
            VAR plain = Decrypt(p, key, 0x(69C4E0D86A7B0430D8CDB78070B4C55A))
            """);
        Assert.That(Variables.Get("cipher").Value, Is.EqualTo("0x(69C4E0D86A7B0430D8CDB78070B4C55A)").IgnoreCase);
        Assert.That(Variables.Get("plain").Value, Is.EqualTo("0x(00112233445566778899AABBCCDDEEFF)").IgnoreCase);
        AssertUnknown(Key(), literal, 16);
        Assert.That(serializer.Serialize([Key()]), Is.EqualTo(before));
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [TestCase(1)] [TestCase(8)] [TestCase(15)] [TestCase(17)] [TestCase(31)] [TestCase(33)]
    public void AesStillRejectsTechnicallyInvalidLengths(int bytes)
    {
        string literal = "0x(" + new string('A', bytes * 2) + ")";
        Run($"KEY key = {literal} PARAM p = Parameters(AES-CBC) VAR data = 0x(000102030405060708090A0B0C0D0E0F)");
        var before = Variables.GetVariables().ToArray();
        string snapshot = serializer.Serialize(before);
        foreach (string function in new[] { "Encrypt", "Decrypt" })
        {
            var runner = new CryptoScriptRunner();
            var error = Assert.Throws<SemanticErrorException>(() =>
                runner.Execute(Parse($"VAR result = {function}(p, key, data)")))!;
            Assert.That(error.SemanticError!.Message, Does.Not.Contain(literal));
            Assert.That(Variables.Contains("result"), Is.False);
            Assert.That(Variables.GetVariables().ToArray(), Is.EqualTo(before));
            Assert.That(serializer.Serialize(before), Is.EqualTo(snapshot));
            AssertUnknown(Key(), literal, bytes);
        }
    }

    [TestCase("0x(00112233445566778899AABBCCDDEEFF)")]
    [TestCase("b64(ABEiM0RVZneImaq7zN3u/w==)")]
    public void SaveAndS01S02RoundTripRetainsUnknownKeyAndAesUsability(string literal)
    {
        string destination = Path.Combine(directory, "key.json");
        Run($"KEY key = {literal} PATH path = \"{destination.Replace("\\", "\\\\")}\" PARAM p = Parameters(AES-ECB, #PAD:NONE)");
        var original = Key();
        string before = serializer.Serialize([original]);
        Run("Save(path, key)");
        var json = JObject.Parse(File.ReadAllText(destination));
        Assert.That((string?)json["variables"]?[0]?["metadata"]?["type"], Is.EqualTo("KEY"));
        var restored = new VariableSnapshotFileStore().Read((PathVariableDeclaration)Variables.Get("path")).Single();
        Assert.That(restored, Is.TypeOf<KeyVariableDeclaration>());
        AssertUnknown((KeyVariableDeclaration)restored, literal, 16);
        Assert.That(serializer.Serialize([restored]), Is.EqualTo(before));
        Assert.That(Key(), Is.SameAs(original));
        Run("VAR originalcipher = Encrypt(p, key, 0x(000102030405060708090A0B0C0D0E0F))");
        // S02 intentionally does not publish. The host explicitly registers the detached result.
        Variables.Add(restored);
        Run("VAR cipher = Encrypt(p, key, 0x(000102030405060708090A0B0C0D0E0F)) VAR plain = Decrypt(p, key, cipher)");
        Assert.That(Variables.Get("cipher").Value, Is.EqualTo(Variables.Get("originalcipher").Value));
        Assert.That(Variables.Get("plain").Value, Is.EqualTo("0x(000102030405060708090A0B0C0D0E0F)").IgnoreCase);
        Assert.That(serializer.Serialize([restored]), Is.EqualTo(before));
        Assert.That(serializer.Serialize([original]), Is.EqualTo(before));
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { destination }));
    }

    [Test]
    public void GenerateKeyKeepsExplicitAlgorithmAndCheckValues()
    {
        Run("KEY imported = GenerateKey(AES-ECB, 0x(000102030405060708090A0B0C0D0E0F)) KEY randomkey = GenerateKey(AES-CBC, 192)");
        foreach (var name in new[] { "imported", "randomkey" })
        {
            var key = Key(name);
            Assert.That(key.KeyType.Algorithm, Is.EqualTo(KeyAlgorithm.Aes));
            Assert.That(key.KeySizeInBits.Bits, Is.EqualTo(name == "imported" ? 128UL : 192UL));
            Assert.That(key.Kcv, Is.Not.Null);
            Assert.That(key.Kcvc, Is.Not.Null);
            Assert.That(key.Kcvk, Is.Not.Null);
        }
    }

    [Test]
    public void HmacExistingUnknownKeyContractRemainsUsable()
    {
        // RFC 4231 test case 1, existing HMAC-SHA256 mechanism.
        string literal = "0x(" + string.Concat(Enumerable.Repeat("0b", 20)) + ")";
        Run($"KEY key = {literal} PARAM p = Parameters(HMAC-SHA256)");
        string before = serializer.Serialize([Key()]);
        Run("VAR mac = Mac(p, key, 0x(4869205468657265))");
        Assert.That(Variables.Get("mac").Value,
            Is.EqualTo("0x(b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7)").IgnoreCase);
        Assert.That(serializer.Serialize([Key()]), Is.EqualTo(before));
        AssertUnknown(Key(), literal, 20);
    }
}
