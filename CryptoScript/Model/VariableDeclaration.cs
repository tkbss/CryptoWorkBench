using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLanguage_TestApp3.Model
{
    public class VariableDeclaration: Statement
    {
        public string Id { get; set; }
        public Type? Type { get; set; }
        public string Value { get; set; }
        public VariableDeclaration() 
        {
            Id = string.Empty;
            Type = null;
            Value = string.Empty;
        }
    }
}
