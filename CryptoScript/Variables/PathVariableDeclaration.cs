using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Variables
{
    public class PathVariableDeclaration : VariableDeclaration
    {       
        public PathVariableDeclaration()
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
