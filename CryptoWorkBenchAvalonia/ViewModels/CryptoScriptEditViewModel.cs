using CryptoScript.ErrorListner;
using CryptoScript.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class CryptoScriptEditViewModel :  ViewModelBase
    {
        StatusViewModel _statusViewModel;
        string _printMessage = string.Empty;
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
    }
}
