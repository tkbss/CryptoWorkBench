using CryptoScript.CryptoAlgorithm.WRAPPERS;
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
        string b = @"D3776S0RS00N0400CT0004050000MIIDszCCApugAwIBAgIIKpD5FKMfCZEwDQYJKoZIhvcNAQELBQAwLTEXMBUGA1UECgwOQWxwaGEgTWVyY2hhbnQxEjAQBgNVBAMMCVNhbXBsZSBDQTAeFw0yMDA4MTUwMjE0MTBaFw0yMTA4MTUwMjE0MTBaME8xFzAVBgNVBAoMDkFscGhhIE1lcmNoYW50MR8wHQYDVQQLDBZUTFMgQ2xpZW50IENlcnRpZmljYXRlMRMwEQYDVQQDDAoxMjM0NTY3ODkwMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA1sRg+wEuje3y14V0tFHpvxxpY/fyrldB0nRctBDn4AvkBfyJuDLG59vqkGXVd8J8YQdwEHZJrVq+7B8rjtM6PMoyH/7QAZZAC7tw740P4cfen1IryubZVviV9QUp+gHToelZfr1rfIsuEGhzo6UhwY70kkS87/rYHCVathZEjMmvUIEdpzg0PZ2+Heg6D35OQ70I+np+BsEQf71Zr+d2iKqVGEd50l8tbn4W3A4rOyUERPTaACwS9rvdF7nlmTqSI5ybN6lmm37a71h77n6M54aaw2KkJYWVo+1stUTyFVsv/YBs9aylbBHQOYqp/U2tB0TxM58QYGzyaWvNqbFzOQIDAQABo4G0MIGxMAkGA1UdEwQCMAAwDgYDVR0PAQH/BAQDAgeAMBMGA1UdJQQMMAoGCCsGAQUFBwMCMB0GA1UdDgQWBBR837QRAGx5uL9xDnRjr9L9WSBSlzAfBgNVHSMEGDAWgBSlXhVYy9bic9OLnRsxsFgKQQbLmTA/BgNVHR8EODA2MDSgMqAwhi5odHRwOi8vY3JsLmFscGhhLW1lcmNoYW50LmV4YW1wbGUvU2FtcGxlQ0EuY3JsMA0GCSqGSIb3DQEBCwUAA4IBAQCH6JusIBSkRDqzAohaSoJVAEwQGMdcUSQWDfMyJjZqkOep1kT8Sl7LolFmmmVRJdkTWZe4PxBfQUc/eIql9BIx90506B+j9aoVA7212OExAid78GgqKA6JoalhYQKRta9ixY8iolydTYyEYpegA1jFZavMQma4ZGwX/bDJWr4+cJYxJXWaf67g4AMqHaWC8J60MVjrrBe9BZ0ZstuIlNkktQUOZanqxqsrFeqz02ibwTwNHtaHQCztB4KgdTkrTNahkqeq6xjafDoTllNo1EddajnbA/cVzF9ZCNigDtg5chXHWIQbgEK7HmU3sY3/wd2Bh1KdF3+vpN+5iZMRNv7ZKP1001D77F007724TS1320200818221218ZPB0D000000000A7C9F8FA80A4BA3555CA071503CE1A6133649BB18A5A9130492172CA4E7360C060379738A28503230BDB04EED4E9B209643867613F5090A0E0392C21EB74747795B397315AB5D1F49A33693533E73AC0BEDA172FF530BE986F5EC1C25F481F05A69DF8B33624E621AF35FFAEC06C2005F37872923EEBFF38182FB290BFBA2A9FF88AD36278625868FA38A0DC9A53E0202C4D1DEF3B9DACFD249DA85DE3CCF92A8E6C0F8CDF8DE5FD17331BE5D580F210CE4EA1B01F1A0BFD6EFF410A71661234AD363D4B60885F00358729900FF95D7C87D3DE6FB4C83B24C8C7BB5A2E3763E9CBA50A0E3A8C1AF908699952BCB6B038FEA9D13FDE08801DC0573E55B842219DBF6D5DA5F028C73793AA718D01DE93D85AE06E7E08DC94ADB4EAA51B6DDAEA3750D0B77467D2982AC96F3EB28889715CBB81C71E97A60E58D44977C1D8220A422E98E17ACEBF72A8A18D4E7FC1695F442860E6063E8BB6BFF2184F77E635C2F5A02DADE4897A3B1374145C3AD6DF06C0D556F5DE9454CF40C4FC8922DFE245F868E668F1DA5BE0079F9D1D1861CA4B5E6C782F296098C07CB43784D64D8B8557410E5BAFF59333A791FF030EB0661C0590A665B50A3A727217100C4550B2AD9C96C658D6731C09B55DFAE665952E2913A4E090F45DCEB45D6683C3FC15E3A4CA49C7F2E684B3580DB47A53E5BDB228FAD250C584548D5DEDBB45004B5E0E75C37ACE8167CC6D9574A74876718D2F42996622B8EC0B895FF7A6739E4CF64B7F03FABDFBC0A565CB3455736D2B4E2B64D6EC175A569F78DB7ACB331B00804279677F4BFD0C35CBF0A38D646AA9051961123E16075A06B6331A9A30601AF3FD6A89AD9924AE1D9EC2FE0FF3B3A1B3E3E13D09B08B80D91F9EDF51B2E6D8DABD0FEB6C5C1085A11FA6A98CE8CC09E36C8A24D981A74E140EF30912E8CDBBE2A0CBD52B40C72D1958F4BB2F49BCBABBD80116FEF21BC91D219EEAEDA4DC11692C624B0836C3137A3BEE4549DEAB750A9DD5ACA7E3F822084783CDFEEB765EBEB9E3CFF053E8B8D5A1F1854B8AFF6325F10B81C7627D0DA895B1D19FEEF0AE3F3E138E87C4ADDF0BA53CA40ED0D1452044600FF4838D710F6D03474C317AC306DD7DA169B6C918E999E3A50DA1A34DDFCA3899F4469B9E969C0BD144F04B2621AB9E9E18455D526844155309565DA9D1726CD3A7ACC5FEDEF30DED078547CED31CEF84A31A810FA966F303CB950ACC324AE54BFAB9A04FAD93C38CD6239D7FAD2C59A9B71171F5676DA8ED3A3FFB5287DF141C1F5CE972CA26857AD3039B82B625960A7859F19EF0E94F8C4680A33189870942139DDFA64D5095FA46EB49085DB99EFC9C6A3F3A290DB9592F8B76B017113F7D1FEFE52E70FE26574467257CFEEA6D3F2BBD1BAEDDDCE3468827568A78536DE78E7AC872247BDB120A55DDE16A3D0CFBB7D097AD7AD0FA2671390D8D532A3915F5B3163FF1EE23553D83A1109980859C420F754BC74ECD1449B9A60EA252D3F035D715BCBD491485261C51238926E290BD7F0617E90BD6AB8B46443B05C28D61F8BB897417926623AF91B499C661629795165EF56460850F1D4F9CE199C2B9E21F1884A4D14644DAE5FB963B880EC2FFF70021772D524289D068A24F0283C42F0B4779996D2CF60EE6E45C364E2547DB92361B3DBCEDBAA96B9F10A1AAA1AB23CDE1B75F3299D4544787A07F6A9F7127
";
        [Test]
        public void TR31BlockParsingTest() 
        {      
            TR31Block tr31block = TR31Block.FromString(b);
            Assert.That(tr31block.Header == "D3776S0RS00N0400");
            Assert.That(tr31block.OptionalBlocks.Count == 4);
            Assert.That(tr31block.OptionalBlocks[0].ID == "CT");
            Assert.That(tr31block.OptionalBlocks[1].ID == "KP");
            Assert.That(tr31block.OptionalBlocks[2].ID == "TS");
            Assert.That(tr31block.OptionalBlocks[3].ID == "PB");
            Assert.That(tr31block.Mac.Length == 16);
        }
        [Test]
        public void VariableTR31StringTest()
        {
            string input = "VAR v1= \"ExampleString\"0x(ABCD1234)0x(1234567890ABCDEF)";
                           
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);            
            var tr31blockVar = res.Statements[0] as StringVariableDeclaration;
            Assert.That(tr31blockVar.Id == "v1");
            Assert.That(tr31blockVar.Value == "\"ExampleString\"0x(ABCD1234)0x(1234567890ABCDEF)");            
        }
        [Test]
        public void TR31HeaderDeclarationTest() 
        {
            string[] fieldNames = new string[] { "KBVID", "KBLEN", "KEYU", "ALGO", "MODEU", "KEYVN", "EXP", "KEYCTX", "NUMOPTB" };
            string input = "TR31H header={KBVID:D;ALGO:A;KEYU:D0;MODEU:B;KEYVN:00;EXP:E;KEYCTX:0;NUMOPTB:0}";
        }
        [Test]
        public void WrapperTR31WrapFunctionTest()
        {
            string input = "KEY kbpk = GenerateKey(AES-CBC,0x(88E1AB2A2E3DD38C1FA039A536500CC8A87AB9D62DC92C01058FA79F44657DE6)) " +
                           "KEY ik=GenerateKey(AES-CBC,0x(3F419E1CB7079442AA37474C2EFBF8B8)) " +
                           "PARAM p=#BLKH:\"D0112P0AE00E0000\" #MECH:WRAP-AES-TR31 #RND:0x(1C2965473CE206BB855B01533782)" +
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
        [Test]
        public void WrapperTr31UnwrapTR31StringAESKeyTest()
        {
            string input = "KEY kbpk = GenerateKey(AES-CBC,0x(88E1AB2A2E3DD38C1FA039A536500CC8A87AB9D62DC92C01058FA79F44657DE6)) " +
                           "VAR tr31Blk=\"D0112P0AE00E0000\"0x(B82679114F470F540165EDFBF7E250FCEA43F810D215F8D207E2E417C07156A2)0x(7E8E31DA05F7425509593D03A457DC34)" +
                           "PARAM p=#MECH:WRAP-AES-TR31" +
                           "KEY ik=Unwrap(p,kbpk,tr31Blk)";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            Assert.That(res.Statements.Count == 4);
            var ik = res.Statements[3] as KeyVariableDeclaration;
            Assert.That(ik.KeyValue.ToUpper() == "0x(3F419E1CB7079442AA37474C2EFBF8B8)".ToUpper());
            string v = "0x(3F419E1CB7079442AA37474C2EFBF8B8)";
            Assert.That(ik.Value.ToUpper() == v.ToUpper());
        }
        [Test]
        public void WrapperTr31UnwrapNormalStringAESKeyTest()
        {
            string input = "KEY kbpk = GenerateKey(AES-CBC,0x(88E1AB2A2E3DD38C1FA039A536500CC8A87AB9D62DC92C01058FA79F44657DE6)) " +
                           "VAR tr31Blk=\"D0112P0AE00E0000B82679114F470F540165EDFBF7E250FCEA43F810D215F8D207E2E417C07156A27E8E31DA05F7425509593D03A457DC34\"" +
                           "PARAM p=#MECH:WRAP-AES-TR31" +
                           "KEY ik=Unwrap(p,kbpk,tr31Blk)";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            Assert.That(res.Statements.Count == 4);
            var ik = res.Statements[3] as KeyVariableDeclaration;
            Assert.That(ik.KeyValue.ToUpper() == "0x(3F419E1CB7079442AA37474C2EFBF8B8)".ToUpper());
            string v = "0x(3F419E1CB7079442AA37474C2EFBF8B8)";
            Assert.That(ik.Value.ToUpper() == v.ToUpper());
        }
        [Test]
        public void WrapperTr31UnwrapNormalStringRSAKeyTest() 
        { 
            string input=
                "VAR block= \""+b+"\""+
                "KEY kbpk = GenerateKey(AES-CBC,0x(FA36E44278DB3AB5F298F9F7DA8F1F88)) " +
                "PARAM p=#MECH:WRAP-AES-TR31" +
                "KEY ik=Unwrap(p,kbpk,block)";
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            Assert.That(res.Statements.Count == 4);
            var ik = res.Statements[3] as KeyVariableDeclaration;
            string partRSAKey="308204a40201000282010100d6c460fb012e8dedf2d78574b451e9bf1c69";
            string pr=FormatConversions.HexStringToString(ik.KeyValue);
            Assert.That(pr.Substring(0, partRSAKey.Length).ToUpper() == partRSAKey.ToUpper());
        }
    }
}
