using CryptoScript.ErrorListner;
using CryptoScript.Model.Ast;

namespace CryptoScript.Model
{
    /// <summary>Frontend coordinator: map and execute one statement at a time.</summary>
    public class CryptoScriptRunner
    {
        public List<SemanticError> SemanticErrors { get; set; } = new();

        public CryptoScriptProgram Execute(CryptoScriptParser.ProgramContext context) =>
            Execute(MapStatements(context));

        public CryptoScriptProgram Execute(IEnumerable<StatementNode?> statements)
        {
            var program = new CryptoScriptProgram();
            // Match the former visitor's per-execution error-list binding.
            var semanticErrors = SemanticErrors;
            foreach (var node in statements)
            {
                var result = StatementEvaluator.Evaluate(node, semanticErrors);
                program.AddStatement(result!);
            }
            return program;
        }

        private static IEnumerable<StatementNode?> MapStatements(CryptoScriptParser.ProgramContext context)
        {
            // Preserve the original last-child (EOF) exclusion and recovery null entries.
            // Yield before mapping the next child so earlier side effects precede later mapping errors.
            for (int i = 0; i < context.ChildCount - 1; i++)
            {
                yield return context.GetChild(i) is CryptoScriptParser.StatementContext statement
                    ? AntlrToStatement.Map(statement)
                    : null;
            }
        }
    }
}
