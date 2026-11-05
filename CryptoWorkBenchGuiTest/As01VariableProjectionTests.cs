using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class As01VariableProjectionTests
{
    [TestCase("AES")]
    [TestCase("TDEA")]
    [TestCase("HMAC")]
    public void SymmetricMetadataKeepsItsAutomationPresentation(string algorithm)
    {
        var snapshot = AutomationProjection.Variables([new VariableModel { Identifier = "key", Type = "KEY",
            IsKey = true, Algorithm = algorithm, Material = "Secret", HasKeySize = true, KeySize = "128" }], "1", DateTimeOffset.UnixEpoch);
        var key = snapshot.Items.Single();
        Assert.That(key.Algorithm, Is.EqualTo(algorithm));
        Assert.That(key.MaterialKind, Is.EqualTo("Secret"));
        Assert.That(key.KeySizeBits, Is.EqualTo(128));
    }

    [TestCase("RSA", "Public")]
    [TestCase("RSA", "Private")]
    [TestCase("EC", "Public")]
    [TestCase("EC", "Private")]
    public void StaleAsymmetricPresentationCannotReachMcp(string algorithm, string material)
    {
        var snapshot = AutomationProjection.Variables([new VariableModel { Identifier = "key", Type = "KEY",
            IsKey = true, Algorithm = algorithm, Material = material }], "1", DateTimeOffset.UnixEpoch);
        Assert.That(snapshot.Items.Single().Algorithm, Is.Null);
        Assert.That(snapshot.Items.Single().MaterialKind, Is.Null);
    }
}
