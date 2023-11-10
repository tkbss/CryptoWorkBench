using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class Expression : Statement
    {
        
        
        public virtual string Value() 
        {
            return string.Empty;
        }
        //public string Value() 
        //{ 
        //    if(!string.IsNullOrEmpty(HexValue))
        //        return HexValue; 
        //    if(!string.IsNullOrEmpty(Base64Value))
        //        return Base64Value;
        //    if(!string.IsNullOrEmpty(StringValue)) 
        //        return StringValue;
        //    return string.Empty;
        //}
        static public Expression Create(string expr)
        {

            var value = new Expression();
            if (expr.StartsWith("0x("))
                return new ExpressionHex() { HexValue = expr };//value.HexValue = expr;
            else if (expr.StartsWith("b64("))
                return new ExpressionBase64() { Base64Value = expr };//value.Base64Value = expr;
            else
                return new ExpressionNumber() { IntegerValue=expr };//value.StringValue = expr;           
        }

        
        public class ExpressionHex : Expression
        {
            public string HexValue { get;  set; }=string.Empty;

            public override string Value()
            {
                return HexValue;
            }
        }
        public class ExpressionBase64 : Expression 
        {
            public string Base64Value { get;  set; }=string.Empty;

            public override string Value()
            {
                return Base64Value;
            }
        }
        public class ExpressionNumber : Expression 
        {
            public string IntegerValue { get; set; } = string.Empty;

            public override string Value()
            {
                return IntegerValue;
            }
        }
    }
    
    
}
