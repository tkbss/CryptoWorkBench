using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using CryptoWorkBenchAvalonia.ViewModels;
using System;
using System.Collections.Generic;

namespace CryptoWorkBenchAvalonia;

public partial class CryptoScriptEditView : UserControl
{
    
    private readonly TextEditor? _textEditor;
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
        _textEditor!.TextArea.TextEntering += this.textEditor_TextArea_TextEntering!;
        _textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged!;

    }
    private void textEditor_TextArea_TextEntering(object sender, TextInputEventArgs e)
    {
        
        if (e == null || e.Text==null)
            return;
        if (e.Text.Contains("\n") || e.Text.Contains("\r"))
        {
            var doc=_textEditor!.Document;            
            int l = _textEditor!.TextArea.Caret.Line-1;
            DocumentLine line = doc.Lines[l];
            string lineText = doc.GetText(line);            
            _viewModel.ParseLine(lineText);
            if(_viewModel.PrintMessage != string.Empty)
            {
                doc.Insert(line.EndOffset, "\n" + _viewModel.PrintMessage);
            }            
            //var tracker=doc.LineTrackers;
        }
    }
    private void Caret_PositionChanged(object sender, EventArgs e)
    {
        _statusTextBlock!.Text = string.Format("Line {0} Column {1}",
         _textEditor!.TextArea.Caret.Line,
         _textEditor.TextArea.Caret.Column);
    }
}