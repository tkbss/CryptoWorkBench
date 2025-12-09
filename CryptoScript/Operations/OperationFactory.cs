using CryptoScript.Operations;
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
                case "mac":
                    return op.Mac;
                case "encrypt":                                     
                    return op.Encrypt;
                case "decrypt":
                    return op.Decrypt;
                case "print":                  
                    return new OutputOperations().Print;
                case "info":
                    return new OutputOperations().Info;
                case "generatekey":                           
                    return op.GenerateKey; 
                case "parameters":
                        return op.GenerateParameters;
                case "wrap":
                    return op.Wrap;
                case "unwrap":
                    return op.Unwrap;
                case "blockheader":
                    return op.BlockHeader;
                case "compare":
                    return GeneralOperations.Compare;
                default:
                {
                        throw new ArgumentException("Unkown function");
                }
                        
            }
            
        }

    }
}
