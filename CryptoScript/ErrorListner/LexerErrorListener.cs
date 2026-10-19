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
        private readonly bool consoleDiagnostics;
        public LexerErrorListener(bool consoleDiagnostics = false) => this.consoleDiagnostics = consoleDiagnostics;
        public static bool LexerErrorOccured { get; set; } = false;              
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var lexer = (Lexer)recognizer;
            ICharStream stream = (ICharStream)lexer.InputStream;
            // TokenStartCharIndex is absolute; Column is only relative to a line.
            // During notification, Index includes the consumed prefix and points
            // at the failing character (or EOF). Never copy beyond that region.
            int start = Math.Clamp(lexer.TokenStartCharIndex, 0, stream.Size);
            int stop = Math.Min(stream.Size - 1, Math.Max(start, stream.Index));
            bool secretImport = SecretImportDiagnosticScope.Contains(recognizer, start, stop);
            if (consoleDiagnostics)
            {
                ConsoleErrorListener<int>.Instance.SyntaxError(output, recognizer, offendingSymbol, line,
                    charPositionInLine, secretImport ? "Invalid token in Secret import; check indices and HEX literals." : msg, e);
                return;
            }
            LexerErrorOccured = true;
            if (secretImport)
            {
                const string message = "Invalid token in Secret import; check indices and HEX literals.";
                SyntaxErrorList.Instance().Add(new LexerError
                {
                    Line = line.ToString(), Column = charPositionInLine.ToString(),
                    Symbol = "<redacted>", Message = message
                });
                // A lexer-only failure must also stop the existing GUI execution
                // path, which publishes the syntax error buffer before evaluation.
                SyntaxErrorListner.SyntaxErrorOccured = true;
                SyntaxErrorListner.ErrorMessage.AppendLine(
                    $"SYNTAX ERROR: Token <redacted> (line {line}, column {charPositionInLine}) : {message}");
                return;
            }
            string token = stop >= start ? stream.GetText(new Interval(start, stop)) : string.Empty;

            SyntaxErrorList.Instance().Add(new LexerError() 
            { 
                   
                Line = line.ToString(), 
                Column = charPositionInLine.ToString(), 
                Symbol = token, 
                Message = msg });           
        }
    }
}
