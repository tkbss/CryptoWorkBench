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
            Func<FunctionCallExpressionNode, Statement> evaluateFunctionCall) =>
            Evaluate(declaration, semanticErrors, evaluateFunctionCall, ParameterEvaluator.Evaluate);

        public static Statement Evaluate(
            VariableDeclarationNode declaration,
            List<SemanticError> semanticErrors,
            Func<FunctionCallExpressionNode, Statement> evaluateFunctionCall,
            Func<string, string, ArgumentParameter> evaluateParameter)
        {
            string Id = declaration.Identifier;
            string TypeName = declaration.TypeName;
            // Check the fixed translation result contract before evaluating the operation.
            if (declaration.Initializer is FunctionCallExpressionNode translation &&
                translation.Name.Equals("PinBlockTranslate", StringComparison.OrdinalIgnoreCase) &&
                !TypeName.Equals("VAR", StringComparison.OrdinalIgnoreCase))
            {
                var error = new SemanticError { Type = "Declaration", Identifier = TypeName,
                    DiagnosticCode = SemanticDiagnosticCode.InvalidDeclaration,
                    Message = $"Declaration type mismatch. Expected type: VAR; actual type: {TypeName}." };
                semanticErrors.Add(error);
                throw new SemanticErrorException { SemanticError = error };
            }
            var type = CryptoType.Parse(TypeName);
            FunctionCallExpressionNode? stmtFromHeader = null;
            if (declaration.Tr31Header is not null)
            {
                if (type is not CryptoTypeTR31Header)
                    throw new ArgumentException("Header initializers require TR31H.");
                // The formerly incomplete brace syntax uses exactly the canonical builder semantics.
                stmtFromHeader = new FunctionCallExpressionNode("CreateHeader", declaration.Tr31Header.RawText,
                    declaration.Tr31Header.Fields ?? throw new ArgumentException("Header initializer requires structured fields."));
            }
            if (type is CryptoTypeTR31Header && declaration.Initializer is not FunctionCallExpressionNode &&
                declaration.Tr31Header is null)
                throw new ArgumentException("TR31H requires CreateHeader or a validated header initializer.");
            if (type is CryptoTypeSecret && declaration.Initializer is not FunctionCallExpressionNode)
            {
                var error = new SemanticError { Type = "Declaration", Identifier = TypeName,
                    Message = "SECRET requires a Share or Secret function result." };
                semanticErrors.Add(error);
                throw new SemanticErrorException { SemanticError = error };
            }
            var declarParam = declaration.Parameters.Items;

            Statement? stmt = null;
            if (stmtFromHeader is not null) stmt = evaluateFunctionCall(stmtFromHeader);
            if (declaration.Initializer is FunctionCallExpressionNode call)
            {
                stmt = evaluateFunctionCall(call);
            }
            if (declaration.Initializer is LiteralInitializerNode expression)
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
                        Parameter.RecordExplicitParameter(param.Type);
                    }
                    catch
                    {
                        SemanticError se = new SemanticError() { Type = "Declaration", Identifier = TypeName };
                        se.Message = declarParam[i].TypeName == "#PSN"
                            ? "Error in parameter declaration: #PSN:<redacted>"
                            : "Error in  parameter declaration : " + declarParam[i].RawText;
                        semanticErrors.Add(se);
                        throw new SemanticErrorException() { SemanticError = se };
                    }

                }
                try
                {
                    if (SecretSharing.SecretParameters.IsSecretMechanism(Parameter.Mechanism))
                        _ = SecretSharing.SecretParameters.Read(Parameter);
                    MechanismParameterContractValidator.ValidatePsnSupport(Parameter.Mechanism, Parameter);
                    MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(Parameter.Mechanism, Parameter);
                    if (Parameter.Mechanism is "EP2-DATA-ENCRYPTION" or "EP2-PAN-RECEIPT-ENCRYPTION" or "EP2-TR31-KEY-BLOCK")
                        MechanismParameterContractValidator.Validate(Parameter.Mechanism, CryptoScriptFunction.Parameters, Parameter);
                }
                catch (Exception exception) when (exception is FunctionContractException ||
                    SecretSharing.SecretParameters.IsSecretMechanism(Parameter.Mechanism) && exception is ArgumentException ||
                    (Parameter.Mechanism is "EP2-DATA-ENCRYPTION" or "EP2-PAN-RECEIPT-ENCRYPTION" or "EP2-TR31-KEY-BLOCK") && exception is ArgumentException)
                {
                    var error = new SemanticError { Type = "Declaration", Identifier = TypeName, Message = exception.Message };
                    semanticErrors.Add(error);
                    throw new SemanticErrorException { SemanticError = error };
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
