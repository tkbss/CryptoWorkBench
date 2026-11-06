using Antlr4.Runtime;
using System.Text;

namespace CryptoScript.ErrorListner
{
    public class SyntaxErrorListner : BaseErrorListener
    {
        private readonly bool consoleDiagnostics;
        public SyntaxErrorListner(bool consoleDiagnostics = false) => this.consoleDiagnostics = consoleDiagnostics;
        public static bool SyntaxErrorOccured { get; set; } = false;
        public static StringBuilder ErrorMessage { get; set; } = new StringBuilder();          
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, 
            int charPositionInLine, string msg, RecognitionException e)
        {
            // Standardwerte von ANTLR übernehmen
            var displayLine = line;
            var displayColumn = charPositionInLine;
            var displaySymbol = offendingSymbol?.Text ?? "";
            var displayMsg = msg;

            int start = e is NoViableAltException alternative ? alternative.StartToken.StartIndex : offendingSymbol?.StartIndex ?? -1;
            bool secretImport = ImportDiagnosticScope.Contains(recognizer, start, offendingSymbol?.StopIndex ?? start, "Secret");
            bool loadImport = ImportDiagnosticScope.Contains(recognizer, start, offendingSymbol?.StopIndex ?? start, "Load");
            bool sensitiveImport = secretImport || loadImport;
            string safeMessage = loadImport ? "Invalid Load syntax; check PATH, selectors and #ONCONFLICT."
                : "Invalid Secret import syntax; check argument separators, indices and HEX literals.";
            if (consoleDiagnostics)
            {
                // FileBuild historically uses ANTLR's console diagnostics.
                var symbol = sensitiveImport ? new CommonToken(offendingSymbol?.Type ?? TokenConstants.InvalidType, "<redacted>")
                    { Line = line, Column = charPositionInLine } : offendingSymbol;
                ConsoleErrorListener<IToken>.Instance.SyntaxError(output, recognizer, symbol, line,
                    charPositionInLine, sensitiveImport ? safeMessage : msg, e);
                return;
            }
            SyntaxErrorOccured = true;
            if (sensitiveImport)
            {
                displaySymbol = "<redacted>";
                displayMsg = safeMessage;
            }
            // Preserve existing diagnostics outside sensitive imports.
            else if (recognizer is Parser parser && offendingSymbol != null)
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
                        displayMsg = $"Unknown Token '{prev.Text}'. ";
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
