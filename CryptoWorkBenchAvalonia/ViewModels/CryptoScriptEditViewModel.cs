using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using ImTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class CryptoScriptEditViewModel :  ViewModelBase
    {
        StatusViewModel _statusViewModel;
        string _printMessage = string.Empty;
        private TextEditor? _textEditor;
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
        public CryptoScriptEditViewModel(StatusViewModel statusViewModel)
        {
            _statusViewModel = statusViewModel;
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

            }
            else
            {
                _statusViewModel.StatusString = "Line successfull parsed";
                var res = prog.Visit(context);
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
                    if (lineText != string.Empty)
                    {
                        byte[] lineBytes = Encoding.ASCII.GetBytes(lineText);
                        fs.Write(lineBytes, 0, lineBytes.Length);
                        fs.WriteByte((byte)'\n');
                    }
                }
            }
        }
        
        public async Task OpenScriptBook()
        {
            await Task.Run(() =>
            {
                string t = "";
            });
        }
    }
}
