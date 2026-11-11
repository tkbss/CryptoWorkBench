using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Dock.Avalonia.Controls;
using Dock.Avalonia.Themes.Fluent;
using Dock.Settings;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;

namespace G1DockFeasibility;

public static class Program
{
    public static string? NativeOutput { get; private set; }
    [STAThread]
    public static int Main(string[] args)
    {
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
        NativeOutput = args.Length == 2 && args[0] == "--native-probe" ? Path.GetFullPath(args[1]) : null;
        return AppBuilder.Configure<ProbeApp>().UsePlatformDetect().LogToTrace()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
    }
}

public sealed class ProbeApp : PrismApplication
{
    public override void Initialize()
    {
        ConfigureVisuals(this);
        base.Initialize();
    }
    public static void ConfigureVisuals(Application app)
    {
        app.Styles.Add(new FluentTheme());
        app.Styles.Add(new DockFluentTheme());
        app.Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://G1DockFeasibility/"))
        { Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml") });
        app.DataTemplates.Add(new ProbeViewLocator());
        DockSettings.UseManagedWindows = false;
        DockSettings.UseOwnerForFloatingWindows = true;
        DockSettings.CloseFloatingWindowsOnMainWindowClose = true;
    }
    protected override void RegisterTypes(IContainerRegistry registry) => Registration.Register(registry);
    protected override AvaloniaObject CreateShell()
    {
        var workspace = Container.Resolve<ProbeWorkspace>();
        var shell = new ProbeWindow(workspace);
        shell.Closed += (_, _) => workspace.Dispose();
        return shell;
    }
    protected override void OnInitialized()
    {
        // The host is a regular ContentControl region. Dock owns its inner layout, not Prism navigation.
        var regions = Container.Resolve<IRegionManager>();
        var shell = (ProbeWindow)MainWindow;
        regions.RegisterViewWithRegion("ProbeDockHost", () => shell.Host);
        if (Program.NativeOutput is { } output)
            shell.Opened += async (_, _) => await NativeProbe.Run(shell, Container, output);
    }
}

public sealed class ProbeWindow : Window
{
    public ProbeWorkspace Workspace { get; }
    public DockControl Host { get; }
    private string? _saved;
    public ProbeWindow(ProbeWorkspace workspace, bool regionHost = true)
    {
        Workspace = workspace;
        Width = 1100; Height = 700; Title = "G1-01 Dock feasibility — isolated prototype";
        Host = new DockControl { Factory = workspace.Factory, Layout = workspace.Root };
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Margin = new Thickness(6) };
        void Add(string title, Action action)
        {
            var button = new Button { Content = title };
            button.Click += (_, _) =>
            {
                try { action(); }
                catch (Exception exception) { Title = $"G1 probe: {exception.Message}"; }
            };
            buttons.Children.Add(button);
        }
        Add("Float editor", () => workspace.Float(DocumentSession.Id));
        Add("Dock editor", () => workspace.Move(DocumentSession.Id, "documents"));
        Add("Float variables", () => workspace.Float(VariablesViewModel.Id));
        Add("Dock variables", () => workspace.Move(VariablesViewModel.Id, "variables-dock"));
        Add("Move variables", () => workspace.Move(VariablesViewModel.Id, "diagnostics-dock"));
        Add("Save layout", () => _saved = workspace.SaveLayout());
        Add("Restore layout", () => { if (_saved is not null) workspace.RestoreLayout(_saved, Host); });
        var initialLayout = workspace.SaveLayout();
        Add("Reset", () => workspace.RestoreLayout(initialLayout, Host));
        var panel = new DockPanel();
        DockPanel.SetDock(buttons, Avalonia.Controls.Dock.Top);
        panel.Children.Add(buttons);
        if (regionHost)
        {
            var content = new ContentControl
            {
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch
            };
            RegionManager.SetRegionName(content, "ProbeDockHost");
            panel.Children.Add(content);
        }
        else panel.Children.Add(Host);
        Content = panel;
    }
}
