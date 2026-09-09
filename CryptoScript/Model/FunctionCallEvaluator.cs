using CryptoScript.ErrorListner;
using CryptoScript.Variables;

namespace CryptoScript.Model
{
    public static class FunctionCallEvaluator
    {
        public static Statement EvaluateFunctionCall(Ast.FunctionCallInitializerNode call,
            List<SemanticError> semanticErrors) =>
            EvaluateFunctionCall(call, semanticErrors, ParameterEvaluator.Evaluate);

        public static Statement EvaluateArgument(Ast.FunctionCallArgumentNode argument, string functionName,
            List<SemanticError> semanticErrors) =>
            EvaluateArgument(argument, functionName, semanticErrors, ParameterEvaluator.Evaluate);

        public static Statement EvaluateFunctionCall(Ast.FunctionCallInitializerNode call,
            List<SemanticError> semanticErrors, Func<string, string, ArgumentParameter> evaluateParameter)
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
                semanticErrors.Add(se);
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
                    semanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError=se};
                }

            }

            try
            {
                Statement[] argValues = arguments.Select(arg => EvaluateArgument(arg, functionName, semanticErrors, evaluateParameter)).ToArray();
                fc.Arguments.AddRange(argValues.OfType<Argument>());
                fc.Call();
                return fc;
            }
            catch(Exception e)
            {
                SemanticError se=new SemanticError() { Type = "FunctionCall",FunctionName=functionName,FunctionCall=fc.CallText };
                se.Message = e.Message;
                semanticErrors.Add(se);
                throw new SemanticErrorException() { SemanticError=se};
            }


        }

        public static Statement EvaluateArgument(Ast.FunctionCallArgumentNode argument, string functionName,
            List<SemanticError> semanticErrors, Func<string, string, ArgumentParameter> evaluateParameter)
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
                    semanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError=se };
                }
                ArgumentMechanism argMech = new ArgumentMechanism() { Mechanism = m };
                return argMech;
            }
            if (argument is Ast.NestedCallArgumentNode nestedCall)
            {
                var fc = EvaluateFunctionCall(nestedCall.Call, semanticErrors, evaluateParameter) as FunctionCall;
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
                    semanticErrors.Add(se);
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

                return evaluateParameter(parameter.TypeName, parameter.RawValue);
            }
            if (argument is Ast.InfoArgumentNode infoNode)
            {
                ArgumentInfo info = new ArgumentInfo() { InfoType= infoNode.RawText };
                return info;
            }
            return new Argument();
        }
    }
}
