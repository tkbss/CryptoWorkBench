using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoScript.ErrorListner;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Tr31ScriptIntegrationTests
{
    private VariableDeclaration[] previous = [];
    [SetUp]
    public void Save()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }
    [TearDown]
    public void Restore()
    {
        VariableDictionary.Instance().Clear();
        foreach (var v in previous) VariableDictionary.Instance().Add(v);
    }
    private static void Run(string script) => new CryptoScriptRunner().Execute(ParserBuilder.StringBuild(script).program());
    private static string Keys(string kbpk, string key) =>
        $"KEY k=GenerateKey(DES3-CBC,0x({kbpk})) KEY t=GenerateKey(DES3-CBC,0x({key})) ";

    [TestCase(0)]
    [TestCase(1)]
    public void AnsiWrapAndBothUnwrapForms(int index)
    {
        var v = Tr31ReferenceVectors.All[index];
        Run(Keys(v.Kbpk, v.Key) + $"PARAM p=#MECH:WRAP-DES3-TR31 #BLKH:\"{v.Header}\" #RND:0x({v.ObfuscationPadding}{v.CipherBlockPadding}) VAR b=Wrap(p,k,t) KEY r=Unwrap(p,k,b)");
        var b = TR31String.FromString(VariableDictionary.Instance().Get("b").Value);
        Assert.Multiple(() =>
        {
            Assert.That(b.Block + Convert.ToHexString(b.Cryptogram) + Convert.ToHexString(b.Mac), Is.EqualTo(v.CompleteBlock));
            Assert.That(Convert.ToHexString(b.Mac), Is.EqualTo(v.Mac));
            Assert.That(Convert.ToHexString(b.Cryptogram), Is.EqualTo(v.Ciphertext));
        });
        CheckKey(v.Key);
        Run($"VAR wire=\"{v.CompleteBlock}\" KEY r=Unwrap(p,k,wire)");
        CheckKey(v.Key);
        var recovered = (KeyVariableDeclaration)VariableDictionary.Instance().Get("r");
        Assert.That(recovered.KeyAttributes!.Any(a => a.ID == "HDR" && a.Data == v.Header[..16]), Is.True);
        if (index == 1) Assert.That(recovered.KeyAttributes.Any(a => a.ID == "KS"), Is.True);
    }

    [TestCase(2, TestName = "CorrectedCReferenceWrapAndBothUnwrapForms")]
    [TestCase(3, TestName = "DerivedCorrectedAReferenceWrapAndBothUnwrapForms")]
    public void VariantReferenceWrapAndBothUnwrapForms(int index)
    {
        // Index 3 is our derived/corrected A reference, not an unchanged ANSI vector.
        var v = Tr31ReferenceVectors.All[index];
        Run(Keys(v.Kbpk, v.Key) + $"PARAM p=#MECH:WRAP-DES3-TR31 #BLKH:\"{v.Header}\" #RND:0x({v.ObfuscationPadding}{v.CipherBlockPadding}) VAR b=Wrap(p,k,t) KEY r=Unwrap(p,k,b)");
        var b = TR31String.FromString(VariableDictionary.Instance().Get("b").Value);
        Assert.Multiple(() =>
        {
            Assert.That(b.Block + Convert.ToHexString(b.Cryptogram) + Convert.ToHexString(b.Mac), Is.EqualTo(v.CompleteBlock));
            Assert.That(Convert.ToHexString(b.Cryptogram), Is.EqualTo(v.Ciphertext));
            Assert.That(Convert.ToHexString(b.Mac), Is.EqualTo(v.Mac));
            Assert.That(b.Mac, Has.Length.EqualTo(4));
        });
        CheckKey(v.Key);

        Run($"VAR wire=\"{v.CompleteBlock}\" KEY r=Unwrap(p,k,wire)");
        CheckKey(v.Key);
        var recovered = (KeyVariableDeclaration)VariableDictionary.Instance().Get("r");
        Assert.That(recovered.KeyAttributes!.Any(a => a.ID == "HDR" && a.Data == v.Header[..16]), Is.True);
        if (index == 2) Assert.That(recovered.KeyAttributes.Any(a => a.ID == "KS"), Is.True);
    }

    [TestCase(16, 16)]
    [TestCase(16, 24)]
    [TestCase(24, 16)]
    [TestCase(24, 24)]
    public void SyntheticRoundtrip(int kbpkBytes, int keyBytes)
    {
        const string source = "0123456789ABCDEFFEDCBA98765432100011223344556677";
        string key = "202122232425262728292A2B2C2D2E2F3031323334353637"[..(keyBytes * 2)];
        Run(Keys(source[..(kbpkBytes * 2)], key) + "PARAM p=#MECH:WRAP-DES3-TR31 #BLKH:\"B0096D0TB00E0000\" VAR b=Wrap(p,k,t) KEY r=Unwrap(p,k,b)");
        CheckKey(key);
    }

    [TestCase('A', 16, 16)]
    [TestCase('A', 16, 24)]
    [TestCase('A', 24, 16)]
    [TestCase('A', 24, 24)]
    [TestCase('C', 16, 16)]
    [TestCase('C', 16, 24)]
    [TestCase('C', 24, 16)]
    [TestCase('C', 24, 24)]
    public void VariantSyntheticRoundtrip(char version, int kbpkBytes, int keyBytes)
    {
        const string source = "0123456789ABCDEFFEDCBA98765432100011223344556677";
        string key = "202122232425262728292A2B2C2D2E2F3031323334353637"[..(keyBytes * 2)];
        string header = $"{version}0088D0TB00E0000";
        Run(Keys(source[..(kbpkBytes * 2)], key) +
            $"PARAM p=#MECH:WRAP-DES3-TR31 #BLKH:\"{header}\" VAR b=Wrap(p,k,t) KEY r=Unwrap(p,k,b)");
        CheckKey(key);
    }

    [TestCase('D', false)]
    [TestCase('D', true)]
    public void RejectsVersionD(char version, bool unwrap)
    {
        var v = Tr31ReferenceVectors.All[0];
        Run(Keys(v.Kbpk, v.Key) + $"PARAM p=#MECH:WRAP-DES3-TR31 #BLKH:\"{version}{v.Header[1..]}\" VAR b=\"{version}{v.CompleteBlock[1..]}\"");
        var error = Assert.Throws<SemanticErrorException>(() => Run(unwrap ? "KEY r=Unwrap(p,k,b)" : "VAR r=Wrap(p,k,t)"));
        Assert.That(error!.SemanticError.Message, Does.Contain("only versions A, B and C are supported"));
        Assert.That(VariableDictionary.Instance().Contains("r"), Is.False);
    }

    [TestCase(2, false)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(3, true)]
    public void TamperedVariantMacFailsWithoutReturningKey(int index, bool composite)
    {
        var v = Tr31ReferenceVectors.All[index];
        string mac = "00" + v.Mac[2..];
        string input = composite ? $"\"{v.Header}\"0x({v.Ciphertext})0x({mac})" : $"\"{v.Header}{v.Ciphertext}{mac}\"";
        Run(Keys(v.Kbpk, v.Key) + $"PARAM p=#MECH:WRAP-DES3-TR31 VAR b={input}");
        var error = Assert.Throws<SemanticErrorException>(() => Run("KEY r=Unwrap(p,k,b)"));
        Assert.That(error!.SemanticError.Message, Does.Contain("authentication failed"));
        Assert.That(VariableDictionary.Instance().Contains("r"), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TamperedMacFailsWithoutReturningKey(bool composite)
    {
        var v = Tr31ReferenceVectors.All[1];
        string mac = "00" + v.Mac[2..];
        string input = composite ? $"\"{v.Header}\"0x({v.Ciphertext})0x({mac})" : $"\"{v.Header}{v.Ciphertext}{mac}\"";
        Run(Keys(v.Kbpk, v.Key) + $"PARAM p=#MECH:WRAP-DES3-TR31 VAR b={input}");
        var error = Assert.Throws<SemanticErrorException>(() => Run("KEY r=Unwrap(p,k,b)"));
        Assert.That(error!.SemanticError.Message, Does.Contain("authentication failed"));
        Assert.That(VariableDictionary.Instance().Contains("r"), Is.False);
    }

    private static void CheckKey(string key)
    {
        var r = (KeyVariableDeclaration)VariableDictionary.Instance().Get("r");
        Assert.Multiple(() =>
        {
            Assert.That(r.KeyValue, Is.EqualTo($"0x({key})").IgnoreCase);
            Assert.That(r.Value, Is.EqualTo(r.KeyValue));
            Assert.That(r.KeySize, Is.EqualTo((key.Length * 4).ToString()));
            Assert.That(r.Mechanism, Is.EqualTo("WRAP-DES3-TR31"));
        });
    }
}
