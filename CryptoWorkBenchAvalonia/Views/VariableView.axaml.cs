using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CryptoWorkBenchAvalonia.ViewModels;

namespace CryptoWorkBenchAvalonia;

public partial class VariableView : UserControl
{
    private VariableViewModel? bound;
    private void Bind(VariableViewModel? value)
    {
        if (ReferenceEquals(value, bound)) return;
        var previous = bound;
        bound = value;
        if (previous != null)
        {
            previous.Automation.EditorCoordinator.WorkspaceChanged -= previous.InvalidateDraft;
            previous.DetachView(this);
        }
        if (bound == null || !ReferenceEquals(bound, value)) return;
        bound.Automation.EditorCoordinator.WorkspaceChanged += bound.InvalidateDraft;
        bound.AttachView(this);
    }

    public VariableView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Bind(DataContext as VariableViewModel);
        DetachedFromVisualTree += (_, _) => Bind(null);
        DataContextChanged += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) != null) Bind(DataContext as VariableViewModel);
        };
    }
}
