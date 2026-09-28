using CryptoScript.Documentation;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.ViewModels;
using FluentAssertions;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class InfoViewModelTests
{
    private const string Mechanisms = "# Mechnisms\n\n- AES-CBC : documented\n- RSA-PSS : undocumented";
    private const string AesCbc = "# MECHANISM AES-CBC\nDocumentation";

    [Test]
    public void SetInfoText_WithMechanismHeading_MarksTextAsMechanismDocumentation()
    {
        var sut = new InfoViewModel();
        const string text = "  # MECHANISM AES-GCM\nDocumentation";

        sut.IsMechanismDocumentation.Should().BeFalse();

        sut.SetInfoText(text);

        sut.InfoText.Should().Be(text);
        sut.IsMechanismDocumentation.Should().BeTrue();
    }

    [Test]
    public void SetInfoText_AfterMechanismDocumentationWithNormalText_ClearsMechanismDocumentationFlag()
    {
        var sut = new InfoViewModel();
        const string mechanismText = "# MECHANISM AES-GCM\nDocumentation";
        const string normalText = "General cryptography documentation";

        sut.SetInfoText(mechanismText);
        sut.IsMechanismDocumentation.Should().BeTrue();

        sut.SetInfoText(normalText);

        sut.InfoText.Should().Be(normalText);
        sut.IsMechanismDocumentation.Should().BeFalse();
    }

    [Test]
    public void InfoBack_AfterSettingTwoTexts_RestoresPreviousText()
    {
        var history = new HistoryService();
        var sut = new InfoViewModel(history);
        const string firstText = "First information text";
        const string secondText = "Second information text";

        sut.SetInfoText(firstText);
        sut.SetInfoText(secondText);

        history.InfoBack();

        sut.InfoText.Should().Be(firstText);
    }

    [Test]
    public void NavigateToMechanism_ExistingDocumentation_ShowsInternalDetailWithoutChangingHistory()
    {
        var history = new HistoryServiceFake();
        var sut = CreateSut(history);
        sut.SetInfoText(Mechanisms);
        int historyEntriesBeforeNavigation = history.AddedInfo.Count;

        bool navigated = sut.NavigateToMechanism("AES-CBC");

        navigated.Should().BeTrue();
        sut.InfoText.Should().Be(AesCbc);
        sut.IsInternalMechanismDetail.Should().BeTrue();
        history.AddedInfo.Should().HaveCount(historyEntriesBeforeNavigation);
        history.AddedInfo.Should().Equal(Mechanisms);
    }

    [Test]
    public void ShowMechanisms_AfterInternalNavigation_RestoresLinkedOverviewWithoutChangingHistory()
    {
        var history = new HistoryServiceFake();
        var sut = CreateSut(history);
        sut.SetInfoText(Mechanisms);
        sut.NavigateToMechanism("AES-CBC");
        int historyEntriesBeforeReturn = history.AddedInfo.Count;

        sut.ShowMechanismsCommand.Execute();

        sut.InfoText.Should().Contain(
            "[AES-CBC](cryptoscript-info://mechanism/AES-CBC)");
        sut.InfoText.Should().Contain("- RSA-PSS : undocumented");
        sut.IsInternalMechanismDetail.Should().BeFalse();
        history.AddedInfo.Should().HaveCount(historyEntriesBeforeReturn);
    }

    [Test]
    public void SetInfoText_DirectMechanismCommand_DoesNotCreateInternalReturnState()
    {
        var sut = CreateSut();

        sut.SetInfoText(AesCbc);

        sut.IsInternalMechanismDetail.Should().BeFalse();
        sut.ShowMechanismsCommand.Execute();
        sut.InfoText.Should().Be(AesCbc);
    }

    [Test]
    public void HistoryNavigation_DuringInternalDetail_EndsInternalNavigationState()
    {
        var history = new HistoryServiceFake();
        var sut = CreateSut(history);
        sut.SetInfoText(Mechanisms);
        sut.NavigateToMechanism("AES-CBC");

        history.ShowFromHistory("Earlier information");

        sut.InfoText.Should().Be("Earlier information");
        sut.IsInternalMechanismDetail.Should().BeFalse();
        sut.NavigateToMechanism("AES-CBC").Should().BeFalse();
    }

    [TestCase("UNKNOWN")]
    [TestCase("RSA-PSS")]
    [TestCase("../AES-CBC")]
    public void NavigateToMechanism_UnknownOrUndocumentedMechanism_DoesNotNavigate(string mechanism)
    {
        var sut = CreateSut();
        sut.SetInfoText(Mechanisms);
        string overview = sut.InfoText;

        bool navigated = sut.NavigateToMechanism(mechanism);

        navigated.Should().BeFalse();
        sut.InfoText.Should().Be(overview);
        sut.IsInternalMechanismDetail.Should().BeFalse();
    }

    [Test]
    public void OpenInfoLinkCommand_InternalMechanismUri_NavigatesToDocumentation()
    {
        var sut = CreateSut();
        sut.SetInfoText(Mechanisms);

        sut.OpenInfoLinkCommand.Execute("cryptoscript-info://mechanism/AES-CBC");

        sut.InfoText.Should().Be(AesCbc);
        sut.IsInternalMechanismDetail.Should().BeTrue();
    }

    [TestCase("https://example.test/AES-CBC")]
    [TestCase("cryptoscript-info://other/AES-CBC")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC/other")]
    [TestCase("cryptoscript-info://mechanism//AES-CBC")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC/")]
    [TestCase("cryptoscript-info://user@mechanism/AES-CBC")]
    [TestCase("cryptoscript-info://mechanism:123/AES-CBC")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC?query=true")]
    [TestCase("cryptoscript-info://mechanism/AES-CBC#fragment")]
    public void OpenInfoLinkCommand_NonInternalOrMalformedUri_DoesNotNavigate(string link)
    {
        var sut = CreateSut();
        sut.SetInfoText(Mechanisms);
        string overview = sut.InfoText;

        sut.OpenInfoLinkCommand.Execute(link);

        sut.InfoText.Should().Be(overview);
        sut.IsInternalMechanismDetail.Should().BeFalse();
    }

    private static InfoViewModel CreateSut(HistoryServiceFake? history = null) =>
        new(history, new InfoDocumentationProviderFake());

    private sealed class InfoDocumentationProviderFake : IInfoDocumentationProvider
    {
        public bool HasDocumentation(string name) => name == "AES-CBC";

        public bool TryGetDocumentation(string name, out string documentation)
        {
            documentation = name switch
            {
                "mechanisms" => Mechanisms,
                "AES-CBC" => AesCbc,
                _ => string.Empty
            };
            return documentation.Length > 0;
        }
    }

    private sealed class HistoryServiceFake : IHistoryService
    {
        public List<string> AddedInfo { get; } = new();

        public bool InfoCanBack => false;
        public bool InfoCanForward => false;
        public bool LineCanBack => false;
        public bool LineCanForward => false;

        public event EventHandler<string>? InfoHistoryChanged;
        public event EventHandler<string>? LineHistoryChanged
        {
            add { }
            remove { }
        }

        public void AddInfo(string entry) => AddedInfo.Add(entry);
        public void AddLine(string entry) { }
        public void InfoBack() { }
        public void InfoForward() { }
        public void LineBack() { }
        public void LineForward() { }

        public void ShowFromHistory(string entry) => InfoHistoryChanged?.Invoke(this, entry);
    }
}
