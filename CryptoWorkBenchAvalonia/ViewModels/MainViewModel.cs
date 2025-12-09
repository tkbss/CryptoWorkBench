using CryptoWorkBenchAvalonia.Services;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace CryptoWorkBenchAvalonia.ViewModels;

public partial class MainViewModel : BindableBase, INavigationAware
{
    private readonly IHistoryService? _historyService;
    public DelegateCommand InfoHistoryBackCommand { get; }
    public DelegateCommand InfoHistoryForwardCommand { get; }
    public DelegateCommand LineHistoryBackCommand { get; }
    public DelegateCommand LineHistoryForwardCommand { get; }

    private string _infoviewtitle = string.Empty;
    public string InfoViewTitle { get => _infoviewtitle; set => SetProperty(ref _infoviewtitle, value); }
    public MainViewModel()
    {
        InfoViewTitle = "Variables";
    }
    public MainViewModel(IHistoryService historyService)
    {
        _historyService = historyService;

        InfoHistoryBackCommand = new DelegateCommand(
            () => _historyService.InfoBack(),
            () => _historyService.InfoCanBack);
        LineHistoryBackCommand = new DelegateCommand(
            () => _historyService.LineBack(),
            () => _historyService.LineCanBack);

        InfoHistoryForwardCommand = new DelegateCommand(
            () => _historyService.InfoForward(),
            () => _historyService.InfoCanForward);
        LineHistoryForwardCommand = new DelegateCommand(
            () => _historyService.LineForward(),
            () => _historyService.LineCanForward);
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
