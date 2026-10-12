using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Services;
using Prism.Navigation;
using Prism.Navigation.Regions;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class CryptoScriptEditViewModel :  ViewModelBase
    {
        StatusViewModel _statusViewModel;
        VariableViewModel _variableViewModel;
        InfoViewModel _infoViewModel;
        MainViewModel _mainViewModel;
        IRegionManager _regions;
        IHistoryService _historyService;
        string _printMessage = string.Empty;
        private TextEditor? _textEditor;
        bool _syntaxErrorOccured = false;
        
        public StatusViewModel Status { get => _statusViewModel; }
        public TextEditor? TextEditor
        {
            get
            {
                return _textEditor;
            }

            set => SetProperty(ref _textEditor, value);
        }
        public string PrintMessage 
        { 
            get => _printMessage; 
            set => SetProperty(ref _printMessage, value); 
        }
        
        public CryptoScriptEditViewModel(StatusViewModel statusViewModel,VariableViewModel variableViewModel,InfoViewModel info,MainViewModel mvm,IRegionManager regions, IHistoryService historyService)
        {
            _historyService = historyService;
            _historyService.LineHistoryChanged += OnHistoryChanged;
            _statusViewModel = statusViewModel;
            _variableViewModel = variableViewModel;
            OutputOperations.PrintEvent += OnPrintEvent;
            OutputOperations.InfoEvent += OnInfoEvent;
            _regions = regions; 
            _infoViewModel = info;
            _mainViewModel = mvm;
        }
        private void OnHistoryChanged(object? sender, string entry)
        {
            TextDocument doc = _textEditor!.Document;
            // letzte Zeile holen
            DocumentLine line = doc.Lines[doc.Lines.Count - 1];            
            doc.Remove(line.Offset, line.Length);
            // neuen Text am Zeilenanfang einsetzen
            doc.Insert(line.Offset, entry);
            _textEditor.Document = doc;
            _textEditor.IsModified = true;
        }
        public void ParseLine(string line)
        {
            _statusViewModel.ClearDiagnostics();
            ExecuteLine(line);
        }

        // Optional runner is a private transport-test seam, not a script function.
        private bool ExecuteLine(string line, int? scriptBookLine = null, CryptoScriptRunner? runner = null)
        {
            _historyService.AddLine(line);
            _printMessage = string.Empty;
            var prog = runner ?? new CryptoScriptRunner();
            CryptoScriptParser parser = ParserBuilder.StringBuild(line);
            CryptoScriptParser.ProgramContext context = parser.program();
            _statusViewModel.StatusString =string.Empty;
            if (SyntaxErrorListner.SyntaxErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                _statusViewModel.StatusString = e;
                SyntaxErrorListner.SyntaxErrorOccured = false;
                _syntaxErrorOccured = true;
                return false;
            }
            else
            {
                _statusViewModel.StatusString = "Line successfull parsed";
                bool diagnosticsReported = false;
                try
                {
                    var res = prog.Execute(context);
                    _statusViewModel.AppendDiagnostics(res.Diagnostics, scriptBookLine: scriptBookLine);
                    diagnosticsReported = true;
                    _variableViewModel.SetupVariables();
                    _syntaxErrorOccured = false;
                    return !_syntaxErrorOccured;
                }
                catch (Exception e) {
                    if (!diagnosticsReported)
                        _statusViewModel.AppendDiagnostics(prog.Diagnostics,
                            (e as SemanticErrorException)?.SemanticError, scriptBookLine);
                    if(e is SemanticErrorException)
                        _statusViewModel.StatusString = "Script execution aborted (semantic error).";
                    else
                    {
                        _statusViewModel.StatusString = "Semantic "+e.Message;
                    }
                    _syntaxErrorOccured = true;
                    return false;
                }
            }
        }
        
        private void OnPrintEvent(string message)
        {
            // Handle the event (e.g., update the status view model)
            _printMessage = message;
        }
        private void OnInfoEvent(string message)
        {
            if (message.Contains("Gesture="))
                return;
                _infoViewModel.SetInfoText(message);
            _mainViewModel.InfoViewTitle = "InfoView";
            _regions.RequestNavigate("InfoRegion", "InfoView");
        }
        

        public void SaveScriptBook(string filePath)
        {

            using (FileStream fs = System.IO.File.Open(filePath, System.IO.FileMode.Append))
            {
                TextDocument doc = _textEditor!.Document;
                foreach (DocumentLine line in doc.Lines)
                {
                    string lineText = doc.GetText(line);
                    string trimmedLine = lineText;
                    if (string.IsNullOrEmpty(trimmedLine.Trim()) == true)
                        continue;
                    if (lineText != string.Empty)
                    {
                        byte[] lineBytes = Encoding.ASCII.GetBytes(lineText);
                        fs.Write(lineBytes, 0, lineBytes.Length);
                        fs.WriteByte((byte)'\n');
                    }
                }
            }
        }
        public bool IsTextEditorEmpty()
        {
            if (_textEditor!.Document.Text.Length == 0)
                return true;
            return false;
        }
        public void OpenScriptBook(string filePath)
        {
            _statusViewModel.ClearDiagnostics();
            LastScriptBookSucceeded = false;
            // Read all lines from the file
            var lines = File.ReadAllLines(filePath).ToList();
            TextDocument doc = _textEditor!.Document;
            ExecuteScriptBook(lines, l =>
                doc.Insert(doc.Lines[doc.Lines.Count - 1].EndOffset, l + "\n"));
            _textEditor.Document= doc;
            _textEditor.IsModified = true;
        }

        public bool LastScriptBookSucceeded { get; private set; }

        private void ExecuteScriptBook(IEnumerable<string> lines, Action<string> appendLine,
            Func<CryptoScriptRunner>? runnerFactory = null)
        {
            LastScriptBookSucceeded = false;
            int bookLine = 0;
            foreach (var line in lines)
            {
                ++bookLine;
                bool succeeded = line.StartsWith("out:", StringComparison.Ordinal) ||
                    ExecuteLine(line, bookLine, runnerFactory?.Invoke());
                appendLine(line);
                if (!succeeded)
                    return;
            }
            LastScriptBookSucceeded = true;
        }
    }
}
