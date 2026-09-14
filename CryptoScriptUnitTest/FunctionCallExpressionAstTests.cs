using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    public class FunctionCallExpressionAstTests
    {
        [Test]
        public void SharesExpressionBasisAndCallTypeAcrossAllPositions()
        {
            ExpressionNode literal = new LiteralInitializerNode("000123");
            var parser = ParserBuilder.StringBuild("VAR result = Outer(Inner(000123)) Outer(Inner(000123))");
            var program = parser.program();
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            ExpressionNode initializer = AntlrToVariableDeclaration.Map(program.statement(0).declaration()).Initializer!;
            var statement = (FunctionCallStatementNode)AntlrToStatement.Map(program.statement(1))!;

            Assert.That(literal, Is.TypeOf<LiteralInitializerNode>());
            Assert.That(initializer, Is.TypeOf<FunctionCallExpressionNode>());
            Assert.That(statement.Call, Is.TypeOf<FunctionCallExpressionNode>());
            var nested = (NestedCallArgumentNode)((FunctionCallExpressionNode)initializer).Arguments[0];
            Assert.That(nested.Call, Is.TypeOf<FunctionCallExpressionNode>());
            Assert.That(statement.Call.CallText, Is.EqualTo(((FunctionCallExpressionNode)initializer).CallText));
        }

        [Test]
        public void MapsAllArgumentKindsInOrderWithoutSemanticValidation()
        {
            var previous = VariableDictionary.Instance().GetVariables().ToArray();
            var call = Map("Unknown(AES-CBC,missing,\"a b\",#IV:0x(AbCd),functions,Nested(000123))");

            Assert.That(call.Name, Is.EqualTo("Unknown"));
            Assert.That(call.CallText,
                Is.EqualTo("Unknown(AES-CBC,missing,\"a b\",#IV:0x(AbCd),functions,Nested(000123))"));
            Assert.That(call.Arguments.Take(5), Is.EqualTo(new FunctionCallArgumentNode[]
            {
                new MechanismArgumentNode("AES-CBC"),
                new VariableArgumentNode("missing"),
                new LiteralArgumentNode("\"a b\""),
                new ParameterArgumentNode("#IV", "0x(AbCd)"),
                new InfoArgumentNode("functions")
            }));
            Assert.That(call.Arguments, Has.Count.EqualTo(6));
            var nested = ((NestedCallArgumentNode)call.Arguments[5]).Call;
            Assert.That(nested.Name, Is.EqualTo("Nested"));
            Assert.That(nested.CallText, Is.EqualTo("Nested(000123)"));
            Assert.That(nested.Arguments, Is.EqualTo(new[] { new LiteralArgumentNode("000123") }));
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.EqualTo(previous));
        }

        [Test]
        public void MapsEmptyCallAndPreservesParseTreeTextConvention()
        {
            var call = Map("Unknown (  )");
            Assert.That(call.Name, Is.EqualTo("Unknown"));
            Assert.That(call.CallText, Is.EqualTo("Unknown()"));
            Assert.That(call.Arguments, Is.Empty);
        }

        [TestCase("999999999999999999999")]
        [TestCase("b64(AQID==)")]
        [TestCase("\"a\\n\\\"b\"")]
        [TestCase("\"header\"0x(AB)0x(cd)")]
        public void PreservesLiteralSyntaxWithoutEvaluation(string literal)
        {
            Assert.That(Map($"Unknown({literal})").Arguments,
                Is.EqualTo(new[] { new LiteralArgumentNode(literal) }));
        }

        private static FunctionCallExpressionNode Map(string initializer)
        {
            var parser = ParserBuilder.StringBuild("VAR result = " + initializer);
            var context = parser.program().statement(0).declaration();
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            return (FunctionCallExpressionNode)AntlrToVariableDeclaration.Map(context).Initializer!;
        }
    }
}
