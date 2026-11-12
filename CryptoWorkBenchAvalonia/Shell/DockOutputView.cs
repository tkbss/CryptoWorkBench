using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Automation;

namespace CryptoWorkBenchAvalonia.Shell;

internal sealed class DockOutputView : UserControl
{
    internal DockOutputView(DockEditorSession session)
    {
        DataContext = session.Output;
        AutomationProperties.SetAutomationId(this, DockShellState.OutputId);
        var clear = new Button { Content = "Clear Output", HorizontalAlignment = HorizontalAlignment.Left };
        clear.Click += (_, _) => session.Output.Clear();
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var list = new ListBox { ItemsSource = session.Output.Entries };
        list.ItemTemplate = new FuncDataTemplate<PrintOutputEntry>((entry, _) =>
        {
            if (entry == null) return new Border();
            var navigate = new Button { Content = entry.Label, HorizontalAlignment = HorizontalAlignment.Left };
            navigate.Click += (_, _) => feedback.Text = session.NavigateOutput(entry)
                ? "" : "Source unavailable, changed, or editor currently busy.";
            var text = new SelectableTextBlock { Text = entry.Text, FontFamily = FontFamily.Parse("Consolas"),
                TextWrapping = TextWrapping.NoWrap, Name = "PrintText" };
            return new StackPanel { Spacing = 4, Margin = new Thickness(4), Children = { navigate, text } };
        });
        ScrollViewer.SetHorizontalScrollBarVisibility(list, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        var panel = new DockPanel { Margin = new Thickness(12), LastChildFill = true };
        DockPanel.SetDock(clear, Avalonia.Controls.Dock.Top); DockPanel.SetDock(feedback, Avalonia.Controls.Dock.Top);
        panel.Children.Add(clear); panel.Children.Add(feedback); panel.Children.Add(list);
        Content = panel;
    }
}
