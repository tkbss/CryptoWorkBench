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
            using var admission = InterpreterExecution.EnterStandaloneOperation();
            return Evaluate(declaration, semanticErrors, evaluateFunctionCall, evaluateParameter, VariableDictionary.Instance());
        }

        internal static Statement Evaluate(VariableDeclarationNode declaration, List<SemanticError> semanticErrors,
            Func<FunctionCallExpressionNode, Statement> evaluateFunctionCall,
            Func<string, string, ArgumentParameter> evaluateParameter, VariableDictionary store, string? text = null, CancellationToken token = default)
        {
            string Id = declaration.Identifier;
            using var reservation = store.Reserve(Id);
            if (reservation is null)
            {
                var error = Duplicate(Id);
                semanticErrors.Add(error.SemanticError!);
                throw error;
            }
            string TypeName = declaration.TypeName;
            if (declaration.Initializer is FunctionCallExpressionNode fileCall)
            {
                FileEncryption.FileEncryptionContract.Validate(fileCall, semanticErrors, TypeName, store: store);
                FileEncryption.FileMacContract.Validate(fileCall, semanticErrors, TypeName, store: store);
                FileEncryption.FileHashContract.Validate(fileCall, semanticErrors, TypeName, store: store);
            }
            if (declaration.Initializer is FunctionCallExpressionNode snapshot &&
                (Operations.SecureSaveOperations.IsVoidSnapshot(snapshot.Name) || Operations.DeleteOperations.IsDelete(snapshot.Name)))
            {
                string name = Operations.DeleteOperations.IsDelete(snapshot.Name) ? "Delete" : Operations.SecureSaveOperations.CanonicalName(snapshot.Name);
                var error = new SemanticError
                {
                    Type = "Declaration", FunctionName = name, FunctionCall = $"{name}(<redacted>)",
                    DiagnosticCode = SemanticDiagnosticCode.InvalidDeclaration,
                    Message = $"{name} has no return value and cannot initialize a variable."
                };
                semanticErrors.Add(error);
                throw new SemanticErrorException { SemanticError = error };
            }
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
            if (type is CryptoTypeVal && declaration.Initializer is not FunctionCallExpressionNode)
            {
                var error = new SemanticError { Type = "Declaration", Identifier = TypeName,
                    DiagnosticCode = SemanticDiagnosticCode.InvalidDeclaration,
                    Message = "VAL requires a Boolean function result (true or false)." };
                semanticErrors.Add(error);
                throw new SemanticErrorException { SemanticError = error };
            }
            if (type is CryptoTypeKey && declaration.Initializer is null &&
                declaration.Tr31Header is null && declaration.Parameters.Items.Count == 0)
                throw InvalidKeyLiteral(semanticErrors);
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
                if (type is CryptoTypePath)
                {
                    stmt = PathInitializerEvaluator.Prepare(expression.RawText, semanticErrors);
                }
                else stmt = Expression.Create(expression.RawText);
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
                var Parameter = ParameterInitializerEvaluator.Prepare(Id, TypeName,
                    declarParam, semanticErrors, evaluateParameter);
                stmt = Parameter;
            }
            if (stmt is FunctionCall functionCall)
            {
                var variable = functionCall.ReturnVariable;
                if (variable != null)
                {
                    if (variable.Owner != null) store.ValidateArgument(variable);
                    if (variable.Type.GetType() != type.GetType())
                    {
                        SemanticError se = new SemanticError() { Type = "Declaration", Identifier = TypeName };
                        se.Message = "Declaration type mismatch. Expected type : " + variable.Type.Name;
                        semanticErrors.Add(se);
                        throw new SemanticErrorException() { SemanticError = se };
                    }
                    store.VerifyResult(variable);
                    variable.Id = Id;
                }
                stmt= variable;
            }

            try { stmt = Expression.PrepareVariable(stmt, Id, type); }
            catch (Exception exception) when (type is CryptoTypeKey &&
                declaration.Initializer is LiteralInitializerNode &&
                exception is ArgumentException or FormatException or OverflowException)
            {
                throw InvalidKeyLiteral(semanticErrors);
            }
            token.ThrowIfCancellationRequested();
            if (stmt is VariableDeclaration prepared)
            {
                if (text is not null) prepared.Text = text;
                reservation.Publish(prepared);
            }
            return stmt;

        }

        internal static SemanticErrorException Duplicate(string id) => new()
        {
            SemanticError = new SemanticError { Type = "Declaration", Identifier = id,
                DiagnosticCode = SemanticDiagnosticCode.InvalidDeclaration,
                Message = $"Variable '{id}' is already declared." }
        };

        private static SemanticErrorException InvalidKeyLiteral(List<SemanticError> semanticErrors)
        {
            var error = new SemanticError
            {
                Type = "Declaration", DiagnosticCode = SemanticDiagnosticCode.InvalidDeclaration,
                Message = "KEY requires a valid nonempty HEX or Base64 literal containing whole bytes."
            };
            semanticErrors.Add(error);
            return new SemanticErrorException { SemanticError = error };
        }
    }
}
