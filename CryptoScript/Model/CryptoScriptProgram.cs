using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class CryptoScriptProgram
    {
        private List<Statement> statements = new List<Statement>();

        public List<Statement> Statements { get => statements; set => statements = value; }
        public void AddStatement(Statement s) { statements.Add(s); }
    }
}
