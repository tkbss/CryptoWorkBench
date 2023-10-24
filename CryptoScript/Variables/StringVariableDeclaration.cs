using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleLanguage_TestApp3.Model;

namespace CryptoScript.Variables
{
    public class StringVariableDeclaration : VariableDeclaration
    {
        
        public string Value { get; set; }
        public StringVariableDeclaration()
        {
            Id = string.Empty;
            Type = null;
            Value = string.Empty;
        }

        public override string PrintOutput()
        {
            return base.PrintOutput();
        }
    }
}
