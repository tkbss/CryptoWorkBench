using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CryptoWorkBenchAvalonia.ViewModels;
using Prism.Ioc;
using System;
using System.Windows.Input;

namespace CryptoWorkBenchAvalonia.Views;

public partial class InfoView : UserControl
{
    private ICommand? _defaultHyperlinkCommand;
    private object? _linkContext;
    private bool _detached;

    public InfoView()
    {
        InitializeComponent();
        InfoMarkdownViewer.DataContextChanged += OnMarkdownDataContextChanged;
        ConfigureHyperlinkCommand();
        AttachedToVisualTree += (_, _) => { _detached = false; ConfigureHyperlinkCommand(); };
        DetachedFromVisualTree += (_, _) => { _detached = true; _linkContext = null; };
    }

    private void OnMarkdownDataContextChanged(object? sender, EventArgs e) =>
        ConfigureHyperlinkCommand();

    private void ConfigureHyperlinkCommand()
    {
        if (_detached || InfoMarkdownViewer.Engine is not global::Markdown.Avalonia.Markdown markdown)
            return;

        var context = _linkContext = new object();
        _defaultHyperlinkCommand ??= markdown.HyperlinkCommand;
        var dataContext = InfoMarkdownViewer.DataContext;
        markdown.HyperlinkCommand = new InfoHyperlinkCommand(dataContext as InfoViewModel, _defaultHyperlinkCommand,
            () => ReferenceEquals(_linkContext, context) && ReferenceEquals(InfoMarkdownViewer.DataContext, dataContext));
    }

    private sealed class InfoHyperlinkCommand : ICommand
    {
        private const string InternalSchemePrefix = "cryptoscript-info:";
        private readonly InfoViewModel? _viewModel;
        private readonly Func<bool> _current;
        private readonly ICommand? _defaultCommand;

        public InfoHyperlinkCommand(InfoViewModel? viewModel, ICommand? defaultCommand, Func<bool> current)
        {
            _viewModel = viewModel;
            _current = current;
            _defaultCommand = defaultCommand;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => _current() && (_viewModel != null && IsInternal(parameter)
            ? _viewModel.OpenInfoLinkCommand.CanExecute((string)parameter!)
            : _defaultCommand?.CanExecute(parameter) == true);

        public void Execute(object? parameter)
        {
            if (!_current()) return;
            if (_viewModel != null && IsInternal(parameter))
                _viewModel.Publish(() => _viewModel.OpenInfoLinkCommand.Execute((string)parameter!));
            else if (_defaultCommand?.CanExecute(parameter) == true)
                _defaultCommand.Execute(parameter);
        }

        private static bool IsInternal(object? parameter) =>
            parameter is string link &&
            link.StartsWith(InternalSchemePrefix, StringComparison.OrdinalIgnoreCase);
    }

    //public InfoView(IContainerProvider container) : this()
    //{
    //    DataContext = container.Resolve<InfoViewModel>();
    //}
}
