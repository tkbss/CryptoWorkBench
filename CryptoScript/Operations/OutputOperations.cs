using CryptoScript.Documentation;
using CryptoScript.Variables;

namespace CryptoScript.Model
{
    public class OutputOperations
    {
        private readonly IInfoDocumentationProvider _infoDocumentationProvider;

        public static event Action<string>? PrintEvent;
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
            if (invocation.Arguments.FirstOrDefault()?.SourceVariable is SecretVariableDeclaration secret)
                return Print(new[] { secret.PrintOutput().ReplaceLineEndings(Environment.NewLine + "out: ") });
            if (invocation.Arguments.FirstOrDefault()?.SourceVariable is KeyVariableDeclaration key)
                // ScriptBooks skip output one physical line at a time. The legacy
                // Print path supplies the first prefix and publishes the event once.
                return Print(new[] { key.PrintOutput().ReplaceLineEndings(Environment.NewLine + "out: ") });
            return Print(invocation.Values);
        }
        public VariableDeclaration Info(OperationInvocation invocation) => Info(invocation.Values);

        public VariableDeclaration Print(string[] args)
        {
            string output = "out: " + args[0];
            if (PrintEvent != null)
                PrintEvent?.Invoke(output);
            Console.WriteLine(output);
            return new VariableDeclaration();
        }

        public VariableDeclaration Info(string[] args)
        {
            string output = string.Empty;
            if (!_infoDocumentationProvider.TryGetDocumentation(args[0], out output))
                Console.WriteLine("No info available");

            if (InfoEvent != null)
                InfoEvent?.Invoke(output);
            Console.WriteLine(output);
            return new VariableDeclaration();
        }
    }
}
