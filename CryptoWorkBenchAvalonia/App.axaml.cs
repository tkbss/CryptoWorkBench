using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

using Avalonia.Markup.Xaml;
using CryptoScript.Documentation;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;
using CryptoWorkBenchAvalonia.Shell;


namespace CryptoWorkBenchAvalonia;

public partial class App : PrismApplication
{
    public bool UseDockShell { get; init; }
    internal System.Action<DockShellWindow>? ObserveDockShell { get; init; }
    internal System.Action? InitializeDockDiagnostics { get; init; }
    public System.Exception? DockShellStartupError { get; private set; }
    private DockShellPresentation? _dockPresentation;
    private DockShellLayout? _dockLayout;
    private DockShellWindow? _dockWindow;
    private DockEditorSession? _dockEditor;
    internal virtual DockLayoutStore CreateLayoutStore() => new();
    internal System.Func<Avalonia.Controls.Window, Avalonia.Controls.Button, Services.Automation.WorkbenchAutomationService, System.Func<System.Threading.Tasks.Task>>? ConfigureDesktopMcp { get; init; }
    public override void Initialize()
    {
        InitializeDockDiagnostics?.Invoke();
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
        base.Initialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {

        // Avalonia 12 disables data-annotations validation by default.

        // Prism kümmert sich jetzt um das Erzeugen der Shell (CreateShell)
        base.OnFrameworkInitializationCompleted();
    }

    protected override AvaloniaObject CreateShell()
    {
        var window = DockShellStartup.Select(UseDockShell, CreateDockShell, CreateLegacyShell, exception =>
        {
            _dockWindow?.ReleaseLayout();
            _dockLayout?.Dispose();
            _dockEditor?.Dispose(); // Structural phase only: registration has not started.
            _dockPresentation?.Dispose();
            _dockPresentation = null;
            DockShellStartupError = exception;
            System.Diagnostics.Trace.WriteLine($"Dock shell initialization failed; using standard workspace: {exception}");
        });
        // Past this boundary a failed productive startup must NOT fall back to a second editor.
        if (window is DockShellWindow dock)
        {
            try { InitializeDockSession(dock); }
            catch
            {
                // The shell is not shown yet and the desktop hook only installs consent handlers.
                // Revoke admission and retire this registration; never create a replacement shell.
                Container.Resolve<Services.Automation.WorkbenchAutomationService>().BeginShutdown();
                try { _dockEditor?.Dispose(); }
                finally
                {
                    Container.Resolve<InfoViewModel>().Dispose();
                    dock.ReleaseLayout();
                    _dockPresentation?.Dispose();
                }
                throw;
            }
        }
        return window;
    }

    protected virtual Avalonia.Controls.Window CreateDockShell()
    {
        var store = CreateLayoutStore();
        _dockLayout = Container.Resolve<DockShellLayout>();
        _dockLayout.StartupLayout = store.Load();
        _dockEditor = Container.Resolve<DockEditorSession>();
        _dockPresentation = new DockShellPresentation(this, _dockEditor,
            Container.Resolve<VariableViewModel>(), Container.Resolve<InfoViewModel>());
        _dockWindow = Container.Resolve<DockShellWindow>();
        _dockWindow.EnablePersistence(store);
        return _dockWindow;
    }

    protected virtual void InitializeDockSession(DockShellWindow window)
    {
        ObserveDockShell?.Invoke(window);
        var editor = _dockEditor ?? Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<Services.Automation.WorkbenchAutomationService>();
        editor.Start(window);
        _dockPresentation?.StartTools();
        window.BindEditor(editor);
        CoordinateClosing(window, service, ConfigureDesktopMcp?.Invoke(window, window.McpButton, service));
        window.Closed += (_, _) =>
        {
            editor.Dispose();
            // Global tool state outlives visual Close/Show and ends only with the application.
            Container.Resolve<InfoViewModel>().Dispose();
            _dockPresentation?.Dispose();
        };
    }

    private Avalonia.Controls.Window CreateLegacyShell()
    {
        var window = Container.Resolve<MainWindow>();
        var service = Container.Resolve<CryptoWorkBenchAvalonia.Services.Automation.WorkbenchAutomationService>();
        CoordinateClosing(window, service, ConfigureDesktopMcp?.Invoke(window, window.McpSessionButton, service));
        window.Closed += (_, _) => Container.Resolve<CryptoScriptEditViewModel>().Dispose();
        return window;
    }

    internal static void CoordinateClosing(Avalonia.Controls.Window window, Services.Automation.WorkbenchAutomationService service,
        System.Func<System.Threading.Tasks.Task>? stopTransport = null,
        System.Action<string>? reportShutdown = null)
    {
        reportShutdown ??= message => window.Title = message;
        bool draining = false;
        bool ready = false;
        window.Closing += async (_, args) =>
        {
            if (ready) return;
            args.Cancel = true;
            if (draining) return;
            draining = true;
            using var notification = new System.Threading.CancellationTokenSource();
            async System.Threading.Tasks.Task NotifyWaiting()
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(5), notification.Token);
                    reportShutdown("CryptoWorkBench — waiting for pending work before closing");
                }
                catch (System.OperationCanceledException) when (notification.IsCancellationRequested) { }
            }
            var notice = NotifyWaiting();
            try
            {
                // Capture before drain or owned-window teardown. Commit only on actual Closed.
                if (window is DockShellWindow dock) dock.CaptureForShutdown();
                service.BeginShutdown();
                if (stopTransport != null) await stopTransport();
                await service.DrainForExitAsync();
            }
            catch (System.Exception)
            {
                reportShutdown("CryptoWorkBench — safe shutdown failed; pending work remains protected");
                draining = false;
                return;
            }
            finally
            {
                notification.Cancel();
                await notice;
            }
            // Close after the original Closing event has unwound, even for an idle workspace.
            UiDispatch.Post(() => { ready = true; window.Close(); });
        };
    }

    protected override void OnInitialized()
    {
        if (MainWindow is DockShellWindow) return;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Exit += (_, _) =>
            {
                Container.Resolve<CryptoScriptEditViewModel>().Dispose();
                Container.Resolve<KeyMapViewModel>().Dispose();
                Container.Resolve<InfoViewModel>().Dispose();
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
        DockShellStartup.Register(containerRegistry);
        containerRegistry.RegisterSingleton<INotificationService, NotificationService>();
        containerRegistry.RegisterSingleton<IHistoryService, HistoryService>();
        containerRegistry.RegisterSingleton<IInfoDocumentationProvider, FileInfoDocumentationProvider>();

        containerRegistry.RegisterInstance(CryptoWorkBenchAvalonia.Services.Automation.WorkbenchAutomationService.Shared);
        containerRegistry.RegisterInstance<CryptoWorkBench.Automation.Abstractions.IWorkbenchAutomationV2>(CryptoWorkBenchAvalonia.Services.Automation.WorkbenchAutomationService.Shared);
        containerRegistry.RegisterInstance<CryptoWorkBench.Automation.Abstractions.IWorkbenchAutomationService>(CryptoWorkBenchAvalonia.Services.Automation.WorkbenchAutomationService.Shared);
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
