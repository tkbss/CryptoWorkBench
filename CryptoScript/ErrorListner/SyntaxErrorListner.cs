using Antlr4.Runtime;
using System.Text;

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
            SyntaxErrorList.Instance().Add(new ParserError()
            {
                Line = line.ToString(),
                Column = charPositionInLine.ToString(),
                Symbol = offendingSymbol.Text,
                Message = msg
            });
            List<string> stack = ((Parser)recognizer).GetRuleInvocationStack().ToList();
            stack.Reverse();
            
            ErrorMessage.AppendLine("Syntax Error!");
            ErrorMessage.AppendLine("Token " + @"\" + offendingSymbol.Text + @"\" + "(line " + line + ", column " + charPositionInLine + ")" + " : " + msg);
            //ErrorMessage.AppendLine("Rule Stack: " + stack);
            //base.SyntaxError(output, recognizer, offendingSymbol, line, charPositionInLine, msg, e);
        }
    }
}
