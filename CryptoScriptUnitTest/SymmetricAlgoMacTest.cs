using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class SymmetricAlgoMacTest
    {
        [Test]
        public void Parameters_AES_CMAC_Test()
        {
            var prog = new AntlrToProgram();
            string input = "PARAM macp1=Parameters(AES-CMAC)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var variable = res.Statements[0] as ParameterVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(variable.Id));
        }
        [Test]
        public void MAC_AES_CMAC_Test()
        {
            var prog = new AntlrToProgram();
            string input = "KEY mack1=GenerateKey(AES-CMAC,128) " +
                "PARAM macp2=Parameters(AES-CMAC) " +
                "VAR macc1 = Mac(macp2,mack1,0x(00112233445566778899AABBCCDDEEFF))";                
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var variable = res.Statements[0] as KeyVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(pv.Id));
            var crypto = res.Statements[2] as StringVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(crypto.Id));
        }
        [Test]
        public void MAC_AES_GMAC_Test()
        {
            var prog = new AntlrToProgram();
            string input = "KEY mack1=GenerateKey(AES-GMAC,256) " +
                "PARAM macp2=Parameters(AES-GMAC) " +
                "VAR macc1 = Mac(macp2,mack1,0x(00112233445566778899AABBCCDDEEFF))";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var variable = res.Statements[0] as KeyVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(pv.Id));
            var crypto = res.Statements[2] as StringVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(crypto.Id));
        }
        [Test]
        public void MAC_AES_CMAC_wrong_mode_in_generate_key_Test()
        {
            var prog = new AntlrToProgram();
            string input = "KEY mack2=GenerateKey(AES-ECB,128) " +
                "PARAM macp3=Parameters(AES-CMAC) " +
                "VAR macc2 = Mac(macp3,mack2,0x(00112233445566778899AABBCCDDEEFF))";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var variable = res.Statements[0] as KeyVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(pv.Id));
            var crypto = res.Statements[2] as StringVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(crypto.Id));
        }
        [Test]
        public void MAC_AES_CMAC_wrong_operation_Test()
        {
            var prog = new AntlrToProgram();
            string input = "KEY mack3=GenerateKey(AES-ECB,128) " +
                "PARAM macp4=Parameters(AES-CMAC) " +
                "VAR macc3 = Encrypt(macp4,mack3,0x(00112233445566778899AABBCCDDEEFF))";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            Assert.Throws<SemanticErrorException>(() => prog.Visit(context));            
        }
    }
}
