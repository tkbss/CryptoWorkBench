using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.ErrorListner
{
    public class SyntaxErrorList
    {
        private static readonly SyntaxErrorList _instance = new SyntaxErrorList();

        private List<SyntaxError> _errors = new List<SyntaxError>();
        private SyntaxErrorList()
        {

        }
        public static List<SyntaxError> Instance()
        {
            return _instance._errors;
        }
    }
}
