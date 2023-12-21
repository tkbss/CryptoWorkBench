using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.ErrorListner
{
    public class SyntaxError
    {
        public  string Line { get; set; } = string.Empty;
        public  string Column { get; set; } = string.Empty;
        public  string Symbol { get; set; } = string.Empty; 
        public  string Message { get; set; } = string.Empty;
    }
    public class ParserError : SyntaxError { }
    public class LexerError : SyntaxError { }
}
