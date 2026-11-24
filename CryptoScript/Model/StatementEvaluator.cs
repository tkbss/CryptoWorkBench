using CryptoScript.ErrorListner;
using CryptoScript.Model.Ast;

namespace CryptoScript.Model
{
    public static class StatementEvaluator
    {
        public static Statement? Evaluate(StatementNode? node, List<SemanticError> semanticErrors)
        {
            using var admission = InterpreterExecution.EnterStandaloneOperation();
            try { return EvaluateCore(node, semanticErrors, null, null); }
            catch (KeyCheckValueSearchException) { return null; }
        }

        internal static Statement? Evaluate(StatementNode? node, List<SemanticError> semanticErrors,
            SemanticDiagnosticExecution execution, DiagnosticLocation location)
        {
            int start = semanticErrors.Count;
            try
            {
                return EvaluateCore(node, semanticErrors, execution, location);
            }
            catch (SemanticErrorException exception)
            {
                execution.Capture(semanticErrors, start, location);
                if (exception.SemanticError is not null)
                    execution.CaptureError(exception.SemanticError, location);
                if (exception is KeyCheckValueSearchException) return null;
                throw;
            }
            finally
            {
                execution.Capture(semanticErrors, start, location);
            }
        }

        private static Statement? EvaluateCore(StatementNode? node, List<SemanticError> semanticErrors,
            SemanticDiagnosticExecution? execution, DiagnosticLocation? location)
        {
            var store = execution?.Store ?? Variables.VariableDictionary.Instance();
            ArgumentParameter Parameter(string type, string value) => ParameterEvaluator.Evaluate(type, value, store);
            switch (node)
            {
                case AssignmentStatementNode assignment:
                    return AssignmentEvaluator.Evaluate(assignment, semanticErrors,
                        call => FunctionCallEvaluator.EvaluateFunctionCall(call, semanticErrors,
                            Parameter, execution, location), store);
                case VariableDeclarationStatementNode declaration:
                    var result = VariableDeclarationEvaluator.Evaluate(
                        declaration.Declaration, semanticErrors,
                        call => FunctionCallEvaluator.EvaluateFunctionCall(call, semanticErrors,
                            Parameter, execution, location), Parameter, store, declaration.RawText, execution?.CancellationToken ?? default);
                    if (result != null && result is not Variables.VariableDeclaration)
                        result.Text = declaration.RawText;
                    return result;
                case FunctionCallStatementNode call:
                    return FunctionCallEvaluator.EvaluateFunctionCall(call.Call, semanticErrors,
                        Parameter, execution, location);
                case null:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(node));
            }
        }
    }
}
