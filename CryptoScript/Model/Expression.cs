using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class Expression : Statement
    {
        public string HexValue { get; private set; }
        public string Base64Value { get; private set; }
        public string StringValue { get; private   set; }
        public string Value() 
        { 
            if(!string.IsNullOrEmpty(HexValue))
                return HexValue; 
            if(!string.IsNullOrEmpty(Base64Value))
                return Base64Value;
            if(!string.IsNullOrEmpty(StringValue)) 
                return StringValue;
            return string.Empty;
        }
        static public Expression Create(string expr) 
        {
            
            var value=new Expression();
            if(expr.StartsWith("0x("))
                value.HexValue=expr;
            else if(expr.StartsWith("b64("))
                value.Base64Value=expr;
            else
                value.StringValue=expr;
            return value;
        }
        
        private Expression() 
        {
            HexValue = string.Empty;
            Base64Value = string.Empty;
            StringValue = string.Empty;
        }
    }
    
    
}
