using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class SymmetricAlgoAEADTests
    {
        [Test]
        public void AEAD_AES_GCM_Test()
        {
            var prog = new AntlrToProgram();
            string input = "KEY gcmk1=GenerateKey(AES-GCM,256) " +
                "PARAM gcmp2=Parameters(AES-GCM) " +
                "VAR gcmc1 = Encrypt(gcmp2,gcmk1,0x(00112233445566778899AABBCCDDEEFF))"+
                "VAR plaintext=Decrypt(gcmp2,gcmk1,gcmc1)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var variable = res.Statements[0] as KeyVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(pv.Id));
            var crypto = res.Statements[2] as StringVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(crypto.Id));
            var plaintext = res.Statements[3] as StringVariableDeclaration;
            Assert.That(plaintext.Value.ToUpper() == "0x(00112233445566778899AABBCCDDEEFF)".ToUpper());
        }
        [Test]
        public void AEAD_AES_CCM_Test()
        {
            var prog = new AntlrToProgram();
            string input = "KEY gcmk1=GenerateKey(AES-GCM,256) " +
                "PARAM gcmp2=Parameters(AES-CCM) " +
                "VAR gcmc1 = Encrypt(gcmp2,gcmk1,0x(00112233445566778899AABBCCDDEEFF))" +
                "VAR plaintext=Decrypt(gcmp2,gcmk1,gcmc1)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var variable = res.Statements[0] as KeyVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(pv.Id));
            var crypto = res.Statements[2] as StringVariableDeclaration;
            Assert.That(VariableDictionary.Instance().Contains(crypto.Id));
            var plaintext = res.Statements[3] as StringVariableDeclaration;
            Assert.That(plaintext.Value.ToUpper() == "0x(00112233445566778899AABBCCDDEEFF)".ToUpper());
        }
    }
}
