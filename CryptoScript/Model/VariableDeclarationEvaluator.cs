using CryptoScript.ErrorListner;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScript.Model
{
    public static class VariableDeclarationEvaluator
    {
        public static Statement Evaluate(
            VariableDeclarationNode declaration,
            List<SemanticError> semanticErrors,
            Func<FunctionCallInitializerNode, Statement> evaluateFunctionCall) =>
            Evaluate(declaration, semanticErrors, evaluateFunctionCall, ParameterEvaluator.Evaluate);

        public static Statement Evaluate(
            VariableDeclarationNode declaration,
            List<SemanticError> semanticErrors,
            Func<FunctionCallInitializerNode, Statement> evaluateFunctionCall,
            Func<string, string, ArgumentParameter> evaluateParameter)
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
                stmt = evaluateFunctionCall(fcontext);
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
                    semanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError=se};
                }
                var mech=declarParam.FirstOrDefault(c => c.RawText.Contains("MECH"));
                if (mech == null)
                {
                    SemanticError se = new SemanticError() { Type = "Declaration", Identifier = TypeName };
                    se.Message = "Type PARAM does not contain element #MECH";
                    semanticErrors.Add(se);
                    throw new SemanticErrorException() { SemanticError = se };
                }
                var Parameter = new ParameterVariableDeclaration();
                Parameter.Mechanism = mech.RawText;
                for (int i = 0; i < declarParam.Count; i++)
                {
                    try
                    {
                        // Preserve the former missing-child failure inside this runtime error boundary.
                        var param = evaluateParameter(
                            declarParam[i].TypeName ?? throw new NullReferenceException(),
                            declarParam[i].RawValue ?? throw new NullReferenceException());
                        Parameter.SetParameter(param);
                    }
                    catch
                    {
                        SemanticError se = new SemanticError() { Type = "Declaration", Identifier = TypeName };
                        se.Message = "Error in  parameter declaration : " + declarParam[i].RawText;
                        semanticErrors.Add(se);
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
                        semanticErrors.Add(se);
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
    }
}
