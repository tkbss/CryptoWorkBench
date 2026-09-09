using CryptoScript.ErrorListner;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    [NonParallelizable]
    public class SemanticErrorTests
    {
        private VariableDeclaration[] previous = null!;

        [SetUp]
        public void SetUp()
        {
            previous = VariableDictionary.Instance().GetVariables().ToArray();
            VariableDictionary.Instance().Clear();
        }

        [TearDown]
        public void TearDown()
        {
            VariableDictionary.Instance().Clear();
            foreach (var variable in previous)
                VariableDictionary.Instance().Add(variable);
        }

        [Test]
        public void Argument_Wrong_number_KeyGeneration_Error()
        {
            var prog = new AntlrToProgram();
            var context = Parse("KEY k=GenerateKey(128)");

            var error = Assert.Throws<SemanticErrorException>(() => prog.Visit(context));

            Assert.That(error!.SemanticError, Is.Not.Null);
            Assert.That(error.SemanticError!.Type, Is.EqualTo("FunctionCall"));
            Assert.That(error.SemanticError.Message, Is.EqualTo("wrong number of arguments"));
            Assert.That(error.SemanticError.FunctionName, Is.EqualTo("GenerateKey"));
            Assert.That(prog.SemanticErrors, Is.EqualTo(new[] { error.SemanticError }));
        }

        [Test]
        public void Argument_Wrong_number_Parameters_Error()
        {
            var prog = new AntlrToProgram();
            var context = Parse("PARAM p11=Parameters()");

            var error = Assert.Throws<SemanticErrorException>(() => prog.Visit(context));

            Assert.That(error!.SemanticError, Is.Not.Null);
            Assert.That(error.SemanticError!.Type, Is.EqualTo("FunctionCall"));
            Assert.That(error.SemanticError.Message, Is.EqualTo("wrong number of arguments"));
            Assert.That(error.SemanticError.FunctionName, Is.EqualTo("Parameters"));
            Assert.That(prog.SemanticErrors, Is.EqualTo(new[] { error.SemanticError }));
        }

        [Test]
        public void Semantic_Parameter_Error()
        {
            var prog = new AntlrToProgram();
            var context = Parse("PARAM p=Parameters(#PAD:YYY)");

            var error = Assert.Throws<SemanticErrorException>(() => prog.Visit(context));

            Assert.That(error!.SemanticError, Is.Not.Null);
            Assert.That(error.SemanticError!.Type, Is.EqualTo("FunctionCall"));
            Assert.That(error.SemanticError.Message, Is.EqualTo("Unknown parameter value : YYY"));
            Assert.That(error.SemanticError.FunctionName, Is.EqualTo("Parameters"));
            Assert.That(prog.SemanticErrors, Is.EqualTo(new[] { error.SemanticError }));
        }

        [Test]
        public void Unknown_function_declaration_Error()
        {
            var prog = new AntlrToProgram();
            var context = Parse("PARAM P=GenerateParameters()");

            var error = Assert.Throws<SemanticErrorException>(() => prog.Visit(context));

            Assert.That(error!.SemanticError, Is.Not.Null);
            Assert.That(error.SemanticError!.Type, Is.EqualTo("FunctionCall"));
            Assert.That(error.SemanticError.Message, Is.EqualTo("Unknown function: GenerateParameters"));
            Assert.That(error.SemanticError.FunctionName, Is.EqualTo("GenerateParameters"));
            Assert.That(prog.SemanticErrors, Is.EqualTo(new[] { error.SemanticError }));
        }

        [Test]
        public void Unknown_function_Error()
        {
            var prog = new AntlrToProgram();
            var context = Parse("Unknown()");

            var error = Assert.Throws<SemanticErrorException>(() => prog.Visit(context));

            Assert.That(error!.SemanticError, Is.Not.Null);
            Assert.That(error.SemanticError!.Type, Is.EqualTo("FunctionCall"));
            Assert.That(error.SemanticError.Message, Is.EqualTo("Unknown function: Unknown"));
            Assert.That(error.SemanticError.FunctionName, Is.EqualTo("Unknown"));
            Assert.That(prog.SemanticErrors, Is.EqualTo(new[] { error.SemanticError }));
        }

        [Test]
        public void Wrong_Declaration_Type_Error()
        {
            var prog = new AntlrToProgram();
            var context = Parse("VAR k2=GenerateKey(AES-CBC,128)");

            var error = Assert.Throws<SemanticErrorException>(() => prog.Visit(context));

            Assert.That(error!.SemanticError, Is.Not.Null);
            Assert.That(error.SemanticError!.Type, Is.EqualTo("Declaration"));
            Assert.That(error.SemanticError.Message, Is.EqualTo("Declaration type mismatch. Expected type : KEY"));
            Assert.That(error.SemanticError.Identifier, Is.EqualTo("VAR"));
            Assert.That(prog.SemanticErrors, Is.EqualTo(new[] { error.SemanticError }));
        }

        [Test]
        public void Wrong_Parameter_Declaration_Type_Error()
        {
            var prog = new AntlrToProgram();
            var context = Parse("KEY p11 = #MECH:AES-CTR #NONCE:0x(00112233445566778899AABB) #COUNTER:0x(00000000) #PAD:PKCS-7 #IV:0x(12345678)");

            var error = Assert.Throws<SemanticErrorException>(() => prog.Visit(context));

            Assert.That(error!.SemanticError, Is.Not.Null);
            Assert.That(error.SemanticError!.Type, Is.EqualTo("Declaration"));
            Assert.That(error.SemanticError.Message, Is.EqualTo("Declaration type mismatch. Expected type : PARAM"));
            Assert.That(error.SemanticError.Identifier, Is.EqualTo("KEY"));
            Assert.That(prog.SemanticErrors, Is.EqualTo(new[] { error.SemanticError }));
        }

        [Test]
        public void Variable_Redeclaration_ReplacesRegisteredDeclaration()
        {
            var prog = new AntlrToProgram();
            var context = Parse("KEY k1=GenerateKey(AES-CBC,128) VAR k1=0x(123)");

            var result = prog.Visit(context);

            Assert.That(prog.SemanticErrors, Is.Empty);
            Assert.That(result.Statements, Has.Count.EqualTo(2));
            Assert.That(result.Statements[0], Is.TypeOf<KeyVariableDeclaration>());
            Assert.That(result.Statements[1], Is.TypeOf<StringVariableDeclaration>());
            var replacement = (StringVariableDeclaration)result.Statements[1];
            Assert.That(replacement.Id, Is.EqualTo("k1"));
            Assert.That(replacement.Type, Is.TypeOf<CryptoTypeVar>());
            Assert.That(replacement.Value, Is.EqualTo("0x(123)"));
            Assert.That(replacement, Is.Not.SameAs(result.Statements[0]));
            Assert.That(VariableDictionary.Instance().Get("k1"), Is.SameAs(replacement));
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.EqualTo(new[] { replacement }));
        }

        private static CryptoScriptParser.ProgramContext Parse(string input)
        {
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
            var parser = ParserBuilder.StringBuild(input);
            var context = parser.program();
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
            Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
            Assert.That(SyntaxErrorList.Instance(), Is.Empty);
            return context;
        }
    }
}
