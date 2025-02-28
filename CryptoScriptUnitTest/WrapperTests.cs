using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class WrapperTests
    {
        
        [Test]
        public void WrapperWrapFunctionTest()
        {
            string input = "KEY kbpk = GenerateKey(AES-CBC,0x(88E1AB2A2E3DD38C1FA039A536500CC8A87AB9D62DC92C01058FA79F44657DE6)) " +
                           "KEY ik=GenerateKey(AES-CBC,0x(3F419E1CB7079442AA37474C2EFBF8B8)) " +
                           "PARAM p=#BLKH:\"D0112P0AE00E0000\"  #BIND:BIND-CMAC #MECH:WRAP-AES-TR31 #RND:0x(1C2965473CE206BB855B01533782)" +
                           "VAR block=Wrap(p,kbpk,ik)";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            Assert.That(res.Statements.Count == 4);
            var tr31blockVar=res.Statements[3] as StringVariableDeclaration;
            TR31String tr31block = TR31String.FromString(tr31blockVar.Value);
            string FromSpec = "\"D0112P0AE00E0000\"" + "0x(B82679114F470F540165EDFBF7E250FCEA43F810D215F8D207E2E417C07156A2)"+"0x(7E8E31DA05F7425509593D03A457DC34)";
            TR31String blockSpec = TR31String.FromString(FromSpec);
            Assert.That(tr31block.Equals(blockSpec));
        }
    }
}
