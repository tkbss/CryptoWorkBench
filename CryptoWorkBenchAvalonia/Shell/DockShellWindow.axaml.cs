using System;
using System.Linq;
using Avalonia.Controls;
using Dock.Avalonia.Controls;

namespace CryptoWorkBenchAvalonia.Shell;

public partial class DockShellWindow : Window
{
    public DockShellLayout Layout { get; }
    public DockControl Host => this.FindControl<DockControl>("DockHost")!;
    public Button McpButton => this.FindControl<Button>("McpConnectButton")!;
    private DockLayoutStore? _store;
    private string? _closingSnapshot;
    private LayoutWindow _normalWindow = new(0, 0, 1200, 760);
    private bool _opened;
    public DockShellWindow(DockShellLayout layout)
    {
        Layout = layout;
        InitializeComponent();
        if (layout.StartupLayout is { } saved)
        {
            var screens = DockWindowGeometry.Screens(this);
            foreach (var floating in saved.Root.Windows ?? [])
            {
                var bounds = DockWindowGeometry.Clamp(new(floating.X, floating.Y, floating.Width, floating.Height), screens);
                floating.X = bounds.X; floating.Y = bounds.Y; floating.Width = bounds.Width; floating.Height = bounds.Height;
            }
            layout.RestoreAtStartup(saved);
            var main = DockWindowGeometry.Clamp(saved.MainWindow, screens);
            _normalWindow = main;
            Position = new Avalonia.PixelPoint((int)main.X, (int)main.Y);
            Width = main.Width; Height = main.Height;
            WindowStartupLocation = WindowStartupLocation.Manual;
            if (main.Maximized) WindowState = WindowState.Maximized;
            layout.StartupLayout = null;
        }
        Host.Factory = layout.Factory;
        Host.Layout = layout.Root;
        layout.RootChanged += () => { Host.Layout = null; Host.Layout = layout.Root; };
        Opened += (_, _) =>
        {
            _opened = true;
            TrackNormalBounds();
            // Startup restore initializes the factory itself; this DockControl
            // leaves InitializeLayout disabled and does not call ShowWindows.
            // Present those existing hosts once the real owner window is open.
            Layout.Root.ShowWindows.Execute(null);
        };
        PositionChanged += (_, _) => TrackNormalBounds();
        SizeChanged += (_, _) => TrackNormalBounds();
        var menu = this.FindControl<Menu>("ShellMenu")!;
        var file = new MenuItem { Header = "_File" };
        file.Items.Add(ActionItem("E_xit", Close));
        var view = new MenuItem { Header = "_View" };
        view.Items.Add(ActionItem("Maximize Active View", () => LayoutAction(DockLayoutCommand.Maximize)));
        view.Items.Add(ActionItem("Restore Layout", () => LayoutAction(DockLayoutCommand.Restore)));
        view.Items.Add(ActionItem("Reset to Default Layout", () => LayoutAction(DockLayoutCommand.Reset)));
        view.Items.Add(new Separator());
        foreach (var state in new[] { layout.State.Document }.Concat(layout.State.Tools))
        {
            var pane = new MenuItem { Header = state.Title };
            pane.Items.Add(ActionItem("_Show", () => layout.Show(state.Id)));
            pane.Items.Add(ActionItem("_Float", () => layout.Float(state.Id)));
            pane.Items.Add(ActionItem("_Dock", () => layout.Dock(state.Id)));
            pane.Items.Add(ActionItem("_Maximize", () => LayoutAction(DockLayoutCommand.Maximize, state.Id)));
            if (!state.IsDocument) pane.Items.Add(ActionItem("_Close", () => layout.Close(state.Id)));
            view.Items.Add(pane);
        }
        menu.Items.Add(file); menu.Items.Add(view);
        Closed += (_, _) =>
        {
            if (_closingSnapshot != null) _store?.Save(_closingSnapshot);
            // Do not tear down a canceled close. No editor/admission operations belong here.
            ReleaseLayout();
        };
    }

    private void TrackNormalBounds()
    {
        if (_opened && WindowState == WindowState.Normal)
            _normalWindow = new(Position.X, Position.Y, ClientSize.Width, ClientSize.Height);
    }
    internal void EnablePersistence(DockLayoutStore store) => _store = store;
    internal void CaptureForShutdown()
    {
        if (_store == null || _closingSnapshot != null) return;
        try { _closingSnapshot = Layout.Capture(_normalWindow with { Maximized = WindowState == WindowState.Maximized }); }
        catch (Exception error) when (DockLayoutStore.Recoverable(error)) { _store.Report("snapshot", error); }
    }
    private void LayoutAction(DockLayoutCommand command, string? paneId = null)
        => this.FindControl<TextBlock>("ShellStatus")!.Text = Layout.ExecuteLayout(command, paneId).Message;

    internal void BindEditor(DockEditorSession editor)
    {
        Layout.BindEditor(editor);
        var file = (MenuItem)this.FindControl<Menu>("ShellMenu")!.Items[0]!;
        file.Items.Insert(0, new MenuItem { Header = "_Open…", Command = editor.OpenCommand });
        file.Items.Insert(1, new MenuItem { Header = "_Save…", Command = editor.SaveCommand });
        this.FindControl<TextBlock>("ShellStatus")!.Text = "Dock editor preview · Open loads text without execution.";
    }

    internal void ReleaseLayout()
    {
        Layout.Dispose();
        Host.Layout = null;
        Host.Factory = null;
    }
    private MenuItem ActionItem(string title, Action action)
    {
        var item = new MenuItem { Header = title };
        item.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.WriteLine(exception);
                this.FindControl<TextBlock>("ShellStatus")!.Text = "The window action could not be completed.";
            }
        };
        return item;
    }
}
