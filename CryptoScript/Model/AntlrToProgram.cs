using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLanguage_TestApp3.Model
{
    public class AntlrToProgram : CryptoScriptBaseVisitor<Program>
    {
        public List<string> SemanticErrors { get; set; } = new List<string>();
        AntlrToStatement statementVisitor = new AntlrToStatement();
        public override Program VisitProgram([NotNull] CryptoScriptParser.ProgramContext context)
        {
            Program prog=new Program();
            statementVisitor.SemanticErrors= SemanticErrors;
            for (int i = 0;i<context.ChildCount;i++) 
            {
                if (i == context.ChildCount - 1) 
                { } 
                else 
                {
                    var statement=statementVisitor.Visit(context.GetChild(i));
                    prog.AddStatement(statement);
                }
            }
            return prog;
        }
    }
}
