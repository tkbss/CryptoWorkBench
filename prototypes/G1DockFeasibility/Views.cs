using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace G1DockFeasibility;

public sealed class EditorView : UserControl
{
    private readonly DocumentSession _session;
    private int _generation;
    public TextEditor Editor { get; } = new() { ShowLineNumbers = true, FontFamily = FontFamily.Default, FontSize = 15 };
    public EditorView(DocumentSession session)
    {
        _session = session;
        session.ViewCount++;
        Content = Editor;
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.AttachCount++;
        _session.AttachedViews++;
        Editor.Document = _session.Document;
        Editor.CaretOffset = Math.Clamp(_session.CaretOffset, 0, _session.Document.TextLength);
        var generation = ++_generation;
        Dispatcher.UIThread.Post(() =>
        {
            if (generation == _generation && TopLevel.GetTopLevel(this) is not null) Editor.TextArea.Focus();
        }, DispatcherPriority.Loaded);
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ++_generation;
        _session.CaretOffset = Editor.CaretOffset;
        _session.DetachCount++;
        _session.AttachedViews--;
        // Release the document subscriptions held by a discarded visual editor.
        Editor.Document = null;
        base.OnDetachedFromVisualTree(e);
    }
}

public sealed class ProbeViewLocator : IDataTemplate
{
    public bool Match(object? data) => data is IDocument or ITool;
    public Control Build(object? data) => (data as IDockable)?.Context switch
    {
        DocumentSession session => new EditorView(session),
        VariablesViewModel vm => ToolView("Variables (placeholder)", vm.State),
        DiagnosticsViewModel vm => ToolView("Diagnostics (placeholder)", vm.State),
        _ => new TextBlock { Text = "Unresolved probe content" }
    };
    private static Control ToolView(string title, WorkspaceState state)
    {
        var text = new TextBlock { Text = $"{title}\nWorkspace: {state.InstanceId}\nVisits: {state.ToolVisits}", TextWrapping = TextWrapping.Wrap };
        var button = new Button { Content = "Visit shared state" };
        button.Click += (_, _) => text.Text = $"{title}\nWorkspace: {state.InstanceId}\nVisits: {++state.ToolVisits}";
        return new StackPanel { Margin = new Thickness(10), Spacing = 12, Children = { text, button } };
    }
}
