using CryptoScript.Documentation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class Ep2DataEncryptionInfoTests
{
    [Test]
    public void PublishedProfileAndLinkedMechanismsNavigateAndReturn()
    {
        const string mechanism = "EP2-DATA-ENCRYPTION";
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocumentation("mechanisms", out string overview), Is.True);
        var model = new InfoViewModel(null, provider);
        model.SetInfoText(overview);
        Assert.That(model.InfoText, Does.Contain("cryptoscript-info://mechanism/" + mechanism));
        string linkedOverview = model.InfoText;
        foreach (var id in new[] {
            InfoDocumentId.CreateMechanism(mechanism),
            InfoDocumentId.CreateMechanism("KDF-EP2-SESSION"),
            InfoDocumentId.CreateMechanism("AES-CTR")
        })
        {
            model.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(id));
            Assert.That(model.InfoText, Does.Contain("# MECHANISM " + id.Mechanism));
            Assert.That(model.InfoText, Is.Not.EqualTo(linkedOverview));
            Assert.That(model.CanNavigateBack, Is.True);
            model.NavigateBackCommand.Execute();
            Assert.That(model.InfoText, Is.EqualTo(linkedOverview));
        }
    }
}
