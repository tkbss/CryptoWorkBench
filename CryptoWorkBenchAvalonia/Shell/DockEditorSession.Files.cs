using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services.Automation;
using Prism.Commands;

namespace CryptoWorkBenchAvalonia.Shell;

public sealed partial class DockEditorSession
{
    public AsyncDelegateCommand OpenCommand { get; private set; } = null!;
    public AsyncDelegateCommand SaveCommand { get; private set; } = null!;
    private void AddFileCommands()
    {
        OpenCommand = new AsyncDelegateCommand(() => FileAction(OpenAsync));
        SaveCommand = new AsyncDelegateCommand(() => FileAction(SaveAsync));
        View.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse("Ctrl+O"), Command = OpenCommand });
        View.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse("Ctrl+S"), Command = SaveCommand });
    }
    private async Task FileAction(Func<Task> action)
    {
        if (!CanRelocate) return;
        try { await action(); }
        catch (Exception) { if (!_service.IsStopping) ViewModel.Status.StatusString = "Document operation failed."; }
    }
    private Window Owner => TopLevel.GetTopLevel(View) as Window ?? _mainWindow
        ?? throw new InvalidOperationException("Editor window unavailable.");

    private Task OpenAsync() => ViewModel.WithFileDialogAsync(async intent =>
    {
        var selected = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open CryptoScript text (without execution)", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("CryptoScript / ScriptBook") { Patterns = ["*.sbk", "*.txt"] }, FilePickerFileTypes.All]
        });
        using var file = selected.Count == 0 ? null : selected[0];
        if (file == null || !_service.IsFileIntentCurrent(intent)) return;
        using var stream = await _service.StartFileIntentStep<Stream>(intent, async () => await file.OpenReadAsync());
        if (stream == null) return;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[AutomationContracts.MaxTextLength + 1];
        int count = await reader.ReadBlockAsync(buffer.AsMemory());
        if (count > AutomationContracts.MaxTextLength) throw new InvalidDataException("Document exceeds editor limit.");
        var text = new string(buffer, 0, count);
        if (!_service.IsFileIntentCurrent(intent)) return;
        if (intent.Snapshot.Length > 0 && await ConfirmAsync("Replace editor text?", "Unsaved editor text will be replaced. Undo remains available.") != true) return;
        LoadText(intent, text);
    });

    // Never invokes parser, ScriptBook execution, VariableState or the legacy Open command.
    internal bool LoadText(WorkbenchAutomationService.SaveIntent intent, string text)
    {
        if (text.Length > AutomationContracts.MaxTextLength || !_service.IsFileIntentCurrent(intent) ||
            _service.IsBusy || _service.EditorCoordinator.Phase != EditorControlPhase.Gui) return false;
        var coordinator = _service.EditorCoordinator;
        using var ticket = coordinator.TryBegin();
        if (ticket == null) return false;
        return coordinator.Commit(ticket, Editor, document =>
        {
            if (!_service.IsFileIntentCurrent(intent)) return;
            document.Replace(0, document.TextLength, text);
            Editor.IsModified = true;
            Editor.CaretOffset = 0;
        });
    }

    private Task SaveAsync() => ViewModel.SaveScriptBookAsync(async intent =>
    {
        var result = await _service.StartFileIntentStep(intent, () => Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save CryptoScript text", DefaultExtension = "sbk", SuggestedFileName = "ScriptBook.sbk", ShowOverwritePrompt = false
        }));
        if (result == null) return null;
        bool transferred = false;
        try
        {
            if (!_service.IsFileIntentCurrent(intent)) return null;
            var path = result.TryGetLocalPath();
            if (path == null) throw new IOException("A local file is required.");
            bool overwrite = File.Exists(path);
            if (overwrite && await ConfirmAsync("Overwrite file?", "Replace the selected file with the current editor text?") != true) return null;
            if (!_service.IsFileIntentCurrent(intent)) return null;
            transferred = true;
            return new(path, overwrite, overwrite ? () => result.DeleteAsync() : null, result, PreserveText: true);
        }
        finally { if (!transferred) result.Dispose(); }
    });

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new Window { Title = title, Width = 450, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var yes = new Button { Content = "Confirm" }; var no = new Button { Content = "Cancel" };
        yes.Click += (_, _) => dialog.Close(true); no.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12,
            Children = { new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, no, yes } };
        return await dialog.ShowDialog<bool>(Owner);
    }
}
