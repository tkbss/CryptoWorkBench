using CryptoScript.Documentation;
using CryptoScript.Variables;

namespace CryptoScript.Model
{
    public class OutputOperations
    {
        private readonly IInfoDocumentationProvider _infoDocumentationProvider;

        public static event Action<string>? PrintEvent;
        public static event Action<PrintResult>? PrintResultEvent;
        public sealed record PrintResult(string Text, Ast.SourcePosition? Source);
        public static event Action<string>? InfoEvent;

        public OutputOperations()
            : this(new FileInfoDocumentationProvider())
        {
        }

        public OutputOperations(IInfoDocumentationProvider infoDocumentationProvider)
        {
            _infoDocumentationProvider = infoDocumentationProvider ??
                throw new ArgumentNullException(nameof(infoDocumentationProvider));
        }

        public VariableDeclaration Print(OperationInvocation invocation)
        {
            if (invocation.Arguments.FirstOrDefault()?.SourceVariable is BlockHeaderVariableDeclaration header)
                return Print(new[] { header.PrintOutput() }, invocation.Source);
            if (invocation.Arguments.FirstOrDefault()?.SourceVariable is SecretVariableDeclaration secret)
                return Print(new[] { secret.PrintOutput().ReplaceLineEndings(Environment.NewLine + "out: ") }, invocation.Source);
            if (invocation.Arguments.FirstOrDefault()?.SourceVariable is KeyVariableDeclaration key)
                // ScriptBooks skip output one physical line at a time. The legacy
                // Print path supplies the first prefix and publishes the event once.
                return Print(new[] { key.PrintOutput().ReplaceLineEndings(Environment.NewLine + "out: ") }, invocation.Source);
            return Print(invocation.Values, invocation.Source);
        }
        public VariableDeclaration Info(OperationInvocation invocation) => Info(invocation.Values);

        public VariableDeclaration Print(string[] args) => Print(args, null);

        private VariableDeclaration Print(string[] args, Ast.SourcePosition? source)
        {
            string output = "out: " + args[0];
            if (PrintEvent != null || PrintResultEvent != null)
            {
                using var callback = InterpreterExecution.HostOutputCallback();
                PrintEvent?.Invoke(output);
                PrintResultEvent?.Invoke(new(output, source));
            }
            Console.WriteLine(output);
            return new VariableDeclaration();
        }

        public VariableDeclaration Info(string[] args)
        {
            string output = string.Empty;
            if (!_infoDocumentationProvider.TryGetDocumentation(args[0], out output))
                Console.WriteLine("No info available");

            if (InfoEvent != null)
            {
                using var callback = InterpreterExecution.HostOutputCallback();
                InfoEvent?.Invoke(output);
            }
            Console.WriteLine(output);
            return new VariableDeclaration();
        }
    }
}
