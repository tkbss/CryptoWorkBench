using CryptoWorkBenchAvalonia.Services;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace CryptoWorkBenchAvalonia.ViewModels;

public partial class MainViewModel : BindableBase, INavigationAware
{
    private readonly IHistoryService _historyService;
    public DelegateCommand HistoryBackCommand { get; }
    public DelegateCommand HistoryForwardCommand { get; }

    private string _infoviewtitle = string.Empty;
    public string InfoViewTitle { get => _infoviewtitle; set => SetProperty(ref _infoviewtitle, value); }
    public MainViewModel()
    {
        InfoViewTitle = "Variables";
    }
    public MainViewModel(IHistoryService historyService)
    {
        _historyService = historyService;

        HistoryBackCommand = new DelegateCommand(
            () => _historyService.Back(),
            () => _historyService.CanBack);

        HistoryForwardCommand = new DelegateCommand(
            () => _historyService.Forward(),
            () => _historyService.CanForward);
    }


    public bool IsNavigationTarget(NavigationContext navigationContext)
    {
        // Auto-allow navigation
        return true;
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // Handle logic when navigating away from this view
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        
    }
}
