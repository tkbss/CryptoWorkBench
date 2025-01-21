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
            string input = "KEY kbpk = GenerateKey(AES-CBC,0x(EF0BA217D99A6D7033227079B3C3F5B16E31E828659AE1A6B5A757C2D8D20133)) " +
                           "KEY ik=GenerateKey(AES-CBC,0x(A714752E27B680B646CB110D6EB31C5C)) " +
                           "PARAM p=#BLKH:\"D0112B1AX00E0000\" #BIND:BIND-CMAC #MECH:WRAP-AES-TR31 "+
                           "VAR block=Wrap(p,kbpk,ik)";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
        }
    }
}
