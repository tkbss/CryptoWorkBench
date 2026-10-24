using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using CryptoScript.Operations;
using CryptoScript.Serialization;

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
            => EvaluateFunctionCall(call, semanticErrors, evaluateParameter, null, null);

        internal static Statement EvaluateFunctionCall(Ast.FunctionCallExpressionNode call,
            List<SemanticError> semanticErrors, Func<string, string, ArgumentParameter> evaluateParameter,
            SemanticDiagnosticExecution? execution, DiagnosticLocation? parent)
        {
            FunctionCall fc = new FunctionCall();
            fc.Location = execution?.BeginOperation(parent!, call.Name);
            int start = semanticErrors.Count;
            try
            {
                return EvaluateFunctionCallCore(call, semanticErrors, evaluateParameter, fc, execution);
            }
            catch (SemanticErrorException exception)
            {
                if (execution is not null)
                {
                    execution.Capture(semanticErrors, start, fc.Location!);
                    if (exception.SemanticError is not null)
                        execution.CaptureError(exception.SemanticError, fc.Location!);
                }
                throw;
            }
            finally
            {
                if (execution is not null)
                    execution.Capture(semanticErrors, start, fc.Location!);
            }
        }

        internal static async Task<Statement> EvaluateSecureSaveAsync(Ast.FunctionCallExpressionNode call,
            List<SemanticError> errors, SemanticDiagnosticExecution execution, DiagnosticLocation parent,
            AsyncOperationContext context)
        {
            var fc = new FunctionCall { Name = "SecureSave", CallText = "SecureSave(<redacted>)",
                Location = execution.BeginOperation(parent, "SecureSave") };
            try
            {
                PreparedSecureSave prepared;
                using (InterpreterExecution.EnterState(runner: true))
                {
                    ValidateArgumentContracts(call, errors);
                    prepared = SecureSaveOperations.Prepare(call);
                }
                using (prepared)
                {
                    using (InterpreterExecution.EnterState(runner: true)) execution.BeforeOperation(fc);
                    await OperationFactory.CreateAsyncOperation(call.Name)(prepared, context).ConfigureAwait(false);
                }
                return fc;
            }
            catch (OperationCanceledException) { throw; }
            catch (SecurePasswordCancelledException) { throw; }
            catch (Exception exception)
            {
                if (exception is SemanticErrorException semantic && semantic.SemanticError is { } existing)
                { execution.CaptureError(existing, fc.Location!); throw; }
                var error = new SemanticError { Type = "FunctionCall", FunctionName = "SecureSave",
                    FunctionCall = "SecureSave(<redacted>)",
                    Message = exception is SecureSaveException or VariableSnapshotFileException or PasswordProtocolException
                        ? exception.Message : "SecureSave failed.",
                    DiagnosticCode = exception is SecureSaveException or SnapshotArgumentException or PasswordProtocolException
                        ? SemanticDiagnosticCode.FunctionContractViolation : SemanticDiagnosticCode.FunctionExecutionFailed };
                errors.Add(error); execution.Report(error, fc.Location!);
                if (exception is PasswordProtocolException) throw;
                throw new SemanticErrorException { SemanticError = error };
            }
        }

        internal static async Task<Statement> EvaluateSecureLoadAsync(Ast.FunctionCallExpressionNode call,
            List<SemanticError> errors, SemanticDiagnosticExecution execution, DiagnosticLocation parent,
            AsyncOperationContext context)
        {
            var fc = new FunctionCall { Name = "SecureLoad", CallText = "SecureLoad(<redacted>)",
                Location = execution.BeginOperation(parent, "SecureLoad") };
            try
            {
                PreparedSecureLoad prepared;
                using (InterpreterExecution.EnterState(runner: true))
                {
                    ValidateArgumentContracts(call, errors);
                    prepared = SecureLoadOperations.Prepare(call, context.CancellationToken);
                }
                using (prepared)
                {
                    using (InterpreterExecution.EnterState(runner: true)) execution.BeforeOperation(fc);
                    await OperationFactory.CreateSecureLoadOperation(call.Name)(prepared, context).ConfigureAwait(false);
                }
                return fc;
            }
            catch (OperationCanceledException) { throw; }
            catch (SecurePasswordCancelledException) { throw; }
            catch (Exception exception)
            {
                if (exception is SemanticErrorException semantic && semantic.SemanticError is { } existing)
                { execution.CaptureError(existing, fc.Location!); throw; }
                var error = new SemanticError { Type = "FunctionCall", FunctionName = "SecureLoad",
                    FunctionCall = "SecureLoad(<redacted>)",
                    Message = exception is SecureLoadException or VariableSnapshotFileException or PasswordProtocolException
                        ? exception.Message : "SecureLoad failed.",
                    DiagnosticCode = exception is SecureLoadException or SnapshotArgumentException or PasswordProtocolException
                        ? SemanticDiagnosticCode.FunctionContractViolation : SemanticDiagnosticCode.FunctionExecutionFailed };
                errors.Add(error); execution.Report(error, fc.Location!);
                if (exception is PasswordProtocolException) throw;
                throw new SemanticErrorException { SemanticError = error };
            }
        }

        private static Statement EvaluateFunctionCallCore(Ast.FunctionCallExpressionNode call,
            List<SemanticError> semanticErrors, Func<string, string, ArgumentParameter> evaluateParameter,
            FunctionCall fc, SemanticDiagnosticExecution? execution)
        {
            string functionName = call.Name;
            fc.CallText = DeleteOperations.IsDelete(functionName) ? "Delete(<redacted>)" : SnapshotOperations.IsSnapshot(functionName)
                ? $"{SnapshotOperations.CanonicalName(functionName)}(<redacted>)" : call.CallText;
            fc.Name = functionName;
            try
            {
                OperationFactory.CreateOperation(functionName);
            }
            catch (Exception)
            {
                SemanticError se=new SemanticError() { Type = "FunctionCall",FunctionName=functionName,FunctionCall=$"{functionName}(<redacted>)" };
                se.Message = "Unknown function: "+functionName;
                semanticErrors.Add(se);
                execution?.Report(se, fc.Location!, SemanticDiagnosticCode.UnknownFunction);
                throw new SemanticErrorException() { SemanticError=se};
            }
            FileEncryption.FileEncryptionContract.Validate(call, semanticErrors);
            FileEncryption.FileMacContract.Validate(call, semanticErrors);
            FileEncryption.FileHashContract.Validate(call, semanticErrors);
            ValidateArgumentContracts(call, semanticErrors);
            if (SecureSaveOperations.IsSecureSave(functionName) || SecureLoadOperations.IsSecureLoad(functionName))
            {
                var error = new SemanticError { Type = "FunctionCall", FunctionName = functionName,
                    FunctionCall = functionName + "(<redacted>)", Message = functionName + " requires asynchronous execution.",
                    DiagnosticCode = SemanticDiagnosticCode.AsyncExecutionRequired };
                semanticErrors.Add(error);
                throw new SemanticErrorException { SemanticError = error };
            }
            var arguments = call.Arguments;
            if (SnapshotOperations.IsSnapshot(functionName) || DeleteOperations.IsDelete(functionName))
            {
                try
                {
                    fc.Arguments.AddRange(DeleteOperations.IsDelete(functionName) ? DeleteOperations.ResolveArguments(call) : SnapshotOperations.IsLoad(functionName)
                        ? SnapshotOperations.ResolveLoadArguments(call) : SnapshotOperations.ResolveSaveArguments(call));
                    fc.Call(execution);
                    return fc;
                }
                catch (Exception e)
                {
                    throw CreateFunctionCallError(e, call, fc, semanticErrors);
                }
            }
            if (arguments.Count == 0)
            {
                try
                {
                    fc.Call(execution);
                    return fc;
                }
                catch (OperationCanceledException) { throw; }
                catch (KeyCheckValueSearchException e)
                {
                    if (!semanticErrors.Contains(e.SemanticError!)) semanticErrors.Add(e.SemanticError!);
                    throw;
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
                    IsRegisteredSafeFunctionError(e, semanticErrors))
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
                Statement[] argValues = arguments.Select(arg => EvaluateArgumentCore(arg, functionName,
                    semanticErrors, evaluateParameter, execution, fc.Location)).ToArray();
                fc.Arguments.AddRange(argValues.OfType<Argument>());
                fc.Call(execution);
                return fc;
            }
            catch (OperationCanceledException) { throw; }
            catch (KeyCheckValueSearchException e)
            {
                if (!semanticErrors.Contains(e.SemanticError!)) semanticErrors.Add(e.SemanticError!);
                throw;
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
                IsRegisteredSafeFunctionError(e, semanticErrors))
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
            => EvaluateArgumentCore(argument, functionName, semanticErrors, evaluateParameter, null, null);

        private static Statement EvaluateArgumentCore(Ast.FunctionCallArgumentNode argument, string functionName,
            List<SemanticError> semanticErrors, Func<string, string, ArgumentParameter> evaluateParameter,
            SemanticDiagnosticExecution? execution, DiagnosticLocation? parent)
        {
            if (argument is Ast.HeaderAttributeArgumentNode field)
            {
                if (functionName != "CreateHeader")
                    throw new ArgumentException("Named header attributes are only accepted by CreateHeader.");
                return new ArgumentHeaderAttribute { Name = field.Name, RawValue = field.RawValue };
            }
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
                if ((SecureSaveOperations.IsVoidSnapshot(nestedCall.Call.Name) || DeleteOperations.IsDelete(nestedCall.Call.Name)))
                {
                    string name = DeleteOperations.IsDelete(nestedCall.Call.Name) ? "Delete" : SecureSaveOperations.CanonicalName(nestedCall.Call.Name);
                    var error = new SemanticError
                    {
                        Type = "FunctionCall", FunctionName = name, FunctionCall = $"{name}(<redacted>)",
                        DiagnosticCode = SemanticDiagnosticCode.FunctionContractViolation,
                        Message = $"{name} has no return value and cannot be used as a function argument."
                    };
                    semanticErrors.Add(error);
                    throw new SemanticErrorException { SemanticError = error };
                }
                var fc = EvaluateFunctionCall(nestedCall.Call, semanticErrors, evaluateParameter,
                    execution, parent) as FunctionCall;
                if (fc != null && fc.ReturnVariable != null)
                {
                    if (fc.ReturnVariable is BlockHeaderVariableDeclaration header)
                        return new ArgumentVariable { Id = header };
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
                if (parameter.TypeName == "#ONCONFLICT")
                    throw new ArgumentException("#ONCONFLICT is accepted only by Load and SecureLoad.");
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
                Location = exception.Function == CryptoScriptFunction.PinBlockTranslate
                    ? new DiagnosticLocation(Mechanism: exception.Mechanism) : null,
                Identifier = exception.ParameterName ?? string.Empty,
                Message = exception.Message
            };
            semanticErrors.Add(error);
            return new SemanticErrorException { SemanticError = error };
        }

        // Inspect the complete argument tree before evaluating any sibling. A void
        // snapshot call or a misplaced Load-only parameter must not trigger earlier
        // nested operations (including Print, Save or file access).
        private static void ValidateArgumentContracts(Ast.FunctionCallExpressionNode call,
            List<SemanticError> semanticErrors)
        {
            foreach (var argument in call.Arguments)
            {
                string? name = null;
                string? message = null;
                if (argument is Ast.NestedCallArgumentNode nested)
                {
                    if ((SecureSaveOperations.IsVoidSnapshot(nested.Call.Name) || DeleteOperations.IsDelete(nested.Call.Name)))
                    {
                        name = DeleteOperations.IsDelete(nested.Call.Name) ? "Delete" : SecureSaveOperations.CanonicalName(nested.Call.Name);
                        message = $"{name} has no return value and cannot be used as a function argument.";
                    }
                    else ValidateArgumentContracts(nested.Call, semanticErrors);
                }
                if (argument is Ast.ParameterArgumentNode { TypeName: "#ONCONFLICT" } &&
                    !SnapshotOperations.IsLoad(call.Name) && !SecureLoadOperations.IsSecureLoad(call.Name))
                {
                    name = call.Name;
                    message = "#ONCONFLICT is accepted only by Load and SecureLoad.";
                }
                if (message is null) continue;
                var error = new SemanticError
                {
                    Type = "FunctionCall", FunctionName = name!, FunctionCall = $"{name}(<redacted>)",
                    DiagnosticCode = SemanticDiagnosticCode.FunctionContractViolation, Message = message
                };
                semanticErrors.Add(error);
                throw new SemanticErrorException { SemanticError = error };
            }
        }

        private static bool IsFunctionContractError(SemanticErrorException exception) =>
            exception.SemanticError is { Type: "FunctionContract", ErrorCode: not null };

        private static SemanticErrorException CreateFunctionCallError(
            Exception exception,
            Ast.FunctionCallExpressionNode call,
            FunctionCall functionCall,
            List<SemanticError> semanticErrors)
        {
            if (call.Name.Equals("MacPack", StringComparison.OrdinalIgnoreCase) ||
                call.Name.Equals("MacVerify", StringComparison.OrdinalIgnoreCase))
            {
                var cwmError = new SemanticError { Type = "FunctionCall", FunctionName = call.Name,
                    FunctionCall = call.Name + "(<redacted>)", Message = call.Name + " failed.",
                    DiagnosticCode = SemanticDiagnosticCode.FunctionExecutionFailed };
                semanticErrors.Add(cwmError);
                return new SemanticErrorException { SemanticError = cwmError };
            }
            if (SnapshotOperations.IsSnapshot(call.Name) || DeleteOperations.IsDelete(call.Name))
            {
                string name = DeleteOperations.IsDelete(call.Name) ? "Delete" : SnapshotOperations.CanonicalName(call.Name);
                var saveError = new SemanticError
                {
                    Type = "FunctionCall", FunctionName = name, FunctionCall = $"{name}(<redacted>)",
                    DiagnosticCode = exception is SnapshotArgumentException argumentError
                        ? argumentError.Code : SemanticDiagnosticCode.FunctionExecutionFailed,
                    Message = exception is SnapshotArgumentException or VariableSnapshotFileException
                        ? exception.Message : $"{name} failed."
                };
                semanticErrors.Add(saveError);
                return new SemanticErrorException { SemanticError = saveError };
            }
            bool sensitive = IsCryptographicFunction(call.Name);
            var error = new SemanticError
            {
                Type = "FunctionCall",
                DiagnosticCode = exception is CryptoAlgorithm.PINBLOCK.PinBlockTranslationException translation
                    ? translation.Code : SemanticDiagnosticCode.Unclassified,
                FunctionName = call.Name,
                FunctionCall = sensitive ? $"{call.Name}(<redacted>)" : functionCall.CallText ?? string.Empty,
                Message = sensitive
                    ? RedactSensitiveValues(exception.Message, call, functionCall.Invocation)
                    : exception.Message
            };
            semanticErrors.Add(error);
            return new SemanticErrorException { SemanticError = error };
        }

        private static bool IsRegisteredSafeFunctionError(
            SemanticErrorException exception,
            List<SemanticError> semanticErrors) =>
            exception.SemanticError is not null &&
            semanticErrors.Contains(exception.SemanticError) &&
            (SnapshotOperations.IsSnapshot(exception.SemanticError.FunctionName) ||
             IsCryptographicFunction(exception.SemanticError.FunctionName) ||
             // Unknown functions are rejected before arguments are evaluated. Keep
             // their already-redacted diagnostic intact through enclosing calls.
             exception.SemanticError.Type == "FunctionCall" &&
             exception.SemanticError.Message == "Unknown function: " + exception.SemanticError.FunctionName);

        private static bool IsCryptographicFunction(string functionName) =>
            functionName.Equals("Check", StringComparison.OrdinalIgnoreCase) ||
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
