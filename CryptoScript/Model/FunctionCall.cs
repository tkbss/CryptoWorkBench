using Antlr4.Runtime;
using CryptoScript.Model;
using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class FunctionCall:Statement
    {
        

        public string Name { get; set; }
        public string? CallText { get; set; }
        public List<Argument> Arguments { get; set; }        
        
        public VariableDeclaration? ReturnVariable { get; set; }

        public FunctionCall()
        {
            
            Name = string.Empty;
            Arguments = new List<Argument>();            
            ReturnVariable = null;
        }
        public void Call() 
        {
            string[]argArray =new string[Arguments.Count];
            int i = 0;
            foreach(var arg in Arguments) 
            {
                if(arg is ArgumentMechanism mech) 
                {
                    argArray[i++] = mech.Mechanism.Value;
                }
                if((arg is ArgumentExpression expr)) 
                {
                    //var expr = arg as ArgumentExpression;
                    argArray[i++] = expr.Expr.Value();
                }
                if(arg is ArgumentVariable variable) 
                {
                    //if (variable.Id is StringVariableDeclaration s)
                    //{
                    //    //var v = variable.Id as StringVariableDeclaration;
                    //    var exp = Expression.Create(s.Value);
                    //    argArray[i++] = exp.Value();
                    //}
                    //if (variable.Id is KeyVariableDeclaration k)
                    //{
                    //    //var v = variable.Id as StringVariableDeclaration;
                    //    var exp = Expression.Create(k.Mechanism);
                    //    argArray[i++] = exp.Value();
                    //    exp= Expression.Create(k.KeySize.ToString());
                    //    argArray[i++] = exp.Value();
                    //}
                    argArray[i++] = variable.Id.Id;
                }
                if (arg is ArgumentParameter param)
                {
                    string functionParam=param.Type+":"+param.Value;
                    argArray[i++] = functionParam;
                }

            }
            var function= OperationFactory.CreateOperation(Name);
            ReturnVariable = function(argArray);           
            
        }
    }
}
