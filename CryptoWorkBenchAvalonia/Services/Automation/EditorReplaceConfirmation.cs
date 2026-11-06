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
        return await ShowAsync(editor, "Confirm editor replacement",
            $"Client: {approval.Principal}\nEditor: {approval.Before.EditorId}   Revision: {approval.Before.Revision}",
            "Current content", approval.Before.Text, "Replacement content", approval.Edit.Text, "Replace", ct);
    }

    internal static async Task<bool?> ShowAsync(TextEditor editor, string title, string context, string firstLabel,
        string firstText, string secondLabel, string secondText, string accept, CancellationToken ct)
    {
        Task<bool?>? result = null;
        await UiDispatch.InvokeAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (TopLevel.GetTopLevel(editor) is not Window { IsVisible: true } owner) return;
            var dialog = new Window { Title = title, Width = 850, Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,*,Auto"), Margin = new Avalonia.Thickness(12) };
            void Add(Control control, int row) { Grid.SetRow(control, row); grid.Children.Add(control); }
            Add(new TextBlock { Text = context }, 0);
            Add(new TextBlock { Text = firstLabel }, 1);
            Add(new TextBox { Text = firstText, IsReadOnly = true, AcceptsReturn = true }, 2);
            Add(new TextBlock { Text = secondLabel }, 3);
            Add(new TextBox { Text = secondText, IsReadOnly = true, AcceptsReturn = true }, 4);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right };
            var yes = new Button { Content = accept }; var no = new Button { Content = "Cancel" };
            yes.Click += (_, _) => dialog.Close(true);
            no.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(no); buttons.Children.Add(yes); Add(buttons, 5);
            dialog.Content = grid;
            result = AwaitDialog(dialog, editor, owner, ct);
        });
        return result is null ? null : await result.ConfigureAwait(false);
    }

    private static async Task<bool?> AwaitDialog(Window dialog, TextEditor editor, Window owner, CancellationToken ct)
    {
        bool ownerLost = false;
        void OwnerClosed(object? sender, EventArgs args)
        {
            ownerLost = true;
            if (dialog.IsVisible) dialog.Close(false);
        }
        owner.Closed += OwnerClosed;
        Task close = Task.CompletedTask;
        var cancellation = ct.Register(() => close = UiDispatch.InvokeAsync(() =>
        {
            if (dialog.IsVisible) dialog.Close(false);
        }).GetTask());
        try
        {
            ct.ThrowIfCancellationRequested();
            bool accepted = await dialog.ShowDialog<bool>(owner);
            ct.ThrowIfCancellationRequested();
            return ownerLost || !owner.IsVisible || TopLevel.GetTopLevel(editor) != owner ? null : accepted;
        }
        finally
        {
            owner.Closed -= OwnerClosed;
            await cancellation.DisposeAsync();
            await close;
        }
    }
}
