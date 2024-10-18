using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.IO;
using System.Linq;
using System.Text;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class CryptoScriptEditViewModel :  ViewModelBase
    {
        StatusViewModel _statusViewModel;
        VariableViewModel _variableViewModel;
        string _printMessage = string.Empty;
        private TextEditor? _textEditor;
        bool _syntaxErrorOccured = false;
        public StatusViewModel Status { get => _statusViewModel; }
        public TextEditor TextEditor
        {
            get => _textEditor;
            set => SetProperty(ref _textEditor, value);
        }   
        public string PrintMessage 
        { 
            get => _printMessage; 
            set => SetProperty(ref _printMessage, value); 
        }
        
        public CryptoScriptEditViewModel(StatusViewModel statusViewModel,VariableViewModel variableViewModel)
        {
            _statusViewModel = statusViewModel;
            _variableViewModel = variableViewModel;
            OutputOperations.PrintEvent += OnPrintEvent;
        }
        public void ParseLine(string line)
        {
            _printMessage = string.Empty;
            var prog = new AntlrToProgram();            
            CryptoScriptParser parser = ParserBuilder.StringBuild(line);
            CryptoScriptParser.ProgramContext context = parser.program();
            _statusViewModel.StatusString =string.Empty;
            if (SyntaxErrorListner.SyntaxErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                _statusViewModel.StatusString = e;
                SyntaxErrorListner.SyntaxErrorOccured = false;
                _syntaxErrorOccured = true;

            }
            else
            {
                _statusViewModel.StatusString = "Line successfull parsed";
                var res = prog.Visit(context);
                _variableViewModel.SetupVariables();                
                _syntaxErrorOccured = false;
            }
        }
        
        private void OnPrintEvent(string message)
        {
            // Handle the event (e.g., update the status view model)
            _printMessage = message;
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
            // Read all lines from the file
            var lines = File.ReadAllLines(filePath).ToList();
            TextDocument doc = _textEditor!.Document;
            foreach (var l in lines)
            {
                if(l.StartsWith("out:") == false)
                    ParseLine(l);
                DocumentLine line = doc.Lines[doc.Lines.Count-1];                
                doc.Insert(line.EndOffset, l+"\n");
                if (_syntaxErrorOccured == true)
                    break;
            }
            _textEditor.Document= doc;
            _textEditor.IsModified = true;
        }
    }
}
