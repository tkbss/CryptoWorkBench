using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.ErrorListner
{
    public class SemanticErrorException:Exception
    {
        public SemanticError SemanticError { get; set; } = null;
    }
}
