using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

// Independent structural coverage for ANSI X9.143-2022 AES key-length obfuscation.
// The published AES-128 reference vector is covered in WrapperTests.
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
            "03F80C3517F56FE9CD297E05471A6D79045722B9DC77C1BD7BD81F9EBCB9E63CFCDCAC1078B339C4D9FA5895E71DF73D",
            "496AC24F72AC279763B7D59F4EF67C91"],
        ["192", "9D375F80EF4CF9CE25DFDB111C92F14BC5322C5B23289EE1",
            "8FFFD3C64540FC403E8A7392EE1B104C9E33DC3261F9CF221197D5945ABCCB53AB3E25377F4056BDB1AD220E921EE42A",
            "59EB85E8CB9CEA08EB9888BE69F4D6E6"],
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
        int obfuscationBytes = 32 - bits / 8;
        const int blockPaddingBytes = 14;
        string padding = Sequence(1, obfuscationBytes + blockPaddingBytes);
        const string header = "D0144D0AB00E0000";
        var block = Wrap(bits, header, padding);
        byte[] clear = Decrypt(block, fixture[1]);
        byte[] suppliedPadding = Convert.FromHexString(padding);
        Assert.Multiple(() =>
        {
            Assert.That(block.Block, Is.EqualTo(header));
            Assert.That(block.Cryptogram, Has.Length.EqualTo(48));
            Assert.That(Convert.ToHexString(block.Cryptogram), Is.EqualTo(fixture[2]));
            Assert.That(Convert.ToHexString(block.Mac), Is.EqualTo(fixture[3]));
            Assert.That(block.ToString(), Is.EqualTo($"\"{header}\"0x({fixture[2]})0x({fixture[3]})").IgnoreCase);
            Assert.That(Convert.ToHexString(clear),
                Is.EqualTo(bits.ToString("X4") + key + padding));
            Assert.That(clear[(2 + bits / 8)..34],
                Is.EqualTo(suppliedPadding[..obfuscationBytes]));
            Assert.That(clear[34..], Is.EqualTo(suppliedPadding[obfuscationBytes..]));
            Assert.That(clear[34..], Has.Length.EqualTo(blockPaddingBytes));
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
        const string header = "D0144D0AB00E0000";
        var block = Wrap(bits, header, random);
        byte[] clear = Decrypt(block, Cases[index][1]);
        // Do not assert random byte values, uniqueness or a statistical property.
        Assert.That(clear, Has.Length.EqualTo(48));
        Assert.That(Convert.ToHexString(clear[..(2 + bits / 8)]),
            Is.EqualTo(bits.ToString("X4") + Sequence(32, bits / 8)));
        CheckUnwrap(block.ToString(), Sequence(32, bits / 8), bits);
    }

    [TestCase(128, 16, 8, 30, 14)]
    [TestCase(192, 8, 0, 22, 6)]
    public void ObfuscationDependsOnWrappedKeyAlgorithm(int bits, int aesObfuscationBytes,
        int tdeaObfuscationBytes, int aesRandomBytes, int tdeaRandomBytes)
    {
        int index = bits == 128 ? 0 : 1;
        int keyBytes = bits / 8;
        byte[] aesClear = Decrypt(Wrap(bits, "AES-CBC", "D0144D0AB00E0000",
            Sequence(1, aesRandomBytes)), Cases[index][1]);
        byte[] tdeaClear = Decrypt(Wrap(bits, "DES3-CBC", "D0112D0TB00E0000",
            Sequence(1, tdeaRandomBytes)), Cases[index][1]);
        var aesKey = new KeyVariableDeclaration { Mechanism = "AES-CBC" };
        var tdeaKey = new KeyVariableDeclaration { Mechanism = "DES3-CBC" };

        Assert.Multiple(() =>
        {
            Assert.That(WrapAESTR31.GetObfuscationPaddingLength(aesKey, keyBytes),
                Is.EqualTo(aesObfuscationBytes));
            Assert.That(WrapAESTR31.GetObfuscationPaddingLength(tdeaKey, keyBytes),
                Is.EqualTo(tdeaObfuscationBytes));
            Assert.That(aesClear, Has.Length.EqualTo(48));
            Assert.That(aesClear[(2 + keyBytes)..34], Has.Length.EqualTo(aesObfuscationBytes));
            Assert.That(aesClear[34..], Has.Length.EqualTo(14));
            Assert.That(tdeaClear, Has.Length.EqualTo(32));
            Assert.That(tdeaClear[(2 + keyBytes)..26], Has.Length.EqualTo(tdeaObfuscationBytes));
            Assert.That(tdeaClear[26..], Has.Length.EqualTo(6));
        });
    }

    [Test]
    public void RewrapUsesAesAlgorithmFromAuthenticatedUnwrappedHeader()
    {
        const string kbpk = "88E1AB2A2E3DD38C1FA039A536500CC8A87AB9D62DC92C01058FA79F44657DE6";
        const string wire =
            "D0144P0AE00E00002C77FA3F4A553BED6E88AE5C172A4166E3D4ACA8E2AC71C1" +
            "58A476FAC12C13C3829DE55D3AB54C48F4C4FEF7AC75E90FC47F1B77E7B19A73" +
            "ED46E64410082557";
        const string random = "1A87BBFA2CFE78D383E5F4C6AA83473C1C2965473CE206BB855B01533782";
        Run($"KEY hk=GenerateKey(AES-CBC,0x({kbpk})) " +
            $"VAR hi=\"{wire}\" PARAM hu=#MECH:WRAP-AES-TR31 " +
            "KEY hr=Unwrap(hu,hk,hi)");
        var recovered = (KeyVariableDeclaration)VariableDictionary.Instance().Get("hr");

        Run($"PARAM hp=#MECH:WRAP-AES-TR31 #BLKH:\"D0144P0AE00E0000\" #RND:0x({random}) " +
            "VAR ho=Wrap(hp,hk,hr)");
        var rewrapped = TR31String.FromString(VariableDictionary.Instance().Get("ho").Value);

        Assert.Multiple(() =>
        {
            Assert.That(recovered.Mechanism, Is.EqualTo("WRAP-AES-TR31"));
            Assert.That(recovered.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Aes)));
            Assert.That(recovered.KeyAttributes.Any(a => a.ID == "HDR" && a.Data == "D0144P0AE00E0000"), Is.True);
            Assert.That(WrapAESTR31.GetObfuscationPaddingLength(recovered, 16), Is.EqualTo(16));
            Assert.That(rewrapped.Block, Is.EqualTo("D0144P0AE00E0000"));
            Assert.That(rewrapped.Cryptogram, Has.Length.EqualTo(48));
        });
    }

    [Test]
    public void RewrapUsesTdeaAlgorithmFromAuthenticatedUnwrappedHeader()
    {
        const string key = "202122232425262728292A2B2C2D2E2F";
        string random = Sequence(1, 14);
        Run($"KEY tk=GenerateKey(AES-CBC,0x({Sequence(0, 16)})) " +
            $"KEY ts=GenerateKey(DES3-CBC,0x({key})) " +
            $"PARAM tw=#MECH:WRAP-AES-TR31 #BLKH:\"D0112D0TB00E0000\" #RND:0x({random}) " +
            "VAR tb=Wrap(tw,tk,ts) PARAM tu=#MECH:WRAP-AES-TR31 KEY tr=Unwrap(tu,tk,tb)");
        var source = (KeyVariableDeclaration)VariableDictionary.Instance().Get("ts");
        var recovered = (KeyVariableDeclaration)VariableDictionary.Instance().Get("tr");
        source.Value = "source-key-not-selected-for-rewrap";

        Run($"PARAM tp=#MECH:WRAP-AES-TR31 #BLKH:\"D0112D0TB00E0000\" #RND:0x({random}) " +
            "VAR to=Wrap(tp,tk,tr)");
        var rewrapped = TR31String.FromString(VariableDictionary.Instance().Get("to").Value);
        byte[] clear = Decrypt(rewrapped, Cases[0][1]);

        Assert.Multiple(() =>
        {
            Assert.That(recovered.Mechanism, Is.EqualTo("WRAP-AES-TR31"));
            Assert.That(recovered.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Tdea)));
            Assert.That(recovered.KeyAttributes.Any(a => a.ID == "HDR" && a.Data == "D0112D0TB00E0000"), Is.True);
            Assert.That(WrapAESTR31.GetObfuscationPaddingLength(recovered, 16), Is.EqualTo(8));
            Assert.That(rewrapped.Block, Is.EqualTo("D0112D0TB00E0000"));
            Assert.That(rewrapped.Cryptogram, Has.Length.EqualTo(32));
            Assert.That(Convert.ToHexString(clear), Is.EqualTo("0080" + key + random));
        });
    }

    [TestCase('H', KeyAlgorithm.Hmac)]
    [TestCase('D', KeyAlgorithm.Unknown)]
    public void UnwrapClassifiesOnlySupportedAuthenticatedHeaderAlgorithms(
        char algorithmCode, KeyAlgorithm expectedAlgorithm)
    {
        string header = $"D0144D0{algorithmCode}B00E0000";
        var block = Wrap(128, header, Sequence(1, 30));

        Run($"VAR mi={block} PARAM mu=#MECH:WRAP-AES-TR31 KEY mr=Unwrap(mu,ck,mi)");
        var result = (KeyVariableDeclaration)VariableDictionary.Instance().Get("mr");

        Assert.That(result.KeyType, Is.EqualTo(KeyType.Secret(expectedAlgorithm)));
        Assert.That(result.Mechanism, Is.EqualTo("WRAP-AES-TR31"));
        Assert.That(result.KeyAttributes.Any(a => a.ID == "HDR" && a.Data == header), Is.True);
    }

    [Test]
    public void UnwrappedTr31KeyMetadataSurvivesJsonRoundtrip()
    {
        const string header = "D0144D0HB00E0000";
        var block = Wrap(128, header, Sequence(1, 30));
        Run($"VAR ji={block} PARAM ju=#MECH:WRAP-AES-TR31 KEY jr=Unwrap(ju,ck,ji)");
        var original = (KeyVariableDeclaration)VariableDictionary.Instance().Get("jr");

        KeyVariableDeclaration restored = KeyVariableDeclaration.Deserialize(original.Serialize());

        Assert.Multiple(() =>
        {
            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Hmac)));
            Assert.That(restored.KeySizeInBits, Is.EqualTo(original.KeySizeInBits));
            Assert.That(restored.KeySize, Is.EqualTo(original.KeySize));
            Assert.That(restored.Mechanism, Is.EqualTo("WRAP-AES-TR31"));
            Assert.That(restored.KeyAttributes.Select(a => (a.ID, a.Length, a.Data)),
                Is.EqualTo(original.KeyAttributes.Select(a => (a.ID, a.Length, a.Data))));
        });
    }

    [Test]
    public void WrapRejectsHeaderWhoseDeclaredLengthDoesNotMatchGeneratedBlock()
    {
        var error = Assert.Throws<CryptoScript.ErrorListner.SemanticErrorException>(() =>
            Wrap(128, "D0112D0AB00E0000", Sequence(1, 30)));
        Assert.That(error!.SemanticError.Message,
            Is.EqualTo("TR-31 header declares total length 112, but wrap produces 144 characters."));
    }

    [Test]
    public void WrapLengthValidationIncludesOptionalBlocks()
    {
        const string header = "D0156P0AE00E0100KS0C12345678";
        var block = Wrap(128, header, Sequence(1, 30));
        string wireBlock = header + Convert.ToHexString(block.Cryptogram) + Convert.ToHexString(block.Mac);
        Assert.Multiple(() =>
        {
            Assert.That(block.Block, Is.EqualTo(header));
            Assert.That(wireBlock, Has.Length.EqualTo(156));
        });
        CheckUnwrap($"\"{wireBlock}\"", Sequence(32, 16), 128);
    }

    [Test]
    public void FullWireUnwrapIgnoresCharactersBeyondDeclaredLength()
    {
        var block = Wrap(128, "D0144D0AB00E0000", Sequence(1, 30));
        CheckUnwrap($"\"{block.Block}{Convert.ToHexString(block.Cryptogram)}{Convert.ToHexString(block.Mac)}DEADBEEF\"", Sequence(32, 16), 128);
    }

    private static TR31String Wrap(int bits, string header, string? random)
        => Wrap(bits, "AES-CBC", header, random);

    private static TR31String Wrap(int bits, string wrappedKeyMechanism, string header, string? random)
    {
        Run($"KEY ck=GenerateKey(AES-CBC,0x({Sequence(0, bits / 8)})) " +
            $"KEY cv=GenerateKey({wrappedKeyMechanism},0x({Sequence(32, bits / 8)})) " +
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
