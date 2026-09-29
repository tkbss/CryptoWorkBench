using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.ViewModels;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CryptoWorkBenchAvalonia;

public partial class CryptoScriptEditView : UserControl
{
    private OverloadInsightWindow _insightWindow;
    private readonly TextEditor? _textEditor;
    private InfoCompletionController? _infoCompletionController;
    private TextBlock? _statusTextBlock;
    private CryptoScriptEditViewModel? _viewModel;
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
            _textEditor.Clear();
            _viewModel.TextEditor = _textEditor;
        }
        _statusTextBlock = this.Find<TextBlock>("StatusText");
        EnsureInfoCompletionController();
        AttachedToVisualTree += (sender, args) => EnsureInfoCompletionController();
        DetachedFromVisualTree += (sender, args) =>
        {
            _infoCompletionController?.Dispose();
            _infoCompletionController = null;
        };
        _textEditor!.TextArea.TextEntering += this.textEditor_TextArea_TextEntering!;
        _textEditor.TextArea.TextEntered += this.textEditor_TextArea_TextEntered!;
        _textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged!;
        

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
    private void textEditor_TextArea_TextEntering(object sender, TextInputEventArgs e)
    {
        
        if (e == null || e.Text==null)
            return;

        _infoCompletionController?.HandleTextEntering(e.Text);

        if (e.Text.Length > 0 && _insightWindow != null)
            _insightWindow?.Hide();
        //{
        //    if (char.IsWhiteSpace(e.Text[0]))
        //    {
        //        _completionWindow.CompletionList.RequestInsertion(e);
        //    }
        //}
        if (e.Text.Contains("\n") || e.Text.Contains("\r"))
        {
            var doc=_textEditor!.Document;            
            int l = _textEditor!.TextArea.Caret.Line-1;
            DocumentLine line = doc.Lines[l];
            string lineText = doc.GetText(line);
            if(_viewModel == null)
                return; 
            _viewModel.ParseLine(lineText);
            if(_viewModel.PrintMessage != string.Empty)
            {
                //_viewModel.NavigateToInfoView();    
                doc.Insert(line.EndOffset, "\n" + _viewModel.PrintMessage);                
            }            
            //var tracker=doc.LineTrackers;
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
                _insightWindow = new OverloadInsightWindow(_textEditor.TextArea);
                _insightWindow.Closed += (o, args) => _insightWindow = null;
                _insightWindow.Provider = provider;
                _insightWindow.Show();
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

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
