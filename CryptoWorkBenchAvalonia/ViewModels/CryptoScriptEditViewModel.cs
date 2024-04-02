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
        public CryptoScriptEditViewModel()
        {
        }
        public void ParseLine(string line)
        {
            var prog = new AntlrToProgram();            
            CryptoScriptParser parser = ParserBuilder.StringBuild(line);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured = false;

            }
            else
            {
                var res = prog.Visit(context);
            }
        }   
    }
}
