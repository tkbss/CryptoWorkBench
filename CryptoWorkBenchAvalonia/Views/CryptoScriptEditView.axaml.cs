using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.ViewModels;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia;

public partial class CryptoScriptEditView : UserControl
{
    private OverloadInsightWindow? _insightWindow;
    private readonly TextEditor? _textEditor;
    private InfoCompletionController? _infoCompletionController;
    private TextBlock? _statusTextBlock;
    private CryptoScriptEditViewModel? _viewModel;
    public Task PendingExecution { get; private set; } = Task.CompletedTask;
    public CryptoScriptEditView()
    {        
        InitializeComponent();      
    }
    // Constructor that accepts a ViewModel
    public CryptoScriptEditView(CryptoScriptEditViewModel vm) : this()
    {
        _viewModel = vm;
        _textEditor = this.FindControl<TextEditor>("Editor");
        if (_textEditor != null && _viewModel != null)
        {
            // Initialize native dirty/history state before publishing the editor.
            _textEditor.Clear();
            _viewModel.TextEditor = _textEditor;
        }
        _statusTextBlock = this.Find<TextBlock>("StatusText");
        AttachedToVisualTree += (sender, args) =>
        {
            _viewModel!.TextEditor = _textEditor;
            _viewModel.AttachEditor();
            try
            {
                EnsureInfoCompletionController();
                ((WorkbenchTextArea)_textEditor!.TextArea).AdmittedInput = OnAdmittedInput;
                _textEditor.TextArea.TextEntered += TextEntered;
                _textEditor.TextArea.Caret.PositionChanged += CaretChanged;
            }
            catch { Detach(); throw; }
        };
        DetachedFromVisualTree += (_, _) => Detach();

        void Detach()
        {
            ((WorkbenchTextArea)_textEditor!.TextArea).AdmittedInput = null;
            _textEditor.TextArea.TextEntered -= TextEntered;
            _textEditor.TextArea.Caret.PositionChanged -= CaretChanged;
            try { _viewModel!.DetachEditor(_textEditor); }
            finally
            {
                var completion = _infoCompletionController;
                _infoCompletionController = null;
                try { completion?.Dispose(); }
                finally { _insightWindow?.Hide(); }
            }
        }
    }

    private void TextEntered(object? sender, TextInputEventArgs args) =>
        ObserveEditor(() => textEditor_TextArea_TextEntered(sender!, args));

    private void CaretChanged(object? sender, EventArgs args) =>
        ObserveEditor(() => Caret_PositionChanged(sender!, args));
    private void ObserveEditor(Action callback)
    {
        if (_textEditor is CryptoWorkBenchAvalonia.Controls.WorkbenchTextEditor { Coordinator: { } coordinator })
            coordinator.Observe(callback);
        else callback();
    }

    private void EnsureInfoCompletionController()
    {
        if (_infoCompletionController == null && _textEditor != null)
        {
            _infoCompletionController = new InfoCompletionController(
                _textEditor,
                new InfoCompletionProvider());
        }
    }
    private void OnAdmittedInput(TextInputEventArgs input, Func<bool> stillAdmitted, EditorCoordinator.OperationTicket ticket)
    {
        if (string.IsNullOrEmpty(input.Text) || input.Handled) return;
        var editor = _textEditor!;
        var vm = _viewModel!;
        var document = editor.Document;
        var caret = editor.TextArea.Caret.Position;
        var selection = editor.TextArea.Selection;
        long lifecycle = vm.EditorLifecycle;
        bool isReturn = input.Text.Contains('\n') || input.Text.Contains('\r');
        var line = isReturn ? document.GetLineByNumber(caret.Line) : null;
        var source = line == null ? null : document.GetText(line);

        bool IsCurrent()
        {
            bool current = stillAdmitted() && vm.EditorLifecycle == lifecycle &&
                ReferenceEquals(editor.Document, document) && editor.TextArea.Caret.Position.Equals(caret) &&
                ReferenceEquals(editor.TextArea.Selection, selection);
            if (!current) input.Handled = true;
            return current;
        }

        ObserveEditor(() => _infoCompletionController?.HandleTextEntering(input.Text));
        if (_insightWindow != null) ObserveEditor(() => _insightWindow?.Hide());
        if (line == null || !IsCurrent()) return;
        if (vm.IsExecuting) { input.Handled = true; return; }

        var anchor = document.CreateAnchor(line.EndOffset);
        anchor.MovementType = AnchorMovementType.BeforeInsertion;
        PendingExecution = ExecuteInputAsync(source!, document, anchor, lifecycle, IsCurrent, input, ticket);
    }

    private async Task ExecuteInputAsync(string line, TextDocument document, TextAnchor anchor, long lifecycle, Func<bool> isCurrent, TextInputEventArgs input, EditorCoordinator.OperationTicket ticket)
    {
        var vm = _viewModel!;
        try
        {
            void Output(string text)
            {
                if (ReferenceEquals(_viewModel, vm) && vm.EditorLifecycle == lifecycle && !anchor.IsDeleted &&
                    ReferenceEquals(document, _textEditor?.Document))
                    document.Insert(anchor.Offset, "\n" + text);
            }
            if (!await vm.ParseInputLineAsync(line, isCurrent, publishOutput: Output, native: ticket))
            {
                // Fresh admission failed synchronously, before returning to native TextEntering.
                input.Handled = true;
                return;
            }
        }
        catch (Exception)
        {
            input.Handled = true;
            // Task is tracked; no async-void exception escapes the input event.
            if (ReferenceEquals(_viewModel, vm) && vm.EditorLifecycle == lifecycle) vm.Status.StatusString = "Script execution failed.";
        }
    }
    private void textEditor_TextArea_TextEntered(object sender, TextInputEventArgs e)
    {
        if (e == null || e.Text == null)
            return;

        if (e.Text=="(")
        {
            var doc = _textEditor!.Document;
            int l = _textEditor!.TextArea.Caret.Line - 1;
            DocumentLine line = doc.Lines[l];
            string lineText = doc.GetText(line);
            var provider=new FunctionOverlayDictionaryModel().GetOverlays(lineText,line.Length);
            bool infoCompletionOpened =
                _infoCompletionController?.OpenForTypedParenthesis() == true;

            if (!infoCompletionOpened && provider != null)
            {
                var window = new OverloadInsightWindow(_textEditor.TextArea);
                _insightWindow = window;
                window.Closed += (sender, args) => ObserveEditor(() =>
                {
                    if (ReferenceEquals(sender, _insightWindow)) _insightWindow = null;
                });
                provider.Observe = action =>
                {
                    if (ReferenceEquals(window, _insightWindow) && ReferenceEquals(doc, _textEditor.Document)) ObserveEditor(action);
                };
                window.Provider = provider;
                window.Show();
            }
        }
    }
    private void Caret_PositionChanged(object sender, EventArgs e)
    {
        _statusTextBlock!.Text = string.Format("Line {0} Column {1}",
         _textEditor!.TextArea.Caret.Line,
         _textEditor.TextArea.Caret.Column);
    }
}
public class MyOverloadProvider : IOverloadProvider
{
    private readonly IList<(string header, string content)> _items;
    private int _selectedIndex;
    internal Action<Action> Observe { get; set; } = action => action();

    public MyOverloadProvider(IList<(string header, string content)> items)
    {
        _items = items;
        SelectedIndex = 0;
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = value;
            OnPropertyChanged();
            // ReSharper disable ExplicitCallerInfoArgument
            OnPropertyChanged(nameof(CurrentHeader));
            OnPropertyChanged(nameof(CurrentContent));
            // ReSharper restore ExplicitCallerInfoArgument
        }
    }

    public int Count => _items.Count;
    public string CurrentIndexText => $"{SelectedIndex + 1} of {Count}";
    public object CurrentHeader => _items[SelectedIndex].header;
    public object CurrentContent => _items[SelectedIndex].content;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        Observe(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }
}
