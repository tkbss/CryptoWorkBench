using Antlr4.Runtime;

namespace CryptoScript.ErrorListner;

internal static class ImportDiagnosticScope
{
    // Use the real lexer, including its recovery, rather than interpreting strings
    // and escapes a second time. This isolated pass has no publishing listeners
    // and never advances or changes the original input/token stream. Completing it
    // also makes context available during an original lexer's error callback.
    internal static bool Contains(IRecognizer recognizer, int start, int stop, string functionName)
    {
        ICharStream? input = recognizer switch
        {
            Parser parser => parser.TokenStream.TokenSource.InputStream,
            Lexer lexer => lexer.InputStream as ICharStream,
            _ => null
        };
        if (input is null || start < 0) return false;
        var scan = new CryptoScriptLexer(new AntlrInputStream(input.ToString() ?? ""));
        scan.RemoveErrorListeners();
        int depth = 0, callDepth = 0, opening = -1;
        bool previousTarget = false;
        stop = Math.Max(start, stop); // EOF tokens have an empty source interval.
        while (true)
        {
            IToken token = scan.NextToken();
            if (token.Type == TokenConstants.EOF)
                return callDepth != 0 && stop >= opening && start <= input.Size;
            if (token.Text == "(")
            {
                depth++;
                if (previousTarget && callDepth == 0)
                {
                    callDepth = depth;
                    opening = token.StartIndex;
                }
            }
            else if (token.Text == ")")
            {
                if (callDepth != 0 && depth == callDepth)
                {
                    if (start <= token.StopIndex && stop >= opening) return true;
                    callDepth = 0;
                }
                depth = Math.Max(0, depth - 1);
            }
            previousTarget = token.Type == CryptoScriptLexer.FN &&
                token.Text.Equals(functionName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
