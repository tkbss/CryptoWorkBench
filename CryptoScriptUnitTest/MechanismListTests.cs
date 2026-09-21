using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class MechanismListTests
{
    private static readonly string[] HmacMechanisms =
    {
        "HMAC-SHA1", "HMAC-SHA224", "HMAC-SHA256", "HMAC-SHA384", "HMAC-SHA512",
        "HMAC-SHA512-224", "HMAC-SHA512-256",
        "HMAC-SHA3-224", "HMAC-SHA3-256", "HMAC-SHA3-384", "HMAC-SHA3-512"
    };

    private static readonly string[] HashMechanisms =
    {
        "HASH-SHA1", "HASH-SHA224", "HASH-SHA256", "HASH-SHA384", "HASH-SHA512",
        "HASH-SHA512-224", "HASH-SHA512-256",
        "HASH-SHA3-224", "HASH-SHA3-256", "HASH-SHA3-384", "HASH-SHA3-512"
    };

    private static readonly string[] Expected =
    {
        "AES-ECB", "AES-CBC", "AES-CTR", "AES-CMAC", "AES-GCM", "AES-CCM", "AES-GMAC",
        "HMAC-SHA1", "HMAC-SHA224", "HMAC-SHA256", "HMAC-SHA384", "HMAC-SHA512",
        "HMAC-SHA512-224", "HMAC-SHA512-256",
        "HMAC-SHA3-224", "HMAC-SHA3-256", "HMAC-SHA3-384", "HMAC-SHA3-512",
        "HASH-SHA1", "HASH-SHA224", "HASH-SHA256", "HASH-SHA384", "HASH-SHA512",
        "HASH-SHA512-224", "HASH-SHA512-256",
        "HASH-SHA3-224", "HASH-SHA3-256", "HASH-SHA3-384", "HASH-SHA3-512",
        "DES3-ECB", "DES3-CBC", "DES3-RETAIL", "DES3-CMAC",
        "WRAP-AES-TR31", "WRAP-DES3-TR31", "WRAP-AES", "WRAP-DES3", "KDF-HKDF",
        "HKDF-EXTRACT", "HKDF-EXPAND", "KDF-SP800-108-COUNTER", "DUKPT-AES-INITIAL-KEY", "KDF-EP2-SESSION",
        "KDF-EP2-PAN-SURROGATE-TRX", "KDF-EP2-PAN-RECEIPT-TRX", "KDF-EP2-PAN-RECEIPT-TRM"
    };

    [Test]
    public void PreservesExactNamesAndOrder()
    {
        Assert.That(MechanismList.Instance.Mechanisms, Has.Count.EqualTo(46));
        Assert.That(MechanismList.Instance.Mechanisms, Is.EqualTo(Expected));
    }

    [TestCaseSource(nameof(HashMechanisms))]
    public void ParsesHashMechanism(string name)
    {
        LexerErrorListener.LexerErrorOccured = false;
        var parser = ParserBuilder.StringBuild($"PARAM p=#MECH:{name}");

        parser.program();

        Assert.Multiple(() =>
        {
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        });
    }

    [TestCaseSource(nameof(HmacMechanisms))]
    public void ParsesHmacMechanism(string name)
    {
        LexerErrorListener.LexerErrorOccured = false;
        var parser = ParserBuilder.StringBuild($"PARAM p=#MECH:{name}");

        parser.program();

        Assert.Multiple(() =>
        {
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
        });
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
