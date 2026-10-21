using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CryptoScript.Documentation;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;


namespace CryptoWorkBenchAvalonia;

public partial class App : PrismApplication
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);        
#if DEBUG
        this.AttachDeveloperTools();
#endif
        base.Initialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Avalonia-Validierung entfernen (wie bisher)
        BindingPlugins.DataValidators.RemoveAt(0);

        // Prism kümmert sich jetzt um das Erzeugen der Shell (CreateShell)
        base.OnFrameworkInitializationCompleted();
    }

    protected override AvaloniaObject CreateShell()
    {
        var window = Container.Resolve<MainWindow>();
        window.Closed += (_, _) => Container.Resolve<CryptoScriptEditViewModel>().Dispose();
        return window;
    }

    protected override void OnInitialized()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Exit += (_, _) =>
            {
                Container.Resolve<CryptoScriptEditViewModel>().Dispose();
                Container.Resolve<KeyMapViewModel>().Dispose();
            };
        var regionManager = Container.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion(RegionNames.InfoRegion, typeof(InfoView));
        regionManager.RegisterViewWithRegion(RegionNames.SidebarRegion, typeof(SidebarView));
        regionManager.RegisterViewWithRegion(RegionNames.ContentRegion, typeof(CryptoScriptEditView));
        regionManager.RegisterViewWithRegion(RegionNames.InfoRegion, typeof(VariableView));        
        regionManager.RegisterViewWithRegion(RegionNames.FooterRegion, typeof(StatusView));
        regionManager.RegisterViewWithRegion(RegionNames.InfoFooterRegion, typeof(KeyMapView));

    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {           
        containerRegistry.RegisterSingleton<INotificationService, NotificationService>();
        containerRegistry.RegisterSingleton<IHistoryService, HistoryService>();
        containerRegistry.RegisterSingleton<IInfoDocumentationProvider, FileInfoDocumentationProvider>();
        
        containerRegistry.RegisterSingleton<MainViewModel>();
        containerRegistry.RegisterSingleton<InfoViewModel>();
        containerRegistry.RegisterSingleton<VariableViewModel>();
        containerRegistry.RegisterSingleton<SidebarViewModel>();
        containerRegistry.RegisterSingleton<CryptoScriptEditViewModel>();
        containerRegistry.RegisterSingleton<KeyMapViewModel>();

        containerRegistry.RegisterForNavigation<CryptoScriptEditView>();
        containerRegistry.RegisterForNavigation<SidebarView>();
        containerRegistry.RegisterForNavigation<InfoView,InfoViewModel>();
        containerRegistry.RegisterForNavigation<KeyMapView,KeyMapViewModel>();
        containerRegistry.RegisterForNavigation<VariableView,VariableViewModel>();

        containerRegistry.RegisterSingleton<StatusViewModel>();
        
        


    }
}
