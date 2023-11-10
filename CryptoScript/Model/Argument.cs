using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoScript.Variables;

namespace CryptoScript.Model
{
    public  class Argument: Statement { }
    public class ArgumentVariable: Argument
    {
        public VariableDeclaration? Id { get; set; }        
        public ArgumentVariable () 
        {
            Id = null;            
        }
    }
    public class ArgumentExpression : Argument
    { 
        public Expression? Expr { get; set; }
        public ArgumentExpression() 
        {
            Expr = null;
        }
    }
    public class ArgumentMechanism: Argument 
    { 
        public Mechanism? Mechanism { get; set; }
        public ArgumentMechanism() 
        {
            Mechanism = null;
        }
    }

}
