using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class ExpressionPath : Expression
    {
        public string PathValue { get; set; } = string.Empty;
        public override string Value()
        {
            return PathValue;
        }
    }
}
