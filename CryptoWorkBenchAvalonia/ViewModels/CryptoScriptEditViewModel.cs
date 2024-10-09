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
        public CryptoScriptEditViewModel(StatusViewModel statusViewModel)
        {
            _statusViewModel = statusViewModel;
        }
        public void ParseLine(string line)
        {
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
    }
}
