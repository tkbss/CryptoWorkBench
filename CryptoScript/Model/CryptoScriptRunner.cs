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
        private int passwordRequestActive;
        public Guid RunnerId { get; } = Guid.NewGuid();
        /// <summary>Identity of the execution actually entered, including a finished or aborted run.
        /// Remains unchanged when cancellation prevents entry into the runner.</summary>
        public Guid? ExecutionId => (activeExecution ?? lastExecution)?.ExecutionId;
        public event PasswordRequestHandler? PasswordRequested;

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
            using var lease = InterpreterExecution.Enter();
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

        public Task<CryptoScriptProgram> ExecuteAsync(CryptoScriptParser.ProgramContext context,
            CancellationToken cancellationToken = default) => ExecuteAsync(MapStatements(context), cancellationToken);

        public Task<CryptoScriptProgram> ExecuteAsync(IEnumerable<StatementNode?> statements,
            CancellationToken cancellationToken = default) =>
            InterpreterExecution.RunRunnerAsync(() => ExecuteAsyncCore(statements, cancellationToken), cancellationToken);

        private async Task<CryptoScriptProgram> ExecuteAsyncCore(IEnumerable<StatementNode?> statements,
            CancellationToken token)
        {
            var execution = new SemanticDiagnosticExecution(beforeOperation, algorithmFactory);
            var parentExecution = activeExecution;
            activeExecution = execution;
            try
            {
                var program = new CryptoScriptProgram();
                var errors = SemanticErrors;
                int index = 0;
                IEnumerator<StatementNode?> enumerator;
                using (InterpreterExecution.EnterState(runner: true)) enumerator = statements.GetEnumerator();
                try
                {
                    // Check cancellation before evaluation, never after a final completed operation.
                    while (true)
                    {
                        bool next;
                        StatementNode? node;
                        using (InterpreterExecution.EnterState(runner: true))
                        {
                            next = enumerator.MoveNext();
                            node = next ? enumerator.Current : null;
                        }
                        if (!next) break;
                        token.ThrowIfCancellationRequested();
                        var location = new DiagnosticLocation(execution.ExecutionId, ++index);
                        if (node is FunctionCallStatementNode secure && Operations.SecureSaveOperations.IsSecureSave(secure.Call.Name))
                        {
                            var context = new AsyncOperationContext(
                                (purpose, ct) => RequestPasswordAsync(execution.ExecutionId, purpose, ct),
                                token, execution.CreateSink(location));
                            program.AddStatement(await FunctionCallEvaluator.EvaluateSecureSaveAsync(
                                secure.Call, errors, execution, location, context).ConfigureAwait(false));
                        }
                        else if (node is FunctionCallStatementNode load && Operations.SecureLoadOperations.IsSecureLoad(load.Call.Name))
                        {
                            var context = new AsyncOperationContext(
                                (purpose, ct) => RequestPasswordAsync(execution.ExecutionId, purpose, ct),
                                token, execution.CreateSink(location));
                            program.AddStatement(await FunctionCallEvaluator.EvaluateSecureLoadAsync(
                                load.Call, errors, execution, location, context).ConfigureAwait(false));
                        }
                        else if (node is AsyncInfrastructureStatement asynchronous)
                        {
                            var context = new AsyncOperationContext(
                                (purpose, ct) => RequestPasswordAsync(execution.ExecutionId, purpose, ct),
                                token, execution.CreateSink(location));
                            try { await asynchronous.Operation(context).ConfigureAwait(false); }
                            catch (PasswordProtocolException error)
                            {
                                var diagnostic = new SemanticError
                                {
                                    Type = "PasswordRequest", Message = error.Message,
                                    DiagnosticCode = SemanticDiagnosticCode.FunctionContractViolation
                                };
                                errors.Add(diagnostic);
                                execution.Report(diagnostic, location);
                                throw;
                            }
                            program.AddStatement(null!);
                        }
                        else
                        {
                            using var state = InterpreterExecution.EnterState(runner: true);
                            program.AddStatement(StatementEvaluator.Evaluate(node, errors, execution, location)!);
                        }
                    }
                    program.SetDiagnostics(execution.GetSnapshot());
                    return program;
                }
                finally
                {
                    using var state = InterpreterExecution.EnterState(runner: true);
                    enumerator.Dispose();
                }
            }
            finally
            {
                activeExecution = parentExecution;
                if (parentExecution is null) lastExecution = execution;
            }
        }

        private async Task<PasswordResponse> RequestPasswordAsync(Guid executionId, PasswordPurpose purpose, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(purpose)) throw new PasswordProtocolException(PasswordProtocolError.PasswordHostContractViolation);
            if (Interlocked.CompareExchange(ref passwordRequestActive, 1, 0) != 0)
                throw new PasswordProtocolException(PasswordProtocolError.PasswordRequestBusy);
            Task<PasswordResponse>? pending = null;
            bool late = false;
            PasswordResponse? response = null;
            try
            {
                var subscribers = PasswordRequested?.GetInvocationList();
                if (subscribers is null || subscribers.Length == 0)
                    throw new PasswordProtocolException(PasswordProtocolError.PasswordHostUnavailable);
                if (subscribers.Length != 1)
                    throw new PasswordProtocolException(PasswordProtocolError.PasswordHostAmbiguous);
                var request = new PasswordRequestEventArgs(RunnerId, executionId, purpose, token);
                try
                {
                    using (InterpreterExecution.HostCallback())
                        pending = ((PasswordRequestHandler)subscribers[0])(this, request);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                { throw new OperationCanceledException(token); }
                catch { throw new PasswordProtocolException(PasswordProtocolError.PasswordHostFailure); }
                if (pending is null) throw new PasswordProtocolException(PasswordProtocolError.PasswordHostContractViolation);
                try { response = await pending.WaitAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    late = true;
                    _ = ObserveLateResponseAsync(pending);
                    throw new OperationCanceledException(token);
                }
                catch { throw new PasswordProtocolException(PasswordProtocolError.PasswordHostFailure); }
                token.ThrowIfCancellationRequested();
                if (response is null) throw new PasswordProtocolException(PasswordProtocolError.PasswordHostContractViolation);
                PasswordValidation.Validate(response, request);
                var supplied = response;
                response = null; // ownership passes to the operation; it must dispose in finally/using
                return supplied;
            }
            finally
            {
                response?.Dispose();
                if (!late) Interlocked.Exchange(ref passwordRequestActive, 0);
            }
        }

        private async Task ObserveLateResponseAsync(Task<PasswordResponse> pending)
        {
            try { (await pending.ConfigureAwait(false))?.Dispose(); }
            catch { /* Observe host faults without retaining/logging their text or exception. */ }
            finally { Interlocked.Exchange(ref passwordRequestActive, 0); }
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
