using CryptoScript.Model;
using SimpleLanguage_TestApp3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Variables
{
    public class VariableDeclaration : Statement
    {
        public string Id { get; set; }
        public CryptoType? Type { get; set; } = null;
        public virtual string Value { get; set; }=string.Empty;
        public virtual string PrintOutput() {  return string.Empty; }
    }
}

