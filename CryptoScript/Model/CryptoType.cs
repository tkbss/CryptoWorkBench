using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class CryptoType : Statement
    {
        
        public CryptoType() 
        {
            if (Lexer == null)
                Lexer = new CryptoScriptLexer(new AntlrInputStream(""));
        }        
        public virtual string Id { get; set; } = string.Empty;
        public virtual string Name { get;  } = string.Empty;
        public static CryptoScriptLexer Lexer { get; set; } = new CryptoScriptLexer(new AntlrInputStream(""));
        public static CryptoType Parse(string typeId) 
        {
            typeId = typeId.ToLower();
            switch(typeId) 
            {
                case "var":
                    return new CryptoTypeVar();
                case "key":
                    return new CryptoTypeKey();
                case "param":
                    return new CryptoTypeParameters();
                case "path":                    
                    return new CryptoTypePath();
                default: throw new Exception();
            }
        }
    }
    public class CryptoTypeKey : CryptoType 
    {
        public override string Id { get; set; }=string.Empty;
        public override string Name { get => "KEY";  }

        public CryptoTypeKey() 
        {
            Id = Lexer.Vocabulary.GetDisplayName(CryptoScriptLexer.T_KEY);
        }
    }
    public class CryptoTypeVar : CryptoType 
    {
        public override string Id { get; set; } = string.Empty;
        public override string Name { get => "VAR"; }
        public CryptoTypeVar() 
        {
            Id = Lexer.Vocabulary.GetDisplayName(CryptoScriptLexer.T_VAR);
        }
    }
    public class CryptoTypeParameters : CryptoType
    {
        public override string Id { get; set; } = string.Empty;
        public override string Name { get => "PARAM"; }
        public CryptoTypeParameters()
        {
            Id = Lexer.Vocabulary.GetDisplayName(CryptoScriptLexer.T_PARAMETER);
        }
    }
    public class CryptoTypePath : CryptoType
    {
        public override string Id { get; set; } = string.Empty;
        public override string Name { get => "PATH"; }
        public CryptoTypePath()
        {
            Id = Lexer.Vocabulary.GetDisplayName(CryptoScriptLexer.T_PATH);
        }
    }
}
