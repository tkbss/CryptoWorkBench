using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class AntlrToProgram : CryptoScriptBaseVisitor<CryptoScriptProgram>
    {
        public List<string> SemanticErrors { get; set; } = new List<string>();
        AntlrToStatement statementVisitor = new AntlrToStatement();
        
        public override CryptoScriptProgram VisitProgram([NotNull] CryptoScriptParser.ProgramContext context)
        {
            CryptoScriptProgram prog=new CryptoScriptProgram();
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
