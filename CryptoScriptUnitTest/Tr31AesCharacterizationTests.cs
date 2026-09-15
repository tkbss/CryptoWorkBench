using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

// Compatibility fixtures, NOT assertions of ANSI 2022 padding conformance.
[NonParallelizable]
public class Tr31AesCharacterizationTests
{
    private VariableDeclaration[] previous = [];

    [SetUp]
    public void SaveVariables()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }

    [TearDown]
    public void RestoreVariables()
    {
        VariableDictionary.Instance().Clear();
        foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
    }

    // Expected values independently calculated with Python cryptography AES-CMAC/CBC.
    private static readonly string[][] Cases =
    [
        ["128", "6479FE6B7C473786456D2A92EA5BBA7E",
            "1BE9D5B5B8A31E4947BA064037DA705B8E9F0119AB5D8B340CB6548D735BA116",
            "1521D79988623B335540BC8FC5B87961"],
        ["192", "9D375F80EF4CF9CE25DFDB111C92F14BC5322C5B23289EE1",
            "83BF0287838BB9B8AA5863A1AAB1D7AA7D40E06FC5F61CDB649B3B7984E794C4",
            "97064CA6E2636A2EBB8915A77AFA52F1"],
        ["256", "90408CF5B9CB450EC1923DCA3B470B08013CE1FC188C5727BE7637C74EBA9D4E",
            "3A69EE827366138597DB815274D751E2EECD620D5C585A8E6F93ECC31A57CB61DC853CDBE83ED414B4694A6A1A4FAE9F",
            "9B7D870BA7DD45EFF8D749469C747F53"]
    ];

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void DeterministicWrapPreservesEveryOutputStageAndBothUnwrapForms(int index)
    {
        var fixture = Cases[index];
        int bits = int.Parse(fixture[0]);
        string key = Sequence(32, bits / 8);
        string padding = Sequence(1, bits == 192 ? 6 : 14);
        string header = bits == 256 ? "D0144D0AB00E0000" : "D0112D0AB00E0000";
        var block = Wrap(bits, header, padding);
        Assert.Multiple(() =>
        {
            Assert.That(block.Block, Is.EqualTo(header));
            Assert.That(Convert.ToHexString(block.Cryptogram), Is.EqualTo(fixture[2]));
            Assert.That(Convert.ToHexString(block.Mac), Is.EqualTo(fixture[3]));
            Assert.That(block.ToString(), Is.EqualTo($"\"{header}\"0x({fixture[2]})0x({fixture[3]})").IgnoreCase);
            Assert.That(Convert.ToHexString(Decrypt(block, fixture[1])),
                Is.EqualTo(bits.ToString("X4") + key + padding));
        });
        CheckUnwrap(block.ToString(), key, bits);
        CheckUnwrap($"\"{header}{fixture[2]}{fixture[3]}\"", key, bits);
    }

    [TestCase(128, null)]
    [TestCase(128, "AA")]
    [TestCase(192, null)]
    [TestCase(192, "AA")]
    [TestCase(256, null)]
    [TestCase(256, "AA")]
    public void MissingOrWrongSizedRandomFallsBackToFiller(int bits, string? random)
    {
        int index = bits == 128 ? 0 : bits == 192 ? 1 : 2;
        string header = bits == 256 ? "D0144D0AB00E0000" : "D0112D0AB00E0000";
        var block = Wrap(bits, header, random);
        byte[] clear = Decrypt(block, Cases[index][1]);
        // Do not assert random byte values, uniqueness or a statistical property.
        Assert.That(clear, Has.Length.EqualTo(bits == 256 ? 48 : 32));
        Assert.That(Convert.ToHexString(clear[..(2 + bits / 8)]),
            Is.EqualTo(bits.ToString("X4") + Sequence(32, bits / 8)));
        CheckUnwrap(block.ToString(), Sequence(32, bits / 8), bits);
    }

    [Test]
    public void IncorrectDeclaredLengthIsPreservedAndCompositeUnwrapStillAcceptsIt()
    {
        var block = Wrap(128, "D9999D0AB00E0000", Sequence(1, 14));
        Assert.That(block.Block, Is.EqualTo("D9999D0AB00E0000"));
        Assert.That(block.Cryptogram, Has.Length.EqualTo(32));
        CheckUnwrap(block.ToString(), Sequence(32, 16), 128);
        // Full-wire parsing uses the declared length and currently fails on truncation.
        Assert.Throws<CryptoScript.ErrorListner.SemanticErrorException>(() =>
            Run($"VAR ci=\"{block.Block}{Convert.ToHexString(block.Cryptogram)}{Convert.ToHexString(block.Mac)}\" PARAM cu=#MECH:WRAP-AES-TR31 KEY cr=Unwrap(cu,ck,ci)"));
    }

    [Test]
    public void FullWireUnwrapIgnoresCharactersBeyondDeclaredLength()
    {
        var block = Wrap(128, "D0112D0AB00E0000", Sequence(1, 14));
        CheckUnwrap($"\"{block.Block}{Convert.ToHexString(block.Cryptogram)}{Convert.ToHexString(block.Mac)}DEADBEEF\"", Sequence(32, 16), 128);
    }

    private static TR31String Wrap(int bits, string header, string? random)
    {
        Run($"KEY ck=GenerateKey(AES-CBC,0x({Sequence(0, bits / 8)})) " +
            $"KEY cv=GenerateKey(AES-CBC,0x({Sequence(32, bits / 8)})) " +
            $"PARAM cp=#MECH:WRAP-AES-TR31 #BLKH:\"{header}\" " +
            (random == null ? "" : $"#RND:0x({random}) ") + "VAR cb=Wrap(cp,ck,cv)");
        return TR31String.FromString(VariableDictionary.Instance().Get("cb").Value);
    }

    private static void CheckUnwrap(string input, string key, int bits)
    {
        Run($"VAR ci={input} PARAM cu=#MECH:WRAP-AES-TR31 KEY cr=Unwrap(cu,ck,ci)");
        var result = (KeyVariableDeclaration)VariableDictionary.Instance().Get("cr");
        Assert.Multiple(() =>
        {
            Assert.That(result.KeyValue, Is.EqualTo($"0x({key})").IgnoreCase);
            Assert.That(result.Value, Is.EqualTo(result.KeyValue));
            Assert.That(result.KeySize, Is.EqualTo(bits.ToString()));
            Assert.That(result.Mechanism, Is.EqualTo("WRAP-AES-TR31"));
        });
    }

    private static byte[] Decrypt(TR31String block, string encryptionKey)
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromHexString(encryptionKey);
        return aes.DecryptCbc(block.Cryptogram, block.Mac, PaddingMode.None);
    }

    private static string Sequence(int first, int count) =>
        Convert.ToHexString(Enumerable.Range(first, count).Select(i => (byte)i).ToArray());

    private static void Run(string script) =>
        new CryptoScriptRunner().Execute(ParserBuilder.StringBuild(script).program());
}
