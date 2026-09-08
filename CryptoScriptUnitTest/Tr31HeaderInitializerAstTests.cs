using Antlr4.Runtime;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    public class Tr31HeaderInitializerAstTests
    {
        [TestCase(false, "{KBVID:D;KEYVN:00;KEYU:D0;}")]
        [TestCase(true, "{KEYU:D0;KEYVN:00;KBVID:D;}")]
        public void MapsValidHeaderTextInFieldOrder(bool reverse, string expected)
        {
            var context = ParseDeclaration(reverse);
            var previous = VariableDictionary.Instance().GetVariables().ToArray();

            var node = AntlrToVariableDeclaration.Map(context);

            Assert.That(node.Tr31Header, Is.Not.Null);
            Assert.That(node.Tr31Header!.RawText, Is.EqualTo(expected));
            Assert.That(node.Tr31Header.RawText, Is.EqualTo(context.tr31Header().GetText()));
            Assert.That(node.Expression, Is.Null);
            Assert.That(node.FunctionCall, Is.Null);
            Assert.That(node.Parameters, Is.Empty);
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.EqualTo(previous));
        }

        [Test]
        public void ValidHeaderStillFailsAtTypeResolution()
        {
            var context = ParseDeclaration(false);
            var previous = VariableDictionary.Instance().GetVariables().ToArray();
            var visitor = new AntlrToStatement();
            var expected = Assert.Throws<Exception>(() => CryptoType.Parse("TR31H"));

            var actual = Assert.Throws<Exception>(() => visitor.VisitDeclaration(context));

            Assert.That(actual!.Message, Is.EqualTo(expected!.Message));
            Assert.That(visitor.SemanticErrors, Is.Empty);
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.EqualTo(previous));
        }

        private static CryptoScriptParser.DeclarationContext ParseDeclaration(bool reverse)
        {
            // Supply grammar-valid token types directly: the existing lexer conflicts are
            // outside this migration. The actual parser constructs the complete declaration.
            var tokens = new List<IToken>
            {
                new CommonToken(CryptoScriptParser.T_TR31H, "TR31H"),
                new CommonToken(CryptoScriptParser.ID, "astHeader"),
                Punctuation("="), Punctuation("{")
            };
            var fields = new[]
            {
                (Name: "KBVID", Value: "D", Type: CryptoScriptParser.TR31_FIELD_VALUE),
                (Name: "KEYVN", Value: "00", Type: CryptoScriptParser.INT),
                (Name: "KEYU", Value: "D0", Type: CryptoScriptParser.TR31_FIELD_VALUE)
            };
            foreach (var field in reverse ? fields.Reverse() : fields)
            {
                tokens.Add(new CommonToken(CryptoScriptParser.TR31_FIELD_NAME, field.Name));
                tokens.Add(Punctuation(":"));
                tokens.Add(new CommonToken(field.Type, field.Value));
                tokens.Add(Punctuation(";"));
            }
            tokens.Add(Punctuation("}"));
            var parser = new CryptoScriptParser(new CommonTokenStream(new ListTokenSource(tokens)));
            var context = parser.program().statement(0).declaration();
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            Assert.That(context.tr31Header().tr31Field(), Has.Length.EqualTo(3));
            return context;
        }

        private static IToken Punctuation(string text) =>
            new CryptoScriptLexer(new AntlrInputStream(text)).NextToken();
    }
}
