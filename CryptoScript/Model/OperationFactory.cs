using SimpleLanguage_TestApp3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    

    public class OperationFactory
    {
        
        
        
        public static Func<string[], string> CreateOperation(string operation)
        { 
                operation = operation.ToLower();
                switch (operation)
                {
                    case "sign":
                        return new CryptoOperations().Sign;
                    case "encrypt":
                        return new CryptoOperations().Encrypt;
                    case "print":
                        return new OutputOperations().Print;
                    default:
                        throw new ArgumentException("Invalid notification type");
                }
            
        }

    }
}
