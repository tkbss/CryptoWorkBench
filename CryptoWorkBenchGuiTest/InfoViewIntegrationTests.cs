using Avalonia.Controls;
using Avalonia.LogicalTree;
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
        viewModel.CanNavigateBack.Should().BeTrue();
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

    [Test]
    public void BackButton_BindsVisibilityAndCommandToInternalNavigationState()
    {
        const string mechanisms = "# Mechnisms\n\n- AES-CBC : documented";
        const string documentation = "# MECHANISM AES-CBC\nDocumentation";
        var viewModel = new InfoViewModel(
            null,
            new DocumentationProviderFake(mechanisms, documentation));
        viewModel.SetInfoText(mechanisms);
        var view = new InfoView { DataContext = viewModel };
        Button backButton = view.GetLogicalDescendants()
            .OfType<Button>()
            .Single(button => Equals(button.Content, "← Back"));

        backButton.IsVisible.Should().BeFalse();
        backButton.Command.Should().BeSameAs(viewModel.NavigateBackCommand);

        viewModel.OpenInfoLinkCommand.Execute(
            "cryptoscript-info://mechanism/AES-CBC");

        backButton.IsVisible.Should().BeTrue();
        backButton.IsEnabled.Should().BeTrue();
        backButton.Command!.Execute(backButton.CommandParameter);

        viewModel.InfoText.Should().Contain("# Mechnisms");
        backButton.IsVisible.Should().BeFalse();
        backButton.Command.CanExecute(backButton.CommandParameter).Should().BeFalse();
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

        public bool HasDocument(InfoDocumentId id) =>
            id == InfoDocumentId.CreateMechanism("AES-CBC");

        public bool TryGetDocument(InfoDocumentId id, out string documentation)
        {
            documentation = HasDocument(id) ? _documentation : string.Empty;
            return documentation.Length > 0;
        }
    }
}
