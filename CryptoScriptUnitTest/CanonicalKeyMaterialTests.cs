using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Operations;
using CryptoScript.Variables;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class CanonicalKeyMaterialTests
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
        foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
    }

    private static void Run(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var program = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        new CryptoScriptRunner().Execute(program);
    }

    private static KeyVariableDeclaration Key(string id) =>
        (KeyVariableDeclaration)VariableDictionary.Instance().Get(id);

    [Test]
    public void GeneratedKeyMutationUpdatesEncryptionMacKcvPrintAndSerialization()
    {
        const string material = "0x(000102030405060708090A0B0C0D0E0F)";
        Run("KEY k=GenerateKey(AES-ECB,128) PARAM e=Parameters(AES-ECB) " +
            "PARAM m=Parameters(AES-CMAC) VAR zero=0x(00000000000000000000000000000000)");
        var key = Key("k");
        var keyType = key.KeyType;
        var usage = key.Usage;
        var attributes = key.KeyAttributes;
        VariableDeclaration baseReference = key;
        baseReference.Value = material;

        Run("VAR encrypted=Encrypt(e,k,zero) VAR decrypted=Decrypt(e,k,encrypted) VAR mac=Mac(m,k,zero)");

        Assert.Multiple(() =>
        {
            Assert.That(VariableDictionary.Instance().Get("encrypted").Value,
                Is.EqualTo("0x(C6A13B37878F5B826F4F8162A1C8D879)").IgnoreCase);
            Assert.That(VariableDictionary.Instance().Get("decrypted").Value,
                Is.EqualTo(VariableDictionary.Instance().Get("zero").Value).IgnoreCase);
            Assert.That(VariableDictionary.Instance().Get("mac").Value,
                Is.EqualTo("0x(BE7ED6AE787DD5544C5561C7DCD06ABB)").IgnoreCase);
            Assert.That(key.Kcv, Is.EqualTo("0x(c6a13b37878f5b826f4f8162a1c8d879)"));
            Assert.That(key.Kcvc, Is.EqualTo("0x(be7ed6ae787dd5544c5561c7dcd06abb)"));
            Assert.That(key.Kcvk, Is.EqualTo("0x(0a940bb5416ef045f1c39458c653ea5a)"));
            Assert.That(Key("k"), Is.SameAs(baseReference));
            Assert.That(key.KeyType, Is.SameAs(keyType));
            Assert.That(key.Usage, Is.SameAs(usage));
            Assert.That(key.KeyAttributes, Is.SameAs(attributes));
            Assert.That(key.PrintOutput(), Does.Contain("Value: " + material).And.Not.Contain("KeyValue:"));
            Assert.That(typeof(KeyVariableDeclaration).GetProperty("KeyValue"), Is.Null);
        });
        var json = JObject.Parse(key.Serialize());
        Assert.That(json.Property("KeyValue"), Is.Null);
        Assert.That(json.Value<string>("Value"), Is.EqualTo(material));
        Assert.That(KeyVariableDeclaration.Deserialize(json.ToString()).Value, Is.EqualTo(material));
        Assert.That(KeyCheckOperations.Check([key.Kcv!, "KCV"]).Value, Is.EqualTo(material));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Tr31UsesCurrentValuesThroughExistingObjectArguments(bool tdea)
    {
        string mechanism, generator, kbpk, material, header, padding, expected;
        if (tdea)
        {
            var vector = Tr31ReferenceVectors.All[0];
            mechanism = "WRAP-DES3-TR31";
            generator = "DES3-ECB";
            kbpk = vector.Kbpk;
            material = vector.Key;
            header = vector.Header;
            padding = vector.ObfuscationPadding + vector.CipherBlockPadding;
            expected = vector.CompleteBlock;
        }
        else
        {
            var vector = Tr31VersionDTransportVectors.All.First(v => v.Algorithm == 'A' && v.Header.Length == 16);
            mechanism = "WRAP-AES-TR31";
            generator = "AES-ECB";
            kbpk = vector.Kbpk;
            material = vector.Key;
            header = vector.Header;
            padding = vector.Random;
            expected = vector.Wire;
        }
        Run($"KEY protection=GenerateKey({generator},{kbpk.Length * 4}) " +
            $"KEY transported=GenerateKey({generator},{material.Length * 4}) " +
            $"PARAM p=#MECH:{mechanism} #BLKH:\"{header}\" #RND:0x({padding})");
        var protection = Key("protection");
        var transported = Key("transported");
        var parameter = VariableDictionary.Instance().Get("p");
        // Capture arguments before mutation: source object identity must govern key reads.
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument(parameter.Value, ResolvedCallArgumentKind.Variable, parameter),
            new ResolvedCallArgument(protection.Value, ResolvedCallArgumentKind.Variable, protection),
            new ResolvedCallArgument(transported.Value, ResolvedCallArgumentKind.Variable, transported)
        });
        protection.Value = $"0x({kbpk})";
        transported.Value = $"0x({material})";

        var wrapped = OperationFactory.CreateInvocationOperation("Wrap")(invocation);
        var block = TR31String.FromString(wrapped.Value);
        Assert.That(block.Block + Convert.ToHexString(block.Cryptogram) + Convert.ToHexString(block.Mac),
            Is.EqualTo(expected).IgnoreCase);
        Run($"VAR wire=\"{expected}\" KEY recovered=Unwrap(p,protection,wire)");
        Assert.That(Key("recovered").Value, Is.EqualTo(transported.Value).IgnoreCase);
        Assert.That(Key("protection"), Is.SameAs(protection));
        Assert.That(Key("transported"), Is.SameAs(transported));
    }
}
