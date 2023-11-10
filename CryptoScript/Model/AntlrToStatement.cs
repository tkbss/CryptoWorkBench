using Antlr4.Runtime.Misc;
using CryptoScript.Variables;

namespace CryptoScript.Model
{




    public class AntlrToStatement : CryptoScriptBaseVisitor<Statement>
    {

        public List<string> SemanticErrors { get; set; }

        public AntlrToStatement()
        {
            SemanticErrors = new List<string>();
        }

        public override Statement VisitArgument([NotNull] CryptoScriptParser.ArgumentContext context)
        {
            var res1 = context.MECHANISM();
            var res2 = context.functionCall();
            var res3 = context.ID();
            var res4 = context.expression();
            if (res1 != null)
            {
                Mechanism m = new Mechanism();
                try
                {
                    m.SetMechanismValue(res1.GetText());
                }
                catch
                {
                    SemanticErrors.Add("Error  unkown mechanism : " + res1.GetText());
                    throw new Exception("semantic error");
                }
                ArgumentMechanism argMech = new ArgumentMechanism() { Mechanism = m };
                return argMech;
            }
            if (res2 != null)
            {
                var fc = VisitFunctionCall(res2) as FunctionCall;
                if (fc != null && fc.ReturnVariable != null)
                {
                    var expr = Expression.Create(fc.ReturnVariable.Value);
                    ArgumentExpression argExpr = new ArgumentExpression() { Expr = expr };
                    return argExpr;
                }

            }
            if (res3 != null)
            {
                string Id = res3.GetText();
                //Id is a variable so
                if (!VariableDictionary.Instance().Contains(Id))
                {
                    SemanticErrors.Add("Error  variable : " + Id + " is not declared");
                    throw new Exception("semantic error");
                }
                ArgumentVariable argVar = new ArgumentVariable();
                argVar.Id = VariableDictionary.Instance().Get(Id);
                return argVar;
            }
            if (res4 != null)
            {
                ArgumentExpression argExpr = new ArgumentExpression() { Expr = Expression.Create(res4.GetText()) };
                return argExpr;
            }
            //ArgumentVariable arg = new ArgumentVariable();
            return new Argument();
        }

        public override Statement VisitArguments([NotNull] CryptoScriptParser.ArgumentsContext context)
        {

            return base.VisitArguments(context);
        }

        public override Statement VisitDeclaration([NotNull] CryptoScriptParser.DeclarationContext context)
        {
            string Id = context.GetChild(1).GetText();
            if (VariableDictionary.Instance().Contains(Id))
            {
                SemanticErrors.Add("Error  variable : " + Id + " already exists");
                throw new Exception();
            }



            var fcontext = context.functionCall();
            var exprContext = context.expression();
            Statement? stmt = null;
            if (fcontext != null)
            {
                stmt = VisitFunctionCall(fcontext);
            }
            if (exprContext != null)
            {
                stmt = VisitExpression(exprContext);
            }
            if (stmt is FunctionCall functionCall)
            {
                var variable = functionCall.ReturnVariable;
                if (variable != null)
                {
                    variable.Id = Id;
                    VariableDictionary.Instance().Add(variable);
                }
            }
            if (stmt is Expression expressionHex)
            {
                var typeCntx = context.type();
                var newVariable = new StringVariableDeclaration();
                newVariable.Id = Id;
                newVariable.Value = expressionHex.Value();
                newVariable.Type = (CryptoType)VisitType(typeCntx);
                newVariable.ValueFormat = FormatConversions.ParseString(expressionHex.Value());
                VariableDictionary.Instance().Add(newVariable);
            }
            return stmt;
        }

        public override Statement VisitExpression([NotNull] CryptoScriptParser.ExpressionContext context)
        {
            return Expression.Create(context.GetText());

        }

        public override Statement VisitFunctionCall([NotNull] CryptoScriptParser.FunctionCallContext context)
        {
            FunctionCall fc = new FunctionCall();
            string functionName = context.ID().GetText();
            fc.CallText = context.GetText();
            fc.Name = functionName;
            var arguments = context.arguments()?.argument();
            if (arguments == null || arguments.Length == 0)
            {
                fc.Call();
                return fc;
            }

            Statement[] argValues = arguments.Select(arg => Visit(arg)).ToArray();
            fc.Arguments.AddRange(argValues.OfType<Argument>());
            fc.Call();

            return fc;
        }

        public override Statement VisitStatement([NotNull] CryptoScriptParser.StatementContext context)
        {
            var fcContext = context.functionCall();
            if (fcContext != null)
            {
                var fc = VisitFunctionCall(fcContext);
                return fc;
            }
            return base.VisitStatement(context);
        }

        public override Statement VisitType([NotNull] CryptoScriptParser.TypeContext context)
        {
            CryptoType type = CryptoType.Parse(context.GetText());
            return type;
        }
    }
}
