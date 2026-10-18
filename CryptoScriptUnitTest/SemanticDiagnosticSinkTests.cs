using CryptoScript.ErrorListner;
using CryptoScript.Model;
using NUnit.Framework;

namespace CryptoScriptUnitTest;

public class SemanticDiagnosticSinkTests
{
    [Test]
    public void ExistingErrorsKeepDefaultsAndFunctionContractCode()
    {
        var error = new SemanticError { ErrorCode = FunctionContractError.UnsupportedFunction };

        Assert.Multiple(() =>
        {
            Assert.That(error.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Error));
            Assert.That(error.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.Unclassified));
            Assert.That(error.Location, Is.Null);
            Assert.That(error.ErrorCode, Is.EqualTo(FunctionContractError.UnsupportedFunction));
        });
    }

    [Test]
    public void DiagnosticEnumsHaveStableValues()
    {
        Assert.That(Enum.GetValues<SemanticDiagnosticSeverity>().Select(value => (int)value),
            Is.EqualTo(new[] { 0, 1 }));
        Assert.That(Enum.GetValues<SemanticDiagnosticCode>().Select(value => (int)value),
            Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }));
        Assert.That((int)SemanticDiagnosticSeverity.Error, Is.Zero);
        Assert.That((int)SemanticDiagnosticSeverity.Warning, Is.EqualTo(1));
        Assert.That((int)SemanticDiagnosticCode.Unclassified, Is.Zero);
        Assert.That((int)SemanticDiagnosticCode.KeyCheckValueNotFound, Is.EqualTo(12));
        Assert.That((int)SemanticDiagnosticCode.KeyCheckValueAmbiguous, Is.EqualTo(13));
        Assert.That((int)SemanticDiagnosticCode.UnknownFunction, Is.EqualTo(1));
        Assert.That((int)SemanticDiagnosticCode.UnknownVariable, Is.EqualTo(2));
        Assert.That((int)SemanticDiagnosticCode.InvalidMechanism, Is.EqualTo(3));
        Assert.That((int)SemanticDiagnosticCode.FunctionContractViolation, Is.EqualTo(4));
        Assert.That((int)SemanticDiagnosticCode.FunctionExecutionFailed, Is.EqualTo(5));
        Assert.That((int)SemanticDiagnosticCode.InvalidDeclaration, Is.EqualTo(6));
        Assert.That((int)SemanticDiagnosticCode.InvalidParameter, Is.EqualTo(7));
        Assert.That((int)SemanticDiagnosticCode.KeyUsageConflict, Is.EqualTo(8));
        Assert.That((int)SemanticDiagnosticCode.KeyTypeConflict, Is.EqualTo(9));
        Assert.That((int)SemanticDiagnosticCode.DesParityViolation, Is.EqualTo(10));
        Assert.That((int)SemanticDiagnosticCode.WeakDes3Key, Is.EqualTo(11));
    }

    [Test]
    public void ReportsBothSeveritiesInOrderWithoutThrowingAndPreservesIdentity()
    {
        ISemanticDiagnosticSink sink = new SemanticDiagnosticSink();
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning };
        var error = new SemanticError();

        Assert.DoesNotThrow(() =>
        {
            sink.Report(warning);
            sink.Report(error);
        });
        var snapshot = ((SemanticDiagnosticSink)sink).GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot, Has.Count.EqualTo(2));
            Assert.That(snapshot[0], Is.SameAs(warning));
            Assert.That(snapshot[1], Is.SameAs(error));
            Assert.That(snapshot.Where(item => item.Severity == SemanticDiagnosticSeverity.Warning),
                Is.EqualTo(new[] { warning }));
            Assert.That(snapshot.Where(item => item.Severity == SemanticDiagnosticSeverity.Error),
                Is.EqualTo(new[] { error }));
        });
    }

    [Test]
    public void DeduplicatesByIdentityButRetainsDistinctEqualContent()
    {
        var sink = new SemanticDiagnosticSink();
        var first = new ValueEqualSemanticError { Message = "Operation diagnostic" };
        var second = new ValueEqualSemanticError { Message = first.Message };

        Assert.Multiple(() =>
        {
            Assert.That(first.Equals(second), Is.True);
            Assert.That(second.Equals(first), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(ReferenceEquals(first, second), Is.False);
        });

        sink.Report(first);
        sink.Report(second);
        sink.Report(first);

        var snapshot = sink.GetSnapshot();
        Assert.That(snapshot, Has.Count.EqualTo(2));
        Assert.That(snapshot[0], Is.SameAs(first));
        Assert.That(snapshot[1], Is.SameAs(second));
    }

    [Test]
    public void SinkInstancesAreIsolated()
    {
        var first = new SemanticDiagnosticSink();
        var second = new SemanticDiagnosticSink();
        first.Report(new SemanticError());

        Assert.That(first.GetSnapshot(), Has.Count.EqualTo(1));
        Assert.That(second.GetSnapshot(), Is.Empty);
    }

    [Test]
    public void LocationSupportsAbsentPartialAndCompleteContext()
    {
        var absent = new DiagnosticLocation();
        var partial = new DiagnosticLocation(FunctionName: "Decrypt");
        var executionId = Guid.NewGuid();
        var complete = new DiagnosticLocation(executionId, 2, 3, 1, "Decrypt", "AES-ECB", "key");
        var error = new SemanticError { Location = complete };

        Assert.Multiple(() =>
        {
            Assert.That(absent.ExecutionId, Is.Null);
            Assert.That(absent.StatementIndex, Is.Null);
            Assert.That(absent.OperationId, Is.Null);
            Assert.That(absent.ParentOperationId, Is.Null);
            Assert.That(absent.FunctionName, Is.Null);
            Assert.That(absent.Mechanism, Is.Null);
            Assert.That(absent.Identifier, Is.Null);
            Assert.That(partial.StatementIndex, Is.Null);
            Assert.That(partial.FunctionName, Is.EqualTo("Decrypt"));
            Assert.That(error.Location, Is.SameAs(complete));
            Assert.That(complete.ExecutionId, Is.EqualTo(executionId));
            Assert.That(complete.StatementIndex, Is.EqualTo(2));
            Assert.That(complete.OperationId, Is.EqualTo(3));
            Assert.That(complete.ParentOperationId, Is.EqualTo(1));
            Assert.That(complete.FunctionName, Is.EqualTo("Decrypt"));
            Assert.That(complete.Mechanism, Is.EqualTo("AES-ECB"));
            Assert.That(complete.Identifier, Is.EqualTo("key"));
        });
    }

    [Test]
    public void SnapshotMembershipIsReadOnlyAndIndependentOfLaterReports()
    {
        var sink = new SemanticDiagnosticSink();
        var first = new SemanticError();
        sink.Report(first);
        var snapshot = sink.GetSnapshot();
        sink.Report(new SemanticError());

        Assert.That(snapshot, Has.Count.EqualTo(1));
        Assert.That(snapshot[0], Is.SameAs(first));
        Assert.That(sink.GetSnapshot(), Has.Count.EqualTo(2));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<SemanticError>)snapshot).Add(new SemanticError()));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<SemanticError>)snapshot)[0] = new SemanticError());
    }

    [Test]
    public void ReportingDoesNotModifyLegacyErrorListOrExceptionSemantics()
    {
        var error = new SemanticError();
        var runner = new CryptoScriptRunner { SemanticErrors = new List<SemanticError> { error } };
        var originalList = runner.SemanticErrors;
        var exception = new SemanticErrorException { SemanticError = error };
        var sink = new SemanticDiagnosticSink();

        Assert.DoesNotThrow(() => sink.Report(error));
        Assert.DoesNotThrow(() => sink.Report(new SemanticError
        {
            Severity = SemanticDiagnosticSeverity.Warning
        }));
        var thrown = Assert.Throws<SemanticErrorException>(() => throw exception);

        Assert.Multiple(() =>
        {
            Assert.That(runner.SemanticErrors, Is.SameAs(originalList));
            Assert.That(originalList, Has.Count.EqualTo(1));
            Assert.That(originalList[0], Is.SameAs(error));
            Assert.That(thrown, Is.SameAs(exception));
            Assert.That(thrown!.SemanticError, Is.SameAs(error));
        });
    }

    [Test]
    public void RejectsNullWithoutChangingTheCollection()
    {
        var sink = new SemanticDiagnosticSink();
        Assert.Throws<ArgumentNullException>(() => sink.Report(null!));
        Assert.That(sink.GetSnapshot(), Is.Empty);
    }

    private sealed class ValueEqualSemanticError : SemanticError
    {
        public override bool Equals(object? obj) =>
            obj is ValueEqualSemanticError other &&
            StringComparer.Ordinal.Equals(Message, other.Message);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Message);
    }
}
