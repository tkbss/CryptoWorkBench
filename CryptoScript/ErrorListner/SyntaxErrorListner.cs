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
            //SyntaxErrorOccured = true;
            //SyntaxErrorList.Instance().Add(new ParserError()
            //{
            //    Line = line.ToString(),
            //    Column = charPositionInLine.ToString(),
            //    Symbol = offendingSymbol.Text,
            //    Message = msg
            //});
            //List<string> stack = ((Parser)recognizer).GetRuleInvocationStack().ToList();
            //stack.Reverse();

            //ErrorMessage.AppendLine("Syntax Error!");
            //ErrorMessage.AppendLine("Token " + @"\" + offendingSymbol.Text + @"\" + "(line " + line + ", column " + charPositionInLine + ")" + " : " + msg);
            ////ErrorMessage.AppendLine("Rule Stack: " + stack);
            ////base.SyntaxError(output, recognizer, offendingSymbol, line, charPositionInLine, msg, e);
            SyntaxErrorOccured = true;

            // Standardwerte von ANTLR übernehmen
            var displayLine = line;
            var displayColumn = charPositionInLine;
            var displaySymbol = offendingSymbol?.Text ?? "";
            var displayMsg = msg;

            // Nur wenn es wirklich ein Parser ist (nicht Lexer)
            if (recognizer is Parser parser && offendingSymbol != null)
            {
                var tokenStream = parser.TokenStream;
                var index = offendingSymbol.TokenIndex;

                if (index > 0)
                {
                    var prev = tokenStream.Get(index - 1);

                    // Spezieller Fall: PARAM-Fehler "#XXX:..."
                    if (offendingSymbol.Text == ":"          // aktuelles Token ist ':'
                        && prev != null
                        && prev.Text != null)
                        //&& prev.Text.StartsWith("#")         // davor steht etwas wie "#XXX"
                        //&& prev.Type != CryptoScriptParser.PARAM_TYPE)
                    {
                        // Fehlermeldung auf das '#XXX'-Token umbiegen
                        displayLine = prev.Line;
                        displayColumn = prev.Column;
                        displaySymbol = prev.Text;
                        displayMsg = $"Unkown Token '{prev.Text}'. ";
                    }
                }
            }

            // In deine Liste schreiben
            SyntaxErrorList.Instance().Add(new ParserError()
            {
                Line = displayLine.ToString(),
                Column = displayColumn.ToString(),
                Symbol = displaySymbol,
                Message = displayMsg
            });
            string error=$"SYNTAX ERROR: Token {displaySymbol} (line {displayLine}, column {displayColumn}) : {displayMsg}";
            ErrorMessage.AppendLine(error);
            
        }
    }
}
