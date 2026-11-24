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
        private bool immutable;
        internal CryptoType Freeze()
        {
            lock (InterpreterExecution.Gate) immutable = true;
            return this;
        }

        public override string Text
        {
            get => base.Text;
            set { lock (InterpreterExecution.Gate) base.Text = value; }
        }
        protected override void VerifyTextChange()
        {
            if (immutable) throw new InvalidOperationException("Bound type metadata is immutable.");
        }
        public virtual string Id
        {
            get;
            set { lock (InterpreterExecution.Gate) { VerifyTextChange(); field = value; } }
        } = string.Empty;
        public virtual string Name { get;  } = string.Empty;
        public static CryptoScriptLexer Lexer
        {
            get;
            set
            {
                lock (InterpreterExecution.Gate)
                {
                    InterpreterExecution.VerifyAccess();
                    if (InterpreterExecution.IsBusy) throw new ExecutionBusyException();
                    field = value;
                }
            }
        } = new CryptoScriptLexer(new AntlrInputStream(""));
        public static CryptoType Parse(string typeId) 
        {
            typeId = typeId.ToLower();
            switch(typeId) 
            {
                case "tr31h":
                    return new CryptoTypeTR31Header();
                case "var":
                    return new CryptoTypeVar();
                case "val":
                    return new CryptoTypeVal();
                case "key":
                    return new CryptoTypeKey();
                case "secret":
                    return new CryptoTypeSecret();
                case "param":
                    return new CryptoTypeParameters();
                case "path":                    
                    return new CryptoTypePath();
                default: throw new Exception();
            }
        }
    }
    public sealed class CryptoTypeVal : CryptoType
    {
        public override string Name => "VAL";
        public CryptoTypeVal() { Id = AntlrLanguageMetadata.GetTypeDisplayName(Lexer, CryptoScriptLexer.T_VAL); }
    }
    public sealed class CryptoTypeSecret : CryptoType
    {
        public override string Name => "SECRET";
        public CryptoTypeSecret() { Id = AntlrLanguageMetadata.GetTypeDisplayName(Lexer, CryptoScriptLexer.T_SECRET); }
    }
    public class CryptoTypeKey : CryptoType 
    {
        public override string Id { get => base.Id; set => base.Id = value; }
        public override string Name { get => "KEY";  }

        public CryptoTypeKey() 
        {
            Id = AntlrLanguageMetadata.GetTypeDisplayName(Lexer, CryptoScriptLexer.T_KEY);
        }
    }
    public class CryptoTypeVar : CryptoType 
    {
        public override string Id { get => base.Id; set => base.Id = value; }
        public override string Name { get => "VAR"; }
        public CryptoTypeVar() 
        {
            Id = AntlrLanguageMetadata.GetTypeDisplayName(Lexer, CryptoScriptLexer.T_VAR);
        }
    }
    public class CryptoTypeParameters : CryptoType
    {
        public override string Id { get => base.Id; set => base.Id = value; }
        public override string Name { get => "PARAM"; }
        public CryptoTypeParameters()
        {
            Id = AntlrLanguageMetadata.GetTypeDisplayName(Lexer, CryptoScriptLexer.T_PARAMETER);
        }
    }
    public class CryptoTypeTR31Header : CryptoType
    {
        public override string Id { get => base.Id; set => base.Id = value; }
        public override string Name { get => "TR31H"; }
        public CryptoTypeTR31Header()
        {
            Id = AntlrLanguageMetadata.GetTypeDisplayName(Lexer, CryptoScriptLexer.T_TR31H);
        }
    }
    
    public class CryptoTypePath : CryptoType
    {
        public override string Id { get => base.Id; set => base.Id = value; }
        public override string Name { get => "PATH"; }
        public CryptoTypePath()
        {
            Id = AntlrLanguageMetadata.GetTypeDisplayName(Lexer, CryptoScriptLexer.T_PATH);
        }
    }
}
