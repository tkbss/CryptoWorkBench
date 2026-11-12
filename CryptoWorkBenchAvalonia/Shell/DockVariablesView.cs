using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.ViewModels;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>Read-only presentation of the existing global variable snapshots.</summary>
public sealed class DockVariablesView : UserControl, IDisposable
{
    public VariableViewModel ViewModel { get; }
    private bool _started;
    private bool _disposed;

    public DockVariablesView(VariableViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        var refresh = new Button { Content = "Refresh stale values" };
        refresh.Bind(Button.CommandProperty, new Binding(nameof(viewModel.RefreshCommand)));
        refresh.Bind(IsVisibleProperty, new Binding(nameof(viewModel.IsSnapshotStale)));
        var stale = new TextBlock { Text = "Displayed snapshot is stale.", TextWrapping = TextWrapping.Wrap };
        stale.Bind(IsVisibleProperty, new Binding(nameof(viewModel.IsSnapshotStale)));
        var header = new StackPanel { Spacing = 4, Children = { stale, refresh } };
        var list = new DataGrid { Name = "VariablesGrid", AutoGenerateColumns = false, IsReadOnly = true,
            CanUserReorderColumns = false, GridLinesVisibility = DataGridGridLinesVisibility.All, MinHeight = 100 };
        list.Bind(DataGrid.ItemsSourceProperty, new Binding(nameof(viewModel.DataVariables)));
        list.Bind(DataGrid.SelectedItemProperty, new Binding(nameof(viewModel.SelectedVariable)) { Mode = BindingMode.TwoWay });
        list.Columns.Add(SummaryColumn("Type", nameof(VariableModel.Type)));
        list.Columns.Add(SummaryColumn("Identifier", nameof(VariableModel.Identifier)));
        list.Columns.Add(SummaryColumn("Value", nameof(VariableModel.Value)));

        var details = new ContentControl { Name = "VariableDetails", ContentTemplate = new FuncDataTemplate<VariableModel>((_, _) => Details()) };
        details.Bind(ContentControl.ContentProperty, new Binding(nameof(viewModel.SelectedVariable)) { Mode = BindingMode.OneWay });
        var scroll = new ScrollViewer { Content = details, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        scroll.Bind(IsVisibleProperty, new Binding(nameof(viewModel.SelectedVariable))
            { Converter = Avalonia.Data.Converters.ObjectConverters.IsNotNull });
        var panel = new Grid { Margin = new Thickness(4), RowDefinitions = new RowDefinitions("Auto,2*,3*"), RowSpacing = 4 };
        panel.RowDefinitions[2].Bind(RowDefinition.HeightProperty, new Binding(nameof(viewModel.SelectedVariable))
        {
            Source = viewModel,
            Converter = new Avalonia.Data.Converters.FuncValueConverter<VariableModel?, GridLength>(
                selected => selected == null ? new GridLength(0) : new GridLength(3, GridUnitType.Star))
        });
        panel.Children.Add(header); Grid.SetRow(list, 1); panel.Children.Add(list); Grid.SetRow(scroll, 2); panel.Children.Add(scroll);
        Content = panel;
    }

    private static SelectableTextBlock Text(string path, string? name = null)
    {
        var text = new SelectableTextBlock { Name = name, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3) };
        text.Bind(TextBlock.TextProperty, new Binding(path) { Mode = BindingMode.OneWay });
        return text;
    }

    private static DataGridTemplateColumn Column(string title, string path) => new()
    {
        Header = title, IsReadOnly = true,
        CellTemplate = new FuncDataTemplate<object>((_, _) => Text(path))
    };

    private static DataGridTemplateColumn SummaryColumn(string title, string path) => new()
    {
        Header = title, IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 60,
        CellTemplate = new FuncDataTemplate<object>((_, _) =>
        {
            var cell = Text(path);
            cell.TextWrapping = TextWrapping.NoWrap;
            cell.TextTrimming = TextTrimming.CharacterEllipsis;
            return cell;
        })
    };

    private static Control Details()
    {
        var panel = new StackPanel { Spacing = 5 };
        foreach (var (label, path, visible) in new (string, string, string?)[]
        {
            ("Identifier", "Identifier", null), ("Type", "Type", null), ("Value", "Value", null),
            ("Format", "ValueFormat", null), ("Mechanism", "Mechanism", "IsParameter"),
            ("Algorithm", "Algorithm", "IsKey"), ("Material", "Material", "IsKey"),
            ("Key size", "KeySize", "HasKeySize"), ("Derivation", "Derivation", "HasDerivation"), ("GMAC", "GMAC", null)
        })
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*") };
            row.Children.Add(new TextBlock { Text = label + ":", Margin = new Thickness(3) });
            var value = Text(path, "Selected" + path); Grid.SetColumn(value, 1); row.Children.Add(value);
            if (visible != null) row.Bind(IsVisibleProperty, new Binding(visible));
            panel.Children.Add(row);
        }
        var parameters = new DataGrid { Name = "ParametersGrid", AutoGenerateColumns = false, IsReadOnly = true,
            CanUserReorderColumns = false, MaxHeight = 300 };
        parameters.Bind(DataGrid.ItemsSourceProperty, new Binding(nameof(VariableModel.Parameters)));
        parameters.Columns.Add(Column("Parameter", nameof(ParameterModel.Parameter)));
        parameters.Columns.Add(Column("Value", nameof(ParameterModel.Value)));
        panel.Children.Add(parameters);
        return panel;
    }

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;
        _started = true;
        ViewModel.Automation.EditorCoordinator.WorkspaceChanged += ViewModel.InvalidateDraft;
        ViewModel.AttachView(this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_started) return;
        ViewModel.Automation.EditorCoordinator.WorkspaceChanged -= ViewModel.InvalidateDraft;
        ViewModel.DetachView(this);
    }
}
