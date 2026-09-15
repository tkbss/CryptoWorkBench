using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

// Legacy characterization: accepting A/B/C here is a defect, not supported TR-31 behavior.
[NonParallelizable]
public class Tr31AesVersionCharacterizationTests
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
        ["D", "1BE9D5B5B8A31E4947BA064037DA705B8E9F0119AB5D8B340CB6548D735BA116", "1521D79988623B335540BC8FC5B87961"]
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
    public void LegacyWrapUsesAesDRegardlessOfHeaderVersion(int index)
    {
        var expected = Outputs[index];
        string header = expected[0] + "0112D0AB00E0000";
        Run($"PARAM p=#MECH:WRAP-AES-TR31 #BLKH:\"{header}\" #RND:0x(0102030405060708090A0B0C0D0E) VAR b=Wrap(p,k,t)");
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
    public void LegacyCompositeUnwrapAcceptsAuthenticatedAesDWithAnyHeaderVersion(int index)
    {
        var expected = Outputs[index];
        // Explicit ciphertext/MAC boundaries bypass wire-parser splitting (A/C=4, B=8, D=16).
        // The 16-byte MAC authenticates the actual A/B/C/D header; no post-MAC relabeling.
        Run($"PARAM p=#MECH:WRAP-AES-TR31 VAR b=\"{expected[0]}0112D0AB00E0000\"0x({expected[1]})0x({expected[2]}) KEY r=Unwrap(p,k,b)");
        Assert.That(((KeyVariableDeclaration)VariableDictionary.Instance().Get("r")).KeyValue,
            Is.EqualTo($"0x({Key})").IgnoreCase);
    }

    [Test]
    public void DWireUnwrapStillSucceeds()
    {
        var expected = Outputs[3];
        Run($"PARAM p=#MECH:WRAP-AES-TR31 VAR b=\"D0112D0AB00E0000{expected[1]}{expected[2]}\" KEY r=Unwrap(p,k,b)");
        Assert.That(((KeyVariableDeclaration)VariableDictionary.Instance().Get("r")).KeyValue,
            Is.EqualTo($"0x({Key})").IgnoreCase);
    }

    [TestCase(0, 8)]
    [TestCase(2, 4)]
    [TestCase(3, 4)]
    public void LegacyWireUnwrapReachesAesIvFailureRatherThanVersionValidation(int referenceIndex, int macBytes)
    {
        // Existing B/C/A reference blocks: correct structure for their own versions.
        // This test diagnoses the distinct wire failure; it is NOT a versions-contract test.
        var reference = Tr31ReferenceVectors.All[referenceIndex];
        var parsed = TR31Block.FromString(reference.CompleteBlock);
        Assert.That(parsed.Mac, Has.Length.EqualTo(macBytes));
        Assert.That(parsed.Cryptogram, Is.EqualTo(Convert.FromHexString(reference.Ciphertext)));
        Run($"VAR b=\"{reference.CompleteBlock}\"");
        var error = Assert.Throws<ArgumentException>(() => new WrapAESTR31().Unwrap(
            ["#MECH:WRAP-AES-TR31", VariableDictionary.Instance().Get("k").Value,
                VariableDictionary.Instance().Get("b").Value]));
        Assert.That(error!.Message, Does.Contain("initialisation vector must be the same length as block size"));
    }

    private static void Run(string script) => new CryptoScriptRunner().Execute(ParserBuilder.StringBuild(script).program());
}
