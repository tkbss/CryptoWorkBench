using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.ViewModels;
using FluentAssertions;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class InfoViewModelTests
{
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
}
