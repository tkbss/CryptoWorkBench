using CryptoScript.ErrorListner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    [NonParallelizable]
    public class SyntaxErrorTests
    {
        [SetUp]
        [TearDown]
        public void ResetErrorState()
        {
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
            SyntaxErrorListner.ErrorMessage.Clear();
            SyntaxErrorList.Instance().Clear();
        }

        [Test]
        public void Simple_Syntax_Error() 
        {
            var prog = new AntlrToProgram();
            string input = "B k5=GenerateKey(AES-ECB,128)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            Assert.That(parser.NumberOfSyntaxErrors, Is.GreaterThan(0));
            Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.True);
        }
        [Test]
        public void FunctionName_Syntax_Error()
        {
            var prog = new AntlrToProgram();
            string input = "KEY k5=generateKey(AES-ECB,128)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            Assert.That(parser.NumberOfSyntaxErrors, Is.GreaterThan(0));
            Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.True);
        }
        [Test]
        public void Parameter_Argument_Syntax_Error()
        {
            var prog = new AntlrToProgram();
            string input = "PARAM p6=Parameters(AES-CTR,#NONC:0x(00112233445566778899AABB),#COU:0x(00000000))";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            Assert.That(parser.NumberOfSyntaxErrors > 0 || LexerErrorListener.LexerErrorOccured, Is.True);
            Assert.That(SyntaxErrorList.Instance(), Is.Not.Empty);
            
        }
        [Test]
        public void Mechanism_Syntax_Error()
        {
            var prog = new AntlrToProgram();
            string input = "KEY k5=GenerateKey(AES-CC,128)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            Assert.That(LexerErrorListener.LexerErrorOccured, Is.True);
            Assert.That(SyntaxErrorList.Instance().OfType<LexerError>(), Is.Not.Empty);

        }

    }
}
