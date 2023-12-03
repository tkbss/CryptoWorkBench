using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.ErrorListner
{
    public class SyntaxErrorListner : BaseErrorListener
    {
        public static bool SyntaxErrorOccured { get; set; } = false;
        public static StringBuilder ErrorMessage { get; set; } = new StringBuilder();
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, 
            int charPositionInLine, string msg, RecognitionException e)
        {
            SyntaxErrorOccured = true;
            List<string> stack = ((Parser)recognizer).GetRuleInvocationStack().ToList();
            stack.Reverse();
            ErrorMessage.AppendLine("Syntax Error!");
            ErrorMessage.AppendLine("Token "+@"\" +offendingSymbol.Text+@"\"+"(line "+line+", column"+ charPositionInLine+")"+" : "+msg);
            ErrorMessage.AppendLine("Rule Stack: "+stack);
            base.SyntaxError(output, recognizer, offendingSymbol, line, charPositionInLine, msg, e);
        }
    }
}
