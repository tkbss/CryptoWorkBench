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

    public InfoView()
    {
        InitializeComponent();
        InfoMarkdownViewer.DataContextChanged += OnMarkdownDataContextChanged;
        ConfigureHyperlinkCommand();
    }

    private void OnMarkdownDataContextChanged(object? sender, EventArgs e) =>
        ConfigureHyperlinkCommand();

    private void ConfigureHyperlinkCommand()
    {
        if (InfoMarkdownViewer.Engine is not global::Markdown.Avalonia.Markdown markdown)
            return;

        _defaultHyperlinkCommand ??= markdown.HyperlinkCommand;
        markdown.HyperlinkCommand = InfoMarkdownViewer.DataContext is InfoViewModel viewModel
            ? new InfoHyperlinkCommand(viewModel.OpenInfoLinkCommand, _defaultHyperlinkCommand)
            : _defaultHyperlinkCommand;
    }

    private sealed class InfoHyperlinkCommand : ICommand
    {
        private const string InternalSchemePrefix = "cryptoscript-info:";
        private readonly ICommand _internalCommand;
        private readonly ICommand? _defaultCommand;

        public InfoHyperlinkCommand(ICommand internalCommand, ICommand? defaultCommand)
        {
            _internalCommand = internalCommand;
            _defaultCommand = defaultCommand;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => IsInternal(parameter)
            ? _internalCommand.CanExecute(parameter)
            : _defaultCommand?.CanExecute(parameter) == true;

        public void Execute(object? parameter)
        {
            if (IsInternal(parameter))
                _internalCommand.Execute(parameter);
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
