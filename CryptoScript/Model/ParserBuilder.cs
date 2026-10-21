using Antlr4.Runtime;
using CryptoScript.ErrorListner;

namespace CryptoScript.Model
{
    public class ParserBuilder
    {
        public static CryptoScriptParser StringBuild(string input) 
        {
            InterpreterExecution.VerifyAccess();

            // Create a CharStream from the input code
            AntlrInputStream stream = new AntlrInputStream(input);

            // Create the lexer
            CryptoScriptLexer lexer = new CryptoScriptLexer(stream);
            lexer.RemoveErrorListeners(); 
            // Remove default console error listener
            lexer.AddErrorListener(new LexerErrorListener());
            // Create a token stream from the lexer
            CommonTokenStream tokens = new CommonTokenStream(lexer);
            // Create the parser
            CryptoScriptParser parser = new CryptoScriptParser(tokens);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new SyntaxErrorListner());
            //Clear all previous  syntax errors
            SyntaxErrorList.Instance().Clear();
            return parser;
        }
        public static CryptoScriptParser FileBuild(string filename) 
        {
            InterpreterExecution.VerifyAccess();
            using StreamReader reader = File.OpenText(filename);

            // Create a CharStream from the input code
            AntlrInputStream input = new AntlrInputStream(reader);

            // Create the lexer
            CryptoScriptLexer lexer = new CryptoScriptLexer(input);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new LexerErrorListener(consoleDiagnostics: true));
            // Create a token stream from the lexer
            CommonTokenStream tokens = new CommonTokenStream(lexer);
            // Create the parser
            CryptoScriptParser parser = new CryptoScriptParser(tokens);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new SyntaxErrorListner(consoleDiagnostics: true));
            return parser;
        }
    }
}
