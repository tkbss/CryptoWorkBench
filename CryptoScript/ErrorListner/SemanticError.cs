using CryptoScript.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.ErrorListner
{
    public class SemanticError
    {   
        public SemanticDiagnosticSeverity Severity { get; set; } = SemanticDiagnosticSeverity.Error;
        public SemanticDiagnosticCode DiagnosticCode { get; set; } = SemanticDiagnosticCode.Unclassified;
        public DiagnosticLocation? Location { get; set; }
        // Reference provenance only: replacing Location explicitly supplies fresh metadata.
        // Instance-owned and non-public; never serialized as part of the public diagnostic.
        internal DiagnosticLocation? ExecutionAssignedLocation { get; set; }
        
       
        public string Message { get; set; }=string.Empty;
        public string Type { get; set; } = string.Empty;    
        public string Identifier { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public string FunctionCall { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;   
        public FunctionContractError? ErrorCode { get; set; }
        

    }
}
