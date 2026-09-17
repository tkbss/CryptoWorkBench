using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class MechanismListTests
{
    private static readonly string[] Expected =
    {
        "AES-ECB", "AES-CBC", "AES-CTR", "AES-CMAC", "AES-GCM", "AES-CCM", "AES-GMAC",
        "DES3-ECB", "DES3-CBC", "DES3-RETAIL", "DES3-CMAC",
        "WRAP-AES-TR31", "WRAP-DES3-TR31", "WRAP-AES", "WRAP-DES3"
    };

    [Test]
    public void PreservesExactNamesAndOrder()
    {
        Assert.That(MechanismList.Instance.Mechanisms, Has.Count.EqualTo(15));
        Assert.That(MechanismList.Instance.Mechanisms, Is.EqualTo(Expected));
    }

    [TestCaseSource(nameof(Expected))]
    public void AcceptsKnownMechanismAndClassifiesItAsParameter(string name)
    {
        var mechanism = new Mechanism();
        mechanism.SetMechanismValue(name);

        Assert.That(mechanism.Value, Is.EqualTo(name));
        Assert.That(FormatConversions.ParseString(name), Is.EqualTo(FormatConversions.PAR));
    }

    [TestCase("UNKNOWN")]
    [TestCase("aes-cbc")]
    [TestCase("Aes-Cbc")]
    [TestCase("AES-CBC ")]
    public void RejectsUnknownOrNonExactNamesWithoutChangingValue(string name)
    {
        var mechanism = new Mechanism();
        mechanism.SetMechanismValue("AES-CBC");

        var error = Assert.Throws<ArgumentException>(() => mechanism.SetMechanismValue(name));

        Assert.That(error!.Message, Is.EqualTo("Unknown mechanism " + name));
        Assert.That(mechanism.Value, Is.EqualTo("AES-CBC"));
        Assert.That(FormatConversions.ParseString(name), Is.EqualTo(string.Empty));
    }
}
