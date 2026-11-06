namespace CryptoScriptUnitTest;

public class PinBlockDocumentationTests
{
    private static readonly string[] Mechanisms =
    {
        "WRAP-AES-PINBLOCK-4",
        "WRAP-DES3-PINBLOCK-0",
        "WRAP-DES3-PINBLOCK-1",
        "WRAP-DES3-PINBLOCK-2",
        "WRAP-DES3-PINBLOCK-3"
    };

    [Test]
    public void AllPinBlockDocumentsStateTheSecurityBoundary()
    {
        foreach (string mechanism in Mechanisms)
        {
            string document = Read(mechanism);
            Assert.Multiple(() =>
            {
                Assert.That(document, Does.Contain("cleartext PINs and key material"), mechanism);
                Assert.That(document, Does.Contain("ordinary process memory"), mechanism);
                Assert.That(document, Does.Contain("neither a secure cryptographic device (SCD) nor a hardware security module (HSM)"), mechanism);
                Assert.That(document, Does.Contain("development, analysis, interoperability testing, and education"), mechanism);
                Assert.That(document, Does.Contain("not a substitute for standards-compliant production PIN processing"), mechanism);
                Assert.That(document, Does.Contain("No PCI compliance or certification is implied"), mechanism);
            });
        }

        string overview = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "InfoDocs", "Info.Mechanisms.md"));
        Assert.That(overview, Does.Contain("PIN-block security boundary"));
        Assert.That(overview, Does.Contain("not an SCD or HSM"));
    }

    [Test]
    public void VariableFieldDocumentsDescribeSecureDefaultsAndExplicitOverrideResponsibility()
    {
        foreach ((string mechanism, string field) in new[]
                 {
                     ("WRAP-DES3-PINBLOCK-1", "#TRANSACTION"),
                     ("WRAP-DES3-PINBLOCK-3", "#FILL"),
                     ("WRAP-AES-PINBLOCK-4", "#RANDOM")
                 })
        {
            string document = Read(mechanism);
            Assert.Multiple(() =>
            {
                Assert.That(document, Does.Contain(field), mechanism);
                Assert.That(document, Does.Contain("cryptographically secure random-number generator"), mechanism);
                Assert.That(document, Does.Contain("reproducible development, analysis, and tests"), mechanism);
                Assert.That(document, Does.Contain("responsible for suitable freshness and entropy"), mechanism);
                Assert.That(document, Does.Contain("reuse impairs"), mechanism);
            });
        }
    }

    [Test]
    public void Format2DocumentStatesItsOfflineOnlyContextAndRuntimeLimit()
    {
        string document = Read("WRAP-DES3-PINBLOCK-2");
        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("offline PIN verification or PIN change in an ICC context"));
            Assert.That(document, Does.Contain("must not be used for online PIN verification"));
            Assert.That(document, Does.Contain("does not model the complete EMV protocol context"));
            Assert.That(document, Does.Contain("cannot determine whether an external use is offline or online"));
            Assert.That(document, Does.Contain("user or integrating system is responsible"));
        });
    }

    [Test]
    public void Des3PinBlockDocumentsStateLegacyLifecycleWithoutUniversalFormat4Claim()
    {
        foreach (string mechanism in Mechanisms.Where(mechanism => mechanism.Contains("DES3")))
        {
            string document = Read(mechanism);
            Assert.Multiple(() =>
            {
                Assert.That(document, Does.Contain("TDEA/DES3 is a legacy algorithm"), mechanism);
                Assert.That(document, Does.Contain("analysis and interoperability with existing systems"), mechanism);
                Assert.That(document, Does.Contain("current, approved AES-based solution"), mechanism);
                Assert.That(document, Does.Contain("format 4"), mechanism);
                Assert.That(document, Does.Contain("not automatically suitable for every existing environment"), mechanism);
            });
        }
    }

    [Test]
    public void FormatsZeroAndThreeDocumentTenToNineteenDigitPanContract()
    {
        Assert.That(Read("WRAP-DES3-PINBLOCK-0"), Does.Contain("10-to-19-digit decimal PAN"));
        Assert.That(Read("WRAP-DES3-PINBLOCK-3"), Does.Contain("10-to-19-digit decimal PAN"));
    }

    [Test]
    public void PinBlockDocumentsSeparateMetadataWarningsFromTechnicalErrors()
    {
        foreach (string mechanism in Mechanisms)
        {
            string document = Read(mechanism);
            Assert.That(document, Does.Contain("non-blocking `KeyTypeConflict`"));
            Assert.That(document, Does.Contain("non-blocking `KeyUsageConflict`"));
            Assert.That(document, Does.Contain("`Encrypt` is not equivalent to `PinEncrypt`"));
            Assert.That(document, Does.Contain("Unknown algorithm and Unspecified usage"));
            Assert.That(document, Does.Contain("no global warning collection"));
            Assert.That(document, Does.Contain("remain blocking errors"));
            Assert.That(document, Does.Contain("Decrypted PIN block is invalid."));
            if (mechanism.Contains("DES3", StringComparison.Ordinal))
            {
                Assert.That(document, Does.Contain("`DesParityViolation`"));
                Assert.That(document, Does.Contain("`WeakDes3Key`"));
                Assert.That(document, Does.Contain("K1=K3 alone"));
            }
        }
    }

    private static string Read(string mechanism) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "InfoDocs", $"Info.Mech.{mechanism}.md"));
}
