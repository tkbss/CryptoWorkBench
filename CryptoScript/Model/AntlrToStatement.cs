using Antlr4.Runtime.Misc;
using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using System.Net.WebSockets;
using static CryptoScript.Model.Expression;

namespace CryptoScript.Model
{
    public class AntlrToStatement : CryptoScriptBaseVisitor<Statement>
    {

        public List<SemanticError> SemanticErrors { get; set; }

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
            SemanticErrors = new List<SemanticError>();
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
                catch(Exception e)
                {                    
                    var se=new SemanticError() {Type="Argument:Mechanism" };
                    se.Message = "Error  unkown mechanism : " + res1.GetText();
                    se.FunctionName = res1.Parent.Parent.Parent.GetText().Split('(')[0];
                    se.Message=e.Message;
                    se.Value = res1.GetText();  
                    SemanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError=se };
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
                    SemanticError se=new SemanticError() { Type = "Variable",Identifier=Id };
                    se.Message = "Error  variable : " + Id + " is not declared";
                    SemanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError=se};
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
            string TypeName= context.GetChild(0).GetText();
            if (VariableDictionary.Instance().Contains(Id))
            {
                SemanticError sem=new SemanticError() { Type = "Variable",Identifier=Id };
                sem.Message = "VAR " + Id +": "+ " already exists";    
                SemanticErrors.Add(sem);
                throw new SemanticErrorException() { SemanticError=sem};
            }
            var type= (CryptoType)VisitType(context.type());
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
                if(!(type is CryptoTypeParameters))
                {
                    SemanticError se=new SemanticError() { Type = "Declaration", Identifier = TypeName };
                    se.Message = "Declaration type mismatch. Expected type : " + "PARAM";
                    SemanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError=se};
                }
                var Parameter=new ParameterVariableDeclaration();
                Parameter.Mechanism = declarParam[0].GetText();
                for(int i=0;i<declarParam.Length;i++)
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
                    if (variable.Type.GetType() != type.GetType())
                    {
                        SemanticError se = new SemanticError() { Type = "Declaration", Identifier = TypeName };
                        se.Message = "Declaration type mismatch. Expected type : " + variable.Type.Name;
                        SemanticErrors.Add(se);
                        throw new SemanticErrorException() { SemanticError = se };
                    }                           
                    variable.Id = Id;
                    VariableDictionary.Instance().Add(variable);
                }
                stmt= variable;
            }
            
            stmt = Expression.BuildVariable(stmt, Id, type);
            return stmt;
             
        }

        public override Statement VisitExpression([NotNull] CryptoScriptParser.ExpressionContext context)
        {
            return Expression.Create(context.GetText());

        }

        public override Statement VisitFunctionCall([NotNull] CryptoScriptParser.FunctionCallContext context)
        {
            FunctionCall fc = new FunctionCall();
            string functionName = context.FN().GetText();
            fc.CallText = context.GetText();
            fc.Name = functionName;
            var arguments = context.arguments()?.argument();
            if (arguments == null || arguments.Length == 0)
            {
                try 
                {
                    fc.Call();
                    return fc;
                }
                catch (Exception e)
                {
                    SemanticError se=new SemanticError() { Type = "FunctionCall",FunctionName=functionName,FunctionCall=fc.CallText };
                    se.Message = "Error  function call : " + e.Message;
                    SemanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError=se};
                }   
               
            }
            Statement[] argValues = arguments.Select(arg => Visit(arg)).ToArray();
            fc.Arguments.AddRange(argValues.OfType<Argument>());
            try
            {
                fc.Call();
                return fc;
            }
            catch(Exception e)
            {
                SemanticError se=new SemanticError() { Type = "FunctionCall",FunctionName=functionName,FunctionCall=fc.CallText };
                se.Message = "Error  function call : "  + e.Message;
                SemanticErrors.Add(se);
                throw new SemanticErrorException() { SemanticError=se};
            }

            
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
