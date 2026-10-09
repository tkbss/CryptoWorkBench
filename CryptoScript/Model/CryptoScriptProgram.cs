using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoScript.ErrorListner;

namespace CryptoScript.Model
{
    public class CryptoScriptProgram
    {
        private List<Statement> statements = new List<Statement>();

        public List<Statement> Statements { get => statements; set => statements = value; }
        /// <summary>
        /// Frozen membership and order; contained diagnostic objects retain their identity and remain mutable.
        /// Explicit reuse in a later execution can update the Location visible through this snapshot.
        /// </summary>
        public IReadOnlyList<SemanticError> Diagnostics { get; private set; }
            = Array.Empty<SemanticError>();
        internal void SetDiagnostics(IReadOnlyList<SemanticError> diagnostics)
            => Diagnostics = diagnostics;
        public void AddStatement(Statement s) { statements.Add(s); }
    }
}
