using CryptoScript.ErrorListner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class SyntaxErrorTests
    {
        [Test]
        public void Simple_Syntax_Error() 
        {
            var prog = new AntlrToProgram();
            string input = "B k5=GenerateKey(AES-ECB,128)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured)
            {
                string e=SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured=false;


            }
            else
                Assert.Fail();
        }
        [Test]
        public void FunctionName_Syntax_Error()
        {
            var prog = new AntlrToProgram();
            string input = "KEY k5=generateKey(AES-ECB,128)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured = false;


            }
            else
                Assert.Fail();
        }
        [Test]
        public void Parameter_Argument_Syntax_Error()
        {
            var prog = new AntlrToProgram();
            string input = "PARAM p6=Parameters(AES-CTR,#NONC:0x(00112233445566778899AABB),#COU:0x(00000000))";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured = false;
            }
            else if(LexerErrorListener.LexerErrorOccured)
            {
                LexerErrorListener.LexerErrorOccured = false;
            }
            else
                Assert.Fail();
            
        }
        [Test]
        public void Mechanism_Syntax_Error()
        {
            var prog = new AntlrToProgram();
            string input = "KEY k5=GenerateKey(AES-CC,128)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (LexerErrorListener.LexerErrorOccured)
            {
                LexerErrorListener.LexerErrorOccured = false;
                var list=SyntaxErrorList.Instance();
            }
            else
                Assert.Fail();

        }

    }
}
