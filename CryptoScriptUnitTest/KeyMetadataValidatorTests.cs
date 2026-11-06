using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

public class KeyMetadataValidatorTests
{
    private static KeyVariableDeclaration Key(KeyAlgorithm algorithm = KeyAlgorithm.Aes) => new()
    {
        Id = "key", KeyType = KeyType.Secret(algorithm),
        Value = "not technically valid key data",
        KeySizeInBits = new KeySize(7), DerivationMechanism = "unchanged"
    };

    [TestCase(KeyAlgorithm.Unknown, KeyAlgorithm.Aes, false)]
    [TestCase(KeyAlgorithm.Aes, KeyAlgorithm.Aes, false)]
    [TestCase(KeyAlgorithm.Tdea, KeyAlgorithm.Aes, true)]
    [TestCase(KeyAlgorithm.Hmac, KeyAlgorithm.Tdea, true)]
    [TestCase(KeyAlgorithm.Aes, KeyAlgorithm.Unknown, false)]
    public void EvaluatesOnlyExplicitAlgorithmConflicts(KeyAlgorithm actual, KeyAlgorithm expected, bool conflict)
    {
        var diagnostics = KeyMetadataValidator.Validate(Key(actual), expected, KeyUsage.Encrypt, "Encrypt", "AES-ECB");
        Assert.That(diagnostics.Count, Is.EqualTo(conflict ? 1 : 0));
        if (conflict)
            Assert.That(diagnostics[0].DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyTypeConflict));
    }

    [TestCase(KeyUsage.Encrypt, KeyUsage.Encrypt, false)]
    [TestCase(KeyUsage.Encrypt | KeyUsage.Decrypt, KeyUsage.Encrypt | KeyUsage.Decrypt, false)]
    [TestCase(KeyUsage.Encrypt, KeyUsage.Encrypt | KeyUsage.Decrypt, true)]
    [TestCase(KeyUsage.MacGenerate, KeyUsage.Decrypt, true)]
    [TestCase(KeyUsage.Encrypt, KeyUsage.None, false)]
    public void EvaluatesRestrictedUsageIncludingCombinedBits(KeyUsage allowed, KeyUsage required, bool conflict)
    {
        var key = Key();
        key.Usage = KeyUsagePolicy.Restricted(allowed);
        var diagnostics = KeyMetadataValidator.Validate(key, KeyAlgorithm.Aes, required, "Decrypt", "AES-ECB");
        Assert.That(diagnostics.Count, Is.EqualTo(conflict ? 1 : 0));
        if (conflict)
            Assert.That(diagnostics[0].DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyUsageConflict));
    }

    [Test]
    public void UnspecifiedUsageDoesNotWarnAndInvalidKeyBytesAreNotInspected()
    {
        Assert.That(KeyMetadataValidator.Validate(Key(), KeyAlgorithm.Aes,
            KeyUsage.Encrypt | KeyUsage.Decrypt, "Decrypt", "AES-ECB"), Is.Empty);
    }

    [Test]
    public void SimultaneousConflictsReportSameObjectsInStableOrderWithoutMutationOrKeyData()
    {
        var key = Key(KeyAlgorithm.Hmac);
        key.Usage = KeyUsagePolicy.Restricted(KeyUsage.MacGenerate);
        string before = key.Serialize();
        var type = key.KeyType;
        var policy = key.Usage;
        var attributes = key.KeyAttributes;
        var sink = new SemanticDiagnosticSink();
        var diagnostics = KeyMetadataValidator.Validate(key, KeyAlgorithm.Aes, KeyUsage.Decrypt,
            "Decrypt", "AES-ECB", sink);
        Assert.That(diagnostics.Select(d => d.DiagnosticCode), Is.EqualTo(new[]
            { SemanticDiagnosticCode.KeyTypeConflict, SemanticDiagnosticCode.KeyUsageConflict }));
        Assert.That(diagnostics.Select(d => d.Severity), Is.All.EqualTo(SemanticDiagnosticSeverity.Warning));
        for (int i = 0; i < diagnostics.Count; i++)
        {
            var diagnostic = diagnostics[i];
            Assert.That(sink.GetSnapshot()[i], Is.SameAs(diagnostic));
            Assert.That(diagnostic.FunctionName, Is.EqualTo("Decrypt"));
            Assert.That(diagnostic.Identifier, Is.EqualTo("key"));
            Assert.That(diagnostic.Location, Is.EqualTo(new DiagnosticLocation(
                FunctionName: "Decrypt", Mechanism: "AES-ECB", Identifier: "key")));
            Assert.That(diagnostic.ErrorCode, Is.Null);
            Assert.That(diagnostic.Value, Is.Empty);
            Assert.That(diagnostic.FunctionCall, Is.Empty);
            Assert.That(SemanticDiagnosticFormatter.Format(diagnostic),
                Does.Not.Contain(key.Value).And.Not.Contain("compliant"));
        }
        Assert.That(diagnostics[0].Message, Does.Contain("Hmac").And.Contain("Aes"));
        Assert.That(diagnostics[1].Message, Does.Contain("MacGenerate").And.Contain("Decrypt"));
        Assert.That(key.Serialize(), Is.EqualTo(before));
        Assert.That(key.KeyType, Is.SameAs(type));
        Assert.That(key.Usage, Is.SameAs(policy));
        Assert.That(key.KeyAttributes, Is.SameAs(attributes));
        Assert.Throws<NotSupportedException>(() => ((IList<SemanticError>)diagnostics).Clear());
    }

    [Test]
    public void MissingIdentifierIsOptionalAndEachValidationProducesIndependentWarnings()
    {
        var key = Key();
        key.Id = string.Empty;
        var first = KeyMetadataValidator.Validate(key, KeyAlgorithm.Tdea, KeyUsage.Encrypt, "Encrypt", "DES3-ECB");
        var second = KeyMetadataValidator.Validate(key, KeyAlgorithm.Tdea, KeyUsage.Encrypt, "Encrypt", "DES3-ECB");
        Assert.That(first[0].Identifier, Is.Empty);
        Assert.That(first[0].Location!.Identifier, Is.Null);
        Assert.That(second[0], Is.Not.SameAs(first[0]));
    }

    [Test]
    public void InvalidProgrammingArgumentsStillFail()
    {
        Assert.Throws<ArgumentNullException>(() => KeyMetadataValidator.Validate(null!, KeyAlgorithm.Aes, KeyUsage.Encrypt, "Encrypt", "AES-ECB"));
        Assert.Throws<ArgumentNullException>(() => KeyMetadataValidator.Validate(Key(), KeyAlgorithm.Aes, KeyUsage.Encrypt, null!, "AES-ECB"));
        Assert.Throws<ArgumentNullException>(() => KeyMetadataValidator.Validate(Key(), KeyAlgorithm.Aes, KeyUsage.Encrypt, "Encrypt", null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyMetadataValidator.Validate(Key(), (KeyAlgorithm)999, KeyUsage.Encrypt, "Encrypt", "AES-ECB"));
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyMetadataValidator.Validate(Key(), KeyAlgorithm.Aes, (KeyUsage)256, "Encrypt", "AES-ECB"));
    }

    [Test]
    public void SinkFailureIsNotSuppressedOrConvertedToSemanticWarning()
    {
        var error = new InvalidOperationException("sink unavailable");
        Assert.That(Assert.Throws<InvalidOperationException>(() => KeyMetadataValidator.Validate(
            Key(), KeyAlgorithm.Tdea, KeyUsage.Encrypt, "Encrypt", "DES3-ECB", new ThrowingSink(error))), Is.SameAs(error));
    }

    private sealed class ThrowingSink(Exception error) : ISemanticDiagnosticSink
    {
        public void Report(SemanticError diagnostic) => throw error;
    }
}
