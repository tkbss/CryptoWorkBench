using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Automation;
using Avalonia.Styling;

namespace CryptoWorkBenchAvalonia.Shell;

internal sealed class DockOutputView : UserControl
{
    internal DockOutputView(DockEditorSession session)
    {
        DataContext = session.Output;
        AutomationProperties.SetAutomationId(this, DockShellState.OutputId);
        var clear = new Button { Content = "Clear Output", HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(4, 0), MinHeight = 0, FontSize = 12 };
        clear.Click += (_, _) => session.Output.Clear();
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var list = new ListBox { Name = "OutputEntries", ItemsSource = session.Output.Entries, Padding = new Thickness(0) };
        list.Styles.Add(new Avalonia.Styling.Style(s => s.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Avalonia.Styling.Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                new Avalonia.Styling.Setter(MarginProperty, new Thickness(0)),
                new Avalonia.Styling.Setter(MinHeightProperty, 0d)
            }
        });
        list.ItemTemplate = new FuncDataTemplate<PrintOutputEntry>((entry, _) =>
        {
            if (entry == null) return new Border();
            var navigate = new Button { Name = "OutputSource", Content = entry.Label,
                HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(2, 0), MinHeight = 0, FontSize = 12 };
            navigate.Classes.Add("output-source");
            navigate.Click += (_, _) =>
            {
                var success = session.NavigateOutput(entry);
                feedback.Text = success ? "" : "Source unavailable, changed, or editor currently busy.";
                feedback.IsVisible = !success;
            };
            var text = new SelectableTextBlock { Text = entry.Text, FontFamily = FontFamily.Parse("Cascadia Code, Consolas, Menlo, monospace"), FontSize = 14,
                TextWrapping = TextWrapping.NoWrap, Name = "PrintText" };
            return new StackPanel { Spacing = 0, Margin = new Thickness(2, 0), Children = { navigate, text } };
        });
        ScrollViewer.SetHorizontalScrollBarVisibility(list, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(list, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        var panel = new DockPanel { Margin = new Thickness(4, 2), LastChildFill = true };
        DockPanel.SetDock(clear, Avalonia.Controls.Dock.Top); DockPanel.SetDock(feedback, Avalonia.Controls.Dock.Top);
        panel.Children.Add(clear); panel.Children.Add(feedback); panel.Children.Add(list);
        Content = panel;
    }
}
