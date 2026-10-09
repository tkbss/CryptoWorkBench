using CryptoScript.ErrorListner;
using CryptoScript.Model.Ast;

namespace CryptoScript.Model
{
    public static class StatementEvaluator
    {
        public static Statement? Evaluate(StatementNode? node, List<SemanticError> semanticErrors)
            => EvaluateCore(node, semanticErrors, null, null);

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
            switch (node)
            {
                case VariableDeclarationStatementNode declaration:
                    var result = VariableDeclarationEvaluator.Evaluate(
                        declaration.Declaration, semanticErrors,
                        call => FunctionCallEvaluator.EvaluateFunctionCall(call, semanticErrors,
                            ParameterEvaluator.Evaluate, execution, location));
                    if (result != null)
                        result.Text = declaration.RawText;
                    return result;
                case FunctionCallStatementNode call:
                    return FunctionCallEvaluator.EvaluateFunctionCall(call.Call, semanticErrors,
                        ParameterEvaluator.Evaluate, execution, location);
                case null:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(node));
            }
        }
    }
}
