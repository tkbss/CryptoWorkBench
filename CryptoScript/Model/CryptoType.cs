using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class CryptoType : Statement
    {
        public CryptoType() 
        {
            Lexer = new CryptoScriptLexer(new AntlrInputStream(""));
            
        }
        public void SetTypeKEY() 
        {
            Id=Lexer.Vocabulary.GetDisplayName(CryptoScriptLexer.T_KEY);
        }
        public void SetTypeVAR()
        {
            Id = Lexer.Vocabulary.GetDisplayName(CryptoScriptLexer.T_VAR);
        }
        public string Id { get; set; } 
        public CryptoScriptLexer Lexer { get; set; }
        public void Check(string type) 
        {
            type = type.ToLower();
            switch(type) 
            {
                case "var":
                    return;
                case "key":
                    return;
                case "data":
                    return;
                default: throw new Exception();
            }
        }
    }
}
