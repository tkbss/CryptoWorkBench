using CryptoScript.CryptoAlgorithm.EMV;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class EmvAcVerifyMechanismTests
{
    private const string Mechanism = "MAC-EMV-AC-VERIFY";

    [SetUp]
    public void SetUp() => EmvAcFixtures.Reset();

    [TestCaseSource(typeof(EmvAcFixtures), nameof(EmvAcFixtures.Matrix))]
    public void SuccessfulVerificationReturnsHex01AndContinues(string cipher, string key, string ac, bool direct, bool literal)
    {
        var runner = EmvAcFixtures.Execute(EmvAcFixtures.Prefix(cipher, key) +
            (direct ? $"PARAM p=#MECH:{Mechanism} #EXPECTED:0x({ac}) " :
                $"PARAM p=Parameters({Mechanism},#EXPECTED:0x({ac})) ") +
            $"VAR verified=Mac(p,sk,{(literal ? "0x(" + EmvAcFixtures.Data + ")" : "data")}) VAR after=0x(AABB)");
        var verified = VariableDictionary.Instance().Get("verified");
        Assert.Multiple(() =>
        {
            Assert.That(verified.Type, Is.TypeOf<CryptoTypeVar>());
            Assert.That(verified.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(verified.Value, Is.EqualTo("0x(01)"));
            Assert.That(VariableDictionary.Instance().Get("after").Value, Is.EqualTo("0x(AABB)"));
            Assert.That(runner.Diagnostics, Is.Empty);
        });
    }

    public static IEnumerable<TestCaseData> Mismatches()
    {
        foreach (var v in EmvAcFixtures.Vectors)
            for (int index = 0; index < 8; index++)
                yield return new TestCaseData(v.Cipher, v.Key, v.Ac, index);
    }

    [TestCaseSource(nameof(Mismatches))]
    public void PublicRunnerFailsClosedForEveryAcByte(string cipher, string key, string ac, int index)
    {
        // Mutate a fixed, independently established AC, never a production-generated expectation.
        byte[] wrong = Convert.FromHexString(ac);
        wrong[index] ^= 0x80;
        string script = EmvAcFixtures.Prefix(cipher, key) +
            $"PARAM p=Parameters({Mechanism},#EXPECTED:0x({Convert.ToHexString(wrong)})) " +
            "VAR verified=Mac(p,sk,data) KEY after=GenerateKey(AES-ECB,128) VAR last=0x(AABB)";
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        var runner = new CryptoScriptRunner(); // Public path; no injected execution seam.
        var error = Assert.Throws<SemanticErrorException>(() => runner.Execute(context));
        var dictionary = VariableDictionary.Instance();
        Assert.Multiple(() =>
        {
            Assert.That(error!.SemanticError!.Type, Is.EqualTo("FunctionCall"));
            Assert.That(error.SemanticError.FunctionName, Is.EqualTo("Mac"));
            Assert.That(error.SemanticError.Message, Is.EqualTo("EMV Application Cryptogram verification failed."));
            Assert.That(error.SemanticError.FunctionCall, Is.EqualTo("Mac(<redacted>)"));
            Assert.That(error.SemanticError.Message, Does.Not.Contain(key).And.Not.Contain(ac).And.Not.Contain(EmvAcFixtures.Data));
            Assert.That(dictionary.Contains("verified"), Is.False);
            Assert.That(dictionary.Contains("after"), Is.False);
            Assert.That(dictionary.Contains("last"), Is.False);
            Assert.That(((KeyVariableDeclaration)dictionary.Get("sk")).KeyValue, Is.EqualTo("0x(" + key + ")").IgnoreCase);
            Assert.That(dictionary.Get("data").Value, Is.EqualTo("0x(" + EmvAcFixtures.Data + ")").IgnoreCase);
            Assert.That(runner.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(runner.Diagnostics[0], Is.SameAs(error.SemanticError));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void VerificationPreservesReferencedInputsAndDoesNotEnforceNewUsagePolicy(bool valid)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
            $"PARAM p=Parameters({Mechanism},#EXPECTED:0x({(valid ? EmvAcFixtures.AesAc : "53A78E1517C22F70")}))");
        var dictionary = VariableDictionary.Instance();
        var key = (KeyVariableDeclaration)dictionary.Get("sk");
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.MacGenerate);
        var parameters = dictionary.Get("p");
        var data = dictionary.Get("data");
        string keyBefore = key.Serialize(), parameterBefore = parameters.Value, dataBefore = data.Value;
        if (valid) EmvAcFixtures.Execute("VAR verified=Mac(p,sk,data)");
        else Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute("VAR verified=Mac(p,sk,data)"));
        Assert.Multiple(() =>
        {
            Assert.That(dictionary.Get("sk"), Is.SameAs(key));
            Assert.That(key.Serialize(), Is.EqualTo(keyBefore));
            Assert.That(dictionary.Get("p"), Is.SameAs(parameters));
            Assert.That(parameters.Value, Is.EqualTo(parameterBefore));
            Assert.That(dictionary.Get("data"), Is.SameAs(data));
            Assert.That(data.Value, Is.EqualTo(dataBefore));
            Assert.That(dictionary.Contains("verified"), Is.EqualTo(valid));
        });
    }

    [TestCase("")]
    [TestCase("#EXPECTED:0x(0001)")]
    [TestCase("#EXPECTED:0x(000102030405060708)")]
    [TestCase("#EXPECTED:0x(000102030405060)")]
    [TestCase("#EXPECTED:\"D3A78E1517C22F70\"")]
    [TestCase("#EXPECTED:b64(06eOFRfCL3A=)")]
    [TestCase("#EXPECTED:12345678")]
    [TestCase("#EXPECTED:sk")]
    [TestCase("#EXPECTED:0x(D3A78E1517C22F70),#EXPECTED:0x(D3A78E1517C22F70)")]
    [TestCase("#EXPECTED:0x(D3A78E1517C22F70),#EXPECTED:0x(53A78E1517C22F70)")]
    [TestCase("#EXPECTED:0x(D3A78E1517C22F70),#PAD:NONE")]
    [TestCase("#EXPECTED:0x(D3A78E1517C22F70),#MACLEN:8")]
    [TestCase("#EXPECTED:0x(D3A78E1517C22F70),#IV:0x(0000000000000000)")]
    [TestCase("#EXPECTED:0x(D3A78E1517C22F70),#MECH:MAC-EMV-AC-VERIFY")]
    [TestCase("#EXPECTED:0x(D3A78E1517C22F70),#MECH:AES-CMAC")]
    public void InvalidParametersFailForBothDeclarationForms(string parameters)
    {
        foreach (bool direct in new[] { false, true })
        {
            EmvAcFixtures.Reset();
            string declaration = direct ? $"PARAM p=#MECH:{Mechanism} {parameters.Replace(',', ' ')} " :
                $"PARAM p=Parameters({Mechanism}{(parameters.Length == 0 ? "" : "," + parameters)}) ";
            Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
                declaration + "VAR verified=Mac(p,sk,data) VAR after=0x(AABB)"));
            Assert.That(VariableDictionary.Instance().Contains("verified"), Is.False);
            Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        }
    }

    [TestCase("\"D3A78E1517C22F70\"")]
    [TestCase("0x(000G000000000000)")]
    [TestCase("0x(D3A78E1517C22F70)junk")]
    public void HostSuppliedExpectedValuesAreStrictlyValidated(string expected)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
            $"PARAM p=Parameters({Mechanism},#EXPECTED:0x({EmvAcFixtures.AesAc}))");
        var parameters = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        parameters.SetParameter("#EXPECTED", expected);
        Assert.Throws<ArgumentException>(() => new MAC_EMV_AC_VERIFY().Mac(EmvAcFixtures.Structured()));
    }

    [TestCase("#MECH:MAC-EMV-AC-VERIFY")]
    [TestCase("Parameters(MAC-EMV-AC-VERIFY,#EXPECTED:0x(D3A78E1517C22F70))")]
    public void VerifyAlsoRequiresGenuineParamReference(string argument)
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix());
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute($"VAR verified=Mac({argument},sk,data)"));
    }

    [Test]
    public void ContradictoryStoredMechIsRejectedByDirectWrapper()
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
            $"PARAM p=Parameters({Mechanism},#EXPECTED:0x({EmvAcFixtures.AesAc}))");
        var parameters = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        parameters.GetParameters()["#MECH"] = "MAC-EMV-AC";
        Assert.Throws<ArgumentException>(() => new MAC_EMV_AC_VERIFY().Mac(EmvAcFixtures.Structured()));
    }

    [TestCase("Mac(p,sk)")]
    [TestCase("Mac(p,sk,data,data)")]
    [TestCase("Mac(p,data,data)")]
    [TestCase("Mac(p,0x(89F7B697A028A93345BE7A409665B9A4),data)")]
    [TestCase("Mac(p,sk,\"000102\")")]
    [TestCase("Mac(p,sk,1)")]
    [TestCase("Mac(p,sk,b64(AAEC))")]
    [TestCase("Mac(p,sk,sk)")]
    [TestCase("Mac(p,sk,p)")]
    [TestCase("Mac(p,sk,Hash(HASH-SHA1,0x(0001)))")]
    public void VerifyEnforcesKeyDataAndArgumentCountContracts(string call)
    {
        Assert.Throws<SemanticErrorException>(() => EmvAcFixtures.Execute(EmvAcFixtures.Prefix() +
            $"PARAM p=Parameters({Mechanism},#EXPECTED:0x({EmvAcFixtures.AesAc})) VAR verified={call} VAR after=0x(AABB)"));
        Assert.That(VariableDictionary.Instance().Contains("verified"), Is.False);
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
    }

    [Test]
    public void ExpectedHexVariableIsAResolvedValueWithoutProvenanceClaims()
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix() + $"VAR expected=0x({EmvAcFixtures.AesAc}) " +
            $"PARAM p=Parameters({Mechanism},#EXPECTED:expected) VAR verified=Mac(p,sk,data)");
        Assert.That(VariableDictionary.Instance().Get("verified").Value, Is.EqualTo("0x(01)"));
    }

    [Test]
    public void InvalidExpectedIsRejectedBeforeWeakKeyCryptography()
    {
        EmvAcFixtures.Execute(EmvAcFixtures.Prefix("DES3-ECB", EmvAcFixtures.Vectors[0].Key) +
            $"PARAM p=Parameters({Mechanism},#EXPECTED:0x({EmvAcFixtures.AesAc}))");
        ((KeyVariableDeclaration)VariableDictionary.Instance().Get("sk")).KeyValue = "0x(" + new string('0', 32) + ")";
        ((ParameterVariableDeclaration)VariableDictionary.Instance().Get("p")).SetParameter("#EXPECTED", "0x(0001)");
        var error = Assert.Throws<ArgumentException>(() => new MAC_EMV_AC_VERIFY().Mac(EmvAcFixtures.Structured()));
        Assert.That(error!.Message, Does.Contain("Expected cryptogram"));
    }
}
