using CryptoScript.Model;

namespace CryptoScriptUnitTest
{
    public class GrammarTests
    {
        string[] input = new string[5];
        [SetUp]
        public void Setup()
        {
            
            input[0] = "VAR key1=0x(1234)";
            input[1] = "print(key1)";
            input[2] = "VAR res = Sign(Encrypt(key1),0x(123))";
            input[3] = "print(res)";
            input[4] = "KEY key=GenerateKey(AES-ECB,128)";
        }

        [Test]
        public void DeclarationTest()
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[0]);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement=res.Statements.FirstOrDefault();
            Assert.IsNotNull(statement);
            
        }
        [Test]
        public void GenerateKeyTest()
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[4]);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            Assert.IsNotNull(statement);
        }

    }
}