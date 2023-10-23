using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLanguage_TestApp3.Model
{
    public class Variable : Statement
    {
        public string Id { get; set; }

        public Variable()
        {
            Id = string.Empty;
        }

        public override string? ToString()
        {
            return Id.ToString();
        }
    }
}
