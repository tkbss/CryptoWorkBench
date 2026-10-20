using System.Text;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Tr31ReviewRegressionTests
{
    private const string Kbpk = "000102030405060708090A0B0C0D0E0F";
    private VariableDeclaration[] previous = [];

    [SetUp] public void Save()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }

    [TearDown] public void Restore()
    {
        VariableDictionary.Instance().Clear();
        foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
    }

    private static void Run(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var tree = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero, script);
        new CryptoScriptRunner().Execute(tree);
    }

    // Construct deliberately malformed but correctly authenticated payloads.
    // Do not use the writer's key-length validation to create negative reader fixtures.
    private static TR31String Authenticated(int bits, char algorithm = 'A', int declaredLength = 144)
    {
        byte[] clear = new byte[48];
        clear[0] = (byte)(bits >> 8);
        clear[1] = (byte)bits;
        string header = $"D{declaredLength:D4}D0{algorithm}B00E0000";
        byte[] kbpk = Convert.FromHexString(Kbpk);
        byte[] macKey = WrapAESTR31.DeriveKey(kbpk, WrapAESTR31.MACDerivationData(128), 128);
        byte[] encKey = WrapAESTR31.DeriveKey(kbpk, WrapAESTR31.EncryptionDerivationData(128), 128);
        byte[] mac = WrapAESTR31.ComputeMAC([.. Encoding.ASCII.GetBytes(header), .. clear], macKey);
        return new TR31String(header, WrapAESTR31.EncryptNoPadding(encKey, mac, clear), mac);
    }

    private static string Wire(TR31String b) => b.Block + Convert.ToHexString(b.Cryptogram) + Convert.ToHexString(b.Mac);
    private static string Quote(string s) => Newtonsoft.Json.JsonConvert.SerializeObject(s);
    private static void SetupAes(char algorithm, bool typed)
    {
        Run($"KEY kbpk=GenerateKey(AES-CBC,0x({Kbpk})) " +
            $"TR31H h=CreateHeader(KBVID:D,KEYU:D0,ALGO:{algorithm},MODEU:B,EXP:E) " +
            "PARAM p=#MECH:WRAP-AES-TR31" + (typed ? " #BLKH:h" : ""));
    }

    [TestCase(257, 'A')]
    [TestCase(255, 'A')]
    [TestCase(0, 'A')]
    [TestCase(9, 'A')]
    [TestCase(384, 'A')]
    [TestCase(64, 'A')]
    [TestCase(256, 'T')]
    [TestCase(64, 'T')]
    public void H01_AuthenticatedInvalidKeyLengthsNeverPublishKey(int bits, char algorithm)
    {
        foreach (bool typed in new[] { false, true })
        {
            SetupAes(algorithm, typed);
            var block = Authenticated(bits, algorithm);
            foreach (string representation in new[] { block.ToString(), Quote(Wire(block)) })
            {
                Run("VAR input=" + representation);
                var before = VariableDictionary.Instance().GetVariables().ToDictionary(v => v.Id, v => v.Value);
                Assert.Throws<SemanticErrorException>(() => Run("KEY rejected=Unwrap(p,kbpk,input)"));
                Assert.That(VariableDictionary.Instance().Contains("rejected"), Is.False);
                foreach (var pair in before)
                    Assert.That(VariableDictionary.Instance().Get(pair.Key).Value, Is.EqualTo(pair.Value));
            }
        }
    }

    [TestCase(128, 'A')]
    [TestCase(192, 'A')]
    [TestCase(256, 'A')]
    [TestCase(128, 'T')]
    [TestCase(192, 'T')]
    public void H01_AuthenticatedSupportedKeyLengthsRemainExact(int bits, char algorithm)
    {
        SetupAes(algorithm, true);
        Run("VAR input=" + Quote(Wire(Authenticated(bits, algorithm))) + " KEY recovered=Unwrap(p,kbpk,input)");
        var key = (KeyVariableDeclaration)VariableDictionary.Instance().Get("recovered");
        Assert.That(key.KeySize, Is.EqualTo(bits.ToString()));
        Assert.That(FormatConversions.HexStringToByteArray(key.Value), Has.Length.EqualTo(bits / 8));
    }

    [TestCase('A')]
    [TestCase('B')]
    [TestCase('C')]
    public void H01_TdeaReadersRejectAuthenticatedInvalidLengths(char version)
    {
        const string kbpk = "0123456789ABCDEFFEDCBA98765432100011223344556677";
        Run($"KEY kbpk=GenerateKey(DES3-CBC,0x({kbpk})) " +
            $"TR31H h=CreateHeader(KBVID:{version},KEYU:D0,ALGO:T,MODEU:B,EXP:E) " +
            "PARAM p=#MECH:WRAP-DES3-TR31 #BLKH:h");
        foreach (int bits in new[] { 0, 9, 64, 256, 264 })
        {
            byte[] clear = new byte[32];
            clear[0] = (byte)(bits >> 8);
            clear[1] = (byte)bits;
            string header = $"{version}{(version == 'B' ? 96 : 88):D4}D0TB00E0000";
            var block = version == 'B'
                ? Tr31VersionBWrap.Wrap(header, Convert.FromHexString(kbpk), clear, analysisKeys: true)
                : Tr31TdeaVariantWrap.Wrap(header, Convert.FromHexString(kbpk), clear, analysisKeys: true);
            Run("VAR input=" + new TR31String(header, block.Ciphertext, block.Mac));
            Assert.Throws<SemanticErrorException>(() => Run("KEY rejected=Unwrap(p,kbpk,input)"));
            Assert.That(VariableDictionary.Instance().Contains("rejected"), Is.False);
        }
    }

    [TestCase("Z")]
    [TestCase("0")]
    public void H02_AuthenticatedD0145RejectsDiscardedLastCharacter(string tail)
    {
        foreach (bool typed in new[] { false, true })
        {
            SetupAes('A', typed);
            Run("VAR input=" + Quote(Wire(Authenticated(128, declaredLength: 145)) + tail));
            Assert.Throws<SemanticErrorException>(() => Run("KEY rejected=Unwrap(p,kbpk,input)"));
            Assert.That(VariableDictionary.Instance().Contains("rejected"), Is.False);
        }
    }

    [TestCase('A')]
    [TestCase('B')]
    [TestCase('C')]
    [TestCase('D')]
    public void H02_AllVersionsRejectIncompleteOrExtraWireData(char version)
    {
        string wire = version == 'D' ? Tr31VersionDTransportVectors.All[0].Wire :
            Tr31ReferenceVectors.All.First(v => v.Header[0] == version).CompleteBlock;
        Assert.DoesNotThrow(() => TR31Block.FromString(wire));
        int cipherStart = TR31Block.FromString(wire).HeaderDataToMac!.Length;
        string[] invalid =
        [
            wire + "Z", wire + "0", wire + "00", wire[..^1], wire[..^2],
            wire[..cipherStart] + "Z" + wire[(cipherStart + 1)..],
            wire[0] + (wire.Length - 2).ToString("D4") + wire[5..],
            wire[0] + (wire.Length + 2).ToString("D4") + wire[5..],
            wire[..16] + wire[18..]
        ];
        foreach (string candidate in invalid)
            Assert.Throws<ArgumentException>(() => TR31Block.FromString(candidate));
    }

    [TestCase('A')]
    [TestCase('B')]
    [TestCase('C')]
    [TestCase('D')]
    public void H02_CompositeCannotMoveOrTruncateCiphertextMacBoundary(char version)
    {
        string wire = version == 'D' ? Tr31VersionDTransportVectors.All[0].Wire :
            Tr31ReferenceVectors.All.First(v => v.Header[0] == version).CompleteBlock;
        var block = TR31Block.FromString(wire);
        string header = Encoding.ASCII.GetString(block.HeaderDataToMac!);
        string cipher = Convert.ToHexString(block.Cryptogram!);
        string mac = Convert.ToHexString(block.Mac!);
        Assert.DoesNotThrow(() => TR31String.FromString($"\"{header}\"0x({cipher})0x({mac})"));
        foreach (var (c, m) in new[]
        {
            (cipher[..^1], mac), (cipher, mac[..^2]),
            (cipher[..^2], cipher[^2..] + mac), (cipher + mac[..2], mac[2..])
        })
            Assert.Throws<ArgumentException>(() => TR31String.FromString($"\"{header}\"0x({c})0x({m})"));
    }

    [TestCase("B0")]
    [TestCase("B1")]
    public void H03_DaIsRejectedOutsideB3(string usage)
    {
        Assert.Throws<SemanticErrorException>(() => Run(
            $"TR31H h=CreateHeader(KBVID:D,KEYU:{usage},ALGO:A,MODEU:X,EXP:E,OPTID:DA,OPTBD:\"01P0TEN\")"));
        Assert.That(VariableDictionary.Instance().Contains("h"), Is.False);
    }

    [TestCase("\"\"")]
    [TestCase("\"01\"")]
    [TestCase("\"02P0TEN\"")]
    [TestCase("\"01P0TE\"")]
    public void H03_B3StillRequiresCompleteDaData(string data) =>
        Assert.Throws<SemanticErrorException>(() => Run(
            $"TR31H h=CreateHeader(KBVID:D,KEYU:B3,ALGO:A,MODEU:X,EXP:E,OPTID:DA,OPTBD:{data})"));

    [TestCase("plain")]
    [TestCase("\\")]
    [TestCase("\\\\ab")] // Exact review label: LB08 followed by two literal backslashes and "ab".
    [TestCase("\\n")]
    [TestCase("\\t")]
    [TestCase("quote \" and 0x(1234) ; , #")]
    public void M01_LegacyAndEscapedRepresentationsAuthenticateExactHeaderBytes(string label)
    {
        foreach (string version in new[] { "A", "B", "C", "D" })
        {
            string mechanism = version == "D" ? "AES" : "DES3";
            string explicitPadding = version == "B" && label == "\\\\ab" ? ",OPTID:PB,OPTBD:\"0000\"" : "";
            Run($"KEY kbpk=GenerateKey({mechanism}-CBC,192) KEY key=GenerateKey({mechanism}-CBC,128) " +
                $"TR31H h=CreateHeader(KBVID:{version},KEYU:D0,ALGO:{(version == "D" ? "A" : "T")},MODEU:B,EXP:E,OPTID:LB,OPTBD:{Quote(label)}{explicitPadding}) " +
                $"PARAM p=#MECH:WRAP-{mechanism}-TR31 #BLKH:h VAR wrapped=Wrap(p,kbpk,key)");
            var block = TR31String.FromString(VariableDictionary.Instance().Get("wrapped").Value);
            if (version == "B" && label == "\\\\ab")
                Assert.That(block.Block, Is.EqualTo("B0112D0TB00E0200LB08\\\\abPB080000"));
            // Inject historical representations directly: the historical wire format is not
            // necessarily a valid contemporary script string literal (e.g. a lone backslash).
            string[] representations =
            [
                FormatConversions.ToTR31String(block.Block, block.Cryptogram, block.Mac),
                "\"" + Wire(block) + "\"", block.ToString(), Quote(Wire(block))
            ];
            foreach (string text in representations)
            {
                VariableDictionary.Instance().Add(new StringVariableDeclaration { Id = "input", Value = text });
                Run("KEY recovered=Unwrap(p,kbpk,input)");
                var recovered = (KeyVariableDeclaration)VariableDictionary.Instance().Get("recovered");
                Assert.That(recovered.Value, Is.EqualTo(VariableDictionary.Instance().Get("key").Value).IgnoreCase);
                Assert.That(recovered.KeyAttributes!.Single(b => b.ID == "LB").Data, Is.EqualTo(label));
                Run($"PARAM raw=#MECH:WRAP-{mechanism}-TR31 KEY legacyRecovered=Unwrap(raw,kbpk,input)");
                Assert.That(VariableDictionary.Instance().Get("legacyRecovered").Value, Is.EqualTo(recovered.Value));
            }
            // Changing printable label bytes must fail authentication, not trigger escape repair.
            var bad = new TR31String(block.Block[..20] + (block.Block[20] == 'x' ? "y" : "x") + block.Block[21..],
                block.Cryptogram, block.Mac);
            VariableDictionary.Instance().Add(new StringVariableDeclaration { Id = "input", Value = bad.ToString() });
            Assert.Throws<SemanticErrorException>(() => Run("KEY rejected=Unwrap(p,kbpk,input)"));
            Assert.That(VariableDictionary.Instance().Contains("rejected"), Is.False);
        }
    }

    [TestCase("tr31")]
    [TestCase("tr31attributes")]
    public void M02_InfoTopicsRemainUsableVariableNames(string name)
    {
        Run($"VAR {name}=\"example\"");
        string? output = null;
        void Capture(string text) => output = text;
        OutputOperations.PrintEvent += Capture;
        try { Run($"Print({name})"); }
        finally { OutputOperations.PrintEvent -= Capture; }
        Assert.That(output, Does.Contain("example"));
        Run($"Info({name}) Info(CreateHeader) Info(types)");
        Assert.That(VariableDictionary.Instance().Get(name).Value, Is.EqualTo("\"example\""));
    }

    [TestCase("Print(CreateHeader)")]
    [TestCase("Print(GenerateKey)")]
    public void M02_BareFunctionNamesAreNotGeneralArguments(string text)
    {
        var parser = ParserBuilder.StringBuild(text);
        parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.GreaterThan(0));
    }
}
