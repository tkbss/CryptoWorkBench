using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;
namespace CryptoWorkBenchAvalonia.Views;

public partial class SidebarView : UserControl
{
    SidebarViewModel _vm;
    public SidebarView()
    {
        InitializeComponent();
    }
    public SidebarView(SidebarViewModel vm):this()
    {

        _vm = vm;        
        DataContext = vm;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        _vm._window = TopLevel.GetTopLevel(this);
        base.OnLoaded(e);
    }
}