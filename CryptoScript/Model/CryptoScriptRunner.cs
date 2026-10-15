using CryptoScript.ErrorListner;
using CryptoScript.Model.Ast;

namespace CryptoScript.Model
{
    /// <summary>Frontend coordinator: map and execute one statement at a time.</summary>
    public class CryptoScriptRunner
    {
        private SemanticDiagnosticExecution? activeExecution;
        private SemanticDiagnosticExecution? lastExecution;
        private readonly Action<FunctionCall, ISemanticDiagnosticSink>? beforeOperation;
        private readonly Func<string, CryptoAlgorithm.CryptoAlgorithm>? algorithmFactory;

        public CryptoScriptRunner() { }

        // Controlled transport-test seam; never registered as a script function or mechanism.
        internal CryptoScriptRunner(Action<FunctionCall, ISemanticDiagnosticSink> beforeOperation)
            : this(beforeOperation, null)
        {
        }

        // Instance-local algorithm selection for tests of the actual operation dispatch.
        internal CryptoScriptRunner(Action<FunctionCall, ISemanticDiagnosticSink>? beforeOperation,
            Func<string, CryptoAlgorithm.CryptoAlgorithm>? algorithmFactory)
        {
            this.beforeOperation = beforeOperation;
            this.algorithmFactory = algorithmFactory;
        }

        public List<SemanticError> SemanticErrors { get; set; } = new();

        /// <summary>
        /// Snapshot of the active execution, or the last finished outermost execution (including aborts).
        /// Returning from a reentrant Execute restores the outer active execution.
        /// Membership and order are read-only; diagnostic objects intentionally remain mutable.
        /// </summary>
        public IReadOnlyList<SemanticError> Diagnostics =>
            (activeExecution ?? lastExecution)?.GetSnapshot() ?? Array.Empty<SemanticError>();

        public CryptoScriptProgram Execute(CryptoScriptParser.ProgramContext context) =>
            Execute(MapStatements(context));

        public CryptoScriptProgram Execute(IEnumerable<StatementNode?> statements)
        {
            var execution = new SemanticDiagnosticExecution(beforeOperation, algorithmFactory);
            var parentExecution = activeExecution;
            activeExecution = execution;
            try
            {
                var program = new CryptoScriptProgram();
                // Match the former visitor's per-execution error-list binding.
                var semanticErrors = SemanticErrors;
                int statementIndex = 0;
                foreach (var node in statements)
                {
                    var location = new DiagnosticLocation(execution.ExecutionId, ++statementIndex);
                    var result = StatementEvaluator.Evaluate(node, semanticErrors, execution, location);
                    program.AddStatement(result!);
                }
                program.SetDiagnostics(execution.GetSnapshot());
                return program;
            }
            finally
            {
                activeExecution = parentExecution;
                if (parentExecution is null)
                    lastExecution = execution;
            }
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
