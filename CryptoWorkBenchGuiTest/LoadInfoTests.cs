using CryptoScript.Documentation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class LoadInfoTests
{
    [Test]
    public void ExistingInfoNavigationOpensLoadAndReturnsToOverview()
    {
        var provider = new FileInfoDocumentationProvider();
        var vm = new InfoViewModel(null, provider);
        var overview = InfoDocumentId.CreateFunctionsOverview();
        Assert.That(provider.TryGetDocument(overview, out var functions), Is.True);
        Assert.That(functions, Does.Contain("cryptoscript-info://function/Load"));
        vm.SetInfoText(functions);
        Assert.That(provider.TryGetDocument(InfoDocumentId.CreateFunction("Load"), out var load), Is.True);
        vm.OpenInfoLinkCommand.Execute("cryptoscript-info://function/Load");
        Assert.That(vm.InfoText, Is.EqualTo(load));
        Assert.That(vm.CanNavigateBack, Is.True);
        vm.NavigateBackCommand.Execute();
        Assert.That(vm.InfoText, Is.EqualTo(functions));
    }
}
