using CryptoScript.Variables;
using FluentAssertions;
using Newtonsoft.Json;

namespace CryptoScriptUnitTest;

public class KeyUsagePolicyTests
{
    [Test]
    public void NewKeyDefaultsToUnspecifiedUsage()
    {
        var key = new KeyVariableDeclaration();

        key.Usage.Should().Be(KeyUsagePolicy.Unspecified);
        key.Usage.Mode.Should().Be(KeyUsageMode.Unspecified);
        key.Usage.AllowedUsages.Should().Be(KeyUsage.None);
    }

    [TestCase(KeyUsage.Encrypt)]
    [TestCase(KeyUsage.Encrypt | KeyUsage.Decrypt)]
    [TestCase(KeyUsage.MacGenerate | KeyUsage.MacVerify)]
    public void RestrictedPolicyAcceptsDefinedAtomicUsages(KeyUsage usages)
    {
        KeyUsagePolicy policy = KeyUsagePolicy.Restricted(usages);

        policy.Mode.Should().Be(KeyUsageMode.Restricted);
        policy.AllowedUsages.Should().Be(usages);
    }

    [Test]
    public void UnspecifiedPolicyRejectsAllowedUsages()
    {
        Action action = () => _ = new KeyUsagePolicy(KeyUsageMode.Unspecified, KeyUsage.Encrypt);

        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void RestrictedPolicyRejectsNone()
    {
        Action action = () => KeyUsagePolicy.Restricted(KeyUsage.None);

        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void UnknownModeIsRejected()
    {
        Action action = () => _ = new KeyUsagePolicy((KeyUsageMode)999, KeyUsage.None);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase((KeyUsage)(1 << 7))]
    [TestCase(KeyUsage.Encrypt | (KeyUsage)(1 << 7))]
    public void UnknownUsageBitsAreRejected(KeyUsage usages)
    {
        Action action = () => KeyUsagePolicy.Restricted(usages);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void LegacyJsonWithoutUsageDefaultsToUnspecified()
    {
        KeyVariableDeclaration key = KeyVariableDeclaration.Deserialize("{}");

        key.Usage.Should().Be(KeyUsagePolicy.Unspecified);
    }

    [Test]
    public void ExplicitNullUsageIsRejected()
    {
        Action action = () => KeyVariableDeclaration.Deserialize("{\"Usage\":null}");

        action.Should().Throw<Exception>();
    }

    [TestCase("{\"Mode\":0,\"AllowedUsages\":1}")]
    [TestCase("{\"Mode\":1,\"AllowedUsages\":0}")]
    [TestCase("{\"Mode\":999,\"AllowedUsages\":0}")]
    [TestCase("{\"Mode\":1,\"AllowedUsages\":128}")]
    [TestCase("{\"Mode\":1,\"AllowedUsages\":129}")]
    public void InvalidJsonCannotBypassUsageInvariants(string usageJson)
    {
        Action action = () => JsonConvert.DeserializeObject<KeyUsagePolicy>(usageJson);

        action.Should().Throw<Exception>();
    }

    [TestCase(KeyUsageMode.Unspecified, KeyUsage.None)]
    [TestCase(KeyUsageMode.Restricted, KeyUsage.Encrypt)]
    [TestCase(KeyUsageMode.Restricted, KeyUsage.Encrypt | KeyUsage.Decrypt)]
    [TestCase(KeyUsageMode.Restricted, KeyUsage.MacGenerate | KeyUsage.MacVerify)]
    public void ValidUsageMetadataSurvivesKeyJsonRoundtrip(KeyUsageMode mode, KeyUsage usages)
    {
        KeyUsagePolicy usage = mode == KeyUsageMode.Unspecified
            ? KeyUsagePolicy.Unspecified
            : KeyUsagePolicy.Restricted(usages);
        var key = new KeyVariableDeclaration { Usage = usage };

        string json = key.Serialize();
        KeyVariableDeclaration restored = KeyVariableDeclaration.Deserialize(json);

        restored.Usage.Should().Be(usage);
    }
}
