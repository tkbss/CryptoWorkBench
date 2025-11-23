using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CryptoWorkBenchAvalonia.ViewModels;
using Prism.Ioc;

namespace CryptoWorkBenchAvalonia.Views;

public partial class InfoView : UserControl
{
    public InfoView()
    {     
        InitializeComponent();
    }
    //public InfoView(IContainerProvider container) : this()
    //{
    //    DataContext = container.Resolve<InfoViewModel>();
    //}
}