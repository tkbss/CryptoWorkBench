using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class OutputOperations
    {
        public VariableDeclaration Print(string[] args) 
        {
            Console.WriteLine(args[0]);
            return new VariableDeclaration();
        }
    }
}
