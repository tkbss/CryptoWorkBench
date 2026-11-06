using System.Collections;
using System.Globalization;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using NUnit.Framework;

namespace CryptoScriptUnitTest;

public class SemanticDiagnosticFormatterTests
{
    [Test]
    public void DefaultsAndEmptyMessage()
    {
        Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError()),
            Is.EqualTo("Error [Unclassified]\nNo diagnostic message."));
    }

    [TestCase(SemanticDiagnosticSeverity.Error, "Error")]
    [TestCase(SemanticDiagnosticSeverity.Warning, "Warning")]
    public void FormatsSeverity(SemanticDiagnosticSeverity severity, string expected)
    {
        Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError { Severity = severity, Message = "m" }),
            Is.EqualTo(expected + " [Unclassified]\nm"));
    }

    [TestCase(SemanticDiagnosticCode.Unclassified, "Unclassified")]
    [TestCase(SemanticDiagnosticCode.UnknownFunction, "UnknownFunction")]
    [TestCase(SemanticDiagnosticCode.UnknownVariable, "UnknownVariable")]
    [TestCase(SemanticDiagnosticCode.InvalidMechanism, "InvalidMechanism")]
    [TestCase(SemanticDiagnosticCode.FunctionContractViolation, "FunctionContractViolation")]
    [TestCase(SemanticDiagnosticCode.FunctionExecutionFailed, "FunctionExecutionFailed")]
    [TestCase(SemanticDiagnosticCode.InvalidDeclaration, "InvalidDeclaration")]
    [TestCase(SemanticDiagnosticCode.InvalidParameter, "InvalidParameter")]
    [TestCase(SemanticDiagnosticCode.KeyUsageConflict, "KeyUsageConflict")]
    [TestCase(SemanticDiagnosticCode.KeyTypeConflict, "KeyTypeConflict")]
    [TestCase(SemanticDiagnosticCode.DesParityViolation, "DesParityViolation")]
    [TestCase(SemanticDiagnosticCode.WeakDes3Key, "WeakDes3Key")]
    public void FormatsDiagnosticCode(SemanticDiagnosticCode code, string expected)
    {
        Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError { DiagnosticCode = code, Message = "m" }),
            Is.EqualTo("Error [" + expected + "]\nm"));
    }

    [Test]
    public void FullLocationHasFixedOrderAndOverridesLegacyNames()
    {
        var diagnostic = new SemanticError
        {
            Location = new DiagnosticLocation(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
                2, 3, 1, "Decrypt", "AES-ECB", "key"),
            FunctionName = "legacy", Identifier = "legacyKey", Message = "m"
        };
        Assert.That(SemanticDiagnosticFormatter.Format(diagnostic), Is.EqualTo(
            "Error [Unclassified] | ExecutionId=01234567-89ab-cdef-0123-456789abcdef" +
            " | Statement=2 | Operation=3 | ParentOperation=1 | Function=Decrypt | Mechanism=AES-ECB | Identifier=key\nm"));
    }

    [Test]
    public void PartialLocationOmitsMissingFields()
    {
        Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError
        {
            Location = new DiagnosticLocation(StatementIndex: 2, Mechanism: "AES-ECB"), Message = "m"
        }), Is.EqualTo("Error [Unclassified] | Statement=2 | Mechanism=AES-ECB\nm"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AbsentAndEmptyLocationsSupportLegacyFallbacks(bool emptyLocation)
    {
        Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError
        {
            Location = emptyLocation ? new DiagnosticLocation() : null,
            FunctionName = "Decrypt", Identifier = "k", Message = "m"
        }), Is.EqualTo("Error [Unclassified] | Function=Decrypt | Identifier=k\nm"));
    }

    [Test]
    public void EmptyLocationNamesFallBackAndEmptyMechanismIsOmitted()
    {
        Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError
        {
            Location = new DiagnosticLocation(FunctionName: "", Identifier: "", Mechanism: ""),
            FunctionName = "Decrypt", Identifier = "k", Message = "m"
        }), Is.EqualTo("Error [Unclassified] | Function=Decrypt | Identifier=k\nm"));
    }

    [Test]
    public void ContractCodeAndCallAreSeparateDetails()
    {
        Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError
        {
            DiagnosticCode = SemanticDiagnosticCode.FunctionContractViolation,
            ErrorCode = FunctionContractError.UnsupportedFunction, FunctionCall = "Unknown()", Message = "m"
        }), Is.EqualTo("Error [FunctionContractViolation]\nm\nContract: UnsupportedFunction\nCall: Unknown()"));
    }

    [TestCase("Grüsse 日本語 🔑\nPAN=4111111111111111\r\nKEY=00112233445566778899AABBCCDDEEFF PIN=1234")]
    [TestCase("  plaintext  ")]
    [TestCase("m\n")]
    public void MessageContentsArePreservedExactly(string message)
    {
        Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError { Message = message }),
            Is.EqualTo("Error [Unclassified]\n" + message));
    }

    [Test]
    public void EmptyLocationWithoutLegacyFieldsIsValid()
    {
        Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError { Location = new DiagnosticLocation() }),
            Is.EqualTo("Error [Unclassified]\nNo diagnostic message."));
    }

    [Test]
    public void MultipleDiagnosticsPreserveOrderAndRepeatedReferences()
    {
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning, Message = "first" };
        var error = new SemanticError { Message = "second" };
        Assert.That(SemanticDiagnosticFormatter.FormatMany(new[] { warning, error, warning }), Is.EqualTo(
            "Warning [Unclassified]\nfirst\n\nError [Unclassified]\nsecond\n\nWarning [Unclassified]\nfirst"));
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public void FormatManyPreservesMessageLineEndingBeforeExactSeparator(string lineEnding)
    {
        var first = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning, Message = "first" + lineEnding };
        var second = new SemanticError { Message = "second" };

        Assert.That(SemanticDiagnosticFormatter.FormatMany(new[] { first, second }), Is.EqualTo(
            "Warning [Unclassified]\nfirst" + lineEnding + "\n\nError [Unclassified]\nsecond"));
    }

    [Test]
    public void SourceIsEnumeratedExactlyOnce()
    {
        var source = new OnceEnumerable(new SemanticError { Message = "a" }, new SemanticError { Message = "b" });
        Assert.That(SemanticDiagnosticFormatter.FormatMany(source),
            Is.EqualTo("Error [Unclassified]\na\n\nError [Unclassified]\nb"));
        Assert.That(source.EnumerationCount, Is.EqualTo(1));
    }

    [Test]
    public void FormattingDoesNotMutateObjectsOrSnapshot()
    {
        var location = new DiagnosticLocation(StatementIndex: 2, FunctionName: "f");
        var diagnostic = new SemanticError
        {
            Severity = SemanticDiagnosticSeverity.Warning, DiagnosticCode = SemanticDiagnosticCode.InvalidParameter,
            Location = location, Message = "m", Type = "type", Identifier = "id", FunctionName = "legacy",
            FunctionCall = "f()", Value = "secret", ErrorCode = FunctionContractError.UnsupportedFunction
        };
        var sink = new SemanticDiagnosticSink();
        sink.Report(diagnostic);
        var snapshot = sink.GetSnapshot();
        SemanticDiagnosticFormatter.FormatMany(snapshot);
        Assert.Multiple(() =>
        {
            Assert.That(snapshot, Has.Count.EqualTo(1));
            Assert.That(snapshot[0], Is.SameAs(diagnostic));
            Assert.That(diagnostic.Location, Is.SameAs(location));
            Assert.That(diagnostic.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
            Assert.That(diagnostic.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.InvalidParameter));
            Assert.That(diagnostic.Message, Is.EqualTo("m"));
            Assert.That(diagnostic.Type, Is.EqualTo("type"));
            Assert.That(diagnostic.Identifier, Is.EqualTo("id"));
            Assert.That(diagnostic.FunctionName, Is.EqualTo("legacy"));
            Assert.That(diagnostic.FunctionCall, Is.EqualTo("f()"));
            Assert.That(diagnostic.Value, Is.EqualTo("secret"));
            Assert.That(diagnostic.ErrorCode, Is.EqualTo(FunctionContractError.UnsupportedFunction));
        });
    }

    [Test]
    public void ReformattingUsesCurrentContentsWithoutCaching()
    {
        var diagnostic = new SemanticError { Message = "old" };
        Assert.That(SemanticDiagnosticFormatter.Format(diagnostic), Is.EqualTo("Error [Unclassified]\nold"));
        diagnostic.Message = "new";
        diagnostic.Severity = SemanticDiagnosticSeverity.Warning;
        diagnostic.DiagnosticCode = SemanticDiagnosticCode.InvalidParameter;
        diagnostic.Location = new DiagnosticLocation(StatementIndex: 3);
        Assert.That(SemanticDiagnosticFormatter.Format(diagnostic),
            Is.EqualTo("Warning [InvalidParameter] | Statement=3\nnew"));
    }

    [TestCase("de-CH")]
    [TestCase("ar-SA")]
    [TestCase("tr-TR")]
    public void NumbersAndUnknownEnumsAreCultureIndependent(string cultureName)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var culture = (CultureInfo)new CultureInfo(cultureName).Clone();
            culture.NumberFormat.NegativeSign = "NEG";
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError
            {
                Severity = (SemanticDiagnosticSeverity)(-42), DiagnosticCode = (SemanticDiagnosticCode)99,
                ErrorCode = (FunctionContractError)(-7), Message = "m",
                Location = new DiagnosticLocation(Guid.Empty, -2, 0, 1234)
            }), Is.EqualTo("-42 [99] | ExecutionId=00000000-0000-0000-0000-000000000000" +
                " | Statement=-2 | Operation=0 | ParentOperation=1234\nm\nContract: -7"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Test]
    public void EmptyCollectionIsEmptyText()
    {
        Assert.That(SemanticDiagnosticFormatter.FormatMany(Array.Empty<SemanticError>()), Is.Empty);
    }

    [Test]
    public void NullInputsHaveDefinedArgumentExceptions()
    {
        Assert.That(Assert.Throws<ArgumentNullException>(() => SemanticDiagnosticFormatter.Format(null!))!.ParamName,
            Is.EqualTo("diagnostic"));
        Assert.That(Assert.Throws<ArgumentNullException>(() => SemanticDiagnosticFormatter.FormatMany(null!))!.ParamName,
            Is.EqualTo("diagnostics"));
        Assert.That(Assert.Throws<ArgumentException>(() => SemanticDiagnosticFormatter.FormatMany(
            new[] { new SemanticError(), null! }))!.ParamName, Is.EqualTo("diagnostics"));
    }

    [Test]
    public void LegacyTypeAndValueAreNotAddedAsAnObjectDump()
    {
        Assert.That(SemanticDiagnosticFormatter.Format(new SemanticError { Type = "type", Value = "secret", Message = "m" }),
            Is.EqualTo("Error [Unclassified]\nm"));
    }

    private sealed class OnceEnumerable(params SemanticError[] values) : IEnumerable<SemanticError>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<SemanticError> GetEnumerator()
        {
            if (++EnumerationCount != 1)
                throw new InvalidOperationException("Source was enumerated more than once.");
            return ((IEnumerable<SemanticError>)values).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
