using CryptoScript.Variables;
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
            CryptoScriptRunner prog = new CryptoScriptRunner();
            string input = "VAR t=1 Print(t)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Execute(context);
        }
        [Test]
        public void Path_Test()
        {
            CryptoScriptRunner prog = new CryptoScriptRunner();
            string input = @"PATH path567=c:\dir1\dir2\file.txt";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Execute(context);
            var pv = res.Statements[0] as PathVariableDeclaration;
            var variables = VariableDictionary.Instance().GetVariables();            
            var variable = variables.First(v=>v.Id=="path567") as PathVariableDeclaration;
            Assert.That(variable.Value == @"c:\dir1\dir2\file.txt");
        }
    }
}
