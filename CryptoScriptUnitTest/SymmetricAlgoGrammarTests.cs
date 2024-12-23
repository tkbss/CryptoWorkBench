


using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    public class SymmetricAlgoGrammarTests
    {
        string[] input = new string[8];
        [SetUp]
        public void Setup()
        {
            
            input[0] = "KEY key0=0x(1234)";
            input[1] = "KEY key1=0x(1234) Print(key1)";
            input[2] = "VAR c1 = Sign(Encrypt(key1),0x(123))";
            input[3] = "Print(c1)";
            input[4] = "KEY key2=GenerateKey(AES-CBC,128)";
            input[5] = "PARAM p1=Parameters(AES-CBC)";
            input[6] = "PARAM p2=Parameters(AES-CBC,#PAD:PKCS-7,#IV:0x(12345678))";
            input[7] = "KEY key=GenerateKey(AES-CBC,128) PARAM p3=Parameters(AES-CBC) VAR c2 = Encrypt(p3,key,0x(12345678))";
        }

        [Test]
        public void DeclarationTest()
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[0]);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement=res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 1);   
            ClassicAssert.IsTrue(statement is StringVariableDeclaration);
            var variable = statement as StringVariableDeclaration;
            ClassicAssert.IsTrue(variable?.Id == "key0");
            ClassicAssert.IsTrue(variable?.Value == "0x(1234)");
            ClassicAssert.IsTrue(variable?.Type is CryptoTypeKey);
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));

            
        }
        [Test] public void DeclarationAndFunctionCallTest() 
        { 
            AntlrToProgram prog = new AntlrToProgram(); 
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[1]);             
            CryptoScriptParser.ProgramContext context = parser.program(); 
            var res = prog.Visit(context); 
            var statement=res.Statements.FirstOrDefault(); 
            ClassicAssert.IsNotNull(statement); 
            ClassicAssert.IsTrue(res.Statements.Count == 2);    
            ClassicAssert.IsTrue(statement is StringVariableDeclaration); 
            var variable = statement as StringVariableDeclaration; 
            ClassicAssert.IsTrue(variable?.Id == "key1"); 
            ClassicAssert.IsTrue(variable?.Value == "0x(1234)"); 
            ClassicAssert.IsTrue(variable?.Type is CryptoTypeKey);
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            statement =res.Statements.LastOrDefault(); 
            ClassicAssert.IsNotNull(statement); 
            ClassicAssert.IsTrue(statement is FunctionCall); 
            var func = statement as FunctionCall; 
            ClassicAssert.IsTrue(func?.Arguments.Count == 1);
            ClassicAssert.IsTrue(func?.Arguments[0] is ArgumentVariable); 
            ClassicAssert.IsTrue(func?.Name == "Print"); 
            ClassicAssert.IsTrue(func?.ReturnVariable is VariableDeclaration); 

        }
        [Test]
        public void GenerateKey_AES_128_Test()
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[4]);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            ClassicAssert.IsTrue(statement is KeyVariableDeclaration);
            var variable = statement as KeyVariableDeclaration;
            ClassicAssert.IsTrue(variable.Id== "key2");
            ClassicAssert.IsTrue(variable.Mechanism== "AES-CBC");
            ClassicAssert.IsTrue(variable.KeySize =="128");
            var key=FormatConversions.HexStringToByteArray(variable.Value);
            ClassicAssert.IsTrue(key.Length==16);           
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
        }
        [Test]
        public void DefaultParameters_AES_CBC_Test()
        {
            AntlrToProgram prog = new AntlrToProgram();
            string input= "PARAM p1=Parameters(AES-CBC)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            ClassicAssert.IsTrue(statement is ParameterVariableDeclaration);
            var variable = statement as ParameterVariableDeclaration;
            var iv = FormatConversions.HexStringToByteArray(variable.GetParameter("IV"));
            ClassicAssert.IsTrue(iv.Length==16);
            ClassicAssert.IsTrue(variable.GetParameter("PAD")=="PKCS-7");
            ClassicAssert.IsTrue(variable.GetParameter("MECH")== "AES-CBC");
            ClassicAssert.IsTrue(variable.Id == "p1");
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            ClassicAssert.IsTrue(string.IsNullOrEmpty(variable.GetParameter("NONCE")));
            ClassicAssert.IsTrue(string.IsNullOrEmpty(variable.GetParameter("COUNTER")));
        }
        [Test]
        public void Parameters_AES_CTR_Test()
        {
            string input= "PARAM p6=Parameters(#MECH:AES-CTR,#NONCE:0x(00112233445566778899AABB),#COUNTER:0x(00000000))";
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
        public void Parameters_AES_CBC_Test()
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[6]);            
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
        [Test]
        public void Encryption_AES_CBC_Test() 
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[7]);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 3);
            var variable = statement as KeyVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(pv.Id));
            var crypto= res.Statements[2] as StringVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(crypto.Id));
        }
        [Test]
        public void Decryption_AES_CBC_Test()
        {
            AntlrToProgram prog = new AntlrToProgram();
            string input= "KEY edkey=GenerateKey(AES-CBC,128) " +
                "PARAM p4=Parameters(AES-CBC) " +
                "VAR c3 = Encrypt(p4,edkey,0x(1234567812345678123456781234567812345678)) " +
                "VAR c4=Decrypt(p4,edkey,c3)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 4);
            var variable = statement as KeyVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(pv.Id));
            var crypto = res.Statements[2] as StringVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(crypto.Id));
            var ct=res.Statements[3] as StringVariableDeclaration;
            ClassicAssert.IsTrue(ct.Value== "0x(1234567812345678123456781234567812345678)");
        }
        [Test]
        public void Decryption_AES_CTR_Test() 
        {
            AntlrToProgram prog = new AntlrToProgram();
            string input = "KEY k4=GenerateKey(AES-CBC,128) " +
                "PARAM p5=Parameters(AES-CTR) " +
                "VAR c5 = Encrypt(p5,k4,0x(1234567812345678123456781234567812345678)) " +
                "VAR c6=Decrypt(p5,k4,c5)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            ClassicAssert.IsTrue(res.Statements.Count == 4);
            var variable = res.Statements[0] as KeyVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(pv.Id));
            var crypto = res.Statements[2] as StringVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(crypto.Id));
            var ct = res.Statements[3] as StringVariableDeclaration;
            ClassicAssert.IsTrue(ct.Value == "0x(1234567812345678123456781234567812345678)");
        }
        [Test]
        public void Decryption_AES_ECB_Test()
        {
            var prog = new AntlrToProgram();
            string input = "KEY k5=GenerateKey(AES-ECB,128) " +
                "PARAM p8=Parameters(AES-ECB) " +
                "VAR c7 = Encrypt(p8,k5,0x(00112233445566778899AABBCCDDEEFF)) " +
                "VAR c8=Decrypt(p8,k5,c7)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var variable = res.Statements[0] as KeyVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(pv.Id));
            var crypto = res.Statements[2] as StringVariableDeclaration;
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(crypto.Id));
            var ct = res.Statements[3] as StringVariableDeclaration;
            ClassicAssert.IsTrue(ct.Value.ToUpper() == "0X(00112233445566778899AABBCCDDEEFF)");
        }
        [Test]
        public void Function_Call_Nested_GenerateKey_Encrypt_Test() 
        {
            var prog = new AntlrToProgram();
            string input = "PARAM p9=Parameters(AES-ECB) " +
                "VAR c9 = Encrypt(p9,GenerateKey(AES-ECB,128),0x(00112233445566778899AABBCCDDEEFF)) ";            
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
        }
        [Test]
        public void Function_Call_Nested_GenerateKey_Parameters_Encrypt_Test()
        {
            var prog = new AntlrToProgram();
            string input = "VAR c10 = Encrypt(Parameters(AES-ECB),GenerateKey(AES-ECB,128),0x(00112233445566778899AABBCCDDEEFF)) ";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
        }

    }
}