using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using AvaloniaEdit;
using CryptoWorkBench.Automation.Abstractions;

namespace CryptoWorkBenchAvalonia.Services.Automation;

internal sealed record EditorReplaceApproval(string Principal, Guid InstanceId, EditorSnapshot Before, EditorEdit Edit);

internal interface IEditorReplaceConfirmation
{
    Task<bool?> ConfirmAsync(TextEditor editor, EditorReplaceApproval approval, CancellationToken ct);
}

internal sealed class EditorReplaceConfirmation : IEditorReplaceConfirmation
{
    public async Task<bool?> ConfirmAsync(TextEditor editor, EditorReplaceApproval approval, CancellationToken ct)
    {
        Task<bool?>? result = null;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (TopLevel.GetTopLevel(editor) is not Window owner) return;
            var dialog = new Window { Title = "Confirm editor replacement", Width = 850, Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,*,Auto"), Margin = new Avalonia.Thickness(12) };
            void Add(Control control, int row) { Grid.SetRow(control, row); grid.Children.Add(control); }
            Add(new TextBlock { Text = $"Client: {approval.Principal}\nEditor: {approval.Before.EditorId}   Revision: {approval.Before.Revision}" }, 0);
            Add(new TextBlock { Text = "Current content" }, 1);
            Add(new TextBox { Text = approval.Before.Text, IsReadOnly = true, AcceptsReturn = true }, 2);
            Add(new TextBlock { Text = "Replacement content" }, 3);
            Add(new TextBox { Text = approval.Edit.Text, IsReadOnly = true, AcceptsReturn = true }, 4);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right };
            var yes = new Button { Content = "Replace" }; var no = new Button { Content = "Cancel" };
            yes.Click += (_, _) => dialog.Close(true);
            no.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(no); buttons.Children.Add(yes); Add(buttons, 5);
            dialog.Content = grid;
            result = AwaitDialog(dialog, owner, ct);
        });
        return result is null ? null : await result.ConfigureAwait(false);
    }

    private static async Task<bool?> AwaitDialog(Window dialog, Window owner, CancellationToken ct)
    {
        using var cancellation = ct.Register(() => Dispatcher.UIThread.Post(() => dialog.Close(false)));
        bool accepted = await dialog.ShowDialog<bool>(owner);
        ct.ThrowIfCancellationRequested();
        return accepted;
    }
}
