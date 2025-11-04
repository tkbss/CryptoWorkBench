using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
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
        return Container.Resolve<MainWindow>();
    }

    protected override void OnInitialized()
    {
        var regionManager = Container.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion(RegionNames.SidebarRegion, typeof(SidebarView));
        regionManager.RegisterViewWithRegion(RegionNames.ContentRegion, typeof(CryptoScriptEditView));
        regionManager.RegisterViewWithRegion(RegionNames.InfoRegion, typeof(VariableView));
        regionManager.RegisterViewWithRegion(RegionNames.FooterRegion, typeof(StatusView));

    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {           
        containerRegistry.RegisterSingleton<INotificationService, NotificationService>();
        // 🔹 Hier MainViewModel beim Container registrieren:
        containerRegistry.RegisterSingleton<MainViewModel>();
        containerRegistry.RegisterSingleton<VariableViewModel>();
        containerRegistry.RegisterForNavigation<CryptoScriptEditView>();
        containerRegistry.RegisterForNavigation<SidebarView>();
        containerRegistry.RegisterForNavigation<VariableView,VariableViewModel>();
        containerRegistry.RegisterSingleton<StatusViewModel>();
        containerRegistry.RegisterSingleton<SidebarViewModel>();
        containerRegistry.RegisterSingleton<CryptoScriptEditViewModel>();
        


    }
}
