using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class ExpressionTR31String:Expression
    {
        public string TR31StringValue { get; set; } = string.Empty;
        public override string Value()
        {
            return TR31StringValue;
        }
    }
}
