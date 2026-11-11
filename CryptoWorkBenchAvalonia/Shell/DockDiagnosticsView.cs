using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.ViewModels;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>Presentation only; the existing global StatusViewModel owns every diagnostic.</summary>
internal sealed class DockDiagnosticsView : UserControl
{
    internal TextBlock Feedback { get; } = new() { TextWrapping = TextWrapping.Wrap };
    internal DockDiagnosticsView(DockEditorSession session)
    {
        var model = session.ViewModel.Status;
        DataContext = model;
        AutomationProperties.SetAutomationId(this, DockShellState.DiagnosticsId);
        var filters = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var filter in Enum.GetValues<DiagnosticFilter>())
        {
            var button = new Button { Content = filter.ToString(), Margin = new Thickness(0, 0, 8, 4) };
            button.Click += (_, _) => model.Filter = filter;
            filters.Children.Add(button);
        }
        var selectedFilter = new TextBlock();
        selectedFilter.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(StatusViewModel.Filter)) { StringFormat = "Showing: {0}" });
        filters.Children.Add(selectedFilter);
        var list = new ListBox { ItemsSource = model.VisibleDiagnostics };
        list.ItemTemplate = new FuncDataTemplate<DiagnosticDisplayItem>((entry, _) =>
        {
            if (entry == null) return new Border();
            var source = new Button { Content = entry.SourceLabel, IsEnabled = entry.HasSource,
                HorizontalAlignment = HorizontalAlignment.Left, Name = "DiagnosticSource" };
            source.Click += (_, _) => Navigate(entry);
            var text = new SelectableTextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap,
                Name = "DiagnosticText", Foreground = Brushes.Black };
            return new Border
            {
                BorderThickness = new Thickness(4, 0, 0, 0), Padding = new Thickness(8), Margin = new Thickness(4),
                BorderBrush = entry.IsWarning ? Brush.Parse("#8A6500") : Brush.Parse("#A32121"),
                Background = entry.IsWarning ? Brush.Parse("#FFF4CC") : Brush.Parse("#FFE1E1"),
                Child = new StackPanel { Spacing = 6, Children =
                {
                    new TextBlock { Text = entry.CategoryText, FontWeight = FontWeight.Bold, Foreground = Brushes.Black },
                    source, text
                } }
            };
        });
        var panel = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(filters, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(Feedback, Avalonia.Controls.Dock.Top);
        panel.Children.Add(filters); panel.Children.Add(Feedback); panel.Children.Add(list);
        Content = panel;

        void Navigate(DiagnosticDisplayItem entry) => Feedback.Text = session.NavigateDiagnostic(entry) switch
        {
            SourceNavigationResult.Success => "",
            SourceNavigationResult.Stale => "Source is outdated: the document or its revision has changed. Run the script again to update diagnostics.",
            SourceNavigationResult.Busy => "Navigation unavailable while the editor is busy or controlled by MCP.",
            SourceNavigationResult.Invalid => "Source position is outside the document bounds.",
            _ => "Source position is unknown."
        };
    }
}

internal sealed class DockStatusView : UserControl
{
    internal DockStatusView(StatusViewModel model)
    {
        DataContext = model;
        AutomationProperties.SetAutomationId(this, DockShellState.StatusId);
        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
        text.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(StatusViewModel.StatusString)));
        Content = new ScrollViewer { Content = text };
    }
}
