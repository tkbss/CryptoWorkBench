using CryptoScript.Model;
using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoScript.ErrorListner;

namespace CryptoScript.Model
{
    public class FunctionCall:Statement
    {
        

        public string Name { get; set; }
        public string? CallText { get; set; }
        public List<Argument> Arguments { get; set; }        
        
        public VariableDeclaration? ReturnVariable { get; set; }
        public OperationInvocation? Invocation { get; private set; }
        public DiagnosticLocation? Location { get; internal set; }

        public FunctionCall()
        {
            
            Name = string.Empty;
            Arguments = new List<Argument>();            
            ReturnVariable = null;
            Invocation = null;
        }
        public void Call() => Call(null);

        internal void Call(SemanticDiagnosticExecution? execution)
        {
            Invocation = new OperationInvocation(Arguments.Select(ResolveArgument));
            if (Location is not null)
                Location = Location with
                {
                    Mechanism = Arguments.OfType<ArgumentMechanism>().FirstOrDefault()?.Mechanism?.Value
                };
            execution?.BeforeOperation(this);
            var function = OperationFactory.CreateInvocationOperation(Name);
            ReturnVariable = function(Invocation);
        }

        private static ResolvedCallArgument ResolveArgument(Argument argument) => argument switch
        {
            ArgumentMechanism mechanism => new ResolvedCallArgument(
                mechanism.Mechanism!.Value, ResolvedCallArgumentKind.Mechanism),
            ArgumentExpression expression => new ResolvedCallArgument(
                expression.Expr!.Value(), expression.Kind),
            ArgumentVariable variable => new ResolvedCallArgument(
                variable.Id!.Value, ResolvedCallArgumentKind.Variable, variable.Id),
            ArgumentParameter parameter => new ResolvedCallArgument(
                parameter.Type + ":" + parameter.Value, ResolvedCallArgumentKind.Parameter),
            ArgumentInfo info => new ResolvedCallArgument(
                info.InfoType, ResolvedCallArgumentKind.Info),
            _ => new ResolvedCallArgument(null, ResolvedCallArgumentKind.Empty)
        };
    }
}
