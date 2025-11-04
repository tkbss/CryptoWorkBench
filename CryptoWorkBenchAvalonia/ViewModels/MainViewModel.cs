using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace CryptoWorkBenchAvalonia.ViewModels;

public partial class MainViewModel : BindableBase, INavigationAware
{
    private string _infoviewtitle = string.Empty;
    public string InfoViewTitle { get => _infoviewtitle; set => SetProperty(ref _infoviewtitle, value); }
    public MainViewModel(IContainerProvider containerProvider)
    {
        InfoViewTitle = "Variables";
        var region= containerProvider.Resolve<IRegionManager>();
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
