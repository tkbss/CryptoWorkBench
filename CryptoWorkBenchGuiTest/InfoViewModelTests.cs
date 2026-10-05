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
    private const string Functions = "# Functions";
    private const string Parameters = "# Parameters";
    private const string NonePadding = "# PADDING NONE\nDocumentation";
    private const string Pkcs7Padding = "# PADDING PKCS-7\nDocumentation";
    private const string Encrypt = "# Encrypt";
    private const string EncryptAesCbc = "# Encrypt with AES-CBC";
    private const string AesCbcIv = "# AES-CBC parameter IV";

    private static readonly InfoDocumentId MechanismsId = InfoDocumentId.CreateMechanismsOverview();
    private static readonly InfoDocumentId FunctionsId = InfoDocumentId.CreateFunctionsOverview();
    private static readonly InfoDocumentId ParametersId = InfoDocumentId.CreateParametersOverview();
    private static readonly InfoDocumentId NonePaddingId = InfoDocumentId.CreatePadding("NONE");
    private static readonly InfoDocumentId Pkcs7PaddingId = InfoDocumentId.CreatePadding("PKCS-7");
    private static readonly InfoDocumentId AesCbcId = InfoDocumentId.CreateMechanism("AES-CBC");
    private static readonly InfoDocumentId EncryptId = InfoDocumentId.CreateFunction("Encrypt");
    private static readonly InfoDocumentId EncryptAesCbcId = InfoDocumentId.CreateMechanismFunction("Encrypt", "AES-CBC");
    private static readonly InfoDocumentId AesCbcIvId = InfoDocumentId.CreateMechanismParameter("AES-CBC", "IV");

    private static IEnumerable<TestCaseData> AllDocumentLinks()
    {
        yield return LinkCase(MechanismsId, Mechanisms);
        yield return LinkCase(FunctionsId, Functions);
        yield return LinkCase(ParametersId, Parameters);
        yield return LinkCase(NonePaddingId, NonePadding);
        yield return LinkCase(Pkcs7PaddingId, Pkcs7Padding);
        yield return LinkCase(AesCbcId, AesCbc);
        yield return LinkCase(EncryptId, Encrypt);
        yield return LinkCase(EncryptAesCbcId, EncryptAesCbc);
        yield return LinkCase(AesCbcIvId, AesCbcIv);
    }

    private static TestCaseData LinkCase(InfoDocumentId id, string markdown) =>
        new TestCaseData(InfoDocumentUri.ToCanonicalString(id), markdown)
            .SetName($"OpenInfoLink_{id.Kind}_ShowsDocument");

    [Test]
    public void SetInfoText_WithMechanismHeading_MarksTextAsMechanismDocumentation()
    {
        var sut = new InfoViewModel();
        const string text = "  # MECHANISM AES-GCM\nDocumentation";

        sut.SetInfoText(text);

        sut.InfoText.Should().Be(text);
        sut.IsMechanismDocumentation.Should().BeTrue();
    }

    [Test]
    public void SetInfoText_AfterMechanismDocumentationWithNormalText_ClearsFlag()
    {
        var sut = new InfoViewModel();
        sut.SetInfoText("# MECHANISM AES-GCM\nDocumentation");

        sut.SetInfoText("General cryptography documentation");

        sut.IsMechanismDocumentation.Should().BeFalse();
    }

    [Test]
    public void InfoBack_AfterSettingTwoTexts_RestoresPreviousText()
    {
        var history = new HistoryService();
        var sut = new InfoViewModel(history);
        sut.SetInfoText("First information text");
        sut.SetInfoText("Second information text");

        history.InfoBack();

        sut.InfoText.Should().Be("First information text");
    }

    [Test]
    public void MechanismsOverview_ToMechanismAndBack_PreservesHistoryAndLinkedOverview()
    {
        var history = new HistoryServiceFake();
        var sut = CreateSut(history);
        sut.SetInfoText(Mechanisms);
        string linkedOverview = sut.InfoText;

        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(AesCbcId));

        sut.InfoText.Should().Be(AesCbc);
        sut.CanNavigateBack.Should().BeTrue();
        history.AddedInfo.Should().Equal(Mechanisms);

        sut.NavigateBackCommand.Execute();

        sut.InfoText.Should().Be(linkedOverview);
        sut.InfoText.Should().Contain("[AES-CBC](cryptoscript-info://mechanism/AES-CBC)");
        sut.InfoText.Should().Contain("- RSA-PSS : undocumented");
        sut.CanNavigateBack.Should().BeFalse();
        history.AddedInfo.Should().Equal(Mechanisms);
    }

    [Test]
    public void MultipleInternalLinks_AndBack_RestoreEveryPreviousPage()
    {
        var history = new HistoryServiceFake();
        var sut = CreateSut(history);
        sut.SetInfoText("Start");
        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(EncryptId));
        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(EncryptAesCbcId));
        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(AesCbcIvId));

        sut.NavigateBackCommand.Execute();
        sut.InfoText.Should().Be(EncryptAesCbc);
        sut.CanNavigateBack.Should().BeTrue();
        sut.NavigateBackCommand.Execute();
        sut.InfoText.Should().Be(Encrypt);
        sut.NavigateBackCommand.Execute();
        sut.InfoText.Should().Be("Start");
        sut.CanNavigateBack.Should().BeFalse();
        history.AddedInfo.Should().Equal("Start");
    }

    [TestCaseSource(nameof(AllDocumentLinks))]
    public void OpenInfoLink_AllDocumentKinds_ShowAvailableDocument(string link, string expectedMarkdown)
    {
        var sut = CreateSut();
        sut.SetInfoText("Start");

        sut.OpenInfoLinkCommand.Execute(link);

        if (link == InfoDocumentUri.ToCanonicalString(MechanismsId))
        {
            sut.InfoText.Should().Contain("# Mechnisms");
            sut.InfoText.Should().Contain(
                "[AES-CBC](cryptoscript-info://mechanism/AES-CBC)");
        }
        else
        {
            sut.InfoText.Should().Be(expectedMarkdown);
        }
        sut.CanNavigateBack.Should().BeTrue();
    }

    [TestCase("cryptoscript-info://function/Decrypt")]
    [TestCase("cryptoscript-info://function/Decrypt/AES-CBC")]
    [TestCase("cryptoscript-info://parameter/AES-CBC/KEYTYPE")]
    public void OpenInfoLink_ValidButUnavailableDocument_DoesNotChangePageOrStack(string link)
    {
        var sut = CreateSut();
        sut.SetInfoText("Start");
        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(EncryptId));
        string current = sut.InfoText;

        sut.OpenInfoLinkCommand.Execute(link);

        sut.InfoText.Should().Be(current);
        sut.CanNavigateBack.Should().BeTrue();
        sut.NavigateBackCommand.Execute();
        sut.InfoText.Should().Be("Start");
        sut.CanNavigateBack.Should().BeFalse();
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
    public void OpenInfoLink_NonInternalOrMalformedUri_DoesNotChangePageOrStack(string link)
    {
        var sut = CreateSut();
        sut.SetInfoText("Start");

        sut.OpenInfoLinkCommand.Execute(link);

        sut.InfoText.Should().Be("Start");
        sut.CanNavigateBack.Should().BeFalse();
    }

    [Test]
    public void OpenInfoLink_WhenProviderCannotLoadDocument_DoesNotChangePageOrStack()
    {
        var provider = CreateProvider();
        provider.FailedDocument = AesCbcId;
        var sut = new InfoViewModel(null, provider);
        sut.SetInfoText("Start");

        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(AesCbcId));

        sut.InfoText.Should().Be("Start");
        sut.CanNavigateBack.Should().BeFalse();
    }

    [TestCase(typeof(FileNotFoundException))]
    [TestCase(typeof(DirectoryNotFoundException))]
    [TestCase(typeof(UnauthorizedAccessException))]
    public void OpenInfoLink_WhenProviderThrowsExpectedLoadException_PreservesInitialState(
        Type exceptionType)
    {
        var history = new HistoryServiceFake();
        var provider = CreateProvider();
        provider.LoadException = CreateLoadException(exceptionType);
        var sut = new InfoViewModel(history, provider);
        sut.SetInfoText("Start");

        Action navigate = () => sut.OpenInfoLinkCommand.Execute(
            InfoDocumentUri.ToCanonicalString(AesCbcId));

        navigate.Should().NotThrow();
        sut.InfoText.Should().Be("Start");
        sut.CanNavigateBack.Should().BeFalse();
        sut.NavigateBackCommand.CanExecute().Should().BeFalse();
        history.AddedInfo.Should().Equal("Start");
    }

    [Test]
    public void OpenInfoLink_WhenProviderThrowsWithExistingStack_PreservesEntireStack()
    {
        var history = new HistoryServiceFake();
        var provider = CreateProvider();
        var sut = new InfoViewModel(history, provider);
        sut.SetInfoText("Start");
        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(EncryptId));
        provider.LoadException = new FileNotFoundException();

        Action navigate = () => sut.OpenInfoLinkCommand.Execute(
            InfoDocumentUri.ToCanonicalString(AesCbcId));

        navigate.Should().NotThrow();
        sut.InfoText.Should().Be(Encrypt);
        sut.CanNavigateBack.Should().BeTrue();
        sut.NavigateBackCommand.CanExecute().Should().BeTrue();
        history.AddedInfo.Should().Equal("Start");

        provider.LoadException = null;
        sut.NavigateBackCommand.Execute();
        sut.InfoText.Should().Be("Start");
        sut.CanNavigateBack.Should().BeFalse();
        history.AddedInfo.Should().Equal("Start");
    }

    [Test]
    public void OpenInfoLink_WhenProviderThrowsUnexpectedException_PropagatesException()
    {
        var provider = CreateProvider();
        provider.LoadException = new InvalidOperationException("Programming error");
        var sut = new InfoViewModel(null, provider);
        sut.SetInfoText("Start");

        Action navigate = () => sut.OpenInfoLinkCommand.Execute(
            InfoDocumentUri.ToCanonicalString(AesCbcId));

        navigate.Should().Throw<InvalidOperationException>();
        sut.InfoText.Should().Be("Start");
        sut.CanNavigateBack.Should().BeFalse();
    }

    [Test]
    public void SetInfoText_AfterInternalNavigation_ClearsInternalStackAndAddsHistory()
    {
        var history = new HistoryServiceFake();
        var sut = CreateSut(history);
        sut.SetInfoText("Start");
        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(EncryptId));

        sut.SetInfoText("Direct result");

        sut.InfoText.Should().Be("Direct result");
        sut.CanNavigateBack.Should().BeFalse();
        sut.NavigateBackCommand.CanExecute().Should().BeFalse();
        history.AddedInfo.Should().Equal("Start", "Direct result");
    }

    [Test]
    public void HistoryNavigation_AfterInternalNavigation_ClearsInternalStack()
    {
        var history = new HistoryServiceFake();
        var sut = CreateSut(history);
        sut.SetInfoText("Start");
        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(EncryptId));

        history.ShowFromHistory("Earlier information");

        sut.InfoText.Should().Be("Earlier information");
        sut.CanNavigateBack.Should().BeFalse();
        sut.NavigateBackCommand.CanExecute().Should().BeFalse();
        history.AddedInfo.Should().Equal("Start");
    }

    [Test]
    public void RealHistoryService_AfterInternalNavigation_ClearsStackAndAllowsNewNavigation()
    {
        var history = new HistoryService();
        var sut = new InfoViewModel(history, CreateProvider());
        var mainViewModel = new MainViewModel(history);
        sut.SetInfoText("First direct result");
        sut.SetInfoText("Second direct result");
        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(EncryptId));

        mainViewModel.InfoHistoryBackCommand.Execute();

        sut.InfoText.Should().Be("First direct result");
        sut.CanNavigateBack.Should().BeFalse();

        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(AesCbcId));
        sut.InfoText.Should().Be(AesCbc);
        sut.CanNavigateBack.Should().BeTrue();
        sut.NavigateBackCommand.Execute();
        sut.InfoText.Should().Be("First direct result");
        sut.CanNavigateBack.Should().BeFalse();

        mainViewModel.InfoHistoryForwardCommand.Execute();

        sut.InfoText.Should().Be("Second direct result");
        sut.CanNavigateBack.Should().BeFalse();
    }

    [Test]
    public void NavigateBackCommand_TracksWhetherInternalStackHasEntries()
    {
        var sut = CreateSut();
        sut.SetInfoText("Start");
        sut.CanNavigateBack.Should().BeFalse();
        sut.NavigateBackCommand.CanExecute().Should().BeFalse();

        sut.OpenInfoLinkCommand.Execute(InfoDocumentUri.ToCanonicalString(FunctionsId));
        sut.CanNavigateBack.Should().BeTrue();
        sut.NavigateBackCommand.CanExecute().Should().BeTrue();

        sut.NavigateBackCommand.Execute();
        sut.CanNavigateBack.Should().BeFalse();
        sut.NavigateBackCommand.CanExecute().Should().BeFalse();
    }

    private static InfoViewModel CreateSut(HistoryServiceFake? history = null) => new(history, CreateProvider());

    private static InfoDocumentationProviderFake CreateProvider() =>
        new(new Dictionary<InfoDocumentId, string>
        {
            [MechanismsId] = Mechanisms,
            [FunctionsId] = Functions,
            [ParametersId] = Parameters,
            [NonePaddingId] = NonePadding,
            [Pkcs7PaddingId] = Pkcs7Padding,
            [AesCbcId] = AesCbc,
            [EncryptId] = Encrypt,
            [EncryptAesCbcId] = EncryptAesCbc,
            [AesCbcIvId] = AesCbcIv
        });

    private static Exception CreateLoadException(Type exceptionType) =>
        exceptionType == typeof(FileNotFoundException)
            ? new FileNotFoundException()
            : exceptionType == typeof(DirectoryNotFoundException)
                ? new DirectoryNotFoundException()
                : new UnauthorizedAccessException();

    private sealed class InfoDocumentationProviderFake : IInfoDocumentationProvider
    {
        private readonly IReadOnlyDictionary<InfoDocumentId, string> _documents;

        public InfoDocumentationProviderFake(IReadOnlyDictionary<InfoDocumentId, string> documents) => _documents = documents;

        public InfoDocumentId? FailedDocument { get; set; }
        public Exception? LoadException { get; set; }

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

        public bool HasDocument(InfoDocumentId id) => _documents.ContainsKey(id);

        public bool TryGetDocument(InfoDocumentId id, out string documentation)
        {
            if (LoadException is not null)
                throw LoadException;

            if (id == FailedDocument)
            {
                documentation = string.Empty;
                return false;
            }

            return _documents.TryGetValue(id, out documentation!);
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
        public event EventHandler<string>? LineHistoryChanged { add { } remove { } }
        public void AddInfo(string entry) => AddedInfo.Add(entry);
        public void AddLine(string entry) { }
        public void InfoBack() { }
        public void InfoForward() { }
        public void LineBack() { }
        public void LineForward() { }
        public void ShowFromHistory(string entry) => InfoHistoryChanged?.Invoke(this, entry);
    }
}
