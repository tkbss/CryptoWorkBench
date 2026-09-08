using Antlr4.Runtime.Misc;
using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using System.Net.WebSockets;
using System.Xml.Linq;
using static CryptoScript.Model.Expression;

namespace CryptoScript.Model
{
    public class AntlrToStatement : CryptoScriptBaseVisitor<Statement>
    {

        public List<SemanticError> SemanticErrors { get; set; }

        public override Statement VisitDeclareparam([NotNull] CryptoScriptParser.DeclareparamContext context)
        {
            var type=context.GetChild(0).GetText();
            var value=context.GetChild(2).GetText();
            return EvaluateParameter(type, value);
        }

        private static ArgumentParameter EvaluateParameter(string type, string value)
        {
            var parameter = new ArgumentParameter();
            parameter.SetParameter(type, value);
            return parameter;
        }

        public AntlrToStatement()
        {
            SemanticErrors = new List<SemanticError>();
        }

        public override Statement VisitArgument([NotNull] CryptoScriptParser.ArgumentContext context)
        {
            return FunctionCallEvaluator.EvaluateArgument(AntlrToFunctionCallInitializer.MapArgument(context),
                context.Parent?.Parent?.GetText().Split('(')[0] ?? string.Empty, SemanticErrors, EvaluateParameter);
        }

        public override Statement VisitArguments([NotNull] CryptoScriptParser.ArgumentsContext context)
        {

            return base.VisitArguments(context);
        }

        public override Statement VisitDeclaration([NotNull] CryptoScriptParser.DeclarationContext context)
        {
            var declaration = AntlrToVariableDeclaration.Map(context);
            return VariableDeclarationEvaluator.Evaluate(
                declaration, SemanticErrors,
                call => FunctionCallEvaluator.EvaluateFunctionCall(call, SemanticErrors, EvaluateParameter),
                EvaluateParameter);
        }

        public override Statement VisitExpression([NotNull] CryptoScriptParser.ExpressionContext context)
        {
            return Expression.Create(context.GetText());

        }

        public override Statement VisitFunctionCall([NotNull] CryptoScriptParser.FunctionCallContext context)
        {
            return FunctionCallEvaluator.EvaluateFunctionCall(
                AntlrToFunctionCallInitializer.Map(context), SemanticErrors, EvaluateParameter);
        }

        public override Statement VisitStatement([NotNull] CryptoScriptParser.StatementContext context)
        {

            var fcContext = context.functionCall();            
            if (fcContext != null)
            {
                var fc = VisitFunctionCall(fcContext);
                return fc;
            }
            var s = base.VisitStatement(context);
            if (s != null)
            {
                s.Text = context.GetText();
            }
            return s;
        }

        public override Statement VisitType([NotNull] CryptoScriptParser.TypeContext context)
        {
            CryptoType type = CryptoType.Parse(context.GetText());
            return type;
        }
    }
}
