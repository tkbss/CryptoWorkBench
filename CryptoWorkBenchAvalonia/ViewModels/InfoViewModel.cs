using CryptoScript.Documentation;
using CryptoWorkBenchAvalonia.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System;
using System.IO;
using System.Text;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class InfoViewModel : BindableBase, INavigationAware
    {
        private const string InternalLinkScheme = "cryptoscript-info";
        private const string MechanismLinkHost = "mechanism";

        private readonly IHistoryService? _history;
        private readonly IInfoDocumentationProvider _documentationProvider;
        private string? _mechanismsOverview;
        private string _infoText;
        private bool _isInternalMechanismDetail;

        public string InfoText
        {
            get => _infoText;
            private set
            {
                if (SetProperty(ref _infoText, value))
                    RaisePropertyChanged(nameof(IsMechanismDocumentation));
            }
        }

        // Info history stores Markdown text, so derive the styling scope from its title.
        public bool IsMechanismDocumentation =>
            _infoText?.TrimStart().StartsWith("# MECHANISM ", StringComparison.Ordinal) == true;

        public bool IsInternalMechanismDetail
        {
            get => _isInternalMechanismDetail;
            private set => SetProperty(ref _isInternalMechanismDetail, value);
        }

        public DelegateCommand<string> OpenInfoLinkCommand { get; }
        public DelegateCommand ShowMechanismsCommand { get; }

        public InfoViewModel()
            : this(null, new FileInfoDocumentationProvider())
        {
        }

        public InfoViewModel(IHistoryService history)
            : this(history, new FileInfoDocumentationProvider())
        {
        }

        public InfoViewModel(
            IHistoryService? history,
            IInfoDocumentationProvider documentationProvider)
        {
            _infoText = "Information text";
            _documentationProvider = documentationProvider ??
                throw new ArgumentNullException(nameof(documentationProvider));
            _history = history;
            OpenInfoLinkCommand = new DelegateCommand<string>(OpenInfoLink);
            ShowMechanismsCommand = new DelegateCommand(ShowMechanisms);

            if (_history != null)
                _history.InfoHistoryChanged += OnHistoryChanged;
        }

        public void SetInfoText(string text)
        {
            EndInternalNavigation();
            ShowCommandResult(text);
            _history?.AddInfo(text);
        }

        public bool NavigateToMechanism(string mechanism)
        {
            if (_mechanismsOverview == null ||
                !_documentationProvider.TryGetDocumentation(mechanism, out string documentation))
            {
                return false;
            }

            InfoText = documentation;
            IsInternalMechanismDetail = true;
            return true;
        }

        private void OpenInfoLink(string link)
        {
            if (!TryGetMechanismFromInternalLink(link, out string mechanism))
                return;

            NavigateToMechanism(mechanism);
        }

        private static bool TryGetMechanismFromInternalLink(string link, out string mechanism)
        {
            mechanism = string.Empty;
            if (!Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) ||
                !uri.Scheme.Equals(InternalLinkScheme, StringComparison.Ordinal) ||
                !uri.Host.Equals(MechanismLinkHost, StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !uri.IsDefaultPort ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                return false;
            }

            string path = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/');
            if (string.IsNullOrEmpty(path) || path.Contains('/') ||
                !link.Equals(
                    $"{InternalLinkScheme}://{MechanismLinkHost}/{Uri.EscapeDataString(path)}",
                    StringComparison.Ordinal))
                return false;

            mechanism = path;
            return true;
        }

        private void ShowMechanisms()
        {
            if (_mechanismsOverview == null || !IsInternalMechanismDetail)
                return;

            InfoText = AddInternalMechanismLinks(_mechanismsOverview);
            IsInternalMechanismDetail = false;
        }

        private void OnHistoryChanged(object? sender, string entry)
        {
            EndInternalNavigation();
            ShowCommandResult(entry);
        }

        private void ShowCommandResult(string text)
        {
            if (IsMechanismsOverview(text))
            {
                _mechanismsOverview = text;
                InfoText = AddInternalMechanismLinks(text);
                return;
            }

            _mechanismsOverview = null;
            InfoText = text;
        }

        private bool IsMechanismsOverview(string text) =>
            _documentationProvider.TryGetDocumentation("mechanisms", out string mechanisms) &&
            string.Equals(text, mechanisms, StringComparison.Ordinal);

        private string AddInternalMechanismLinks(string markdown)
        {
            var result = new StringBuilder(markdown.Length + 512);
            using var reader = new StringReader(markdown);
            string? line;
            bool firstLine = true;

            while ((line = reader.ReadLine()) != null)
            {
                if (!firstLine)
                    result.AppendLine();
                firstLine = false;
                result.Append(AddInternalMechanismLink(line));
            }

            if (markdown.EndsWith('\n'))
                result.AppendLine();

            return result.ToString();
        }

        private string AddInternalMechanismLink(string line)
        {
            const string prefix = "- ";
            const string separator = " :";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                return line;

            int separatorIndex = line.IndexOf(separator, prefix.Length, StringComparison.Ordinal);
            if (separatorIndex < 0)
                return line;

            string mechanism = line[prefix.Length..separatorIndex];
            if (!_documentationProvider.HasDocumentation(mechanism))
                return line;

            string link = $"{InternalLinkScheme}://{MechanismLinkHost}/{Uri.EscapeDataString(mechanism)}";
            return $"- [{mechanism}]({link}){line[separatorIndex..]}";
        }

        private void EndInternalNavigation()
        {
            IsInternalMechanismDetail = false;
            _mechanismsOverview = null;
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
        }
    }
}
