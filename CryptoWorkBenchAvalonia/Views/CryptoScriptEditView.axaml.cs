using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using CommunityToolkit.Mvvm.DependencyInjection;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.ViewModels;
using ImTools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CryptoWorkBenchAvalonia;

public partial class CryptoScriptEditView : UserControl
{
    public CompletionWindow? _completionWindow;
    private OverloadInsightWindow _insightWindow;
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
        _textEditor.TextArea.TextEntered += this.textEditor_TextArea_TextEntered!;
        _textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged!;
        

    }
    private void textEditor_TextArea_TextEntering(object sender, TextInputEventArgs e)
    {
        
        if (e == null || e.Text==null)
            return;
        
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
            if (provider != null)
            {
                _insightWindow = new OverloadInsightWindow(_textEditor.TextArea);
                _insightWindow.Closed += (o, args) => _insightWindow = null;
                _insightWindow.Provider = provider;
                _insightWindow.Show();
            }
            

            //_completionWindow = new CompletionWindow(_textEditor.TextArea);
            //_completionWindow.Closed += (o, args) => _completionWindow = null;

            //var data = _completionWindow.CompletionList.CompletionData;
            //data.Add(new MyCompletionData("Encrypt(parameter,key,data)"));
            ////for (int i = 0; i < 5; i++)
            ////{
            ////    data.Add(new MyCompletionData("Item" + i.ToString()));
            ////}

            ////data.Insert(3, new MyCompletionData("long item to demosntrate dynamic poup resizing"));

            //_completionWindow.Show();
        }
    }
    private void Caret_PositionChanged(object sender, EventArgs e)
    {
        _statusTextBlock!.Text = string.Format("Line {0} Column {1}",
         _textEditor!.TextArea.Caret.Line,
         _textEditor.TextArea.Caret.Column);
    }
}
public class MyCompletionData : ICompletionData
{
    public MyCompletionData(string text)
    {
        Text = text;
    }

    public IImage Image => null;

    public string Text { get; }

    // Use this property if you want to show a fancy UIElement in the list.
    public object Content => _contentControl ??= BuildContentControl();

    public object Description => "Description for " + Text;

    public double Priority { get; } = 0;

    public void Complete(TextArea textArea, ISegment completionSegment,
        EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }

    Control BuildContentControl()
    {
        TextBlock textBlock = new TextBlock();
        textBlock.Text = Text;
        textBlock.Margin = new Thickness(5);
        textBlock.Width = 500;

        return textBlock;
    }

    Control _contentControl;
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

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
