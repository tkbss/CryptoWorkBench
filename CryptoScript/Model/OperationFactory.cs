using CryptoScript.Variables;

namespace CryptoScript.Model
{


    public class OperationFactory
    {

        
        

        public static Func<string[], VariableDeclaration> CreateOperation(string operation)
        { 
            operation = operation.ToLower();
            var op = new CryptoOperations();             
            switch (operation)
            {
                case "sign":                                  
                    return op.Sign;
                case "encrypt":                                     
                    return op.Encrypt;
                case "print":                  
                    return new OutputOperations().Print;
                case "generatekey":                           
                    return op.GenerateKey; 
                default:
                    throw new ArgumentException("Invalid notification type");
            }
            
        }

    }
}
