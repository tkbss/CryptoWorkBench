using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    public class VariableDeclarationAstTests
    {
        [TestCase("VAR astValue = \"hello world\"", "VAR", "\"hello world\"")]
        [TestCase("KEY astValue = 0x(1234)", "KEY", "0x(1234)")]
        [TestCase("PATH astValue = /tmp/example", "PATH", "/tmp/example")]
        [TestCase("VAR astValue = b64(AQID==)", "VAR", "b64(AQID==)")]
        [TestCase("VAR astValue = 000123", "VAR", "000123")]
        [TestCase("VAR astValue = 999999999999999999999", "VAR", "999999999999999999999")]
        [TestCase("VAR astValue = \"header\"0x(AB)0x(cd)", "VAR", "\"header\"0x(AB)0x(cd)")]
        [TestCase("VAR astValue = \"a\\n\\\"b\"", "VAR", "\"a\\n\\\"b\"")]
        [TestCase("TR31H astValue = 0x(ABcd)", "TR31H", "0x(ABcd)")]
        public void MapsDeclarationWithoutCreatingRuntimeVariable(string input, string type, string value)
        {
            var context = Parse(input);
            var variables = VariableDictionary.Instance().GetVariables().ToArray();

            var node = AntlrToVariableDeclaration.Map(context);

            Assert.That(node.Identifier, Is.EqualTo("astValue"));
            Assert.That(node.TypeName, Is.EqualTo(type));
            Assert.That(node.Expression, Is.Not.Null);
            Assert.That(node.Expression!.RawText, Is.EqualTo(value));
            Assert.That(node.Expression.RawText, Is.EqualTo(context.expression().GetText()));
            Assert.That(node.FunctionCall, Is.Null);
            Assert.That(node.Parameters, Is.Empty);
            Assert.That(node.Tr31Header, Is.Null);
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.EqualTo(variables));
        }

        [TestCase("KEY astRuntime = 0x(ABcd)")]
        [TestCase("VAR astRuntime = b64(AQID==)")]
        [TestCase("VAR astRuntime = \"a\\n\\\"b\"")]
        [TestCase("VAR astRuntime = 000123")]
        [TestCase("VAR astRuntime = 999999999999999999999")]
        [TestCase("PATH astRuntime = /tmp/example")]
        [TestCase("VAR astRuntime = \"header\"0x(AB)0x(cd)")]
        [TestCase("TR31H astRuntime = 0x(1234)")]
        public void LiteralRuntimeMatchesPreviousParseTreePath(string input)
        {
            var context = Parse(input);
            var dictionary = VariableDictionary.Instance();
            var previous = dictionary.GetVariables().ToArray();
            try
            {
                VariableDeclaration? expected = null;
                // Reproduce the pre-migration literal path, including type resolution order.
                var expectedError = CaptureException(() =>
                {
                    var type = CryptoType.Parse(context.type().GetText());
                    expected = (VariableDeclaration)Expression.BuildVariable(
                        Expression.Create(context.expression().GetText()), "astRuntime", type);
                });
                dictionary.Clear();
                foreach (var variable in previous)
                    dictionary.Add(variable);

                VariableDeclaration? actual = null;
                var actualError = CaptureException(() =>
                    actual = (VariableDeclaration)new AntlrToStatement().VisitDeclaration(context));

                Assert.That(actualError?.GetType(), Is.EqualTo(expectedError?.GetType()));
                Assert.That(actualError?.Message, Is.EqualTo(expectedError?.Message));
                if (expectedError == null)
                {
                    Assert.That(actual, Is.Not.Null);
                    Assert.That(actual!.GetType(), Is.EqualTo(expected!.GetType()));
                    Assert.That(actual.Type!.GetType(), Is.EqualTo(expected.Type!.GetType()));
                    Assert.That(actual.Value, Is.EqualTo(expected.Value));
                    Assert.That(actual.ValueFormat, Is.EqualTo(expected.ValueFormat));
                    Assert.That(dictionary.Get("astRuntime"), Is.SameAs(actual));
                }
            }
            finally
            {
                dictionary.Clear();
                foreach (var variable in previous)
                    dictionary.Add(variable);
            }
        }

        [Test]
        public void MapsFunctionCallWithoutExecutingOrResolvingArguments()
        {
            var context = Parse("VAR astResult = Encrypt(astMissingParameter,astMissingKey,0x(1234))");
            var variables = VariableDictionary.Instance().GetVariables().ToArray();

            var node = AntlrToVariableDeclaration.Map(context);

            Assert.That(node.FunctionCall, Is.Not.Null);
            Assert.That(node.FunctionCall!.Name, Is.EqualTo("Encrypt"));
            Assert.That(node.FunctionCall.CallText, Is.EqualTo(context.functionCall().GetText()));
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

        private static Exception? CaptureException(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception error)
            {
                return error;
            }
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
