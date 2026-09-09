using CryptoScript.ErrorListner;

namespace CryptoScript.Model
{
    /// <summary>Frontend coordinator: map and execute one statement at a time.</summary>
    public class CryptoScriptRunner
    {
        public List<SemanticError> SemanticErrors { get; set; } = new();

        public CryptoScriptProgram Execute(CryptoScriptParser.ProgramContext context)
        {
            var program = new CryptoScriptProgram();
            // Match the former visitor's per-execution error-list binding.
            var semanticErrors = SemanticErrors;
            // Preserve the original last-child (EOF) exclusion and recovery null entries.
            for (int i = 0; i < context.ChildCount - 1; i++)
            {
                var node = context.GetChild(i) is CryptoScriptParser.StatementContext statement
                    ? AntlrToStatement.Map(statement)
                    : null;
                var result = StatementEvaluator.Evaluate(node, semanticErrors);
                program.AddStatement(result!);
            }
            return program;
        }
    }
}
