using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services.Inspector;
using CryptoWorkBenchAvalonia.ViewModels;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>Read-only presentation of the existing global variable snapshots.</summary>
public sealed class DockVariablesView : UserControl, IDisposable
{
    public VariableViewModel ViewModel { get; }
    private Action<string>? _reportFeedback;
    private long _copyGeneration;
    private bool _started;
    private bool _disposed;

    public DockVariablesView(VariableViewModel viewModel, Action? inspect = null)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        var refresh = new Button { Content = "Refresh stale values" };
        refresh.Bind(Button.CommandProperty, new Binding(nameof(viewModel.RefreshCommand)));
        refresh.Bind(IsVisibleProperty, new Binding(nameof(viewModel.IsSnapshotStale)));
        var stale = new TextBlock { Text = "Displayed snapshot is stale.", TextWrapping = TextWrapping.Wrap };
        stale.Bind(IsVisibleProperty, new Binding(nameof(viewModel.IsSnapshotStale)));
        var header = new StackPanel { Spacing = 4, Children = { stale, refresh } };
        if (inspect != null)
        {
            var button = new Button { Content = "Inspect Variable" };
            button.Bind(IsEnabledProperty, new Binding(nameof(viewModel.SelectedVariable))
                { Converter = Avalonia.Data.Converters.ObjectConverters.IsNotNull });
            button.Click += (_, _) => inspect();
            header.Children.Add(button);
        }
        var list = new VariableGrid(viewModel) { Name = "VariablesGrid", AutoGenerateColumns = false, IsReadOnly = true,
            CanUserReorderColumns = false, GridLinesVisibility = DataGridGridLinesVisibility.All, MinHeight = 100 };
        list.Bind(DataGrid.ItemsSourceProperty, new Binding(nameof(viewModel.DataVariables)));
        list.Bind(DataGrid.SelectedItemProperty, new Binding(nameof(viewModel.SelectedVariable)) { Mode = BindingMode.OneWay });
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

    // DataGrid rebuilds its rows on attach, temporarily selecting null and then
    // its currency row. Only user selection belongs in the shared model.
    private sealed class VariableGrid : DataGrid
    {
        private readonly VariableViewModel _variables;
        private bool _attached;
        protected override Type StyleKeyOverride => typeof(DataGrid);

        internal VariableGrid(VariableViewModel variables)
        {
            _variables = variables;
            SelectionChanged += (_, _) =>
            {
                if (_attached) _variables.SelectedVariable = SelectedItem as VariableModel;
            };
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SelectedItem = _variables.SelectedVariable;
            _attached = true;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _attached = false;
            base.OnDetachedFromVisualTree(e);
        }
    }

    private SelectableTextBlock Text(string path, string? name = null)
    {
        var text = new SelectableTextBlock { Name = name, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3) };
        string? abbreviatedSource = null;
        var converter = new FuncValueConverter<string?, string?>(source =>
        {
            var preview = source == null ? null : DisplayTextPreview.Shorten(source);
            abbreviatedSource = ReferenceEquals(source, preview) ? null : source;
            return preview;
        });
        text.Bind(TextBlock.TextProperty, new Binding(path) { Mode = BindingMode.OneWay, Converter = converter });
        text.CopyingToClipboard += async (_, e) =>
        {
            // The final ellipsis represents the omitted suffix. Only expand it on explicit copy.
            int start = Math.Min(text.SelectionStart, text.SelectionEnd);
            int end = Math.Max(text.SelectionStart, text.SelectionEnd);
            if (start == end) return;
            // Each selection copy supersedes earlier feedback, including a normal control copy.
            long generation = ++_copyGeneration;
            try
            {
                if (abbreviatedSource is not { } source || end != text.Text?.Length)
                {
                    if (OperatingSystem.IsWindows()) InspectorClipboard.ValidateWindowsText(text.SelectedText);
                    return;
                }

                e.Handled = true;
                string selected = source[start..];
                if (OperatingSystem.IsWindows()) InspectorClipboard.ValidateWindowsText(selected);
                var clipboard = TopLevel.GetTopLevel(text)?.Clipboard ?? throw new InvalidOperationException("Clipboard unavailable.");
                await clipboard.SetTextAsync(selected);
                if (generation == _copyGeneration)
                    _reportFeedback?.Invoke($"Copied {selected.Length:N0} characters.");
            }
            catch (Exception error)
            {
                e.Handled = true;
                if (generation == _copyGeneration)
                    _reportFeedback?.Invoke(DisplayTextPreview.Concat("Copy failed. ", error.GetBaseException().Message));
            }
        };
        return text;
    }

    private DataGridTemplateColumn Column(string title, string path) => new()
    {
        Header = title, IsReadOnly = true,
        CellTemplate = new FuncDataTemplate<object>((_, _) => Text(path))
    };

    private DataGridTemplateColumn SummaryColumn(string title, string path) => new()
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

    private Control Details()
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

    // Retire feedback on the UI thread while the original clipboard transfer finishes.
    internal void InvalidatePendingCopyFeedback()
    {
        VerifyAccess();
        ++_copyGeneration;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        InvalidatePendingCopyFeedback();
        base.OnDetachedFromVisualTree(e);
    }

    internal void Start(Action<string> reportFeedback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;
        _reportFeedback = reportFeedback;
        _started = true;
        ViewModel.Automation.EditorCoordinator.WorkspaceChanged += ViewModel.InvalidateDraft;
        ViewModel.AttachView(this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reportFeedback = null;
        if (!_started) return;
        ViewModel.Automation.EditorCoordinator.WorkspaceChanged -= ViewModel.InvalidateDraft;
        ViewModel.DetachView(this);
    }
}
