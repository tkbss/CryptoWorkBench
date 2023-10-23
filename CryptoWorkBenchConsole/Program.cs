using Antlr4.Runtime;
using SimpleLanguage_TestApp3.Model;

namespace CryptoWorkBenchConsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            AntlrToProgram prog = new AntlrToProgram();
            string[] input = CreateInput();
            foreach (string inputItem in input)
            {
                Console.WriteLine(inputItem);
                CryptoScriptParser parser = ParserBuilder.StringBuild(inputItem);
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new BaseErrorListener());
                CryptoScriptParser.ProgramContext context = parser.program();
                var res = prog.Visit(context);
            }
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