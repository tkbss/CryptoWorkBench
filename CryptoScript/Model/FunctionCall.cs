using CryptoScript.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLanguage_TestApp3.Model
{
    public class FunctionCall:Statement
    {
        Dictionary<string, Func<string[], string>> functions = 
            new Dictionary<string, Func<string[], string>>();

        public string Name { get; set; }
        public string? CallText { get; set; }
        public List<Argument> Arguments { get; set; }
        public Type ReturnType { get; set; }
        public string ReturnValue { get; set; }

        public FunctionCall()
        {
            CryptoOperations operations = new CryptoOperations();
            functions["Encrypt"] = operations.Encrypt;
            functions["Sign"] = operations.Sign;

            Name = string.Empty;
            Arguments = new List<Argument>();
            ReturnType = new Type();
            ReturnValue = string.Empty;
        }
        public void Call() 
        {
            string[]argArray =new string[Arguments.Count];
            int i = 0;
            foreach(var arg in Arguments) 
            {
                if((arg is ArgumentExpression expr)) 
                {
                    //var expr = arg as ArgumentExpression;
                    argArray[i++] = expr.Expr.Value();
                }
                if(arg is ArgumentVariable variable) 
                {
                    var exp=Expression.Create(variable.Id.Value);
                    argArray[i++] = exp.Value();
                }
                
            }
            var function= OperationFactory.CreateOperation(Name);
            ReturnValue = function(argArray);
            //ReturnValue = functions[Name](argArray);

            ReturnType = new Type();
            ReturnType.Id = "VAR";
        }
    }
}
