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
        public virtual string Id { get; set; } = string.Empty; 
        public CryptoScriptLexer Lexer { get; set; }
        public static CryptoType Parse(string typeId) 
        {
            typeId = typeId.ToLower();
            switch(typeId) 
            {
                case "var":
                    return new CryptoTypeVar();
                case "key":
                    return new CryptoTypeKey();                
                default: throw new Exception();
            }
        }
    }
    public class CryptoTypeKey : CryptoType 
    {
        public override string Id { get; set; }=string.Empty;
        public CryptoTypeKey() 
        {
            Id = Lexer.Vocabulary.GetDisplayName(CryptoScriptLexer.T_KEY);
        }
    }
    public class CryptoTypeVar : CryptoType 
    {
        public override string Id { get; set; } = string.Empty;
        public CryptoTypeVar() 
        {
            Id = Lexer.Vocabulary.GetDisplayName(CryptoScriptLexer.T_VAR);
        }
    }
}
