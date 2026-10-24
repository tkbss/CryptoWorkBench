using CryptoScript.Documentation;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class FileHashInfoTests
{
    [Test]
    public void CompletionOverlayHelpAndNavigation()
    {
        Assert.That(new InfoCompletionProvider().GetCompletionItems("Info(", 5), Does.Contain("Hash"));
        var overlays = new FunctionOverlayDictionaryModel().GetOverlays("Hash(", 5);
        Assert.That(overlays, Is.Not.Null);
        Assert.That(new FunctionOverlayDictionaryModel().functionOverloads["Hash"].Select(x => x.description),
            Has.Some.Contains("PATH"));
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocument(InfoDocumentId.CreateFunction("Hash"), out string page), Is.True);
        Assert.That(page, Does.Contain("65,536").And.Contain("uppercase"));
        var vm = new InfoViewModel(null, provider);
        vm.SetInfoText("Start");
        vm.OpenInfoLinkCommand.Execute("cryptoscript-info://function/Hash");
        Assert.That(vm.InfoText, Is.EqualTo(page));
        vm.NavigateBackCommand.Execute();
        Assert.That(vm.InfoText, Is.EqualTo("Start"));
    }
}
