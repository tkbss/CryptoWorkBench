using CryptoScript.Documentation;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class CreateHeaderInfoTests
{
    [Test]
    public void CompletionAndOverlayExposeBuilderAndBothTopics()
    {
        var completion = new InfoCompletionProvider();
        Assert.That(completion.GetCompletionItems("Info(", 5),
            Is.SupersetOf(new[] { "tr31", "CreateHeader", "tr31attributes" }));
        Assert.That(new FunctionOverlayDictionaryModel().GetOverlays("CreateHeader(", 13), Is.Not.Null);
    }

    [Test]
    public void DeployedDocumentsNavigateAndReturnInOrder()
    {
        var provider = new FileInfoDocumentationProvider();
        var vm = new InfoViewModel(null, provider);
        vm.SetInfoText("Start");
        string[] uris = ["cryptoscript-info://overview/tr31",
            "cryptoscript-info://function/CreateHeader", "cryptoscript-info://overview/tr31attributes"];
        var pages = new List<string> { "Start" };
        foreach (string uri in uris)
        {
            Assert.That(InfoDocumentUri.TryParse(uri, out var id), Is.True);
            Assert.That(provider.TryGetDocument(id!, out string page), Is.True);
            vm.OpenInfoLinkCommand.Execute(uri);
            Assert.That(vm.InfoText, Is.EqualTo(page));
            Assert.That(vm.CanNavigateBack, Is.True);
            pages.Add(page);
        }
        for (int i = pages.Count - 2; i >= 0; i--)
        {
            vm.NavigateBackCommand.Execute();
            Assert.That(vm.InfoText, Is.EqualTo(pages[i]));
        }
        Assert.That(vm.CanNavigateBack, Is.False);
    }
}
