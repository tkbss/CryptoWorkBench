using CryptoScript.ErrorListner;
using CryptoScript.Model.Ast;

namespace CryptoScript.Model
{
    public static class StatementEvaluator
    {
        public static Statement? Evaluate(StatementNode? node, List<SemanticError> semanticErrors)
        {
            switch (node)
            {
                case VariableDeclarationStatementNode declaration:
                    var result = VariableDeclarationEvaluator.Evaluate(
                        declaration.Declaration, semanticErrors,
                        call => FunctionCallEvaluator.EvaluateFunctionCall(call, semanticErrors));
                    if (result != null)
                        result.Text = declaration.RawText;
                    return result;
                case FunctionCallStatementNode call:
                    return FunctionCallEvaluator.EvaluateFunctionCall(call.Call, semanticErrors);
                case null:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(node));
            }
        }
    }
}
