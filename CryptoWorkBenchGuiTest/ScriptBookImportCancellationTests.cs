using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class ScriptBookImportCancellationTests
{
    [TestCase("Yes")]
    [TestCase("No")]
    [TestCase("Close")]
    [TestCase("Escape")]
    [TestCase("Owner")]
    [TestCase("EmptyEditor")]
    [TestCase("Cancel")]
    [TestCase("Unknown")]
    public async Task ActualDialogPreservesImportIntent(string choice)
    {
        await GuiTestSession.Session.Dispatch(async () =>
        {
            await RunScenario(choice, dialog =>
            {
                dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
                if (dialog.IsVisible)
                    dialog.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            });
            return true;
        }, CancellationToken.None);
    }

    // Shared with the external native Windows probe: only keyboard delivery differs.
    public static async Task RunScenario(string choice, Action<Window> escape, Action<string>? log = null)
    {
        bool imports = choice is "Yes" or "No" or "EmptyEditor";
        var dictionary = VariableDictionary.Instance();
        var previousVariables = dictionary.GetVariables().ToArray();
        string directory = Path.Combine(Path.GetTempPath(), "A12-F1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "harmless.sbk");
        const string script = "VAR f1Imported=0x(AB)\n";
        await File.WriteAllTextAsync(filePath, script);
        using var lockedFile = !imports
            ? new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None)
            : null;

        var service = new WorkbenchAutomationService();
        var status = new StatusViewModel();
        using var vm = new CryptoScriptEditViewModel(status, new VariableViewModel(), new InfoViewModel(),
            new MainViewModel(), null!, new HistoryService(), service);
        var view = new CryptoScriptEditView(vm);
        var editor = view.FindControl<WorkbenchTextEditor>("Editor")!;
        var owner = new Window { Content = view, Width = 640, Height = 360, Title = "A12-F1 " + choice };
        Task? operation = null;
        var providerField = typeof(TopLevel).GetField("_storageProvider", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object? oldProvider = null;
        bool providerReplaced = false;
        try
        {
            dictionary.Clear();
            dictionary.Add(new StringVariableDeclaration { Id = "f1Existing", Value = "retained" });
            owner.Show();
            var sidebar = new SidebarViewModel(null!, vm, null!) { _window = owner };
            var file = DispatchProxy.Create<IStorageFile, StorageProbe>();
            var fileProbe = (StorageProbe)(object)file;
            fileProbe.FilePath = filePath;
            var provider = DispatchProxy.Create<IStorageProvider, StorageProbe>();
            ((StorageProbe)(object)provider).File = file;
            oldProvider = providerField.GetValue(owner);
            providerField.SetValue(owner, provider);
            providerReplaced = true;

            if (choice != "EmptyEditor")
            {
                editor.Text = "BASE\n";
                editor.Document.Insert(editor.Document.TextLength, "undo-kept\n");
                editor.Document.Insert(editor.Document.TextLength, "redo-kept\n");
                editor.Document.UndoStack.Undo();
                Assert.That(editor.Document.UndoStack.CanUndo && editor.Document.UndoStack.CanRedo, Is.True);
            }
            var document = editor.Document;
            var version = document.Version;
            string beforeText = editor.Text;
            string beforeStatus = status.StatusString;
            var beforeState = (await service.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default)).Data!;
            var beforeEditor = (await service.GetEditorAsync(WorkbenchAutomationService.LocalCaller, default)).Data!;
            int changes = 0;
            document.Changed += (_, _) => changes++;

            // Execute the public command, including GuardAsync and normal command reentrancy handling.
            operation = ((Prism.Commands.IAsyncCommand)sidebar.OpenScriptBookCommand).ExecuteAsync(null);
            if (choice != "EmptyEditor")
            {
                var dialog = await WaitForDialog(owner, operation);
                Assert.That(dialog.Owner, Is.SameAs(owner));
                Assert.That(operation.IsCompleted, Is.False);
                if (choice == "Close") dialog.Close();
                else if (choice == "Owner") owner.Close();
                else if (choice == "Escape")
                {
                    var focus = dialog.GetVisualDescendants().OfType<Button>()
                        .First(b => b.Content?.ToString() == "Yes");
                    Assert.That(focus.Focus(), Is.True);
                    escape(dialog);
                }
                else
                {
                    string buttonName = choice == "Unknown" ? "No" : choice;
                    var button = dialog.GetVisualDescendants().OfType<Button>()
                        .Single(b => b.Content?.ToString() == buttonName);
                    // Invalid-result fault injection still goes through the actual MessageBox command.
                    if (choice == "Unknown") button.CommandParameter = "unexpected-result";
                    typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(button, null);
                }
            }
            await operation.WaitAsync(TimeSpan.FromSeconds(4));
            await service.DrainAsync();
            Assert.That(owner.OwnedWindows, Is.Empty);
            var afterState = (await service.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default)).Data!;
            Assert.That(vm.IsExecuting, Is.False);
            Assert.That(afterState.ActiveExecutionId, Is.Null);
            Assert.That(afterState.MutationBusy, Is.False);
            Assert.That(editor.Document, Is.SameAs(document));

            int changesBeforeHistoryCheck = changes;
            if (imports)
            {
                Assert.That(fileProbe.PathReads, Is.EqualTo(1));
                Assert.That(vm.LastScriptBookSucceeded, Is.True);
                Assert.That(dictionary.Contains("f1Imported"), Is.True, "the real runner must execute the harmless book");
                Assert.That(dictionary.Get("f1Imported").Value, Is.EqualTo("0x(AB)"));
                Assert.That(dictionary.Contains("f1Existing"), Is.EqualTo(choice != "Yes"));
                Assert.That(afterState.LastExecutionId, Is.Not.EqualTo(beforeState.LastExecutionId));
                Assert.That(editor.Text, Is.EqualTo((choice == "Yes" ? "" : beforeText) + script));
            }
            else
            {
                Assert.That(fileProbe.PathReads, Is.Zero, "cancellation must stop before even evaluating the selected file path");
                Assert.That(vm.LastScriptBookSucceeded, Is.False);
                Assert.That(dictionary.Contains("f1Imported"), Is.False);
                Assert.That(dictionary.GetVariables().Select(v => v.Id), Is.EqualTo(new[] { "f1Existing" }));
                Assert.That(afterState.StateVersion, Is.EqualTo(beforeState.StateVersion));
                Assert.That(afterState.LastExecutionId, Is.EqualTo(beforeState.LastExecutionId), "no accepted queue operation");
                Assert.That(status.StatusString, Is.EqualTo(beforeStatus));
                Assert.That(status.Diagnostics, Is.Empty);
                Assert.That(changes, Is.Zero);
                Assert.That(document.Version, Is.SameAs(version));
                Assert.That(editor.Text, Is.EqualTo(beforeText));
                Assert.That(document.UndoStack.CanUndo && document.UndoStack.CanRedo, Is.True);
                if (choice != "Owner")
                {
                    var afterEditor = (await service.GetEditorAsync(WorkbenchAutomationService.LocalCaller, default)).Data!;
                    Assert.That(afterEditor.EditorId, Is.EqualTo(beforeEditor.EditorId));
                    Assert.That(afterEditor.Revision, Is.EqualTo(beforeEditor.Revision));
                }
                // Verify both original history directions still perform the original edits.
                document.UndoStack.Redo();
                Assert.That(editor.Text, Is.EqualTo(beforeText + "redo-kept\n"));
                document.UndoStack.Undo();
                Assert.That(editor.Text, Is.EqualTo(beforeText));
                document.UndoStack.Undo();
                Assert.That(editor.Text, Is.EqualTo("BASE\n"));
            }
            log?.Invoke($"PASS {choice}: pathReads={fileProbe.PathReads}, imported={dictionary.Contains("f1Imported")}, accepted={afterState.LastExecutionId != beforeState.LastExecutionId}, documentChangesBeforeHistoryCheck={changesBeforeHistoryCheck}");
        }
        finally
        {
            foreach (var dialog in owner.OwnedWindows.ToArray()) dialog.Close();
            if (operation is not null)
                await operation.WaitAsync(TimeSpan.FromSeconds(5));
            if (providerReplaced) providerField.SetValue(owner, oldProvider);
            if (owner.IsVisible) owner.Close();
            await service.DisposeAsync();
            dictionary.Clear();
            foreach (var variable in previousVariables) dictionary.Add(variable);
            lockedFile?.Dispose();
            Directory.Delete(directory, true);
        }
    }

    private static async Task<Window> WaitForDialog(Window owner, Task operation)
    {
        for (int i = 0; i < 100; i++)
        {
            if (owner.OwnedWindows.SingleOrDefault() is { } dialog) return dialog;
            if (operation.IsCompleted) break;
            await Task.Delay(10);
        }
        throw new AssertionException("Import confirmation did not open.");
    }

    public class StorageProbe : DispatchProxy
    {
        public string? FilePath;
        public IStorageFile? File;
        public int PathReads;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "OpenFilePickerAsync": return Task.FromResult<IReadOnlyList<IStorageFile>>(new[] { File! });
                case "get_Path": PathReads++; return new Uri(FilePath!);
                case "get_Name": return Path.GetFileName(FilePath);
                case "get_CanOpen": return true;
                case "Dispose": return null;
                default: throw new NotSupportedException("F1 storage probe: " + method.Name);
            }
        }
    }
}

