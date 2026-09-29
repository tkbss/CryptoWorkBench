using CryptoScript.CryptoAlgorithm;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

public class AlgorithmFactoryTests
{
    [Test]
    public void EveryProductiveMechanismResolvesToAConcreteAlgorithm()
    {
        Assert.That(MechanismList.Instance.Mechanisms, Has.Count.EqualTo(47));

        foreach (string mechanism in MechanismList.Instance.Mechanisms)
        {
            CryptoAlgorithm algorithm = AlgorithmFactory.Create(mechanism);

            Assert.That(algorithm, Is.Not.TypeOf<CryptoAlgorithm>(), mechanism);
            Assert.That(algorithm, Is.Not.TypeOf<SymmetricCryptoAlgorithm>(), mechanism);
        }
    }

    [TestCase("WRAP-AES")]
    [TestCase("WRAP-DES3")]
    [TestCase("RSA-PSS")]
    [TestCase("RSA-OAEP")]
    [TestCase("ECDSA")]
    [TestCase("UNKNOWN")]
    [TestCase("AES-UNKNOWN")]
    [TestCase("WRAP-AES-UNKNOWN")]
    [TestCase("HMAC-SHA999")]
    [TestCase("NOT-AES-CBC")]
    [TestCase("HMAC-SHA256-EXTRA")]
    [TestCase("WRAP-AES-TR31-EXTRA")]
    [TestCase("BLOCKHEADER-AES-TR31")]
    [TestCase("BLOCKHEADER-AES-TR31-EXTRA")]
    [TestCase("BLOCKHEADER-WRAP-AES-TR31-EXTRA")]
    public void UnsupportedMechanismsDoNotFallBackToAnAlgorithm(string mechanism)
    {
        var error = Assert.Throws<NotSupportedException>(() => AlgorithmFactory.Create(mechanism));

        Assert.That(error!.Message, Is.EqualTo($"Unsupported mechanism: {mechanism}."));
    }

    [TestCase("AES-CBC", typeof(CryptoScript.CryptoAlgorithm.AES.AES))]
    [TestCase("DES3-CBC", typeof(CryptoScript.CryptoAlgorithm.DES3.DES3))]
    [TestCase("HMAC-SHA256", typeof(CryptoScript.CryptoAlgorithm.HMAC.HMAC))]
    [TestCase("HASH-SHA256", typeof(CryptoScript.CryptoAlgorithm.HASH.HASH))]
    [TestCase("WRAP-AES-TR31", typeof(CryptoScript.CryptoAlgorithm.WRAPPERS.WrapAESTR31))]
    [TestCase("WRAP-DES3-TR31", typeof(CryptoScript.CryptoAlgorithm.WRAPPERS.WrapDES3TR31))]
    [TestCase("BLOCKHEADER-WRAP-AES-TR31", typeof(CryptoScript.CryptoAlgorithm.WRAPPERS.Tr31BlockHeader))]
    public void ImplementedMechanismsStillResolveToTheirAlgorithms(string mechanism, Type expectedType)
    {
        Assert.That(AlgorithmFactory.Create(mechanism), Is.TypeOf(expectedType));
    }

    [Test]
    public void CryptoOperationsBlockHeaderUsesTheInternalTr31FactoryMechanism()
    {
        var operations = new CryptoOperations();

        VariableDeclaration result = operations.BlockHeader("WRAP-AES-TR31");

        Assert.That(result, Is.TypeOf<BlockHeaderVariableDeclaration>());
        var header = (BlockHeaderVariableDeclaration)result;
        Assert.Multiple(() =>
        {
            Assert.That(header.Type, Is.TypeOf<CryptoTypeTR31Header>());
            Assert.That(
                header.Value,
                Is.EqualTo("{KBVID:D,KBLEN:0144,KEYU:D0,ALGO:A,MODEU:B,KEYVN:00,EXP:S,KEYCTX:0,NUMOPTB:00,RSV:0}"));
        });
    }
}
