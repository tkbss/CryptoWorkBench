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
}
