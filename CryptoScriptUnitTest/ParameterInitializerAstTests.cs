using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    public class ParameterInitializerAstTests
    {
        [Test]
        public void MapsMissingValueAndRetainsRuntimeDiagnostic()
        {
            var parser = ParserBuilder.StringBuild("PARAM astIncomplete = #MECH:AES-CBC #IV:");
            parser.RemoveErrorListeners();
            var context = parser.program().statement(0).declaration();
            Assert.That(parser.NumberOfSyntaxErrors, Is.GreaterThan(0));

            var node = AntlrToVariableDeclaration.Map(context);

            // ANTLR recovery leaves an empty declareparam context for the incomplete suffix.
            Assert.That(node.Parameters[1], Is.EqualTo(new ParameterInitializerNode(null, null, "")));
            var visitor = new AntlrToStatement();
            var error = Assert.Throws<CryptoScript.ErrorListner.SemanticErrorException>(
                () => visitor.VisitDeclaration(context));
            Assert.That(error!.SemanticError.Type, Is.EqualTo("Declaration"));
            Assert.That(error.SemanticError.Message, Is.EqualTo("Error in  parameter declaration : "));
            Assert.That(visitor.SemanticErrors.Single(), Is.SameAs(error.SemanticError));
        }

        [TestCase("#MECH : AES-CBC", "#MECH", "AES-CBC")]
        [TestCase("#PAD : PKCS-7", "#PAD", "PKCS-7")]
        [TestCase("#IV : 0x(AbCd)", "#IV", "0x(AbCd)")]
        [TestCase("#IV : astMissing", "#IV", "astMissing")]
        [TestCase("#IV : \"raw value\"", "#IV", "\"raw value\"")]
        [TestCase("#IV : \"a\\n\\\"b\"", "#IV", "\"a\\n\\\"b\"")]
        [TestCase("#IV : \"MECH\"", "#IV", "\"MECH\"")]
        public void MapsSyntaxWithoutValidationOrResolution(string syntax, string type, string value)
        {
            var previous = VariableDictionary.Instance().GetVariables().ToArray();
            // VAR deliberately has the wrong runtime type and most cases lack #MECH.
            var declaration = Parse("VAR astParameters = " + syntax);
            var node = AntlrToVariableDeclaration.Map(declaration);

            Assert.That(node.Parameters, Is.EqualTo(new[]
            {
                new ParameterInitializerNode(type, value, type + ":" + value)
            }));
            Assert.That(node.Parameters[0].RawText, Is.EqualTo(declaration.declareparam(0).GetText()));
            Assert.That(node.Expression, Is.Null);
            Assert.That(node.FunctionCall, Is.Null);
            Assert.That(node.Tr31Header, Is.Null);

            // The existing function-call AST and its parameter mapping remain unchanged.
            var direct = AntlrToVariableDeclaration.Map(Parse("VAR astCall = Unknown(" + syntax + ")")).FunctionCall!;
            Assert.That(direct.Arguments, Is.EqualTo(new[] { new ParameterArgumentNode(type, value) }));
            var outer = AntlrToVariableDeclaration.Map(Parse("VAR astCall = Unknown(Nested(" + syntax + "))")).FunctionCall!;
            var nested = ((NestedCallArgumentNode)outer.Arguments.Single()).Call;
            Assert.That(nested.Arguments, Is.EqualTo(new[] { new ParameterArgumentNode(type, value) }));
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.EqualTo(previous));
        }

        [Test]
        public void PreservesOrderAndDuplicates()
        {
            var node = AntlrToVariableDeclaration.Map(Parse(
                "PARAM astParameters = #IV:0x(Ab) #MECH:AES-CBC #IV:0x(cd) #MECH:AES-ECB"));

            Assert.That(node.Parameters.Select(p => p.RawText), Is.EqualTo(new[]
            {
                "#IV:0x(Ab)", "#MECH:AES-CBC", "#IV:0x(cd)", "#MECH:AES-ECB"
            }));
        }

        private static CryptoScriptParser.DeclarationContext Parse(string input)
        {
            var parser = ParserBuilder.StringBuild(input);
            var declaration = parser.program().statement(0).declaration();
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            return declaration;
        }
    }
}
