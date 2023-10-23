using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLanguage_TestApp3.Model
{
    public class Program
    {
        private List<Statement> statements = new List<Statement>();

        public List<Statement> Statements { get => statements; set => statements = value; }
        public void AddStatement(Statement s) { statements.Add(s); }
    }
}
