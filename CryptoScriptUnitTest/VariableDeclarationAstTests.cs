using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    public class VariableDeclarationAstTests
    {
        [TestCase("VAR astValue = \"hello world\"", "VAR", "\"hello world\"")]
        [TestCase("KEY astValue = 0x(1234)", "KEY", "0x(1234)")]
        [TestCase("PATH astValue = /tmp/example", "PATH", "/tmp/example")]
        public void MapsDeclarationWithoutCreatingRuntimeVariable(string input, string type, string value)
        {
            var context = Parse(input);
            var variables = VariableDictionary.Instance().GetVariables().ToArray();

            var node = AntlrToVariableDeclaration.Map(context);

            Assert.That(node.Identifier, Is.EqualTo("astValue"));
            Assert.That(node.TypeName, Is.EqualTo(type));
            Assert.That(node.Expression, Is.SameAs(context.expression()));
            Assert.That(node.Expression!.GetText(), Is.EqualTo(value));
            Assert.That(node.FunctionCall, Is.Null);
            Assert.That(node.Parameters, Is.Empty);
            Assert.That(node.Tr31Header, Is.Null);
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.EqualTo(variables));
        }

        [Test]
        public void RetainsFunctionCallWithoutExecutingOrResolvingArguments()
        {
            var context = Parse("VAR astResult = Encrypt(astMissingParameter,astMissingKey,0x(1234))");
            var variables = VariableDictionary.Instance().GetVariables().ToArray();

            var node = AntlrToVariableDeclaration.Map(context);

            Assert.That(node.FunctionCall, Is.SameAs(context.functionCall()));
            Assert.That(node.Expression, Is.Null);
            Assert.That(node.Parameters, Is.Empty);
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.EqualTo(variables));
        }

        [Test]
        public void RetainsParameterOrderWithoutCheckingDeclarationType()
        {
            var context = Parse("VAR astParameters = #MECH:AES-CBC #PAD:PKCS-7 #IV:0x(1234)");

            var node = AntlrToVariableDeclaration.Map(context);

            Assert.That(node.TypeName, Is.EqualTo("VAR"));
            Assert.That(node.Parameters.Select(p => p.GetText()),
                Is.EqualTo(new[] { "#MECH:AES-CBC", "#PAD:PKCS-7", "#IV:0x(1234)" }));
            for (int i = 0; i < node.Parameters.Count; i++)
                Assert.That(node.Parameters[i], Is.SameAs(context.declareparam(i)));
            Assert.That(node.Expression, Is.Null);
            Assert.That(node.FunctionCall, Is.Null);
        }

        [Test]
        public void RetainsUnmigratedHeaderWithoutResolvingRuntimeType()
        {
            // Header lexing is outside this migration; test the mapping boundary directly.
            var context = Parse("TR31H astHeader =");
            var header = new CryptoScriptParser.Tr31HeaderContext(context, -1);
            context.AddChild(header);

            var node = AntlrToVariableDeclaration.Map(context);

            Assert.That(node.TypeName, Is.EqualTo("TR31H"));
            Assert.That(node.Tr31Header, Is.SameAs(header));
            Assert.That(node.Parameters, Is.Empty);
        }

        [Test]
        public void MapsEmptyInitializerWithoutAddingValidation()
        {
            var node = AntlrToVariableDeclaration.Map(Parse("VAR astEmpty ="));

            Assert.That(node.Identifier, Is.EqualTo("astEmpty"));
            Assert.That(node.Expression, Is.Null);
            Assert.That(node.FunctionCall, Is.Null);
            Assert.That(node.Parameters, Is.Empty);
            Assert.That(node.Tr31Header, Is.Null);
        }

        private static CryptoScriptParser.DeclarationContext Parse(string input)
        {
            var parser = ParserBuilder.StringBuild(input);
            var program = parser.program();
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            return program.statement(0).declaration();
        }
    }
}
