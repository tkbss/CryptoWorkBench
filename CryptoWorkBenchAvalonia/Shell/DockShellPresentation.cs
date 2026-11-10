using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Automation;
using Dock.Avalonia.Themes.Fluent;
using Dock.Model.Core;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>Only installed for the opt-in shell; removable if shell construction fails.</summary>
internal sealed class DockShellPresentation : IDisposable
{
    private readonly Application _app;
    private readonly DockFluentTheme _theme = new();
    private readonly PlaceholderTemplate _template;
    internal DockVariablesView? Variables { get; }
    internal InfoView? Info { get; }
    internal DockOutputView? Output { get; }
    internal DockDiagnosticsView? Diagnostics { get; }
    internal DockStatusView? Status { get; }
    private ContentControl? _diagnosticsPresenter;
    private ContentControl? _statusPresenter;
    private ContentControl? _outputPresenter;
    private ContentControl? _variablesPresenter;
    private ContentControl? _infoPresenter;
    public DockShellPresentation(Application app, DockEditorSession? editor = null,
        VariableViewModel? variables = null, InfoViewModel? info = null)
    {
        if (editor != null)
        {
            Output = new DockOutputView(editor);
            Diagnostics = new DockDiagnosticsView(editor);
            Status = new DockStatusView(editor.ViewModel.Status);
        }
        if (variables != null) Variables = new DockVariablesView(variables);
        if (info != null)
        {
            Info = new InfoView { DataContext = info };
            Info.FindControl<Markdown.Avalonia.MarkdownScrollViewer>("InfoMarkdownViewer")!.SelectionEnabled = true;
        }
        _template = new PlaceholderTemplate(this, editor);
        _app = app;
        app.Styles.Add(_theme);
        app.DataTemplates.Add(_template);
    }
    internal void StartTools() => Variables?.Start();
    private static Control Present(Control view, ref ContentControl? previous)
    {
        if (previous != null) previous.Content = null;
        previous = new ContentControl { Content = view };
        return previous;
    }
    public void Dispose()
    {
        Variables?.Dispose();
        if (_diagnosticsPresenter != null) _diagnosticsPresenter.Content = null;
        if (_statusPresenter != null) _statusPresenter.Content = null;
        _diagnosticsPresenter = _statusPresenter = null;
        if (_outputPresenter != null) _outputPresenter.Content = null;
        _outputPresenter = null;
        if (_variablesPresenter != null) _variablesPresenter.Content = null;
        if (_infoPresenter != null) _infoPresenter.Content = null;
        _variablesPresenter = _infoPresenter = null;
        _app.DataTemplates.Remove(_template); _app.Styles.Remove(_theme);
    }
    private sealed class PlaceholderTemplate(DockShellPresentation owner, DockEditorSession? editor) : IDataTemplate
    {
        public bool Match(object? data) => data is IDockable { Context: DockPaneState };
        public Control Build(object? data)
        {
            if (data is not IDockable { Factory: EditorDockFactory { LayoutOwner: { } layout } } pane) return BuildContent(data);
            // Dock can retain the normal tab template while the focused tree creates a
            // second presenter. Reclaim the session-owned view whenever that template
            // is displayed again; constructing a detached template must not steal it.
            var content = new ContentControl();
            content.AttachedToVisualTree += (_, _) => content.Content = BuildContent(data);
            var actions = new MenuItem { Header = "Layout" };
            var feedback = new TextBlock { FontSize = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            void Add(string name, DockLayoutCommand command)
            {
                var item = new MenuItem { Header = name };
                item.Click += (_, _) => feedback.Text = layout.ExecuteLayout(command, pane.Id).Message;
                actions.Items.Add(item);
            }
            Add("Maximize Active View", DockLayoutCommand.Maximize);
            Add("Restore Layout", DockLayoutCommand.Restore);
            Add("Reset to Default Layout", DockLayoutCommand.Reset);
            var menu = new Menu { Items = { actions } };
            var bar = new DockPanel { Children = { menu, feedback } };
            DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
            return new DockPanel { Children = { bar, content } };
        }
        private Control BuildContent(object? data) => data switch
        {
            IDockable { Id: DockShellState.DocumentId } when editor != null => editor.CreatePresenter(),
            IDockable { Id: DockShellState.VariablesId } when owner.Variables != null => Present(owner.Variables, ref owner._variablesPresenter),
            IDockable { Id: DockShellState.OutputId } when owner.Output != null => Present(owner.Output, ref owner._outputPresenter),
            IDockable { Id: DockShellState.DiagnosticsId } when owner.Diagnostics != null => Present(owner.Diagnostics, ref owner._diagnosticsPresenter),
            IDockable { Id: DockShellState.StatusId } when owner.Status != null => Present(owner.Status, ref owner._statusPresenter),
            IDockable { Id: DockShellState.InfoId } when owner.Info != null => Present(owner.Info, ref owner._infoPresenter),
            _ => new DockPlaceholderView((DockPaneState)((IDockable)data!).Context!)
        };
    }
}

public sealed class DockPlaceholderView : UserControl
{
    public DockPaneState State { get; }
    public TextBox Notes { get; }
    public DockPlaceholderView(DockPaneState state)
    {
        State = state;
        DataContext = state;
        AutomationProperties.SetAutomationId(this, state.Id);
        Notes = new TextBox { AcceptsReturn = true, MinHeight = 100, PlaceholderText = "Notes" };
        Notes.Bind(TextBox.TextProperty, new Binding(nameof(DockPaneState.Note)) { Mode = BindingMode.TwoWay });
        Content = new StackPanel
        {
            Margin = new Thickness(16), Spacing = 12,
            Children =
            {
                new TextBlock { Text = state.Title, FontSize = 22 },
                new TextBlock { Text = "Preview placeholder", Opacity = .6 }, Notes
            }
        };
    }
}
