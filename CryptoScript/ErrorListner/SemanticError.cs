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
        
       
        public string Message { get; set; }=string.Empty;
        public string Type { get; set; } = string.Empty;    
        public string Identifier { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public string FunctionCall { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;   
        

    }
}
