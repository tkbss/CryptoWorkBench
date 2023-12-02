using Antlr4.Runtime.Misc;
using CryptoScript.Variables;

namespace CryptoScript.Model
{




    public class AntlrToStatement : CryptoScriptBaseVisitor<Statement>
    {

        public List<string> SemanticErrors { get; set; }

        public override Statement VisitDeclareparam([NotNull] CryptoScriptParser.DeclareparamContext context)
        {
            var parameter = new ArgumentParameter();
            int cnt = context.ChildCount;
            var type=context.GetChild(0).GetText();
            var value=context.GetChild(2).GetText();
            parameter.SetParameter(type, value);            
            return parameter;
        }

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
            var res5 = context.declareparam();
            
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
            if(res5!=null)
            {

                return VisitDeclareparam(res5);
            }
            
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
            var declarParam=context.declareparam();
            
            Statement? stmt = null;
            if (fcontext != null)
            {
                stmt = VisitFunctionCall(fcontext);
            }
            if (exprContext != null)
            {
                stmt = VisitExpression(exprContext);
            }
            if(declarParam!=null && declarParam.Length!=0)
            {
                var Parameter=new ParameterVariableDeclaration();
                Parameter.Mechanism = declarParam[0].GetText();
                for(int i=1;i<declarParam.Length;i++)
                {
                    var param = VisitDeclareparam(declarParam[i]) as ArgumentParameter;
                    Parameter.SetParameter(param);
                }
                var typeCntx = context.type(); 
                Parameter.Id = Id;
                Parameter.Type = (CryptoType)VisitType(typeCntx);
                VariableDictionary.Instance().Add(Parameter);
                stmt = Parameter;
            }
            if (stmt is FunctionCall functionCall)
            {
                var variable = functionCall.ReturnVariable;
                if (variable != null)
                {
                    variable.Id = Id;
                    VariableDictionary.Instance().Add(variable);
                }
                stmt= variable;
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
                stmt = newVariable;
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
            try
            {
                fc.Call();
            }
            catch(Exception e)
            {
                SemanticErrors.Add("Error  function call : " + fc.Name + " " + e.Message);
                throw new Exception("semantic error");
            }

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
