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
            return EvaluateArgument(AntlrToFunctionCallInitializer.MapArgument(context),
                context.Parent?.Parent?.GetText().Split('(')[0] ?? string.Empty);
        }

        private Statement EvaluateArgument(Ast.FunctionCallArgumentNode argument, string functionName)
        {
            if (argument is Ast.MechanismArgumentNode mechanism)
            {
                
                Mechanism m = new Mechanism();
                try
                {
                    m.SetMechanismValue(mechanism.RawText);
                }
                catch(Exception e)
                {                    
                    var se=new SemanticError() {Type="Argument:Mechanism" };
                    se.Message = "Error unknown mechanism: " + mechanism.RawText;
                    se.FunctionName = functionName;
                    se.Message=e.Message;
                    se.Value = mechanism.RawText;
                    SemanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError=se };
                }
                ArgumentMechanism argMech = new ArgumentMechanism() { Mechanism = m };
                return argMech;
            }
            if (argument is Ast.NestedCallArgumentNode nestedCall)
            {
                var fc = EvaluateFunctionCall(nestedCall.Call) as FunctionCall;
                if (fc != null && fc.ReturnVariable != null)
                {
                    var expr = Expression.Create(fc.ReturnVariable.Value);
                    ArgumentExpression argExpr = new ArgumentExpression() { Expr = expr };
                    return argExpr;
                }

            }
            if (argument is Ast.VariableArgumentNode variable)
            {
                string Id = variable.Identifier;
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
            if (argument is Ast.LiteralArgumentNode literal)
            {
                ArgumentExpression argExpr = new ArgumentExpression() { Expr = Expression.Create(literal.RawText) };
                return argExpr;
            }
            if (argument is Ast.ParameterArgumentNode parameter)
            {

                return EvaluateParameter(parameter.TypeName, parameter.RawValue);
            }
            if (argument is Ast.InfoArgumentNode infoNode)
            {
                ArgumentInfo info = new ArgumentInfo() { InfoType= infoNode.RawText };
                return info;
            }
            return new Argument();
        }

        public override Statement VisitArguments([NotNull] CryptoScriptParser.ArgumentsContext context)
        {

            return base.VisitArguments(context);
        }

        public override Statement VisitDeclaration([NotNull] CryptoScriptParser.DeclarationContext context)
        {
            return EvaluateDeclaration(AntlrToVariableDeclaration.Map(context));
        }

        private Statement EvaluateDeclaration(Ast.VariableDeclarationNode declaration)
        {
            string Id = declaration.Identifier;
            string TypeName = declaration.TypeName;
            var type = CryptoType.Parse(TypeName);
            var fcontext = declaration.FunctionCall;
            var expression = declaration.Expression;
            var declarParam = declaration.Parameters;

            Statement? stmt = null;
            if (fcontext != null)
            {
                stmt = EvaluateFunctionCall(fcontext);
            }
            if (expression != null)
            {
                stmt = Expression.Create(expression.RawText);
            }
            if(declarParam.Count!=0)
            {
                if(!(type is CryptoTypeParameters))
                {
                    SemanticError se=new SemanticError() { Type = "Declaration", Identifier = TypeName };
                    se.Message = "Declaration type mismatch. Expected type : " + "PARAM";
                    SemanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError=se};
                }                
                var mech=declarParam.FirstOrDefault(c => c.RawText.Contains("MECH"));
                if (mech == null) 
                {
                    SemanticError se = new SemanticError() { Type = "Declaration", Identifier = TypeName };
                    se.Message = "Type PARAM does not contain element #MECH";
                    SemanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError = se };
                }
                var Parameter = new ParameterVariableDeclaration();
                Parameter.Mechanism = mech.RawText;
                for (int i = 0; i < declarParam.Count; i++)
                {
                    try
                    {
                        // Preserve the former missing-child failure inside this runtime error boundary.
                        var param = EvaluateParameter(
                            declarParam[i].TypeName ?? throw new NullReferenceException(),
                            declarParam[i].RawValue ?? throw new NullReferenceException());
                        Parameter.SetParameter(param);
                    }
                    catch 
                    {
                        SemanticError se = new SemanticError() { Type = "Declaration", Identifier = TypeName };
                        se.Message = "Error in  parameter declaration : " + declarParam[i].RawText;
                        SemanticErrors.Add(se);
                        throw new SemanticErrorException() { SemanticError = se };
                    }
                    
                }
                Parameter.Id = Id;
                Parameter.Type = CryptoType.Parse(TypeName);
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
            return EvaluateFunctionCall(AntlrToFunctionCallInitializer.Map(context));
        }

        private Statement EvaluateFunctionCall(Ast.FunctionCallInitializerNode call)
        {
            FunctionCall fc = new FunctionCall();
            string functionName = call.Name;
            fc.CallText = call.CallText;
            fc.Name = functionName;
            try 
            {
                OperationFactory.CreateOperation(functionName);
            } 
            catch (Exception ex) 
            { 
                SemanticError se=new SemanticError() { Type = "FunctionCall",FunctionName=functionName,FunctionCall=fc.CallText };
                se.Message = "Unknown function: "+functionName;
                SemanticErrors.Add(se);
                throw new SemanticErrorException() { SemanticError=se};
            }
            var arguments = call.Arguments;
            if (arguments.Count == 0)
            {
                try 
                {
                    fc.Call();
                    return fc;
                }
                catch (Exception e)
                {
                    SemanticError se=new SemanticError() { Type = "FunctionCall",FunctionName=functionName,FunctionCall=fc.CallText };
                    se.Message = e.Message;
                    SemanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError=se};
                }   
               
            }
            
            try
            {
                Statement[] argValues = arguments.Select(arg => EvaluateArgument(arg, functionName)).ToArray();
                fc.Arguments.AddRange(argValues.OfType<Argument>());
                fc.Call();
                return fc;
            }
            catch(Exception e)
            {
                SemanticError se=new SemanticError() { Type = "FunctionCall",FunctionName=functionName,FunctionCall=fc.CallText };
                se.Message = e.Message;
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
