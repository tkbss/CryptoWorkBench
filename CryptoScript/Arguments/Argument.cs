using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
    public class ArgumentInfo: Argument 
    {
        public string InfoType { get; set; } = string.Empty;
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
    public class ArgumentParameter: Argument
    {
        
        public string Type { get; set; }    = string.Empty;
        public string Value { get; set; } = string.Empty;
        
        public ArgumentParameter()
        {
            
        }
        public void SetParameter(string type, string value)
        {
            Type = type;
            SetValue(value);
        }
        private void SetValue(string value)
        {
            string formatType=FormatConversions.ParseString(value);
            if (formatType==FormatConversions.HEX || formatType == FormatConversions.B64 || formatType == FormatConversions.STR)
            {
                Value = value;
                return;
            }
            if (ParameterTypeList.Instance.Paddings.Contains(value))
            {
                Value = value;
                return;
            }
            if (ParameterTypeList.Instance.Mechanism.Contains(value))
            {
                Value = value;
                return;
            }
            if (VariableDictionary.Instance().Contains(value))
                Value = VariableDictionary.Instance().Get(value).Value;
            else
                throw new ArgumentException("Unknown parameter value : " + value);
        }
    }

}
