using Antlr4.Runtime;
using CryptoScript.Model;
using CryptoScript.ErrorListner;

namespace CryptoWorkBenchConsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            
            CryptoScriptRunner prog = new CryptoScriptRunner();
            string[] input = CreateInput();
            foreach (string inputItem in input)
            {
                Console.WriteLine(inputItem);
                CryptoScriptParser parser = ParserBuilder.StringBuild(inputItem);
                
                
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new BaseErrorListener());
                CryptoScriptParser.ProgramContext context = parser.program();
                ExecuteProgram(prog, context, Console.Error);
            }
        }
        private static void ExecuteProgram(CryptoScriptRunner runner,
            CryptoScriptParser.ProgramContext context, TextWriter diagnosticOutput)
        {
            CryptoScriptProgram result;
            try
            {
                result = runner.Execute(context);
            }
            catch (Exception exception)
            {
                var diagnostics = runner.Diagnostics.ToList();
                if (exception is SemanticErrorException { SemanticError: { } error } &&
                    !diagnostics.Any(item => ReferenceEquals(item, error)))
                    diagnostics.Add(error);
                try
                {
                    WriteDiagnostics(diagnostics, diagnosticOutput);
                }
                catch (Exception writerError) when (writerError is IOException or ObjectDisposedException)
                {
                    // Best-effort diagnostics on an already failed script. Preserve its original exception.
                }
                throw;
            }
            WriteDiagnostics(result.Diagnostics, diagnosticOutput);
        }

        private static void WriteDiagnostics(IEnumerable<SemanticError> diagnostics, TextWriter output)
        {
            var text = SemanticDiagnosticFormatter.FormatMany(diagnostics);
            if (text.Length > 0)
                output.WriteLine(text);
        }

        private static string[] CreateInput()
        {
            string[] input = new string[4];
            input[0] = "VAR key1=0x(1234)";
            input[1] = "print(key1)";
            input[2] = "VAR res = Sign(Encrypt(key1),0x(123))";
            input[3] = "print(res)";
            return input;
        }
    }
}
