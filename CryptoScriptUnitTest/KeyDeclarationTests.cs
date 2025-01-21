using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class KeyDeclarationTests
    {
        [Test]
        public void GeneratePredefinedAES256KBPKTest()
        {
            string input = "KEY kbpk=GenerateKey(AES-CBC,0x(EF0BA217D99A6D7033227079B3C3F5B16E31E828659AE1A6B5A757C2D8D20133))";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            ClassicAssert.IsTrue(statement is KeyVariableDeclaration);
        }
        [Test]
        public void GeneratePredefinedAESKBPKAndIKTest()
        {
            string input = "KEY kbpk=GenerateKey(AES-CBC,0x(EF0BA217D99A6D7033227079B3C3F5B16E31E828659AE1A6B5A757C2D8D20133)) " +
                            "KEY ik=GenerateKey(AES-CBC,0x(A714752E27B680B646CB110D6EB31C5C))";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 2);
            ClassicAssert.IsTrue(statement is KeyVariableDeclaration);
        }
        [Test]
        public void GenerateKeyAES128_Test()
        {
            string input = "KEY key2=GenerateKey(AES-CBC,128)";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            ClassicAssert.IsTrue(statement is KeyVariableDeclaration);
            var variable = statement as KeyVariableDeclaration;
            ClassicAssert.IsTrue(variable.Id == "key2");
            ClassicAssert.IsTrue(variable.Mechanism == "AES-CBC");
            ClassicAssert.IsTrue(variable.KeySize == "128");
            var key = FormatConversions.HexStringToByteArray(variable.Value);
            ClassicAssert.IsTrue(key.Length == 16);
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
        }
        [Test]
        public void GenerateKeyAES128ExistingKeyTest()
        {
            string input = "KEY k=GenerateKey(AES-CBC,0x(A714752E27B680B646CB110D6EB31C5C))";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);

        }
    }
}
