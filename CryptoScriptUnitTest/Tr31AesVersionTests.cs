using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

// Regression tests converted from the former characterization of non-D acceptance.
[NonParallelizable]
public class Tr31AesVersionTests
{
    private const string Kbpk = "000102030405060708090A0B0C0D0E0F";
    private const string Key = "202122232425262728292A2B2C2D2E2F";
    private VariableDeclaration[] previous = [];

    // Independent Python cryptography AES-CMAC/CBC results, recomputed for EACH header.
    // A/B/C entries are deliberately mislabeled AES-D constructions, not ANSI vectors.
    private static readonly string[][] Outputs =
    [
        ["A", "EEBAE3A348F0A32E1A3934215A1FD001F5CCAFF010CBE5744EF82EB5523CA592", "6FA4B869D3777D15B2A6712AF074A2B9"],
        ["B", "D920F1B610F5754A77F41D0CD5FBA082E8D5DE85CB8A347DAD2F027C9AF72F29", "16755BB577B865A63BA7EE401A0E00BF"],
        ["C", "981AD5F6DF8E8D3EA57A01DC2B6B4C530A50A1B3B62764671DA101F80CC537D7", "D2A1B7E3772CE52C4E80709F13726F21"],
        ["D", "03F80C3517F56FE9CD297E05471A6D79045722B9DC77C1BD7BD81F9EBCB9E63CFCDCAC1078B339C4D9FA5895E71DF73D", "496AC24F72AC279763B7D59F4EF67C91"]
    ];

    [SetUp]
    public void Save()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
        Run($"KEY k=GenerateKey(AES-CBC,0x({Kbpk})) KEY t=GenerateKey(AES-CBC,0x({Key}))");
    }

    [TearDown]
    public void Restore()
    {
        VariableDictionary.Instance().Clear();
        foreach (var v in previous) VariableDictionary.Instance().Add(v);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void WrapRequiresDAndPreservesDOutput(int index)
    {
        var expected = Outputs[index];
        string header = expected[0] + (index == 3 ? "0144" : "0112") + "D0AB00E0000";
        string random = index == 3
            ? "0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E"
            : "0102030405060708090A0B0C0D0E";
        Run($"PARAM p=#MECH:WRAP-AES-TR31 #BLKH:\"{header}\" " +
            $"#RND:0x({random})");
        if (index != 3)
        {
            AssertScriptVersionFailure("VAR b=Wrap(p,k,t)");
            return;
        }
        Run("VAR b=Wrap(p,k,t)");
        var block = TR31String.FromString(VariableDictionary.Instance().Get("b").Value);
        Assert.Multiple(() =>
        {
            Assert.That(block.Block, Is.EqualTo(header));
            Assert.That(Convert.ToHexString(block.Cryptogram), Is.EqualTo(expected[1]));
            Assert.That(Convert.ToHexString(block.Mac), Is.EqualTo(expected[2]));
            Assert.That(block.Mac, Has.Length.EqualTo(16));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void CompositeUnwrapRequiresDEvenWithCorrectAesAuthentication(int index)
    {
        var expected = Outputs[index];
        // Explicit ciphertext/MAC boundaries bypass wire-parser splitting (A/C=4, B=8, D=16).
        // The 16-byte MAC authenticates the actual A/B/C/D header; no post-MAC relabeling.
        string length = index == 3 ? "0144" : "0112";
        Run($"PARAM p=#MECH:WRAP-AES-TR31 VAR b=\"{expected[0]}{length}D0AB00E0000\"0x({expected[1]})0x({expected[2]})");
        if (index != 3)
        {
            AssertScriptVersionFailure("KEY r=Unwrap(p,k,b)");
            return;
        }
        Run("KEY r=Unwrap(p,k,b)");
        Assert.That(((KeyVariableDeclaration)VariableDictionary.Instance().Get("r")).KeyValue,
            Is.EqualTo($"0x({Key})").IgnoreCase);
    }

    [Test]
    public void DWireUnwrapStillSucceeds()
    {
        var expected = Outputs[3];
        Run($"PARAM p=#MECH:WRAP-AES-TR31 VAR b=\"D0144D0AB00E0000{expected[1]}{expected[2]}\" KEY r=Unwrap(p,k,b)");
        Assert.That(((KeyVariableDeclaration)VariableDictionary.Instance().Get("r")).KeyValue,
            Is.EqualTo($"0x({Key})").IgnoreCase);
    }

    [TestCase(0, 8)]
    [TestCase(2, 4)]
    [TestCase(3, 4)]
    public void WireUnwrapRejectsVersionBeforeAesIvProcessing(int referenceIndex, int macBytes)
    {
        // Existing B/C/A reference blocks: correct structure for their own versions.
        // The adapter must reject the version, rather than reaching the former CBC IV failure.
        var reference = Tr31ReferenceVectors.All[referenceIndex];
        var parsed = TR31Block.FromString(reference.CompleteBlock);
        Assert.That(parsed.Mac, Has.Length.EqualTo(macBytes));
        Assert.That(parsed.Cryptogram, Is.EqualTo(Convert.FromHexString(reference.Ciphertext)));
        Run($"VAR b=\"{reference.CompleteBlock}\"");
        var error = Assert.Throws<NotSupportedException>(() => new WrapAESTR31().Unwrap(
            ["#MECH:WRAP-AES-TR31", VariableDictionary.Instance().Get("k").Value,
                VariableDictionary.Instance().Get("b").Value]));
        Assert.That(error!.Message, Is.EqualTo("WRAP-AES-TR31 supports only version D."));
    }

    [TestCase('A')]
    [TestCase('B')]
    [TestCase('C')]
    public void WrapRejectsVersionBeforeKeyLookupOrCryptography(char version)
    {
        var error = Assert.Throws<NotSupportedException>(() => new WrapAESTR31().Wrap(
            [$"#MECH:WRAP-AES-TR31#BLKH:\"{version}0112D0AB00E0000\"", "missing", "missing"]));
        Assert.That(error!.Message, Is.EqualTo("WRAP-AES-TR31 supports only version D."));
    }

    private static void AssertScriptVersionFailure(string script)
    {
        var error = Assert.Throws<CryptoScript.ErrorListner.SemanticErrorException>(() => Run(script));
        Assert.That(error!.SemanticError.Message, Is.EqualTo("WRAP-AES-TR31 supports only version D."));
    }

    private static void Run(string script) => new CryptoScriptRunner().Execute(ParserBuilder.StringBuild(script).program());
}
