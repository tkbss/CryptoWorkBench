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
            AntlrToProgram prog = new AntlrToProgram();
            string input = "VAR t=1 Print(t)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
        }
        [Test]
        public void Path_Test()
        {
            AntlrToProgram prog = new AntlrToProgram();
            string input = @"PATH p=c:\dir1\dir2\file.txt";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var pv = res.Statements[0] as PathVariableDeclaration;
            var variables = VariableDictionary.Instance().GetVariables();
            Assert.That(variables.Count() == 1);
            var variable = variables.First() as PathVariableDeclaration;
            Assert.That(variable != null);
        }
    }
}
