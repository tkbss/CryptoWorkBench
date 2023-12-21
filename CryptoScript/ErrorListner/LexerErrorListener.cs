using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.ErrorListner
{
    public class LexerErrorListener : IAntlrErrorListener<int>
    {
        public static bool LexerErrorOccured { get; set; } = false;              
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            LexerErrorOccured = true;
            ICharStream stream = (ICharStream)recognizer.InputStream;
            string allCharsFromPosition = stream.GetText(new Interval(charPositionInLine, stream.Size));
            string token=string.Empty;
            foreach (var c in allCharsFromPosition) 
            {
                if(c==',' || c==' ' || c== ')')
                {
                    break;
                }
                else
                {
                    token += c;
                }
            }

            SyntaxErrorList.Instance().Add(new LexerError() 
            { 
                   
                Line = line.ToString(), 
                Column = charPositionInLine.ToString(), 
                Symbol = token, 
                Message = msg });           
        }
    }
}
