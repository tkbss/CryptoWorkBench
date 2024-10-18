using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    

    public class Mechanism : Statement
    {
        public string Value { get; private set; } = string.Empty;
        public Mechanism()
        {

        }
        public void SetMechanismValue(string value)
        {
            if (MechanismList.Instance.Mechanisms.Contains(value))
                Value = value;
            else
                throw new ArgumentException("Unkown mechanism "+value);
        }
    }
}
