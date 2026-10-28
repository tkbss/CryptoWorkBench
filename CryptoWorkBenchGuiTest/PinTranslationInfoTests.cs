using CryptoScript.CryptoAlgorithm.PINBLOCK;
using CryptoScript.Documentation;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using ColorTextBlock.Avalonia;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Views;

namespace CryptoWorkBenchGuiTest;

public class PinTranslationInfoTests
{
    private static IEnumerable<TestCaseData> MatrixLinks() =>
        PinBlockTranslationRules.Entries.Select(r => new TestCaseData(r.SourceFormat, r.TargetFormat));

    [TestCaseSource(nameof(MatrixLinks))]
    [NonParallelizable]
    public async Task RenderedMatrixLinkAndBackNavigateThroughActualView(int source, int target)
    {
        var session = GuiTestSession.Session;
        await session.Dispatch(() =>
        {
            var model = new InfoViewModel(null, new FileInfoDocumentationProvider());
            var view = new InfoView { DataContext = model };
            OutputOperations.InfoEvent += model.SetInfoText;
            try
            {
                new CryptoScriptRunner().Execute(ParserBuilder.StringBuild("Info(pintranslations)").program());
                string matrix = model.InfoText;
                Assert.That(matrix, Does.Contain("PIN"));
                string uri = InfoDocumentUri.ToCanonicalString(InfoDocumentId.CreatePinTranslation(source, target));
                var links = view.GetLogicalDescendants().OfType<CHyperlink>().ToArray();
                Assert.That(links.Count(l => l.CommandParameter?.StartsWith("cryptoscript-info:") == true), Is.EqualTo(25));
                var link = links.Single(l => l.CommandParameter == uri);
                // Execute the callback installed by the Markdown engine on the rendered hyperlink.
                Assert.That(link.Command, Is.Not.Null);
                link.Command!.Invoke(link.CommandParameter!);
                Assert.That(model.InfoText, Does.Contain($"# PIN translation {source} to {target}"));
                var back = view.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "← Back"));
                Assert.That(back.IsVisible, Is.True);
                Assert.That(back.Command!.CanExecute(back.CommandParameter), Is.True);
                back.Command.Execute(back.CommandParameter);
                Assert.That(model.InfoText, Is.EqualTo(matrix));
                Assert.That(view.GetLogicalDescendants().OfType<CHyperlink>().Any(l => l.CommandParameter == uri), Is.True);
            }
            finally { OutputOperations.InfoEvent -= model.SetInfoText; }
        }, CancellationToken.None);
    }

    [Test]
    public void CompletionAndEveryDetailNavigateBackToMatrix()
    {
        var completion = new InfoCompletionProvider();
        Assert.That(completion.GetCompletionItems("Info(", 5), Does.Contain("pintranslations"));
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocumentation("pintranslations", out var overview), Is.True);
        var model = new InfoViewModel(null, provider);
        model.SetInfoText(overview);
        string initial = model.InfoText;
        foreach (var rule in PinBlockTranslationRules.Entries)
        {
            model.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(
                InfoDocumentId.CreatePinTranslation(rule.SourceFormat, rule.TargetFormat)));
            Assert.That(model.InfoText, Does.Contain($"# PIN translation {rule.SourceFormat} to {rule.TargetFormat}"));
            Assert.That(model.CanNavigateBack, Is.True);
            model.NavigateBackCommand.Execute();
            Assert.That(model.InfoText, Is.EqualTo(initial));
        }
    }
}
