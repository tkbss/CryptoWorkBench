using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class OutputGrammarTests
    {
        [Test]
        public void Output_Number_Test()
        {
            AntlrToProgram prog = new AntlrToProgram();
            string input = "VAR t=1 Print(t)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
        }
     }
}
