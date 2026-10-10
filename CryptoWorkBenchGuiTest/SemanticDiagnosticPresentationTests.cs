using System.Collections.Generic;
using CryptoScript.ErrorListner;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class SemanticDiagnosticPresentationTests
{
    [Test]
    public void CollectionIsReadOnlyAndNotifiesVisibility()
    {
        var status = new StatusViewModel();
        int changes = 0;
        status.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(status.HasDiagnostics)) ++changes; };
        Assert.That(status.HasDiagnostics, Is.False);
        status.AppendDiagnostics(new[] { new SemanticError() });
        Assert.That(status.HasDiagnostics, Is.True);
        Assert.Throws<NotSupportedException>(() => ((IList<CryptoWorkBenchAvalonia.Models.DiagnosticDisplayItem>)status.Diagnostics).Clear());
        status.ClearDiagnostics();
        Assert.That(status.HasDiagnostics, Is.False);
        Assert.That(changes, Is.EqualTo(2));
    }

    [Test]
    public void PreservesOrderFormatterTextSeverityAndBookContextWithoutMutatingLocation()
    {
        var status = new StatusViewModel();
        var location = new DiagnosticLocation(StatementIndex: 2, OperationId: 2, ParentOperationId: 1,
            FunctionName: "Unwrap", Mechanism: "WRAP-AES-TR31");
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning, Message = "a\nsecret", Location = location };
        var error = new SemanticError { Message = "b" };
        status.AppendDiagnostics(new[] { warning, error }, scriptBookLine: 7);
        Assert.Multiple(() =>
        {
            Assert.That(status.Diagnostics.Select(item => item.Text),
                Is.EqualTo(new[] { SemanticDiagnosticFormatter.Format(warning), SemanticDiagnosticFormatter.Format(error) }));
            Assert.That(status.Diagnostics[0].IsWarning, Is.True);
            Assert.That(status.Diagnostics[0].IsError, Is.False);
            Assert.That(status.Diagnostics[1].IsError, Is.True);
            Assert.That(status.Diagnostics.Select(item => item.ScriptBookLine), Is.EqualTo(new int?[] { 7, 7 }));
            Assert.That(warning.Location, Is.SameAs(location));
            Assert.That(warning.Location.StatementIndex, Is.EqualTo(2));
        });
        var captured = status.Diagnostics[0].Text;
        warning.Message = "changed";
        warning.Severity = SemanticDiagnosticSeverity.Error;
        Assert.That(status.Diagnostics[0].Text, Is.EqualTo(captured));
        Assert.That(status.Diagnostics[0].IsWarning, Is.True);
    }

    [Test]
    public void ExceptionDeduplicationUsesIdentityAndKeepsDifferentEqualMessages()
    {
        var status = new StatusViewModel();
        var first = new SemanticError { Message = "same" };
        var second = new SemanticError { Message = "same" };
        status.AppendDiagnostics(new[] { first, second }, second);
        Assert.That(status.Diagnostics, Has.Count.EqualTo(2));
        status.ClearDiagnostics();
        status.AppendDiagnostics(new[] { first }, second);
        Assert.That(status.Diagnostics, Has.Count.EqualTo(2));
        Assert.That(status.Diagnostics.Select(item => item.Text),
            Is.EqualTo(new[] { SemanticDiagnosticFormatter.Format(first), SemanticDiagnosticFormatter.Format(second) }));
    }

    [Test]
    public void EmptyAppendAndStatusChangesDoNotRemoveDiagnostics()
    {
        var status = new StatusViewModel();
        status.AppendDiagnostics(new[] { new SemanticError { Severity = SemanticDiagnosticSeverity.Warning } });
        var item = status.Diagnostics[0];
        status.AppendDiagnostics(Array.Empty<SemanticError>());
        status.StatusString = "Script book loaded";
        Assert.That(status.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(status.Diagnostics[0], Is.SameAs(item));
    }
}
