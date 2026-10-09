using CryptoScript.ErrorListner;
using CryptoScript.Variables;

namespace CryptoScript.Model
{
    public static class FunctionCallEvaluator
    {
        public static Statement EvaluateFunctionCall(Ast.FunctionCallExpressionNode call,
            List<SemanticError> semanticErrors) =>
            EvaluateFunctionCall(call, semanticErrors, ParameterEvaluator.Evaluate);

        public static Statement EvaluateArgument(Ast.FunctionCallArgumentNode argument, string functionName,
            List<SemanticError> semanticErrors) =>
            EvaluateArgument(argument, functionName, semanticErrors, ParameterEvaluator.Evaluate);

        public static Statement EvaluateFunctionCall(Ast.FunctionCallExpressionNode call,
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
            catch (Exception)
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
                catch (FunctionContractException e)
                {
                    throw CreateFunctionContractError(e, semanticErrors);
                }
                catch (SemanticErrorException e) when (IsFunctionContractError(e))
                {
                    throw;
                }
                catch (SemanticErrorException e) when (
                    IsRegisteredSensitiveFunctionError(e, semanticErrors))
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw CreateFunctionCallError(e, call, fc, semanticErrors);
                }

            }

            try
            {
                Statement[] argValues = arguments.Select(arg => EvaluateArgument(arg, functionName, semanticErrors, evaluateParameter)).ToArray();
                fc.Arguments.AddRange(argValues.OfType<Argument>());
                fc.Call();
                return fc;
            }
            catch (FunctionContractException e)
            {
                throw CreateFunctionContractError(e, semanticErrors);
            }
            catch (SemanticErrorException e) when (IsFunctionContractError(e))
            {
                throw;
            }
            catch (SemanticErrorException e) when (
                IsRegisteredSensitiveFunctionError(e, semanticErrors))
            {
                throw;
            }
            catch(Exception e)
            {
                throw CreateFunctionCallError(e, call, fc, semanticErrors);
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
                    ArgumentExpression argExpr = new ArgumentExpression()
                    {
                        Expr = expr,
                        Kind = ResolvedCallArgumentKind.NestedFunctionCall
                    };
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
                ArgumentExpression argExpr = new ArgumentExpression()
                {
                    Expr = Expression.Create(literal.RawText),
                    Kind = FormatConversions.ParseString(literal.RawText) == FormatConversions.HEX
                        ? ResolvedCallArgumentKind.HexLiteral
                        : ResolvedCallArgumentKind.OtherLiteral
                };
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

        private static SemanticErrorException CreateFunctionContractError(
            FunctionContractException exception,
            List<SemanticError> semanticErrors)
        {
            var error = new SemanticError
            {
                Type = "FunctionContract",
                FunctionName = exception.Function.ToString(),
                ErrorCode = exception.Error,
                Identifier = exception.ParameterName ?? string.Empty,
                Message = exception.Message
            };
            semanticErrors.Add(error);
            return new SemanticErrorException { SemanticError = error };
        }

        private static bool IsFunctionContractError(SemanticErrorException exception) =>
            exception.SemanticError is { Type: "FunctionContract", ErrorCode: not null };

        private static SemanticErrorException CreateFunctionCallError(
            Exception exception,
            Ast.FunctionCallExpressionNode call,
            FunctionCall functionCall,
            List<SemanticError> semanticErrors)
        {
            bool sensitive = IsCryptographicFunction(call.Name);
            var error = new SemanticError
            {
                Type = "FunctionCall",
                FunctionName = call.Name,
                FunctionCall = sensitive ? $"{call.Name}(<redacted>)" : functionCall.CallText ?? string.Empty,
                Message = sensitive
                    ? RedactSensitiveValues(exception.Message, call, functionCall.Invocation)
                    : exception.Message
            };
            semanticErrors.Add(error);
            return new SemanticErrorException { SemanticError = error };
        }

        private static bool IsRegisteredSensitiveFunctionError(
            SemanticErrorException exception,
            List<SemanticError> semanticErrors) =>
            exception.SemanticError is not null &&
            semanticErrors.Contains(exception.SemanticError) &&
            IsCryptographicFunction(exception.SemanticError.FunctionName);

        private static bool IsCryptographicFunction(string functionName) =>
            Enum.TryParse(functionName, ignoreCase: true, out CryptoScriptFunction _);

        private static string RedactSensitiveValues(
            string message,
            Ast.FunctionCallExpressionNode call,
            OperationInvocation? invocation)
        {
            IEnumerable<string> rawValues = call.Arguments.SelectMany(GetPotentiallySensitiveText);
            if (invocation is not null)
            {
                rawValues = rawValues.Concat(invocation.Arguments
                    .Where(argument =>
                        argument.Kind is ResolvedCallArgumentKind.Expression or
                            ResolvedCallArgumentKind.HexLiteral or
                            ResolvedCallArgumentKind.OtherLiteral or
                            ResolvedCallArgumentKind.NestedFunctionCall or
                            ResolvedCallArgumentKind.Variable ||
                        argument.Kind == ResolvedCallArgumentKind.Parameter &&
                        IsSensitiveNamedParameter(argument.Value))
                    .Select(argument => argument.Value)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .Select(value => value!));
            }

            foreach (string value in rawValues.Distinct(StringComparer.Ordinal))
                message = message.Replace(value, "<redacted>", StringComparison.Ordinal);
            return message;
        }

        private static IEnumerable<string> GetPotentiallySensitiveText(
            Ast.FunctionCallArgumentNode argument) => argument switch
        {
            Ast.LiteralArgumentNode literal => new[] { literal.RawText },
            Ast.ParameterArgumentNode parameter when IsSensitiveNamedParameter(parameter.TypeName) =>
                new[] { parameter.RawValue },
            Ast.NestedCallArgumentNode nested => new[] { nested.Call.CallText },
            _ => Array.Empty<string>()
        };

        private static bool IsSensitiveNamedParameter(string? parameter)
        {
            if (string.IsNullOrEmpty(parameter))
                return false;
            string name = parameter.Split(':', 2)[0].TrimStart('#');
            return name.Equals("PAN", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("PSN", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("TRANSACTION", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("FILL", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("RANDOM", StringComparison.OrdinalIgnoreCase);
        }
    }
}
