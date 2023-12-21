using CryptoScript.ErrorListner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class SemanticErrorTests
    {
        [Test]
        public void Argument_Wrong_number_KeyGeneration_Error()
        {
            var prog = new AntlrToProgram();
            string input = "KEY k11=GenerateKey(128)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured || LexerErrorListener.LexerErrorOccured)
            {
                string e=SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured=false;
                Assert.Fail();
            }
            try 
            {                 
                var res = prog.Visit(context);
            }
            catch (SemanticErrorException e)
            {
                Assert.AreEqual("FunctionCall", e.SemanticError.Type);
            }
            
        }
        [Test]
        public void Argument_Wrong_number_Parameters_Error()
        {
            var prog = new AntlrToProgram();
            string input = "PARAM p11=Parameters()";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured || LexerErrorListener.LexerErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured = false;
                Assert.Fail();
            }
            try
            {
                var res = prog.Visit(context);
            }
            catch (SemanticErrorException e)
            {
                Assert.AreEqual("FunctionCall", e.SemanticError.Type);
            }

        }
        [Test]
        public void Unkown_function_declaration_Error()
        {
            var prog = new AntlrToProgram();
            string input = "PARAM P=GenerateParameters()";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured || LexerErrorListener.LexerErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured = false;
                Assert.Fail();
            }
            try
            {
                var res = prog.Visit(context);
            }
            catch (SemanticErrorException e)
            {
                Assert.AreEqual("FunctionCall", e.SemanticError.Type);
            }

        }
        [Test]
        public void Unkown_function_Error()
        {
            var prog = new AntlrToProgram();
            string input = "Unkown()";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured || LexerErrorListener.LexerErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured = false;
                Assert.Fail();
            }
            try
            {
                var res = prog.Visit(context);
            }
            catch (SemanticErrorException e)
            {
                Assert.AreEqual("FunctionCall", e.SemanticError.Type);
            }

        }
        [Test]
        public void Variable_Redeclartion_Error()
        {
            var prog = new AntlrToProgram();
            string input = "KEY k1=GenerateKey(AES-CBC,128) VAR k1=0x(123)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured || LexerErrorListener.LexerErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured = false;
                Assert.Fail();
            }
            try
            {
                var res = prog.Visit(context);
            }
            catch (SemanticErrorException e)
            {
                Assert.AreEqual("Variable", e.SemanticError.Type);
                Assert.AreEqual("k1", e.SemanticError.Identifier);
            }

        }
        [Test]
        public void Wrong_Declaration_Type_Error()
        {
            var prog = new AntlrToProgram();
            string input = "VAR k2=GenerateKey(AES-CBC,128)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured || LexerErrorListener.LexerErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured = false;
                Assert.Fail();
            }
            try
            {
                var res = prog.Visit(context);
            }
            catch (SemanticErrorException e)
            {
                Assert.AreEqual("Declaration", e.SemanticError.Type);                
            }

        }
        [Test]
        public void Wrong_Parameter_Declaration_Type_Error()
        {
            var prog = new AntlrToProgram();
            string input = "KEY p11 = AES-CTR #NONCE:0x(00112233445566778899AABB) #COUNTER:0x(00000000) #PAD:PKCS-7 #IV:0x(12345678)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            if (SyntaxErrorListner.SyntaxErrorOccured || LexerErrorListener.LexerErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                SyntaxErrorListner.SyntaxErrorOccured = false;
                Assert.Fail();
            }
            try
            {
                var res = prog.Visit(context);
            }
            catch (SemanticErrorException e)
            {
                Assert.AreEqual("Declaration", e.SemanticError.Type);
            }

        }
    }
}
