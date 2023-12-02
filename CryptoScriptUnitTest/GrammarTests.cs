


using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    public class GrammarTests
    {
        string[] input = new string[8];
        [SetUp]
        public void Setup()
        {
            
            input[0] = "KEY key0=0x(1234)";
            input[1] = "KEY key1=0x(1234) print(key1)";
            input[2] = "VAR c1 = Sign(Encrypt(key1),0x(123))";
            input[3] = "print(c1)";
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
            Assert.IsNotNull(statement);
            Assert.IsTrue(res.Statements.Count == 1);   
            Assert.IsTrue(statement is StringVariableDeclaration);
            var variable = statement as StringVariableDeclaration;
            Assert.IsTrue(variable.Id == "key0");
            Assert.IsTrue(variable.Value == "0x(1234)");
            Assert.IsTrue(variable.Type is CryptoTypeKey);
            Assert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));

            
        }
        [Test] public void DeclarationAndFunctionCallTest() 
        { 
            AntlrToProgram prog = new AntlrToProgram(); 
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[1]);             
            CryptoScriptParser.ProgramContext context = parser.program(); 
            var res = prog.Visit(context); 
            var statement=res.Statements.FirstOrDefault(); 
            Assert.IsNotNull(statement); 
            Assert.IsTrue(res.Statements.Count == 2);    
            Assert.IsTrue(statement is StringVariableDeclaration); 
            var variable = statement as StringVariableDeclaration; 
            Assert.IsTrue(variable.Id == "key1"); 
            Assert.IsTrue(variable.Value == "0x(1234)"); 
            Assert.IsTrue(variable.Type is CryptoTypeKey);
            Assert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            statement =res.Statements.LastOrDefault(); 
            Assert.IsNotNull(statement); 
            Assert.IsTrue(statement is FunctionCall); 
            var func = statement as FunctionCall; 
            Assert.IsTrue(func.Arguments.Count == 1);
            Assert.IsTrue(func.Arguments[0] is ArgumentVariable); 
            Assert.IsTrue(func.Name == "print"); 
            Assert.IsTrue(func.ReturnVariable is VariableDeclaration); 

        }
        [Test]
        public void GenerateKeyTest()
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[4]);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            Assert.IsNotNull(statement);
            Assert.IsTrue(res.Statements.Count == 1);
            Assert.IsTrue(statement is KeyVariableDeclaration);
            var variable = statement as KeyVariableDeclaration;
            Assert.IsTrue(variable.Id== "key2");
            Assert.IsTrue(variable.Mechanism== "AES-CBC");
            Assert.IsTrue(variable.KeySize =="128");
            var key=FormatConversions.HexStringToByteArray(variable.Value);
            Assert.IsTrue(key.Length==16);           
            Assert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
        }
        [Test]
        public void DefaultParametersTest()
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[5]);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            Assert.IsNotNull(statement);
            Assert.IsTrue(res.Statements.Count == 1);
            Assert.IsTrue(statement is ParameterVariableDeclaration);
            var variable = statement as ParameterVariableDeclaration;
            var iv = FormatConversions.HexStringToByteArray(variable.IV);
            Assert.IsTrue(iv.Length==16);
            Assert.IsTrue(variable.Padding=="PKCS-7");
            Assert.IsTrue(variable.Mechanism=="AES-CBC");
            Assert.IsTrue(variable.Id == "p1");
            Assert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            Assert.IsTrue(string.IsNullOrEmpty(variable.Nonce));
            Assert.IsTrue(string.IsNullOrEmpty(variable.Counter));
        }
        [Test]
        public void ParametersTest()
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[6]);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            Assert.IsNotNull(statement);
            Assert.IsTrue(res.Statements.Count == 1);
            Assert.IsTrue(statement is ParameterVariableDeclaration);
            var variable = statement as ParameterVariableDeclaration;
            Assert.IsTrue(variable.IV == "0x(12345678)");
            Assert.IsTrue(variable.Padding == "PKCS-7");
            Assert.IsTrue(variable.Mechanism == "AES-CBC");
            Assert.IsTrue(variable.Id == "p2");
            Assert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
        }
        [Test]
        public void EncryptionAESTest() 
        {
            AntlrToProgram prog = new AntlrToProgram();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input[7]);            
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            Assert.IsNotNull(statement);
            Assert.IsTrue(res.Statements.Count == 3);
            var variable = statement as KeyVariableDeclaration;
            Assert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            Assert.IsTrue(VariableDictionary.Instance().Contains(pv.Id));
            var crypto= res.Statements[2] as StringVariableDeclaration;
            Assert.IsTrue(VariableDictionary.Instance().Contains(crypto.Id));
        }
        [Test]
        public void DecryptionAESTest()
        {
            AntlrToProgram prog = new AntlrToProgram();
            string input= "KEY edkey=GenerateKey(AES-CBC,128) PARAM p4=Parameters(AES-CBC) VAR c3 = Encrypt(p4,edkey,0x(1234567812345678123456781234567812345678)) VAR c4=Decrypt(p4,edkey,c3)";
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Visit(context);
            var statement = res.Statements.FirstOrDefault();
            Assert.IsNotNull(statement);
            Assert.IsTrue(res.Statements.Count == 4);
            var variable = statement as KeyVariableDeclaration;
            Assert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
            var pv = res.Statements[1] as ParameterVariableDeclaration;
            Assert.IsTrue(VariableDictionary.Instance().Contains(pv.Id));
            var crypto = res.Statements[2] as StringVariableDeclaration;
            Assert.IsTrue(VariableDictionary.Instance().Contains(crypto.Id));
        }

    }
}