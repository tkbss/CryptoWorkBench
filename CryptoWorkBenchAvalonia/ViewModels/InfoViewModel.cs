using CryptoWorkBenchAvalonia.Services;
using Markdig;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace CryptoWorkBenchAvalonia.ViewModels
{
	public class InfoViewModel : BindableBase,INavigationAware
	{
        private readonly IHistoryService? _history;
        private string _infoText;
        public string InfoText
        {
            get => _infoText;
            set
            {
               SetProperty(ref _infoText, value);
                //if (string.IsNullOrEmpty(_infoText)) return;
                // Bei anderung von Markdown immer neu nach HTML konvertieren
                //var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                //InfoHtml = Markdig.Markdown.ToHtml(_infoText ?? string.Empty);
            }
        }
        public void SetInfoText(string text)
        {
            InfoText = text;            
            _history?.AddInfo(_infoText!);
        }
        private string _infoHtml;
        public string InfoHtml
        {
            get => _infoHtml;
            private set => SetProperty(ref _infoHtml, value);
        }
        public InfoViewModel() 
        { 
            _infoText = "Information text";
            _infoHtml= string.Empty;
        }
        public InfoViewModel(IHistoryService history)
        {
            _infoText = "Information text";
            _infoHtml = string.Empty;
            _history = history;
            // Service-Event abonnieren
            _history.InfoHistoryChanged += OnHistoryChanged;
        }
        private void OnHistoryChanged(object? sender, string entry)
        {
            InfoText = entry;
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            
        }
    }
}