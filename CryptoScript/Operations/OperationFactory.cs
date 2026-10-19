using CryptoScript.Operations;
using CryptoScript.Variables;

namespace CryptoScript.Model
{


    public class OperationFactory
    { 
        public static Func<OperationInvocation, VariableDeclaration> CreateInvocationOperation(string operation)
            => CreateInvocationOperation(operation, null);

        internal static Func<OperationInvocation, VariableDeclaration> CreateInvocationOperation(string operation,
            Func<string, CryptoAlgorithm.CryptoAlgorithm>? algorithmFactory)
        {
            operation = operation.ToLower();
            var op = algorithmFactory is null ? new CryptoOperations() : new CryptoOperations(algorithmFactory);
            if (operation == "share") return SecretOperations.Share;
            if (operation == "secret") return SecretOperations.Secret;
            if (operation == "restore") return SecretOperations.Restore;
            switch (operation)
            {
                case "mac":
                    return op.Mac;
                case "pinblocktranslate":
                    return op.PinBlockTranslate;
                case "hash":
                    return op.Hash;
                case "derive":
                    return op.Derive;
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
                case "check":
                    return KeyCheckOperations.Check;
                case "compare":
                    return GeneralOperations.Compare;
                default:
                    throw new ArgumentException("Unknown function");
            }
        }

        public static Func<string[], VariableDeclaration> CreateOperation(string operation)
        { 
            operation = operation.ToLower();
            if (operation is "share" or "secret" or "restore") return SecretOperations.RequireTypedArguments;
            var op = new CryptoOperations();             
            switch (operation)
            {
                case "mac":
                    return op.Mac;
                case "pinblocktranslate":
                    return op.PinBlockTranslate;
                case "hash":
                    return op.Hash;
                case "derive":
                    return op.Derive;
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
                case "check":
                    return KeyCheckOperations.Check;
                case "compare":
                    return GeneralOperations.Compare;
                default:
                {
                        throw new ArgumentException("Unknown function");
                }
                        
            }
            
        }

    }
}
