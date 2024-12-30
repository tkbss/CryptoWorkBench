using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class SymmetricAlgoParameterTests
    {
        [Test]
        public void Parameters_Declaration_Test()
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
        public void Parameter_ADATA_Declaration_Test() 
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
        public void DefaultParameters_AES_CCM_Test()
        {
            AntlrToProgram prog = new AntlrToProgram();
            string input = "PARAM p1=Parameters(AES-CCM)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            var variable = statement as ParameterVariableDeclaration;
            Assert.That(variable.GetParameter("MECH") == "AES-CCM");
        }
        [Test]
        public void DefaultParameters_AES_CBC_Test()
        {
            AntlrToProgram prog = new AntlrToProgram();
            string input = "PARAM p1=Parameters(AES-CBC)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            Assert.That(res.Statements.Count == 1);
            Assert.That(statement is ParameterVariableDeclaration);
            var variable = statement as ParameterVariableDeclaration;
            var iv = FormatConversions.HexStringToByteArray(variable.GetParameter("IV"));
            Assert.That(iv.Length == 16);
            Assert.That(variable.GetParameter("PAD") == "PKCS-7");
            Assert.That(variable.GetParameter("MECH") == "AES-CBC");
            ClassicAssert.IsTrue(variable.Id == "p1");
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            ClassicAssert.IsTrue(string.IsNullOrEmpty(variable.GetParameter("NONCE")));
            ClassicAssert.IsTrue(string.IsNullOrEmpty(variable.GetParameter("COUNTER")));
        }
        [Test]
        public void Default_Parameters_AES_CTR_Test()
        {
            string input = "PARAM p6=Parameters(#MECH:AES-CTR,#NONCE:0x(00112233445566778899AABB),#COUNTER:0x(00000000))";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            var variable = res.Statements[0] as ParameterVariableDeclaration;
            ClassicAssert.IsTrue(variable.Value.Contains("AES-CTR"));
            ClassicAssert.IsTrue(variable.Value.Contains("#NONCE:0x(00112233445566778899AABB)"));
            ClassicAssert.IsTrue(variable.Value.Contains("#COUNTER:0x(00000000)"));
            ClassicAssert.IsTrue(variable.GetParameter("NONCE") == "0x(00112233445566778899AABB)");
            ClassicAssert.IsTrue(variable.GetParameter("COUNTER") == "0x(00000000)");
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
        }
        [Test]
        public void Default_Parameters_AES_GCM_Test()
        {
            string input = "PARAM p6=Parameters(#MECH:AES-GCM)";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            var variable = res.Statements[0] as ParameterVariableDeclaration;
            Assert.That(variable.Value.Contains("AES-GCM"));           
            Assert.That(variable.GetParameter("ADATA") == "\"DEFAULT_GCM_AUTHENTICATION_DATA\"");
            Assert.That(VariableDictionary.Instance().Contains(variable.Id));
        }

        [Test]
        public void Default_Parameters_AES_CBC_Test()
        {
            string input = "PARAM p2=Parameters(AES-CBC,#PAD:PKCS-7,#IV:0x(12345678))";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            ClassicAssert.IsTrue(statement is ParameterVariableDeclaration);
            var variable = statement as ParameterVariableDeclaration;
            ClassicAssert.IsTrue(variable.GetParameter("IV") == "0x(12345678)");
            ClassicAssert.IsTrue(variable.GetParameter("PAD") == "PKCS-7");
            ClassicAssert.IsTrue(variable.Mechanism == "AES-CBC");
            ClassicAssert.IsTrue(variable.Id == "p2");
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
        }
    }
}
