using Antlr4.Runtime;
using CryptoScript.Model;
using SimpleLanguage_TestApp3.Model;

namespace CryptoScriptUnitTest
{
    public class GrammarTests
    {
        string[] input = new string[4];
        [SetUp]
        public void Setup()
        {
            
            input[0] = "VAR key1=0x(1234)";
            input[1] = "print(key1)";
            input[2] = "VAR res = Sign(Encrypt(key1),0x(123))";
            input[3] = "print(res)";
        }

        [Test]
        public void DeclarationTest()
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[0]);
            var lexer = new CryptoScriptLexer(new AntlrInputStream(input[0]));
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement=res.Statements.FirstOrDefault();
            Assert.Pass();
        }

    }
}