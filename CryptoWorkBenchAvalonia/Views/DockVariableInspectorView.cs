using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using CryptoWorkBenchAvalonia.Services.Inspector;
using CryptoWorkBenchAvalonia.ViewModels;

namespace CryptoWorkBenchAvalonia.Views;

// The shell owns the VM and view. Reparenting does not close the session.
internal sealed class DockVariableInspectorView : UserControl
{
    private readonly VariableInspectorViewModel model;
    private readonly TextBlock heading = new(), status = new(), preview = new();
    private readonly WrapPanel ancestors = new();
    private readonly StackPanel rows = new() { Spacing = 2 };
    private readonly TextBox full = new() { IsReadOnly = true, AcceptsReturn = true, MaxHeight = 180 };
    private readonly Button back, previous, next, cancel, previousText, nextText, reload;
    private readonly StackPanel detailPanel = new();
    private readonly TextBlock textRange = new();
    private const int TextPageSize = 4096;
    private int textPage;
    private string? shownText;
    private InspectorPage? rendered;
    internal DockVariableInspectorView(VariableInspectorViewModel model)
    {
        this.model = model;
        var layout = new DockPanel { Margin = new Thickness(6) };
        var header = new StackPanel { Spacing = 3 };
        var actions = new WrapPanel();
        back = Action("‹", "Parent property", () => model.OpenAsync(null,back:true));
        previous = Action("←", "Previous entries", () => model.OpenAsync(null,model.PageNumber-1));
        next = Action("→", "Next entries", () => model.OpenAsync(null,model.PageNumber+1));
        reload = Action("Reload","Retry inspector capture",model.ReloadAsync);
        cancel = Action("Cancel", "Cancel capture", () => { model.Cancel(); return Task.CompletedTask; });
        foreach (var button in new[] { back, previous, next, Action("Full value","Open full value",() => model.ValueAsync(null,false,null)),
            Action("⧉","Copy full value",() => model.ValueAsync(null,false,CopyAsync)),
            Action("Calculate & copy","Load including KCV, KCVC and KCVK and copy",() => model.ValueAsync(null,true,CopyAsync)),reload,cancel })
            actions.Children.Add(button);
        var columns = new Grid { ColumnDefinitions = new("2*,*,2*,Auto") };
        var columnNames = new[] { "Property", "Type", "Value", "Copy" };
        for (int i = 0; i < columnNames.Length; i++)
        {
            var label = Label(columnNames[i]); Grid.SetColumn(label,i); columns.Children.Add(label);
        }
        foreach (var item in new Control[] { heading,status,ancestors,preview,actions,columns }) header.Children.Add(item);
        KeyDown += async (_,args) =>
        {
            if (args.Handled || args.Source is TextBox || model.Busy) return;
            if (args.Key == Key.Left && model.CanBack)
            {
                args.Handled = true; await model.OpenAsync(null,back:true);
            }
            else if (args.Key == Key.Right && args.Source is Button b && b.Tag is InspectorRow r)
            {
                args.Handled = true; await model.OpenAsync(r);
            }
        };
        DockPanel.SetDock(header,Avalonia.Controls.Dock.Top); layout.Children.Add(header);
        var detailActions = new WrapPanel();
        previousText = Action("←", "Previous text page", () => ShowTextPage(textPage-1));
        nextText = Action("→", "Next text page", () => ShowTextPage(textPage+1));
        detailActions.Children.Add(previousText); detailActions.Children.Add(textRange); detailActions.Children.Add(nextText);
        detailPanel.Children.Add(detailActions); detailPanel.Children.Add(full);
        DockPanel.SetDock(detailPanel,Avalonia.Controls.Dock.Bottom); layout.Children.Add(detailPanel);
        detailPanel.IsVisible = false;
        AutomationProperties.SetName(full,"Full property value");
        layout.Children.Add(new ScrollViewer { Content = rows });
        Content = layout;
        model.Changed += Render;
        Render();
    }
    internal async Task CopyAsync(string value)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard ?? throw new InvalidOperationException("Clipboard unavailable.");
        await InspectorClipboard.SetAsync(clipboard,value,model.ActionCancellation);
    }
    private static Button Action(string text,string name,Func<Task> run)
    {
        var button = new Button { Content = text };
        AutomationProperties.SetName(button,name);
        ToolTip.SetTip(button,name);
        button.Click += async (_,_) => await run();
        return button;
    }
    private static TextBlock Label(string text) => new() { Text = DisplayTextPreview.Shorten(text), TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center };
    private Task ShowTextPage(int requested)
    {
        var value = shownText ?? "";
        textPage = Math.Clamp(requested,0,Math.Max(0,value.Length-1)/TextPageSize);
        int start = textPage*TextPageSize;
        int count = Math.Min(TextPageSize,value.Length-start);
        full.Text = value.Substring(start,count);
        previousText.IsEnabled = textPage > 0;
        nextText.IsEnabled = value.Length-start > count;
        textRange.Text = count == 0 ? "Empty" : $"{start+1:N0}–{start+count:N0} / {value.Length:N0}";
        return Task.CompletedTask;
    }
    private void Render()
    {
        var page = model.Page;
        heading.Text = DisplayTextPreview.Concat(model.Title, "  ", page?.RuntimeType);
        heading.TextWrapping = TextWrapping.Wrap;
        status.Text = model.Status;
        status.TextWrapping = TextWrapping.Wrap;
        preview.Text = DisplayTextPreview.Concat(model.Location, "  ", page?.Preview);
        preview.TextTrimming = TextTrimming.CharacterEllipsis;
        if (!ReferenceEquals(shownText,model.Detail))
        {
            shownText = model.Detail;
            detailPanel.IsVisible = shownText != null;
            _ = ShowTextPage(0);
        }
        // Returning to the root can disable the focused Back button. Remember
        // focus before changing button states or removing the old breadcrumb.
        bool restoreFocus = rows.IsKeyboardFocusWithin || ancestors.IsKeyboardFocusWithin ||
            back.IsFocused && !model.CanBack;
        back.IsEnabled = model.CanBack;
        previous.IsEnabled = model.PageNumber > 0;
        next.IsEnabled = page?.More == true;
        reload.IsVisible = model.CanReload;
        cancel.IsEnabled = model.Busy;
        if (ReferenceEquals(page,rendered)) return;
        rows.Children.Clear(); ancestors.Children.Clear(); rendered = page;
        for (int i = 0; i < model.Depth; i++)
        {
            int depth = i;
            ancestors.Children.Add(Action(DisplayTextPreview.Concat("▾ ", model.AncestorName(i)), DisplayTextPreview.Concat("Collapse to ", model.AncestorName(i)),
                () => model.OpenAsync(null,ancestor:depth)));
        }
        if (page == null) return;
        foreach (var row in page.Rows)
        {
            var line = new Grid { ColumnDefinitions = new("2*,*,2*,Auto") };
            bool computed = model.NeedsCalculation(row);
            var open = Action(DisplayTextPreview.Shorten(row.Name), DisplayTextPreview.Concat(computed ? "Calculate " : "Inspect ", row.Name),
                () => computed ? model.CalculateAsync(row) : model.OpenAsync(row));
            open.HorizontalAlignment = HorizontalAlignment.Stretch;
            open.Content = Label(DisplayTextPreview.Concat(row.State == "Reference · self" ? "↩ " : computed ? "Calculate " : "› ", row.Name));
            open.Tag = computed ? null : row;
            open.IsEnabled = row.State is "NotLoaded" or "NotCalculated" or "Method adapter" or "Reference · self" or "Reference";
            var type = Label(row.Type); Grid.SetColumn(type,1);
            var state = Label(row.Preview ?? (row.State == "NotCalculated" ? "Calculate" : row.State)); Grid.SetColumn(state,2);
            var copy = Action("⧉", DisplayTextPreview.Concat(computed ? "Calculate and copy " : "Copy ", row.Name),
                () => model.ValueAsync(row,computed,CopyAsync));
            copy.IsEnabled = open.IsEnabled; Grid.SetColumn(copy,3);
            foreach (var item in new Control[] {open,type,state,copy}) line.Children.Add(item);
            rows.Children.Add(line);
        }
        if (restoreFocus) Dispatcher.UIThread.Post(() => (rows.Children.OfType<Grid>().SelectMany(g => g.Children.OfType<Button>()).FirstOrDefault(b => b.IsEnabled) ?? back).Focus());
    }
}
