using CryptoScript.Documentation;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class InfoViewModel : BindableBase, INavigationAware
    {
        private readonly IHistoryService? _history;
        private readonly IInfoDocumentationProvider _documentationProvider;
        private readonly Stack<InternalNavigationEntry> _internalNavigation = new();
        private string _infoText;
        private InfoDocumentId? _currentDocumentId;
        private string? _currentDocumentTitle;

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

        public bool CanNavigateBack => _internalNavigation.Count > 0;

        public DelegateCommand<string> OpenInfoLinkCommand { get; }
        public DelegateCommand NavigateBackCommand { get; }

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
            NavigateBackCommand = new DelegateCommand(
                NavigateBack,
                () => CanNavigateBack);

            if (_history != null)
                _history.InfoHistoryChanged += OnHistoryChanged;
        }

        public void SetInfoText(string text)
        {
            EndInternalNavigation();
            ShowCommandResult(text);
            _history?.AddInfo(text);
        }

        private void OpenInfoLink(string link)
        {
            if (!InfoDocumentUri.TryParse(link, out InfoDocumentId? documentId) ||
                documentId is null ||
                !TryLoadDocument(documentId, out string documentation))
                return;

            _internalNavigation.Push(new InternalNavigationEntry(
                InfoText,
                _currentDocumentId,
                _currentDocumentTitle));

            string? title = InfoDocumentCatalog.TryGet(
                documentId,
                out InfoDocumentCatalogEntry? entry)
                ? entry!.DisplayTitle
                : null;
            ShowDocument(documentation, documentId, title);
            OnInternalNavigationChanged();
        }

        private bool TryLoadDocument(
            InfoDocumentId documentId,
            out string documentation)
        {
            try
            {
                return _documentationProvider.TryGetDocument(
                    documentId,
                    out documentation);
            }
            catch (IOException)
            {
                documentation = string.Empty;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                documentation = string.Empty;
                return false;
            }
        }

        private void NavigateBack()
        {
            if (_internalNavigation.Count == 0)
                return;

            InternalNavigationEntry previous = _internalNavigation.Pop();
            InfoText = previous.Markdown;
            _currentDocumentId = previous.DocumentId;
            _currentDocumentTitle = previous.Title;
            OnInternalNavigationChanged();
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
                InfoText = AddInternalMechanismLinks(text);
                _currentDocumentId = InfoDocumentId.CreateMechanismsOverview();
                _currentDocumentTitle = InfoDocumentCatalog.TryGet(
                    _currentDocumentId,
                    out InfoDocumentCatalogEntry? entry)
                    ? entry!.DisplayTitle
                    : null;
                return;
            }

            InfoText = text;
        }

        private void ShowDocument(
            string markdown,
            InfoDocumentId documentId,
            string? title)
        {
            InfoText = documentId.Kind == InfoDocumentKind.MechanismsOverview
                ? AddInternalMechanismLinks(markdown)
                : markdown;
            _currentDocumentId = documentId;
            _currentDocumentTitle = title;
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
            if (!MechanismRegistry.TryGet(mechanism, out _))
                return line;

            InfoDocumentId documentId = InfoDocumentId.CreateMechanism(mechanism);
            if (!_documentationProvider.HasDocument(documentId))
                return line;

            string link = InfoDocumentUri.ToCanonicalString(documentId);
            return $"- [{mechanism}]({link}){line[separatorIndex..]}";
        }

        private void EndInternalNavigation()
        {
            _internalNavigation.Clear();
            _currentDocumentId = null;
            _currentDocumentTitle = null;
            OnInternalNavigationChanged();
        }

        private void OnInternalNavigationChanged()
        {
            RaisePropertyChanged(nameof(CanNavigateBack));
            NavigateBackCommand.RaiseCanExecuteChanged();
        }

        private sealed record InternalNavigationEntry(
            string Markdown,
            InfoDocumentId? DocumentId,
            string? Title);

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
        }
    }
}
