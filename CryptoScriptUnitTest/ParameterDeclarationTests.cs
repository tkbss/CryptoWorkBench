using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class ParameterDeclarationTests
    {
        [Test]
        public void ParametersCTRDeclarationTest()
        {
            string input = "PARAM p7= #MECH:AES-CTR #NONCE:0x(00112233445566778899AABB) #COUNTER:0x(00000000) #PAD:PKCS-7 #IV:0x(12345678)";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            var variable = res.Statements[0] as ParameterVariableDeclaration;
            ClassicAssert.IsTrue(variable.GetParameter("NONCE") == "0x(00112233445566778899AABB)");
            ClassicAssert.IsTrue(variable.GetParameter("COUNTER") == "0x(00000000)");
            ClassicAssert.IsTrue(variable.GetParameter("IV") == "0x(12345678)");
            ClassicAssert.IsTrue(variable.GetParameter("PAD") == "PKCS-7");
            ClassicAssert.IsTrue(variable.GetParameter("MECH") == "AES-CTR");
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
        }
        [Test]
        public void ParametersGCMDeclarationTest()
        {
            string input = "PARAM p8=#MECH:AES-GCM #ADATA:\"AUTEHTICATION_DATA\"";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            var variable = res.Statements[0] as ParameterVariableDeclaration;
            Assert.That(variable.GetParameter("ADATA") == "\"AUTEHTICATION_DATA\"");

        }
        [Test]
        public void WrapperParameterErrorTest()
        {
            string input = "PARAM p=#BLKH:\"D0112B1AX00E0000\" #BIND=BIND-CMAC #MECH:WRAP-TR31";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            try
            {
                var res = prog.Visit(context);
                var statement = res.Statements.FirstOrDefault();
                var variable = statement as ParameterVariableDeclaration;
                Assert.That(variable.GetParameter("MECH") == "WRAP-TR31");
            }
            catch (SemanticErrorException e)
            {
                Console.WriteLine(e.SemanticError.Message);
                Assert.Pass();
                return;
            }

        }
        [Test]
        public void WrapperParameterTest()
        {
            string input = "PARAM p=#BLKH:\"D0112B1AX00E0000\" #BIND:BIND-CMAC #MECH:WRAP-AES-TR31";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            try
            {
                var res = prog.Visit(context);
                var statement = res.Statements.FirstOrDefault();
                var variable = statement as ParameterVariableDeclaration;
                Assert.That(variable.GetParameter("MECH") == "WRAP-AES-TR31");
            }
            catch (SemanticErrorException e)
            {
                Console.WriteLine(e.SemanticError.Message);
                Assert.Fail();
                return;
            }

        }
    }
}
