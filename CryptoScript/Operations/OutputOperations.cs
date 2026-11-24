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
        public sealed record Observer(Action<PrintResult> Print, Action<string> Info)
        {
            // A desktop job reserves its multicast position before the runner-start fence.
            // The runner holds the same subscription through execution, including direct Core calls.
            private static readonly Dictionary<Observer, int> subscriptions = new(ReferenceEqualityComparer.Instance);

            public IDisposable Subscribe()
            {
                lock (InterpreterExecution.Gate)
                {
                    if (!subscriptions.TryGetValue(this, out int count))
                    {
                        PrintResultEvent += OnPrint;
                        InfoEvent += OnInfo;
                    }
                    subscriptions[this] = count + 1;
                    return new Subscription(this);
                }
            }

            private void OnPrint(PrintResult result)
            {
                if (ReferenceEquals(InterpreterExecution.CaptureFrame()?.Output, this)) Print(result);
            }

            private void OnInfo(string text)
            {
                if (ReferenceEquals(InterpreterExecution.CaptureFrame()?.Output, this)) Info(text);
            }

            private sealed class Subscription(Observer observer) : IDisposable
            {
                private bool disposed;
                public void Dispose()
                {
                    lock (InterpreterExecution.Gate)
                    {
                        if (disposed) return;
                        disposed = true;
                        if (--subscriptions[observer] != 0) return;
                        subscriptions.Remove(observer);
                        PrintResultEvent -= observer.OnPrint;
                        InfoEvent -= observer.OnInfo;
                    }
                }
            }
        }
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
            invocation.Verify();
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
        public VariableDeclaration Info(OperationInvocation invocation)
        {
            invocation.Verify();
            return Info(invocation.Values);
        }

        public VariableDeclaration Print(string[] args) => Print(args, null);

        private VariableDeclaration Print(string[] args, Ast.SourcePosition? source)
        {
            using var publication = InterpreterExecution.EnterOutput();
            string output = "out: " + args[0];
            if (PrintEvent != null || PrintResultEvent != null)
            {
                using var callback = InterpreterExecution.HostOutputCallback();
                PrintEvent?.Invoke(output);
                var result = new PrintResult(output, source);
                PrintResultEvent?.Invoke(result);
            }
            Console.WriteLine(output);
            return new VariableDeclaration();
        }

        public VariableDeclaration Info(string[] args)
        {
            using var publication = InterpreterExecution.EnterOutput();
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
