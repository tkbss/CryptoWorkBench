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
        public static event Action<string>? PrintEvent;
        public VariableDeclaration Print(string[] args) 
        {
            string output= "out: "+ args[0];
            if(PrintEvent != null)
                PrintEvent?.Invoke(output);
            Console.WriteLine(output);
            return new VariableDeclaration();
        }
    }
}
