using Avalonia.Controls;
using CryptoScript.Documentation;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;
using FluentAssertions;
using Markdown.Avalonia;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class InfoViewIntegrationTests
{
    [Test]
    public void MarkdownEngine_WithInfoViewModel_ForwardsInternalLinksToViewModelCommand()
    {
        const string mechanisms = "# Mechnisms\n\n- AES-CBC : documented";
        const string documentation = "# MECHANISM AES-CBC\nDocumentation";
        var viewModel = new InfoViewModel(
            null,
            new DocumentationProviderFake(mechanisms, documentation));
        viewModel.SetInfoText(mechanisms);
        var view = new InfoView { DataContext = viewModel };
        var markdownViewer = view.FindControl<MarkdownScrollViewer>("InfoMarkdownViewer");

        markdownViewer.Should().NotBeNull();
        ((global::Markdown.Avalonia.Markdown)markdownViewer!.Engine).HyperlinkCommand!
            .Execute("cryptoscript-info://mechanism/AES-CBC");

        viewModel.InfoText.Should().Be(documentation);
        viewModel.IsInternalMechanismDetail.Should().BeTrue();
    }

    [Test]
    public void MarkdownEngine_WithInfoViewModel_RetainsDefaultHandlingForExternalLinks()
    {
        var viewModel = new InfoViewModel(
            null,
            new DocumentationProviderFake("# Mechnisms", "documentation"));
        var view = new InfoView { DataContext = viewModel };
        var markdownViewer = view.FindControl<MarkdownScrollViewer>("InfoMarkdownViewer");

        bool canOpenExternalLink = ((global::Markdown.Avalonia.Markdown)markdownViewer!.Engine).HyperlinkCommand!
            .CanExecute("https://example.test/documentation");

        canOpenExternalLink.Should().BeTrue();
    }

    private sealed class DocumentationProviderFake : IInfoDocumentationProvider
    {
        private readonly string _mechanisms;
        private readonly string _documentation;

        public DocumentationProviderFake(string mechanisms, string documentation)
        {
            _mechanisms = mechanisms;
            _documentation = documentation;
        }

        public bool HasDocumentation(string name) => name == "AES-CBC";

        public bool TryGetDocumentation(string name, out string documentation)
        {
            documentation = name switch
            {
                "mechanisms" => _mechanisms,
                "AES-CBC" => _documentation,
                _ => string.Empty
            };
            return documentation.Length > 0;
        }
    }
}
